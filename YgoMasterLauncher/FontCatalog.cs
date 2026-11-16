using System;
using System.Collections.Generic;
using System.Drawing.Text;

namespace YgoMasterLauncher
{
    /// <summary>A single font face (family + style).</summary>
    public struct FontFace
    {
        public string Family;
        public string Style;

        public FontFace(string family, string style)
        {
            Family = family;
            Style = style;
        }

        public string Key { get { return Family + "|" + Style; } }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Style) || Style.Equals("Regular", StringComparison.OrdinalIgnoreCase))
            {
                return Family;
            }
            return Family + "  (" + Style + ")";
        }
    }

    /// <summary>
    /// A short hand-picked list of clean Arabic UI fonts. Every one of them contains the
    /// Arabic Presentation Forms-B block (U+FE70..U+FEFF) that the pre-shaped card text
    /// needs, so any entry the user picks renders correctly. Only the fonts actually
    /// installed on this PC are shown.
    /// </summary>
    static class FontCatalog
    {
        static readonly FontFace[] Curated =
        {
            new FontFace("Arial", "Regular"),
            new FontFace("Tahoma", "Regular"),
            new FontFace("Segoe UI", "Regular"),
            new FontFace("Microsoft Sans Serif", "Regular"),
            new FontFace("Calibri", "Regular"),
            new FontFace("Times New Roman", "Regular"),
            new FontFace("Sakkal Majalla", "Regular"),
            new FontFace("Neo Sans Arabic", "Regular"),
            new FontFace("Droid Arabic Naskh", "Regular"),
            new FontFace("Amiri", "Regular")
        };

        public static readonly FontFace Default = new FontFace("Arial", "Regular");

        /// <summary>Returns the curated fonts that are installed on this PC (Arial always first).</summary>
        public static List<FontFace> GetAvailable()
        {
            HashSet<string> installed = InstalledFamilies();
            List<FontFace> result = new List<FontFace>();
            foreach (FontFace face in Curated)
            {
                if (installed.Contains(face.Family))
                {
                    result.Add(face);
                }
            }
            if (!installed.Contains(Default.Family) && result.Count == 0)
            {
                result.Add(Default);
            }
            return result;
        }

        static HashSet<string> InstalledFamilies()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (InstalledFontCollection collection = new InstalledFontCollection())
                {
                    foreach (System.Drawing.FontFamily family in collection.Families)
                    {
                        set.Add(family.Name);
                    }
                }
            }
            catch
            {
            }
            return set;
        }
    }
}
