using System;
using System.Collections.Generic;
using System.IO;

namespace YgoMasterConfig
{
    /// <summary>
    /// Installs / removes the Arabic card text pack.
    ///
    /// The game has no Arabic language slot and it reads card text from its own encrypted
    /// bundles, so the Arabic text is applied at runtime by YgoMasterClient (it hooks
    /// YgomGame.Card.Content and serves the files below). The only thing "installing" needs
    /// to do is place the marker that turns that hook on. The clean English game files are
    /// left completely untouched, so nothing about the game/server data can break.
    ///
    /// Layout inside the game folder:
    ///   Data/ArabicPack/ar/             Arabic card files served at runtime
    ///   Data/ArabicPack/original/       clean English files (used to recognise card data)
    ///   Data/ArabicPack/installed.txt   marker (presence = Arabic enabled)
    /// </summary>
    public static class ArabicPack
    {
        static readonly string[] CardFiles =
        {
            "CARD_Indx.bytes",
            "CARD_Name.bytes",
            "CARD_Desc.bytes"
        };

        public static string GetCardDataDir(string baseDir)
        {
            return Path.Combine(baseDir, "Data", "CardData", "en-US");
        }

        public static string GetPackDir(string baseDir)
        {
            return Path.Combine(baseDir, "Data", "ArabicPack");
        }

        public static string GetArabicDir(string baseDir)
        {
            return Path.Combine(GetPackDir(baseDir), "ar");
        }

        public static string GetOriginalDir(string baseDir)
        {
            return Path.Combine(GetPackDir(baseDir), "original");
        }

        public static string GetBackupDir(string baseDir)
        {
            return Path.Combine(GetPackDir(baseDir), "backup");
        }

        public static string GetMarkerPath(string baseDir)
        {
            return Path.Combine(GetPackDir(baseDir), "installed.txt");
        }

        /// <summary>File that stores the chosen Arabic font (family on line 1, style on line 2).</summary>
        public static string GetFontPath(string baseDir)
        {
            return Path.Combine(GetPackDir(baseDir), "font.txt");
        }

        /// <summary>Reads the chosen Arabic font. Returns false when the default should be used.</summary>
        public static bool TryGetFont(string baseDir, out string family, out string style)
        {
            family = null;
            style = null;
            try
            {
                string path = GetFontPath(baseDir);
                if (!File.Exists(path))
                {
                    return false;
                }
                string[] lines = File.ReadAllLines(path);
                if (lines.Length == 0 || string.IsNullOrEmpty(lines[0].Trim()))
                {
                    return false;
                }
                family = lines[0].Trim();
                style = lines.Length > 1 && !string.IsNullOrEmpty(lines[1].Trim()) ? lines[1].Trim() : "Regular";
                return true;
            }
            catch
            {
                family = null;
                style = null;
                return false;
            }
        }

        /// <summary>Writes the chosen Arabic font. Passing an empty family restores the default.</summary>
        public static void SetFont(string baseDir, string family, string style)
        {
            string path = GetFontPath(baseDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (string.IsNullOrEmpty(family))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }
            if (string.IsNullOrEmpty(style))
            {
                style = "Regular";
            }
            File.WriteAllText(path, family.Trim() + Environment.NewLine + style.Trim() + Environment.NewLine);
        }

        /// <summary>True when the Arabic pack files were shipped with this launcher.</summary>
        public static bool IsAvailable(string baseDir)
        {
            if (string.IsNullOrEmpty(baseDir))
            {
                return false;
            }
            string ar = GetArabicDir(baseDir);
            foreach (string file in CardFiles)
            {
                if (!File.Exists(Path.Combine(ar, file)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when the Arabic pack is currently written into the game data.</summary>
        public static bool IsInstalled(string baseDir)
        {
            if (string.IsNullOrEmpty(baseDir))
            {
                return false;
            }
            return File.Exists(GetMarkerPath(baseDir));
        }

        public static bool Install(string baseDir, out string error)
        {
            error = null;
            if (!IsAvailable(baseDir))
            {
                error = "The Arabic pack files are missing (Data/ArabicPack/ar).";
                return false;
            }

            try
            {
                // Make sure any English files left over from an older launcher version are
                // put back, then just drop the marker that enables the runtime hook.
                RestoreEnglish(baseDir);
                File.WriteAllText(GetMarkerPath(baseDir),
                    "Arabic card text installed by YgoMaster Launcher." + Environment.NewLine);
                return true;
            }
            catch (Exception e)
            {
                error = "Failed to install the Arabic pack:\n" + e.Message;
                return false;
            }
        }

        public static bool Uninstall(string baseDir, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(baseDir))
            {
                error = "Invalid game folder.";
                return false;
            }

            try
            {
                RestoreEnglish(baseDir);
                string marker = GetMarkerPath(baseDir);
                if (File.Exists(marker))
                {
                    File.Delete(marker);
                }
                return true;
            }
            catch (Exception e)
            {
                error = "Failed to remove the Arabic pack:\n" + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Puts the clean English card files back into Data/CardData/en-US if an older
        /// launcher version had overwritten them with Arabic.
        /// </summary>
        static void RestoreEnglish(string baseDir)
        {
            string cardDir = GetCardDataDir(baseDir);
            string backupDir = GetBackupDir(baseDir);
            string originalDir = GetOriginalDir(baseDir);
            if (!Directory.Exists(cardDir))
            {
                return;
            }
            foreach (string file in CardFiles)
            {
                string live = Path.Combine(cardDir, file);
                string backup = Path.Combine(backupDir, file);
                string original = Path.Combine(originalDir, file);
                if (File.Exists(backup) && !FilesEqual(live, backup))
                {
                    File.Copy(backup, live, true);
                }
                else if (File.Exists(original) && !FilesEqual(live, original))
                {
                    // Only restore when the live file is not the shipped English one.
                    byte[] liveBytes = ReadAllBytesOrNull(live);
                    byte[] arabicBytes = ReadAllBytesOrNull(Path.Combine(GetArabicDir(baseDir), file));
                    if (liveBytes != null && arabicBytes != null && liveBytes.Length == arabicBytes.Length &&
                        BytesEqual(liveBytes, arabicBytes))
                    {
                        File.Copy(original, live, true);
                    }
                }
            }
        }

        static byte[] ReadAllBytesOrNull(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch
            {
                return null;
            }
        }

        static bool FilesEqual(string a, string b)
        {
            byte[] ab = ReadAllBytesOrNull(a);
            byte[] bb = ReadAllBytesOrNull(b);
            if (ab == null || bb == null)
            {
                return ab == bb;
            }
            return BytesEqual(ab, bb);
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
