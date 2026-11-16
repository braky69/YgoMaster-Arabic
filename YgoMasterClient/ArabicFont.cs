using System;
using System.Collections.Generic;
using System.IO;
using IL2CPP;

namespace YgoMasterClient
{
    /// <summary>
    /// Makes the pre-shaped Arabic card text render in game.
    ///
    /// The card text files are generated already shaped and in visual order (Arabic
    /// Presentation Forms-B, U+FE70..U+FEFF) because TextMeshPro has no OpenType shaping
    /// or bidi support. The remaining problem is the glyphs: the game's own fonts do not
    /// contain those code points, so they come out as squares ("tofu"). This class builds
    /// a TextMeshPro font asset from an OS font that *does* contain them and registers it
    /// as a fallback so every TMP text can use it.
    ///
    /// The game ships a modern TMP (3.2 / Unity 6) whose OS-font helper
    /// (UnityEngine.Font.CreateDynamicFontFromOSFont) is stripped, but which does expose
    /// TMP_FontAsset.CreateFontAsset(familyName, styleName, pointSize) -> a "DynamicOS"
    /// font asset that reads glyphs straight from the system font at runtime.
    ///
    /// Three creation paths are attempted, in order:
    ///   1. TMP_FontAsset.CreateFontAsset(familyName, styleName, pointSize)  (DynamicOS)
    ///   2. TMP_FontAsset.CreateFontAsset(fontFilePath, faceIndex, ...)      (Dynamic, from
    ///      a real OS font file found via UnityEngine.Font.GetPathsToOSFonts)
    ///   3. UnityEngine.Font.CreateDynamicFontFromOSFont + CreateFontAsset(Font)  (legacy)
    ///
    /// The fallback is registered in several places so it works no matter which path the
    /// game uses:
    ///   1. the global TMP_Settings fallback list (covers every text component),
    ///   2. TMP_Settings.fallbackOSFontAssets (TMP's own OS font fallback list),
    ///   3. every TMP_FontAsset that gets enabled (per asset fallback table),
    ///   4. the font of every TMP_Text that gets enabled.
    ///
    /// Everything is best-effort and gated on Data/ArabicPack/installed.txt, so turning
    /// the pack off leaves the game completely untouched and a failure can never break it.
    /// </summary>
    unsafe static class ArabicFont
    {
        delegate void Del_Void(IntPtr thisPtr);
        delegate void Del_SetFont(IntPtr thisPtr, IntPtr value);

        static Hook<Del_Void> hookFontAsset;
        static Hook<Del_Void> hookText;
        static Hook<Del_SetFont> hookSetFont;

        static IL2Class tmpFontAssetClass;
        static IL2Class tmpSettingsClass;
        static IL2Method createOSFont;               // UnityEngine.Font.CreateDynamicFontFromOSFont (usually stripped)
        static IL2Method createFontAssetFromFont;    // TMPro.TMP_FontAsset.CreateFontAsset(UnityEngine.Font)
        static IL2Method createFontAssetFromName;    // TMPro.TMP_FontAsset.CreateFontAsset(string familyName, string styleName, int pointSize)
        static IL2Method createFontAssetFromPath;    // TMPro.TMP_FontAsset.CreateFontAsset(string fontFilePath, int faceIndex, int pointSize, int padding, GlyphRenderMode, int w, int h)
        static IL2Method getPathsToOSFonts;          // UnityEngine.Font.GetPathsToOSFonts
        static IL2Property fallbackProp;             // TMPro.TMP_FontAsset.fallbackFontAssetTable
        static IL2Field fallbackField;               // TMPro.TMP_FontAsset.m_FallbackFontAssetTable
        static IL2Property tmpSettingsFallback;      // TMPro.TMP_Settings.fallbackFontAssets
        static IL2Property tmpSettingsFallbackOS;    // TMPro.TMP_Settings.fallbackOSFontAssets
        static IL2Property tmpSettingsInstance;      // TMPro.TMP_Settings.instance
        static IL2Field tmpSettingsFallbackField;    // TMPro.TMP_Settings.m_fallbackFontAssets
        static IL2Property tmpTextFont;              // TMPro.TMP_Text.font
        static IL2Property tmpAssetMaterial;         // TMPro.TMP_Asset.material
        static IL2Property materialShader;           // UnityEngine.Material.shader

        static int sdfaaValue = -1;                  // UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA

        static string customFamily;                  // user-chosen font (Data/ArabicPack/font.txt)
        static string customStyle;

        static bool resolved;
        static bool hooked;
        static int createAttempts;
        static IntPtr arabicAsset = IntPtr.Zero;
        static IntPtr arabicHandle = IntPtr.Zero;    // GC root, keeps the asset alive
        static bool globalRegistered;
        static bool globalOSRegistered;
        static bool loggedOsPaths;
        static readonly HashSet<IntPtr> augmented = new HashSet<IntPtr>();

        // OS fonts that contain Arabic Presentation Forms-B, in priority order.
        static readonly string[][] OsFonts =
        {
            new string[] { "Arial", "Regular" },
            new string[] { "Tahoma", "Regular" },
            new string[] { "Segoe UI", "Regular" },
            new string[] { "Times New Roman", "Regular" },
            new string[] { "Microsoft Sans Serif", "Regular" },
            new string[] { "Sakkal Majalla", "Regular" },
            new string[] { "Traditional Arabic", "Regular" },
            new string[] { "Arabic Typesetting", "Regular" }
        };

        // Font files (lowercase file name) that are known to contain Arabic glyphs.
        static readonly string[] PreferredFontFiles =
        {
            "arial.ttf", "tahoma.ttf", "segoeui.ttf", "times.ttf", "calibri.ttf",
            "micross.ttf", "arialuni.ttf", "trado.ttf", "tradbdo.ttf", "majalla.ttf",
            "arabtype.ttf", "seguisym.ttf", "verdana.ttf"
        };

        // ------------------------------------------------------------------ logging
        static readonly object logLock = new object();
        static string logPath;

        internal static void Log(string message)
        {
            try
            {
                Console.WriteLine("[ArabicFont] " + message);
            }
            catch { }
            try
            {
                if (logPath == null)
                {
                    string dir = Path.Combine(Program.DataDir, "ArabicPack");
                    try { Directory.CreateDirectory(dir); } catch { }
                    logPath = Path.Combine(dir, "arabicfont.log");
                }
                lock (logLock)
                {
                    File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        static ArabicFont()
        {
            try
            {
                Log("static ctor: IsLive=" + Program.IsLive + " DataDir=" + Program.DataDir);
                if (Program.IsLive)
                {
                    return;
                }
                if (!ArabicPackInstalled())
                {
                    Log("Arabic pack not installed, skipping");
                    return;
                }
                LoadCustomFont();
                Resolve();
                Hook();
            }
            catch (Exception e)
            {
                Log("static ctor failed: " + e);
            }
        }

        /// <summary>
        /// Runs on the game's main thread (queued through Win32Hooks). Safe to call more
        /// than once: the first successful call creates the font and registers it, later
        /// calls only retry if an earlier attempt failed.
        /// </summary>
        public static void Init()
        {
            try
            {
                if (Program.IsLive || !ArabicPackInstalled())
                {
                    return;
                }
                if (!resolved)
                {
                    Resolve();
                }
                EnsureArabic();
                Log("Init: EnsureArabic done (asset=" + (arabicAsset != IntPtr.Zero) + ")");
                RegisterGlobal();
                Log("Init: RegisterGlobal done (ok=" + globalRegistered + ")");
                RegisterGlobalOS();
                Log("Init: RegisterGlobalOS done (ok=" + globalOSRegistered + ")");
            }
            catch (Exception e)
            {
                Log("Init failed: " + e);
            }
        }

        static bool ArabicPackInstalled()
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

        /// <summary>
        /// Reads the font chosen in the launcher (Data/ArabicPack/font.txt: family on line 1,
        /// style on line 2). When present it is tried before the built-in default list.
        /// </summary>
        static void LoadCustomFont()
        {
            try
            {
                if (string.IsNullOrEmpty(Program.DataDir))
                {
                    return;
                }
                string path = Path.Combine(Program.DataDir, "ArabicPack", "font.txt");
                if (!File.Exists(path))
                {
                    return;
                }
                string[] lines = File.ReadAllLines(path);
                if (lines.Length == 0 || string.IsNullOrEmpty(lines[0].Trim()))
                {
                    return;
                }
                customFamily = lines[0].Trim();
                customStyle = lines.Length > 1 && !string.IsNullOrEmpty(lines[1].Trim()) ? lines[1].Trim() : "Regular";
                Log("configured Arabic font: '" + customFamily + "' style '" + customStyle + "'");
            }
            catch (Exception e)
            {
                Log("font.txt read failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ reflection
        static void Resolve()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            try
            {
                IL2Assembly tmp = Assembler.GetAssembly("Unity.TextMeshPro");
                tmpFontAssetClass = tmp.GetClass("TMP_FontAsset", "TMPro");
                Log("TMP_FontAsset=" + (tmpFontAssetClass != null));
                if (tmpFontAssetClass != null)
                {
                    foreach (IL2Method m in tmpFontAssetClass.GetMethods(x => x.Name == "CreateFontAsset"))
                    {
                        Log("  CreateFontAsset sig: " + m.GetSignature());
                    }

                    // Preferred (TMP 3.2): CreateFontAsset(string familyName, string styleName, int pointSize)
                    createFontAssetFromName = tmpFontAssetClass.GetMethod("CreateFontAsset", x =>
                    {
                        IL2Param[] p = x.GetParameters();
                        return p.Length == 3 && p[0].Type.Name.Contains("String") &&
                               p[1].Type.Name.Contains("String") && p[2].Type.Name.Contains("Int32");
                    });
                    if (createFontAssetFromName == null)
                    {
                        createFontAssetFromName = tmpFontAssetClass.GetMethod("CreateFontAsset", x =>
                        {
                            IL2Param[] p = x.GetParameters();
                            return p.Length == 2 && p[0].Type.Name.Contains("String") && p[1].Type.Name.Contains("String");
                        });
                    }

                    // CreateFontAsset(string fontFilePath, int faceIndex, int pointSize, int padding,
                    //                  GlyphRenderMode renderMode, int atlasWidth, int atlasHeight)
                    createFontAssetFromPath = tmpFontAssetClass.GetMethod("CreateFontAsset", x =>
                    {
                        IL2Param[] p = x.GetParameters();
                        return p.Length == 7 && p[0].Type.Name.Contains("String");
                    });

                    // Legacy: CreateFontAsset(UnityEngine.Font)
                    createFontAssetFromFont = tmpFontAssetClass.GetMethod("CreateFontAsset", x => x.GetParameters().Length == 1);

                    // The per-font fallback table is a public property in TMP 3.2 (backed by
                    // a private m_FallbackFontAssetTable field).
                    fallbackProp = tmpFontAssetClass.GetProperty("fallbackFontAssetTable");
                    if (fallbackProp == null)
                    {
                        fallbackField = tmpFontAssetClass.GetField("fallbackFontAssetTable");
                    }
                    if (fallbackField == null)
                    {
                        fallbackField = tmpFontAssetClass.GetField("m_FallbackFontAssetTable");
                    }
                    Log("  fallbackFontAssetTable property=" + (fallbackProp != null) +
                        " field=" + (fallbackField != null));
                }

                tmpSettingsClass = tmp.GetClass("TMP_Settings", "TMPro");
                Log("TMP_Settings=" + (tmpSettingsClass != null));
                if (tmpSettingsClass != null)
                {
                    tmpSettingsFallback = tmpSettingsClass.GetProperty("fallbackFontAssets");
                    tmpSettingsFallbackOS = tmpSettingsClass.GetProperty("fallbackOSFontAssets");
                    tmpSettingsInstance = tmpSettingsClass.GetProperty("instance");
                    tmpSettingsFallbackField = tmpSettingsClass.GetField("m_fallbackFontAssets");
                    Log("  fallbackFontAssets=" + (tmpSettingsFallback != null) +
                        " fallbackOSFontAssets=" + (tmpSettingsFallbackOS != null) +
                        " instance=" + (tmpSettingsInstance != null) +
                        " m_fallbackFontAssets=" + (tmpSettingsFallbackField != null));
                }

                IL2Class textClass = tmp.GetClass("TMP_Text", "TMPro");
                if (textClass != null)
                {
                    tmpTextFont = textClass.GetProperty("font");
                }
                Log("TMP_Text.font=" + (tmpTextFont != null));

                IL2Class assetClass = tmp.GetClass("TMP_Asset", "TMPro");
                if (assetClass != null)
                {
                    tmpAssetMaterial = assetClass.GetProperty("material");
                }
                Log("TMP_Asset.material=" + (tmpAssetMaterial != null));

                try
                {
                    IL2Assembly core = Assembler.GetAssembly("UnityEngine.CoreModule");
                    IL2Class matClass = core != null ? core.GetClass("Material", "UnityEngine") : null;
                    if (matClass != null)
                    {
                        materialShader = matClass.GetProperty("shader");
                    }
                }
                catch (Exception e)
                {
                    Log("Material resolve failed: " + e.Message);
                }
                Log("Material.shader=" + (materialShader != null));

                IL2Assembly textRendering = Assembler.GetAssembly("UnityEngine.TextRenderingModule");
                IL2Class fontClass = textRendering.GetClass("Font", "UnityEngine");
                if (fontClass != null)
                {
                    foreach (IL2Method m in fontClass.GetMethods(x => x.Name == "CreateDynamicFontFromOSFont"))
                    {
                        Log("  CreateDynamicFontFromOSFont sig: " + m.GetSignature());
                    }
                    createOSFont = fontClass.GetMethod("CreateDynamicFontFromOSFont", x =>
                    {
                        IL2Param[] p = x.GetParameters();
                        return p.Length == 2 && p[0].Type.Name.Contains("String") && p[1].Type.Name.Contains("Int32");
                    });
                    getPathsToOSFonts = fontClass.GetMethod("GetPathsToOSFonts", x => x.GetParameters().Length == 0);
                    Log("  GetPathsToOSFonts=" + (getPathsToOSFonts != null));
                }

                // GlyphRenderMode.SDFAA (needed for the file path overload)
                try
                {
                    IL2Assembly fontEngine = Assembler.GetAssembly("UnityEngine.TextCoreFontEngineModule");
                    IL2Class grm = null;
                    if (fontEngine != null)
                    {
                        grm = fontEngine.GetClass("GlyphRenderMode", "UnityEngine.TextCore.LowLevel");
                        if (grm == null)
                        {
                            grm = fontEngine.GetClass("GlyphRenderMode");
                        }
                    }
                    if (grm != null)
                    {
                        foreach (IL2Field f in grm.GetFields())
                        {
                            if (f.Name == "SDFAA")
                            {
                                IL2Object v = f.GetValue();
                                if (v != null)
                                {
                                    sdfaaValue = v.GetValueRef<int>();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log("GlyphRenderMode resolve failed: " + e.Message);
                }

                Log("resolved (fromName=" + (createFontAssetFromName != null) +
                    ", fromPath=" + (createFontAssetFromPath != null) +
                    ", fromFont=" + (createFontAssetFromFont != null) +
                    ", osFont=" + (createOSFont != null) +
                    ", sdfaa=" + sdfaaValue + ")");
            }
            catch (Exception e)
            {
                Log("Resolve failed: " + e);
            }
        }

        static void Hook()
        {
            if (hooked)
            {
                return;
            }
            hooked = true;
            try
            {
                if (tmpFontAssetClass != null)
                {
                    hookFontAsset = TryHook(tmpFontAssetClass, "OnEnable", FontAssetEnable);
                    if (hookFontAsset == null)
                    {
                        hookFontAsset = TryHook(tmpFontAssetClass, "Awake", FontAssetEnable);
                    }
                }
                IL2Class textClass = Assembler.GetAssembly("Unity.TextMeshPro").GetClass("TMP_Text", "TMPro");
                if (textClass != null)
                {
                    hookText = TryHook(textClass, "OnEnable", TextEnable);
                    if (hookText == null)
                    {
                        hookText = TryHook(textClass, "Awake", TextEnable);
                    }
                    IL2Property fontProp = textClass.GetProperty("font");
                    if (fontProp != null && fontProp.GetSetMethod() != null)
                    {
                        try
                        {
                            hookSetFont = new Hook<Del_SetFont>(SetFontImpl, fontProp.GetSetMethod());
                        }
                        catch (Exception e)
                        {
                            Log("set font hook failed: " + e.Message);
                        }
                    }
                }
                Log("hooks: fontAsset=" + (hookFontAsset != null) + " text=" + (hookText != null) +
                    " setFont=" + (hookSetFont != null));
            }
            catch (Exception e)
            {
                Log("Hook failed: " + e.Message);
            }
        }

        static Hook<Del_Void> TryHook(IL2Class classInfo, string methodName, Del_Void handler)
        {
            try
            {
                if (classInfo == null)
                {
                    return null;
                }
                IL2Method method = classInfo.GetMethod(methodName, x => x.GetParameters().Length == 0);
                if (method == null)
                {
                    return null;
                }
                return new Hook<Del_Void>(handler, method);
            }
            catch (Exception e)
            {
                Log("TryHook(" + methodName + ") failed: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ hooks
        static void FontAssetEnable(IntPtr thisPtr)
        {
            try { hookFontAsset.Original(thisPtr); } catch { }
            try
            {
                EnsureArabic();
                if (arabicAsset != IntPtr.Zero && thisPtr != arabicAsset)
                {
                    AddFallback(thisPtr);
                    RegisterGlobal();
                }
            }
            catch (Exception e)
            {
                Log("font asset hook failed: " + e.Message);
            }
        }

        static void TextEnable(IntPtr thisPtr)
        {
            try { hookText.Original(thisPtr); } catch { }
            try
            {
                EnsureArabic();
                if (arabicAsset != IntPtr.Zero && tmpTextFont != null)
                {
                    IL2Method get = tmpTextFont.GetGetMethod();
                    if (get != null)
                    {
                        IL2Object font = get.Invoke(thisPtr);
                        if (font != null && font.ptr != IntPtr.Zero)
                        {
                            AddFallback(font.ptr);
                        }
                    }
                }
                RegisterGlobal();
            }
            catch (Exception e)
            {
                Log("text hook failed: " + e.Message);
            }
        }

        static void SetFontImpl(IntPtr thisPtr, IntPtr value)
        {
            try { hookSetFont.Original(thisPtr, value); } catch { }
            try
            {
                EnsureArabic();
                if (value != IntPtr.Zero)
                {
                    AddFallback(value);
                }
                RegisterGlobal();
            }
            catch (Exception e)
            {
                Log("set font hook failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ font asset
        static void EnsureArabic()
        {
            if (arabicAsset != IntPtr.Zero || createAttempts >= 6)
            {
                return;
            }
            createAttempts++;

            // 0) The font chosen in the launcher, when one is configured.
            if (createFontAssetFromName != null && !string.IsNullOrEmpty(customFamily))
            {
                if (TryCreateFromFamily(customFamily, customStyle))
                {
                    return;
                }
                if (!string.Equals(customStyle, "Regular", StringComparison.OrdinalIgnoreCase) &&
                    TryCreateFromFamily(customFamily, "Regular"))
                {
                    return;
                }
                if (TryCreateFromFamily(customFamily, "Normal"))
                {
                    return;
                }
                if (TryCreateFromFamily(customFamily, "Bold"))
                {
                    return;
                }
                Log("configured font '" + customFamily + "' failed; using the default font list");
            }

            // 1) TMP 3.2 DynamicOS font asset built from a system font family.
            if (createFontAssetFromName != null)
            {
                foreach (string[] pair in OsFonts)
                {
                    if (TryCreateFromFamily(pair[0], pair[1]))
                    {
                        return;
                    }
                }
            }

            // 2) Build the font asset straight from an OS font file.
            if (createFontAssetFromPath != null && sdfaaValue >= 0)
            {
                string[] paths = GetOSFontPaths();
                if (paths.Length > 0)
                {
                    List<string> ordered = new List<string>();
                    List<string> rest = new List<string>();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        string name = SafeFileName(paths[i]);
                        bool preferred = false;
                        for (int j = 0; j < PreferredFontFiles.Length; j++)
                        {
                            if (name == PreferredFontFiles[j])
                            {
                                preferred = true;
                                break;
                            }
                        }
                        if (preferred)
                        {
                            ordered.Add(paths[i]);
                        }
                        else if (rest.Count < 60)
                        {
                            rest.Add(paths[i]);
                        }
                    }
                    ordered.AddRange(rest);
                    foreach (string path in ordered)
                    {
                        if (TryCreateFromPath(path))
                        {
                            return;
                        }
                    }
                }
            }

            // 3) Legacy: Font.CreateDynamicFontFromOSFont + CreateFontAsset(Font).
            if (createOSFont != null && createFontAssetFromFont != null)
            {
                foreach (string[] pair in OsFonts)
                {
                    try
                    {
                        int size = 90;
                        IL2Object fontObj = createOSFont.Invoke(new IntPtr[] { new IL2String(pair[0]).ptr, new IntPtr(&size) });
                        if (fontObj == null || fontObj.ptr == IntPtr.Zero)
                        {
                            continue;
                        }
                        IL2Object asset = createFontAssetFromFont.Invoke(new IntPtr[] { fontObj.ptr });
                        if (asset != null && asset.ptr != IntPtr.Zero)
                        {
                            arabicAsset = asset.ptr;
                            arabicHandle = Import.Handler.il2cpp_gchandle_new(arabicAsset, true);
                            Log("created Arabic font asset from OS font object '" + pair[0] + "' ptr=" + arabicAsset.ToString("X"));
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Log("legacy font '" + pair[0] + "' failed: " + e.Message);
                    }
                }
            }

            Log("could not create Arabic font asset (attempt " + createAttempts + ")");
        }

        static bool TryCreateFromFamily(string familyName, string styleName)
        {
            try
            {
                int size = 90;
                IL2Object asset = createFontAssetFromName.Invoke(new IntPtr[]
                {
                    new IL2String(familyName).ptr,
                    new IL2String(styleName).ptr,
                    new IntPtr(&size)
                });
                if (asset != null && asset.ptr != IntPtr.Zero)
                {
                    Accept(asset.ptr, "family '" + familyName + " " + styleName + "'");
                    return true;
                }
                Log("CreateFontAsset(family='" + familyName + "', style='" + styleName + "') -> null");
            }
            catch (Exception e)
            {
                Log("family font '" + familyName + "' failed: " + e.Message);
            }
            return false;
        }

        static bool TryCreateFromPath(string path)
        {
            try
            {
                int faceIndex = 0;
                int pointSize = 90;
                int padding = 9;
                int mode = sdfaaValue;
                int width = 1024;
                int height = 1024;
                IL2Object asset = createFontAssetFromPath.Invoke(new IntPtr[]
                {
                    new IL2String(path).ptr,
                    new IntPtr(&faceIndex),
                    new IntPtr(&pointSize),
                    new IntPtr(&padding),
                    new IntPtr(&mode),
                    new IntPtr(&width),
                    new IntPtr(&height)
                });
                if (asset != null && asset.ptr != IntPtr.Zero)
                {
                    Accept(asset.ptr, "file '" + path + "'");
                    return true;
                }
            }
            catch (Exception e)
            {
                Log("file font '" + path + "' failed: " + e.Message);
            }
            return false;
        }

        static void Accept(IntPtr asset, string how)
        {
            arabicAsset = asset;
            arabicHandle = Import.Handler.il2cpp_gchandle_new(arabicAsset, true);
            Log("created Arabic font asset from " + how + " ptr=" + arabicAsset.ToString("X"));
            LogMaterialInfo(asset);
        }

        /// <summary>
        /// Read-only diagnostic: a DynamicOS font asset whose material has no shader would
        /// render as blank text instead of Arabic glyphs.
        /// </summary>
        static void LogMaterialInfo(IntPtr asset)
        {
            try
            {
                if (tmpAssetMaterial == null)
                {
                    return;
                }
                IL2Method getMat = tmpAssetMaterial.GetGetMethod();
                if (getMat == null)
                {
                    return;
                }
                IL2Object mat = getMat.Invoke(asset);
                IntPtr matPtr = mat != null ? mat.ptr : IntPtr.Zero;
                Log("  material ptr=" + matPtr.ToString("X"));
                if (matPtr == IntPtr.Zero || materialShader == null)
                {
                    return;
                }
                IL2Method getShader = materialShader.GetGetMethod();
                if (getShader == null)
                {
                    return;
                }
                IL2Object shader = getShader.Invoke(matPtr);
                Log("  material.shader ptr=" + (shader != null ? shader.ptr.ToString("X") : "null"));
            }
            catch (Exception e)
            {
                Log("  material info failed: " + e.Message);
            }
        }

        static string[] GetOSFontPaths()
        {
            try
            {
                if (getPathsToOSFonts == null)
                {
                    return new string[0];
                }
                IL2Object res = getPathsToOSFonts.Invoke();
                if (res == null)
                {
                    Log("GetPathsToOSFonts -> null");
                    return new string[0];
                }
                string[] paths = res.UnboxArray<string>();
                if (paths == null)
                {
                    paths = new string[0];
                }
                if (!loggedOsPaths)
                {
                    loggedOsPaths = true;
                    Log("GetPathsToOSFonts count=" + paths.Length);
                    int shown = paths.Length < 8 ? paths.Length : 8;
                    for (int i = 0; i < shown; i++)
                    {
                        Log("  os font: " + paths[i]);
                    }
                }
                return paths;
            }
            catch (Exception e)
            {
                Log("GetPathsToOSFonts failed: " + e.Message);
                return new string[0];
            }
        }

        static string SafeFileName(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                return name == null ? string.Empty : name.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        static void RegisterGlobal()
        {
            if (globalRegistered || arabicAsset == IntPtr.Zero)
            {
                return;
            }

            // Preferred: TMP_Settings.fallbackFontAssets property (List<TMP_FontAsset>).
            try
            {
                if (tmpSettingsFallback != null)
                {
                    IL2Method get = tmpSettingsFallback.GetGetMethod();
                    Log("RegisterGlobal: getter=" + (get != null) + " static=" + (get != null && get.IsStatic) +
                        " ret=" + (get != null ? get.ReturnType.Name : "?"));
                    if (get != null)
                    {
                        IL2Object listObj = get.Invoke();
                        Log("RegisterGlobal: fallbackFontAssets ptr=" + (listObj != null ? listObj.ptr.ToString("X") : "null"));
                        if (listObj != null && listObj.ptr != IntPtr.Zero)
                        {
                            IL2ListExplicit list = new IL2ListExplicit(listObj.ptr, tmpFontAssetClass);
                            list.Add(arabicAsset);
                            globalRegistered = true;
                            Log("registered global TMP fallback (property), count=" + list.Count);
                            return;
                        }
                        Log("TMP_Settings.fallbackFontAssets returned null");
                    }
                }
            }
            catch (Exception e)
            {
                Log("global fallback (property) failed: " + e.Message);
            }

            // Fallback: TMP_Settings.instance.m_fallbackFontAssets field.
            try
            {
                if (tmpSettingsInstance != null && tmpSettingsFallbackField != null)
                {
                    IL2Method getInstance = tmpSettingsInstance.GetGetMethod();
                    if (getInstance != null)
                    {
                        IL2Object inst = getInstance.Invoke();
                        if (inst != null && inst.ptr != IntPtr.Zero)
                        {
                            IL2Object listObj = tmpSettingsFallbackField.GetValue(inst.ptr);
                            IntPtr listPtr = listObj != null ? listObj.ptr : IntPtr.Zero;
                            IL2ListExplicit list = new IL2ListExplicit(listPtr, tmpFontAssetClass, true);
                            if (listPtr == IntPtr.Zero)
                            {
                                tmpSettingsFallbackField.SetValue(inst.ptr, list.ptr);
                            }
                            list.Add(arabicAsset);
                            globalRegistered = true;
                            Log("registered global TMP fallback (field), count=" + list.Count);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log("global fallback (field) failed: " + e.Message);
            }
        }

        /// <summary>
        /// TMP's own OS font fallback list. When present it is a List&lt;string&gt; of font
        /// family names; TMP then builds DynamicOS assets for missing glyphs by itself.
        /// </summary>
        static void RegisterGlobalOS()
        {
            if (globalOSRegistered || tmpSettingsFallbackOS == null)
            {
                return;
            }
            try
            {
                IL2Method get = tmpSettingsFallbackOS.GetGetMethod();
                if (get == null)
                {
                    return;
                }
                string ret = get.ReturnType.Name;
                if (ret == null || !ret.Contains("String"))
                {
                    Log("fallbackOSFontAssets type is not List<string>: " + ret);
                    globalOSRegistered = true;
                    return;
                }
                IL2Object listObj = get.Invoke();
                if (listObj == null || listObj.ptr == IntPtr.Zero)
                {
                    Log("fallbackOSFontAssets is null");
                    return;
                }
                IL2ListExplicit list = new IL2ListExplicit(listObj.ptr, IL2SystemClass.String);
                foreach (string[] pair in OsFonts)
                {
                    list.Add(new IL2String(pair[0]).ptr);
                }
                globalOSRegistered = true;
                Log("registered OS fallback font names (count=" + list.Count + ")");
            }
            catch (Exception e)
            {
                Log("fallbackOSFontAssets failed: " + e.Message);
            }
        }

        static void AddFallback(IntPtr fontAsset)
        {
            if (arabicAsset == IntPtr.Zero || fontAsset == IntPtr.Zero || fontAsset == arabicAsset)
            {
                return;
            }
            if ((fallbackProp == null && fallbackField == null) || augmented.Contains(fontAsset))
            {
                return;
            }
            try
            {
                IntPtr listPtr = IntPtr.Zero;
                IL2ListExplicit list = null;
                if (fallbackProp != null)
                {
                    IL2Method get = fallbackProp.GetGetMethod();
                    if (get != null)
                    {
                        IL2Object current = get.Invoke(fontAsset);
                        listPtr = current != null ? current.ptr : IntPtr.Zero;
                    }
                    list = new IL2ListExplicit(listPtr, tmpFontAssetClass, true);
                    if (listPtr == IntPtr.Zero)
                    {
                        IL2Method set = fallbackProp.GetSetMethod();
                        if (set != null)
                        {
                            set.Invoke(fontAsset, new IntPtr[] { list.ptr });
                        }
                    }
                }
                else
                {
                    IL2Object current = fallbackField.GetValue(fontAsset);
                    listPtr = current != null ? current.ptr : IntPtr.Zero;
                    list = new IL2ListExplicit(listPtr, tmpFontAssetClass, true);
                    if (listPtr == IntPtr.Zero)
                    {
                        fallbackField.SetValue(fontAsset, list.ptr);
                    }
                }
                list.Add(arabicAsset);
                augmented.Add(fontAsset);
                Log("added Arabic fallback to font asset " + fontAsset.ToString("X") + " (count=" + list.Count + ")");
            }
            catch (Exception e)
            {
                Log("AddFallback failed: " + e.Message);
            }
        }
    }
}
