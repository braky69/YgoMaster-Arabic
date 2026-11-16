using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Matches arab-duelists.com cards (by normalized English name) to the game's card
// index and regenerates CARD_Name.bytes / CARD_Desc.bytes / CARD_Indx.bytes with
// Arabic text (pre-shaped + bidi-reordered for a plain LTR renderer).
class ArabGen
{
    static void Main(string[] args)
    {
        string repo = args.Length > 0 ? args[0] : ".";
        string cacheDir = args.Length > 1 ? args[1] : Path.Combine(repo, "arab-localization", "cache");
        string outDir = args.Length > 2 ? args[2] : Path.Combine(repo, "arab-localization", "out");
        string cardDataDir = Path.Combine(repo, "dist", "stage", "YgoMaster", "Data", "CardData", "en-US");

        Console.WriteLine("repo       = " + repo);
        Console.WriteLine("cache      = " + cacheDir);
        Console.WriteLine("cardData   = " + cardDataDir);

        // --- load game card data ---
        byte[] indx = File.ReadAllBytes(Path.Combine(cardDataDir, "CARD_Indx.bytes"));
        byte[] nameBytes = File.ReadAllBytes(Path.Combine(cardDataDir, "CARD_Name.bytes"));
        byte[] descBytes = File.ReadAllBytes(Path.Combine(cardDataDir, "CARD_Desc.bytes"));
        int n = indx.Length / 8;

        string[] names = new string[n];
        string[] descs = new string[n];
        for (int i = 0; i < n; i++)
        {
            names[i] = ReadCString(nameBytes, (int)BitConverter.ToUInt32(indx, i * 8));
            descs[i] = ReadCString(descBytes, (int)BitConverter.ToUInt32(indx, i * 8 + 4));
        }
        Console.WriteLine("game cards = " + n);

        // --- load site data ---
        var byExact = new Dictionary<string, SiteCard>();
        var byLoose = new Dictionary<string, SiteCard>();
        var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        int files = 0, bad = 0;
        foreach (var f in Directory.GetFiles(cacheDir, "*.json"))
        {
            string text;
            try { text = File.ReadAllText(f, Encoding.UTF8); } catch { bad++; continue; }
            Dictionary<string, object> d;
            try { d = js.Deserialize<Dictionary<string, object>>(text); } catch { bad++; continue; }
            if (d == null || !d.ContainsKey("name_official")) { bad++; continue; }
            string en = Str(d, "name_official");
            string ar = Str(d, "name");
            if (string.IsNullOrEmpty(en) || string.IsNullOrEmpty(ar)) { bad++; continue; }
            string arDescText = Str(d, "desc_main");
            string arPendText = Str(d, "desc_pendulum");
            var sc = new SiteCard { English = en, Arabic = ar, ArabicMain = Clean(arDescText), ArabicPend = Clean(arPendText), Id = Str(d, "id") };
            string k = Norm(en);
            if (!byExact.ContainsKey(k)) byExact[k] = sc;
            string lk = Loose(en);
            if (!string.IsNullOrEmpty(lk) && !byLoose.ContainsKey(lk)) byLoose[lk] = sc;
            files++;
        }
        Console.WriteLine("site cards = " + files + " (bad=" + bad + ")");

        // --- match ---
        int matchedExact = 0, matchedLoose = 0, unmatched = 0;
        var unmatchedNames = new List<string>();
        var arName = new string[n];
        var arDesc = new string[n];
        for (int i = 0; i < n; i++)
        {
            string en = names[i];
            if (string.IsNullOrEmpty(en)) continue;
            SiteCard sc;
            if (byExact.TryGetValue(Norm(en), out sc)) matchedExact++;
            else if (byLoose.TryGetValue(Loose(en), out sc)) matchedLoose++;
            else { unmatched++; if (unmatchedNames.Count < 5000) unmatchedNames.Add(en); continue; }
            arName[i] = ArabicText.Prepare(sc.Arabic);
            string main = string.IsNullOrEmpty(sc.ArabicMain) ? null : ArabicText.Prepare(sc.ArabicMain);
            string pend = string.IsNullOrEmpty(sc.ArabicPend) ? null : ArabicText.Prepare(sc.ArabicPend);
            if (main == null && pend == null)
            {
                arDesc[i] = null;
            }
            else if (pend == null)
            {
                arDesc[i] = main;
            }
            else
            {
                // Keep the exact separator the game's Content class splits on
                // (verified against the shipped en-US CARD_Desc.bytes: "\n[Pendulum Effect]\n").
                arDesc[i] = (main ?? string.Empty) + "\n[Pendulum Effect]\n" + pend;
            }
        }
        Console.WriteLine("matched exact = " + matchedExact + ", loose = " + matchedLoose + ", unmatched = " + unmatched);

        // --- rebuild name / desc / indx ---
        Directory.CreateDirectory(outDir);
        using (var nameOut = new MemoryStream())
        using (var descOut = new MemoryStream())
        {
            var indxOut = new MemoryStream(n * 8);
            var bw = new BinaryWriter(indxOut);
            for (int i = 0; i < n; i++)
            {
                uint no = (uint)nameOut.Position;
                string nm = arName[i] ?? names[i];
                WriteCString(nameOut, nm);
                uint dof = (uint)descOut.Position;
                string dc = arDesc[i] ?? descs[i];
                WriteCString(descOut, dc);
                bw.Write(no);
                bw.Write(dof);
            }
            File.WriteAllBytes(Path.Combine(outDir, "CARD_Indx.bytes"), indxOut.ToArray());
            File.WriteAllBytes(Path.Combine(outDir, "CARD_Name.bytes"), nameOut.ToArray());
            File.WriteAllBytes(Path.Combine(outDir, "CARD_Desc.bytes"), descOut.ToArray());
        }
        File.WriteAllLines(Path.Combine(outDir, "unmatched.txt"), unmatchedNames.ToArray(), new UTF8Encoding(false));
        Console.WriteLine("Wrote Arabic pack to " + outDir);
    }

    class SiteCard { public string English, Arabic, ArabicMain, ArabicPend, Id; }

    static string Str(Dictionary<string, object> d, string key)
    {
        object o;
        if (d.TryGetValue(key, out o) && o != null) return o.ToString();
        return "";
    }

    static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = Regex.Replace(s, "<[^>]+>", " ");
        s = System.Net.WebUtility.HtmlDecode(s);
        return s.Trim();
    }

    static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else sb.Append(' ');
        }
        return Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
    }

    static string Loose(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    static string ReadCString(byte[] b, int off)
    {
        if (off < 0 || off >= b.Length) return "";
        int end = off;
        while (end < b.Length && b[end] != 0) end++;
        return Encoding.UTF8.GetString(b, off, end - off);
    }

    static void WriteCString(Stream s, string str)
    {
        if (string.IsNullOrEmpty(str)) { s.WriteByte(0); return; }
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte(0);
    }
}
