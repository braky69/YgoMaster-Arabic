using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using IL2CPP;

namespace YgoMasterClient
{
    /// <summary>
    /// Shows the Arabic card text inside the game.
    ///
    /// Writing the Arabic files over Data/CardData/en-US has NO visible effect: the game
    /// loads card text from its own encrypted asset bundles through
    /// YgomGame.Card.Content. This class therefore hooks the Content accessors the game
    /// uses when it draws a card and returns the Arabic strings / bytes instead.
    ///
    /// The Arabic data itself is the pack the launcher installed into Data/ArabicPack/ar.
    /// Everything is gated on Data/ArabicPack/installed.txt and wrapped in try/catch, so
    /// turning the pack off (or shipping a broken pack) can never break the game.
    /// </summary>
    static class ArabicCardText
    {
        // The exact separator the game's Content class splits pendulum descriptions on.
        // Verified against the shipped en-US CARD_Desc.bytes ("...\n[Pendulum Effect]\n...").
        const string PendulumMarker = "[Pendulum Effect]";

        // ---- YgomGame.Card.Content signatures (from global-metadata.dat v39) ----
        //   string       GetName(int cardId, bool replaceAlnum)
        //   string       GetRubyName(int cardId, bool replaceAlnum)
        //   string       GetDesc(int cardId, bool replaceAlnum)
        //   string       GetRawDesc(int cardId)
        //   string       GetDescWithoutPendulum(int cardId, bool replaceAlnum)
        //   string       GetDescOfPendulum(int cardId, bool replaceAlnum)
        //   string       GetDescWithoutPendulumForCardInfo(int cardId, bool replaceAlnum)
        //   string       GetDescOfPendulumForCardInfo(int cardId, bool replaceAlnum)
        //   byte[]       GetBytesDecryptionData(string path)
        //   IEnumerator  GetBytesDecryptionDataAsync(string path, Action<byte[]> resultCallback)
        //   byte[]       GetBytesDecryptionDataCore(byte[] outData)
        delegate IntPtr Del_Name(IntPtr thisPtr, int cardId, bool replaceAlnum);
        delegate IntPtr Del_Desc(IntPtr thisPtr, int cardId, bool replaceAlnum);
        delegate IntPtr Del_RawDesc(IntPtr thisPtr, int cardId);
        delegate IntPtr Del_Bytes(IntPtr thisPtr, IntPtr pathPtr);
        delegate IntPtr Del_BytesAsync(IntPtr thisPtr, IntPtr pathPtr, IntPtr callbackPtr);
        delegate IntPtr Del_BytesCore(IntPtr thisPtr, IntPtr outDataPtr);

        static Hook<Del_Name> hookGetName;
        static Hook<Del_Name> hookGetRubyName;
        static Hook<Del_Desc> hookGetDesc;
        static Hook<Del_RawDesc> hookGetRawDesc;
        static Hook<Del_Desc> hookGetDescNoPend;
        static Hook<Del_Desc> hookGetDescPend;
        static Hook<Del_Desc> hookGetDescNoPendInfo;
        static Hook<Del_Desc> hookGetDescPendInfo;
        static Hook<Del_Bytes> hookGetBytes;
        static Hook<Del_BytesAsync> hookGetBytesAsync;
        static Hook<Del_BytesCore> hookGetBytesCore;

        // cardId -> text. cardId is the internal id stored in CARD_Prop (PropA & 0xFFFF),
        // which is exactly what the game passes to Content.GetName / GetDesc.
        static readonly Dictionary<int, string> nameById = new Dictionary<int, string>();
        static readonly Dictionary<int, string> descById = new Dictionary<int, string>();
        static readonly Dictionary<int, string> mainById = new Dictionary<int, string>();
        static readonly Dictionary<int, string> pendById = new Dictionary<int, string>();

        static byte[] arabicNameBytes;
        static byte[] arabicDescBytes;
        static byte[] arabicIndxBytes;

        // The clean English card files, used to recognise which file the game is decrypting
        // (GetBytesDecryptionDataCore only receives the raw bytes, not the path).
        static byte[] originalIndxBytes;
        static byte[] originalNameBytes;
        static byte[] originalDescBytes;

        static bool loggedName;
        static bool loggedDesc;
        static int nameHits;
        static int descHits;
        static int coreCalls;
        static readonly HashSet<string> seenPaths = new HashSet<string>();

        // ------------------------------------------------------------------ logging
        static readonly object logLock = new object();
        static string logPath;

        static void Log(string message)
        {
            try
            {
                Console.WriteLine("[ArabicCardText] " + message);
            }
            catch { }
            try
            {
                if (logPath == null)
                {
                    string dir = Path.Combine(Program.DataDir, "ArabicPack");
                    try { Directory.CreateDirectory(dir); } catch { }
                    logPath = Path.Combine(dir, "arabiccardtext.log");
                }
                lock (logLock)
                {
                    File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        static ArabicCardText()
        {
            try
            {
                Log("static ctor: IsLive=" + Program.IsLive + " DataDir=" + Program.DataDir);
                if (Program.IsLive)
                {
                    return;
                }
                if (!PackInstalled())
                {
                    Log("Arabic pack not installed, skipping");
                    return;
                }
                if (!LoadPack())
                {
                    Log("could not load Arabic pack data, skipping");
                    return;
                }

                IL2Assembly assembly = Assembler.GetAssembly("Assembly-CSharp");
                IL2Class content = assembly.GetClass("Content", "YgomGame.Card");
                if (content == null)
                {
                    Log("YgomGame.Card.Content not found");
                    return;
                }

                // NOTE: the parameter count filter is important - the Content class has
                // more than one method named GetName / GetDesc and GetMethod(name) alone
                // returns whichever happens to come first in the metadata.
                hookGetName = HookMethod(content, "GetName", 2, (Del_Name)GetNameImpl);
                hookGetRubyName = HookMethod(content, "GetRubyName", 2, (Del_Name)GetRubyNameImpl);
                hookGetRawDesc = HookMethod(content, "GetRawDesc", 1, (Del_RawDesc)GetRawDescImpl);
                hookGetDesc = HookMethod(content, "GetDesc", 2, (Del_Desc)GetDescImpl);
                hookGetDescNoPend = HookMethod(content, "GetDescWithoutPendulum", 2, (Del_Desc)GetMainImpl);
                hookGetDescPend = HookMethod(content, "GetDescOfPendulum", 2, (Del_Desc)GetPendImpl);
                hookGetDescNoPendInfo = HookMethod(content, "GetDescWithoutPendulumForCardInfo", 2, (Del_Desc)GetMainInfoImpl);
                hookGetDescPendInfo = HookMethod(content, "GetDescOfPendulumForCardInfo", 2, (Del_Desc)GetPendInfoImpl);

                // These two are logging-only: the async one MUST keep returning the original
                // IEnumerator (Unity coroutine) or the game hangs while loading card data.
                hookGetBytes = HookMethod(content, "GetBytesDecryptionData", 1, (Del_Bytes)GetBytesImpl);
                hookGetBytesAsync = HookMethod(content, "GetBytesDecryptionDataAsync", 2, (Del_BytesAsync)GetBytesAsyncImpl);
                // The actual byte substitution happens here - it returns a byte[] so it is safe.
                hookGetBytesCore = HookMethod(content, "GetBytesDecryptionDataCore", 1, (Del_BytesCore)GetBytesCoreImpl);

                Log("init ok (cards=" + nameById.Count +
                    ", name=" + (hookGetName != null) +
                    ", ruby=" + (hookGetRubyName != null) +
                    ", rawDesc=" + (hookGetRawDesc != null) +
                    ", desc=" + (hookGetDesc != null) +
                    ", main=" + (hookGetDescNoPend != null) +
                    ", pend=" + (hookGetDescPend != null) +
                    ", mainInfo=" + (hookGetDescNoPendInfo != null) +
                    ", pendInfo=" + (hookGetDescPendInfo != null) +
                    ", bytes=" + (hookGetBytes != null) +
                    ", bytesAsync=" + (hookGetBytesAsync != null) +
                    ", core=" + (hookGetBytesCore != null) + ")");
            }
            catch (Exception e)
            {
                Log("init failed: " + e);
            }
        }

        static bool PackInstalled()
        {
            try
            {
                if (string.IsNullOrEmpty(Program.DataDir))
                {
                    return false;
                }
                return File.Exists(Path.Combine(Program.DataDir, "ArabicPack", "installed.txt"));
            }
            catch
            {
                return false;
            }
        }

        static bool LoadPack()
        {
            string packDir = Path.Combine(Program.DataDir, "ArabicPack");
            string arDir = Path.Combine(packDir, "ar");
            string indxPath = Path.Combine(arDir, "CARD_Indx.bytes");
            string namePath = Path.Combine(arDir, "CARD_Name.bytes");
            string descPath = Path.Combine(arDir, "CARD_Desc.bytes");
            // CARD_Prop is shipped unmodified with YgoMaster (it maps card id -> index).
            string propPath = Path.Combine(Program.DataDir, "CardData", "#", "CARD_Prop.bytes");
            if (!File.Exists(indxPath) || !File.Exists(namePath) || !File.Exists(descPath) || !File.Exists(propPath))
            {
                Log("pack files missing (indx=" + File.Exists(indxPath) + " name=" + File.Exists(namePath) +
                    " desc=" + File.Exists(descPath) + " prop=" + File.Exists(propPath) + ")");
                return false;
            }

            arabicIndxBytes = File.ReadAllBytes(indxPath);
            arabicNameBytes = File.ReadAllBytes(namePath);
            arabicDescBytes = File.ReadAllBytes(descPath);
            byte[] prop = File.ReadAllBytes(propPath);

            // The clean English files shipped with the pack. Used by the Core hook to know
            // which file is being decrypted (it never receives the path).
            string originalDir = Path.Combine(packDir, "original");
            string enUsDir = Path.Combine(Program.DataDir, "CardData", "en-US");
            originalIndxBytes = ReadOriginal(originalDir, enUsDir, "CARD_Indx.bytes");
            originalNameBytes = ReadOriginal(originalDir, enUsDir, "CARD_Name.bytes");
            originalDescBytes = ReadOriginal(originalDir, enUsDir, "CARD_Desc.bytes");
            Log("original files: indx=" + (originalIndxBytes != null ? originalIndxBytes.Length : -1) +
                " name=" + (originalNameBytes != null ? originalNameBytes.Length : -1) +
                " desc=" + (originalDescBytes != null ? originalDescBytes.Length : -1));

            int count = arabicIndxBytes.Length / 8;
            int propCount = prop.Length / 8;
            for (int i = 0; i < count; i++)
            {
                int cardId = i < propCount ? (BitConverter.ToInt32(prop, i * 8) & 0xFFFF) : 0;
                if (cardId <= 0)
                {
                    continue;
                }
                if (nameById.ContainsKey(cardId))
                {
                    continue; // first index wins (matches the game's own alt-art handling)
                }

                string name = ReadCString(arabicNameBytes, (int)BitConverter.ToUInt32(arabicIndxBytes, i * 8));
                string full = ReadCString(arabicDescBytes, (int)BitConverter.ToUInt32(arabicIndxBytes, i * 8 + 4));

                nameById[cardId] = name;
                descById[cardId] = full;

                int marker = full.IndexOf(PendulumMarker, StringComparison.Ordinal);
                if (marker >= 0)
                {
                    mainById[cardId] = full.Substring(0, marker).TrimEnd('\n', '\r');
                    pendById[cardId] = full.Substring(marker + PendulumMarker.Length).TrimStart('\n', '\r');
                }
                else
                {
                    mainById[cardId] = full;
                    pendById[cardId] = null;
                }
            }
            return nameById.Count > 0;
        }

        static byte[] ReadOriginal(string originalDir, string enUsDir, string file)
        {
            try
            {
                string path = Path.Combine(originalDir, file);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
                path = Path.Combine(enUsDir, file);
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
            }
            catch { }
            return null;
        }

        static string ReadCString(byte[] buffer, int offset)
        {
            if (buffer == null || offset < 0 || offset >= buffer.Length)
            {
                return string.Empty;
            }
            int end = offset;
            while (end < buffer.Length && buffer[end] != 0)
            {
                end++;
            }
            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        // ------------------------------------------------------------------ hooks
        static IntPtr GetNameImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return NameImpl(hookGetName, thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetRubyNameImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return NameImpl(hookGetRubyName, thisPtr, cardId, replaceAlnum);
        }

        static IntPtr NameImpl(Hook<Del_Name> hook, IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            try
            {
                string name;
                if (nameById.TryGetValue(cardId, out name) && name != null)
                {
                    if (nameHits < 6)
                    {
                        nameHits++;
                        Log("GetName(" + cardId + ") -> Arabic (" + nameHits + "): " + name);
                    }
                    return new IL2String(name).ptr;
                }
                if (!loggedName)
                {
                    loggedName = true;
                    Log("GetName(" + cardId + ") -> no Arabic mapping");
                }
            }
            catch (Exception e)
            {
                Log("GetName failed: " + e.Message);
            }
            return hook.Original(thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetDescImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return DescImpl(hookGetDesc, descById, "GetDesc", thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetMainImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return DescImpl(hookGetDescNoPend, mainById, "GetDescWithoutPendulum", thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetPendImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return DescImpl(hookGetDescPend, pendById, "GetDescOfPendulum", thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetMainInfoImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return DescImpl(hookGetDescNoPendInfo, mainById, "GetDescWithoutPendulumForCardInfo", thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetPendInfoImpl(IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            return DescImpl(hookGetDescPendInfo, pendById, "GetDescOfPendulumForCardInfo", thisPtr, cardId, replaceAlnum);
        }

        static IntPtr DescImpl(Hook<Del_Desc> hook, Dictionary<int, string> map, string label, IntPtr thisPtr, int cardId, bool replaceAlnum)
        {
            try
            {
                string text;
                if (map.TryGetValue(cardId, out text) && text != null)
                {
                    if (descHits < 6)
                    {
                        descHits++;
                        Log(label + "(" + cardId + ") -> Arabic (" + descHits + ")");
                    }
                    return new IL2String(text).ptr;
                }
            }
            catch (Exception e)
            {
                Log("GetDesc failed: " + e.Message);
            }
            return hook.Original(thisPtr, cardId, replaceAlnum);
        }

        static IntPtr GetRawDescImpl(IntPtr thisPtr, int cardId)
        {
            try
            {
                string text;
                if (descById.TryGetValue(cardId, out text) && text != null)
                {
                    return new IL2String(text).ptr;
                }
            }
            catch (Exception e)
            {
                Log("GetRawDesc failed: " + e.Message);
            }
            return hookGetRawDesc.Original(thisPtr, cardId);
        }

        /// <summary>Logging only - must return the original result so the game keeps working.</summary>
        static IntPtr GetBytesImpl(IntPtr thisPtr, IntPtr pathPtr)
        {
            try
            {
                if (pathPtr != IntPtr.Zero)
                {
                    LogPath("sync", new IL2String(pathPtr).ToString());
                }
            }
            catch { }
            return hookGetBytes.Original(thisPtr, pathPtr);
        }

        /// <summary>Logging only - MUST return the original IEnumerator (Unity coroutine).</summary>
        static IntPtr GetBytesAsyncImpl(IntPtr thisPtr, IntPtr pathPtr, IntPtr callbackPtr)
        {
            try
            {
                if (pathPtr != IntPtr.Zero)
                {
                    LogPath("async", new IL2String(pathPtr).ToString());
                }
            }
            catch { }
            return hookGetBytesAsync.Original(thisPtr, pathPtr, callbackPtr);
        }

        /// <summary>Substitutes the Arabic bytes for the card data files.</summary>
        static IntPtr GetBytesCoreImpl(IntPtr thisPtr, IntPtr outDataPtr)
        {
            IntPtr result = hookGetBytesCore.Original(thisPtr, outDataPtr);
            try
            {
                if (result != IntPtr.Zero)
                {
                    int len = (int)Import.Object.il2cpp_array_get_byte_length(result);
                    if (coreCalls < 12)
                    {
                        coreCalls++;
                        Log("Core call in=" + (outDataPtr != IntPtr.Zero ? (int)Import.Object.il2cpp_array_get_byte_length(outDataPtr) : -1) +
                            " out=" + len);
                    }
                    byte[] arabic = MatchDecrypted(result, len);
                    if (arabic != null)
                    {
                        Log("Core matched len=" + len + " -> Arabic");
                        return MakeArray(arabic);
                    }
                }
            }
            catch (Exception e)
            {
                Log("Core failed: " + e.Message);
            }
            return result;
        }

        static void LogPath(string kind, string path)
        {
            try
            {
                if (seenPaths.Count < 40 && seenPaths.Add(kind + ":" + path))
                {
                    Log(kind + " path: " + path);
                }
            }
            catch { }
        }

        static byte[] MatchDecrypted(IntPtr arrPtr, int len)
        {
            if (originalIndxBytes != null && len == originalIndxBytes.Length && ArrayEquals(arrPtr, originalIndxBytes))
            {
                return arabicIndxBytes;
            }
            if (originalNameBytes != null && len == originalNameBytes.Length && ArrayEquals(arrPtr, originalNameBytes))
            {
                return arabicNameBytes;
            }
            if (originalDescBytes != null && len == originalDescBytes.Length && ArrayEquals(arrPtr, originalDescBytes))
            {
                return arabicDescBytes;
            }
            return null;
        }

        static bool ArrayEquals(IntPtr arrPtr, byte[] expected)
        {
            byte[] actual = new IL2Array<byte>(arrPtr).ToByteArray();
            if (actual.Length != expected.Length)
            {
                return false;
            }
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }

        static IntPtr MakeArray(byte[] data)
        {
            IL2Array<byte> array = new IL2Array<byte>(data.Length, IL2SystemClass.Byte);
            array.CopyFrom(data);
            return array.ptr;
        }

        // ------------------------------------------------------------------ hook setup
        static Hook<T> HookMethod<T>(IL2Class classInfo, string name, int paramCount, T handler) where T : class
        {
            try
            {
                IL2Method method = classInfo.GetMethod(name, x => x.GetParameters().Length == paramCount);
                if (method == null)
                {
                    Log("method not found: " + name + "/" + paramCount);
                    return null;
                }
                return new Hook<T>(handler, method);
            }
            catch (Exception e)
            {
                Log("hook " + name + " failed: " + e.Message);
                return null;
            }
        }
    }
}
