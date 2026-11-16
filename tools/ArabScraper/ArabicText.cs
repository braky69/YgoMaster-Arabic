using System;
using System.Collections.Generic;
using System.Text;

// Arabic text preparation for engines that do NOT perform OpenType shaping or the
// Unicode Bidirectional Algorithm (e.g. Unity TextMeshPro without HarfBuzz).
//
// Prepare() does two things:
//   1. Shape()  : converts logical Arabic letters into their contextual presentation
//                 forms (Arabic Presentation Forms-B, U+FE70..U+FEFF) and forms the
//                 Lam-Alef ligatures.
//   2. Reorder(): runs a practical subset of the Unicode Bidirectional Algorithm so the
//                 result is in VISUAL order (left-to-right), which is what a plain
//                 left-to-right text renderer needs.
//
// The output is intended to be drawn as-is by a simple LTR renderer.
public static class ArabicText
{
    // ---------------------------------------------------------------- joining table
    private enum J { N, R, D, T } // Non-joining, Right-joining, Dual-joining, Transparent

    private struct Form
    {
        public J Type;
        public char Iso, Fin, Ini, Med;
        public Form(J t, char iso, char fin, char ini, char med) { Type = t; Iso = iso; Fin = fin; Ini = ini; Med = med; }
        public Form(J t, char iso) { Type = t; Iso = iso; Fin = iso; Ini = iso; Med = iso; }
        public Form(J t, char iso, char fin) { Type = t; Iso = iso; Fin = fin; Ini = iso; Med = fin; }
    }

    private static readonly Dictionary<char, Form> Forms = new Dictionary<char, Form>();

    static ArabicText()
    {
        // --- Standard Arabic (U+0620..U+064A) ---
        Forms['\u0621'] = new Form(J.N, '\uFE80');                              // hamza
        Forms['\u0622'] = new Form(J.R, '\uFE81', '\uFE82');                    // alef madda
        Forms['\u0623'] = new Form(J.R, '\uFE83', '\uFE84');                    // alef hamza above
        Forms['\u0624'] = new Form(J.R, '\uFE85', '\uFE86');                    // waw hamza
        Forms['\u0625'] = new Form(J.R, '\uFE87', '\uFE88');                    // alef hamza below
        Forms['\u0626'] = new Form(J.D, '\uFE89', '\uFE8A', '\uFE8B', '\uFE8C'); // yeh hamza
        Forms['\u0627'] = new Form(J.R, '\uFE8D', '\uFE8E');                    // alef
        Forms['\u0628'] = new Form(J.D, '\uFE8F', '\uFE90', '\uFE91', '\uFE92'); // beh
        Forms['\u0629'] = new Form(J.R, '\uFE93', '\uFE94');                    // teh marbuta
        Forms['\u062A'] = new Form(J.D, '\uFE95', '\uFE96', '\uFE97', '\uFE98'); // teh
        Forms['\u062B'] = new Form(J.D, '\uFE99', '\uFE9A', '\uFE9B', '\uFE9C'); // theh
        Forms['\u062C'] = new Form(J.D, '\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0'); // jeem
        Forms['\u062D'] = new Form(J.D, '\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4'); // hah
        Forms['\u062E'] = new Form(J.D, '\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8'); // khah
        Forms['\u062F'] = new Form(J.R, '\uFEA9', '\uFEAA');                    // dal
        Forms['\u0630'] = new Form(J.R, '\uFEAB', '\uFEAC');                    // thal
        Forms['\u0631'] = new Form(J.R, '\uFEAD', '\uFEAE');                    // reh
        Forms['\u0632'] = new Form(J.R, '\uFEAF', '\uFEB0');                    // zain
        Forms['\u0633'] = new Form(J.D, '\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4'); // seen
        Forms['\u0634'] = new Form(J.D, '\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8'); // sheen
        Forms['\u0635'] = new Form(J.D, '\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC'); // sad
        Forms['\u0636'] = new Form(J.D, '\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0'); // dad
        Forms['\u0637'] = new Form(J.D, '\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4'); // tah
        Forms['\u0638'] = new Form(J.D, '\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8'); // zah
        Forms['\u0639'] = new Form(J.D, '\uFEC9', '\uFECA', '\uFECB', '\uFECC'); // ain
        Forms['\u063A'] = new Form(J.D, '\uFECD', '\uFECE', '\uFECF', '\uFED0'); // ghain
        Forms['\u0640'] = new Form(J.D, '\u0640', '\u0640', '\u0640', '\u0640'); // tatweel
        Forms['\u0641'] = new Form(J.D, '\uFED1', '\uFED2', '\uFED3', '\uFED4'); // feh
        Forms['\u0642'] = new Form(J.D, '\uFED5', '\uFED6', '\uFED7', '\uFED8'); // qaf
        Forms['\u0643'] = new Form(J.D, '\uFED9', '\uFEDA', '\uFEDB', '\uFEDC'); // kaf
        Forms['\u0644'] = new Form(J.D, '\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0'); // lam
        Forms['\u0645'] = new Form(J.D, '\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4'); // meem
        Forms['\u0646'] = new Form(J.D, '\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8'); // noon
        Forms['\u0647'] = new Form(J.D, '\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC'); // heh
        Forms['\u0648'] = new Form(J.R, '\uFEED', '\uFEEE');                    // waw
        Forms['\u0649'] = new Form(J.D, '\uFEEF', '\uFEF0', '\uFBE8', '\uFBE9'); // alef maksura
        Forms['\u064A'] = new Form(J.D, '\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4'); // yeh

        // --- Common extended letters (Persian / Urdu / Arabic Supplement) ---
        Forms['\u0679'] = new Form(J.D, '\uFB66', '\uFB67', '\uFB68', '\uFB69'); // tteh
        Forms['\u067A'] = new Form(J.D, '\uFB5E', '\uFB5F', '\uFB60', '\uFB61'); // tteheh
        Forms['\u067B'] = new Form(J.D, '\uFB52', '\uFB53', '\uFB54', '\uFB55'); // beeh
        Forms['\u067E'] = new Form(J.D, '\uFB56', '\uFB57', '\uFB58', '\uFB59'); // peh
        Forms['\u0680'] = new Form(J.D, '\uFB5A', '\uFB5B', '\uFB5C', '\uFB5D'); // beheh
        Forms['\u0683'] = new Form(J.D, '\uFB76', '\uFB77', '\uFB78', '\uFB79'); // nyah
        Forms['\u0684'] = new Form(J.D, '\uFB72', '\uFB73', '\uFB74', '\uFB75'); // dyeh
        Forms['\u0686'] = new Form(J.D, '\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D'); // tcheh
        Forms['\u0687'] = new Form(J.D, '\uFB7E', '\uFB7F', '\uFB80', '\uFB81'); // tcheheh
        Forms['\u0688'] = new Form(J.R, '\uFB88', '\uFB89');                    // ddal
        Forms['\u0691'] = new Form(J.R, '\uFB8C', '\uFB8D');                    // rreh
        Forms['\u0698'] = new Form(J.R, '\uFB8A', '\uFB8B');                    // jeh
        Forms['\u06A9'] = new Form(J.D, '\uFB8E', '\uFB8F', '\uFB90', '\uFB91'); // keheh
        Forms['\u06AF'] = new Form(J.D, '\uFB92', '\uFB93', '\uFB94', '\uFB95'); // gaf
        Forms['\u06BA'] = new Form(J.D, '\uFB9E', '\uFB9F');                    // noon ghunna
        Forms['\u06BE'] = new Form(J.D, '\uFBAA', '\uFBAB', '\uFBAC', '\uFBAD'); // heh doachashmee
        Forms['\u06C0'] = new Form(J.R, '\uFBA4', '\uFBA5');                    // heh with yeh
        Forms['\u06C1'] = new Form(J.D, '\uFBA6', '\uFBA7', '\uFBA8', '\uFBA9'); // heh goal
        Forms['\u06CC'] = new Form(J.D, '\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF'); // farsi yeh
        Forms['\u06D2'] = new Form(J.R, '\uFBAE', '\uFBAF');                    // yeh barree
        Forms['\u06D3'] = new Form(J.R, '\uFBB0', '\uFBB1');                    // yeh barree hamza

        // Transparent characters (diacritics) - do not affect joining, kept in place.
        for (char c = '\u064B'; c <= '\u065F'; c++) Transparent.Add(c);
        Transparent.Add('\u0670');
        Transparent.Add('\u06D6'); Transparent.Add('\u06D7'); Transparent.Add('\u06D8');
        Transparent.Add('\u06D9'); Transparent.Add('\u06DA'); Transparent.Add('\u06DB');
        Transparent.Add('\u06DC'); Transparent.Add('\u06DF'); Transparent.Add('\u06E0');
        Transparent.Add('\u06E1'); Transparent.Add('\u06E2'); Transparent.Add('\u06E3');
        Transparent.Add('\u06E4'); Transparent.Add('\u06E7'); Transparent.Add('\u06E8');
        Transparent.Add('\u06EA'); Transparent.Add('\u06EB'); Transparent.Add('\u06EC');
        Transparent.Add('\u06ED');
        Transparent.Add('\u0610'); Transparent.Add('\u0611'); Transparent.Add('\u0612');
        Transparent.Add('\u0613'); Transparent.Add('\u0614'); Transparent.Add('\u0615');
        Transparent.Add('\u0616'); Transparent.Add('\u0617'); Transparent.Add('\u0618');
        Transparent.Add('\u0619'); Transparent.Add('\u061A');
    }

    private static readonly HashSet<char> Transparent = new HashSet<char>();

    // Lam + Alef ligatures: lam, alef -> (isolated, final)
    private static char Ligature(char alef, bool final)
    {
        switch (alef)
        {
            case '\u0622': return final ? '\uFEF6' : '\uFEF5';
            case '\u0623': return final ? '\uFEF8' : '\uFEF7';
            case '\u0625': return final ? '\uFEFA' : '\uFEF9';
            case '\u0627': return final ? '\uFEFC' : '\uFEFB';
            default: return '\0';
        }
    }

    public static string Prepare(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return ReorderLines(Shape(text));
    }

    // The Unicode bidi algorithm is defined per line (paragraph): each line is reordered
    // on its own and the lines keep their top-to-bottom order. Reordering the whole string
    // in one pass would also reverse the order of the lines themselves (the last line would
    // render first), which is what made multi-line card effects read from the bottom up.
    public static string ReorderLines(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\n') < 0)
        {
            return Reorder(s);
        }
        string[] lines = s.Split('\n');
        var sb = new StringBuilder(s.Length + 8);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            string line = lines[i];
            bool cr = line.Length > 0 && line[line.Length - 1] == '\r';
            if (cr) line = line.Substring(0, line.Length - 1);
            sb.Append(Reorder(line));
            if (cr) sb.Append('\r');
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ shaping
    public static string Shape(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int n = text.Length;
        char[] a = text.ToCharArray();

        var effType = new J[n];
        var consumed = new bool[n];
        var noForward = new bool[n];
        var ligPartner = new int[n];
        for (int i = 0; i < n; i++) ligPartner[i] = -1;

        for (int i = 0; i < n; i++)
        {
            if (Transparent.Contains(a[i])) { effType[i] = J.T; continue; }
            Form f;
            effType[i] = Forms.TryGetValue(a[i], out f) ? f.Type : J.N;
        }

        // Detect Lam-Alef ligature pairs.
        for (int i = 0; i < n; i++)
        {
            if (a[i] != '\u0644' || consumed[i]) continue;
            int j = NextIdx(a, consumed, i, n);
            if (j < 0) continue;
            if (a[j] == '\u0622' || a[j] == '\u0623' || a[j] == '\u0625' || a[j] == '\u0627')
            {
                ligPartner[i] = j;
                consumed[j] = true;
                noForward[i] = true;
            }
        }

        var sb = new StringBuilder(n + 16);
        for (int i = 0; i < n; i++)
        {
            char c = a[i];
            if (consumed[i]) continue;
            if (Transparent.Contains(c)) { sb.Append(c); continue; }

            Form f;
            if (!Forms.TryGetValue(c, out f)) { sb.Append(c); continue; }

            // Lam-Alef ligature output
            if (c == '\u0644' && noForward[i])
            {
                int j = ligPartner[i];
                int pj = PrevIdx(a, consumed, i);
                bool joinPrev = pj >= 0 && effType[pj] == J.D && !noForward[pj];
                char lig = j >= 0 ? Ligature(a[j], joinPrev) : '\0';
                if (lig != '\0') { sb.Append(lig); continue; }
            }

            int prev = PrevIdx(a, consumed, i);
            int next = NextIdx(a, consumed, i, n);

            bool canPrev = prev >= 0 && effType[prev] == J.D && !noForward[prev]
                           && (f.Type == J.D || f.Type == J.R);
            bool canNext = f.Type == J.D && !noForward[i]
                           && next >= 0 && (effType[next] == J.D || effType[next] == J.R);

            char res;
            if (canPrev && canNext) res = f.Med;
            else if (canPrev) res = f.Fin;
            else if (canNext) res = f.Ini;
            else res = f.Iso;
            sb.Append(res);
        }
        return sb.ToString();
    }

    private static int PrevIdx(char[] a, bool[] consumed, int i)
    {
        for (int k = i - 1; k >= 0; k--)
            if (!consumed[k] && !Transparent.Contains(a[k])) return k;
        return -1;
    }

    private static int NextIdx(char[] a, bool[] consumed, int i, int n)
    {
        for (int k = i + 1; k < n; k++)
            if (!consumed[k] && !Transparent.Contains(a[k])) return k;
        return -1;
    }

    // ------------------------------------------------------------------ bidi reorder
    private enum B { L, R, EN, AN, ON } // strong L, strong R, numbers, other neutral

    private static readonly Dictionary<char, char> Mirror = new Dictionary<char, char>
    {
        {'(',')'}, {')','('}, {'[',']'}, {']','['}, {'{','}'}, {'}','{'},
        {'<','>'}, {'>','<'}, {'\u00AB','\u00BB'}, {'\u00BB','\u00AB'},
        {'\u2039','\u203A'}, {'\u203A','\u2039'},
        {'\u3008','\u3009'}, {'\u3009','\u3008'}, {'\u300A','\u300B'}, {'\u300B','\u300A'},
        {'\u300C','\u300D'}, {'\u300D','\u300C'}, {'\u300E','\u300F'}, {'\u300F','\u300E'},
        {'\u3010','\u3011'}, {'\u3011','\u3010'}, {'\u3014','\u3015'}, {'\u3015','\u3014'},
        {'\u3016','\u3017'}, {'\u3017','\u3016'}, {'\u3018','\u3019'}, {'\u3019','\u3018'},
        {'\u301A','\u301B'}, {'\u301B','\u301A'}
    };

    private static B Classify(char c)
    {
        if (c >= '0' && c <= '9') return B.EN;
        if (c >= '\u0660' && c <= '\u0669') return B.AN; // Arabic-Indic digits
        if (c >= '\u06F0' && c <= '\u06F9') return B.AN; // Extended Arabic-Indic
        if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return B.L;
        if (c < 0x0590) return B.ON;
        if (c <= 0x05FF) return B.ON;                     // Hebrew block (not expected)
        if (c >= 0x0600 && c <= 0x06FF) return B.R;       // Arabic
        if (c >= 0x0750 && c <= 0x077F) return B.R;       // Arabic Supplement
        if (c >= 0x08A0 && c <= 0x08FF) return B.R;       // Arabic Extended-A
        if (c >= 0xFB50 && c <= 0xFDFF) return B.R;       // Presentation Forms-A
        if (c >= 0xFE70 && c <= 0xFEFF) return B.R;       // Presentation Forms-B
        if (c >= 0x0700 && c <= 0x074F) return B.R;       // Syriac (treat R)
        // Latin-1 / Latin Extended letters and general punctuation
        if (c >= 0x00C0 && c <= 0x024F) return B.L;
        return B.ON;
    }

    private static bool IsNeutral(B t) { return t == B.ON; }

    private static string Reorder(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int n = s.Length;
        char[] a = s.ToCharArray();

        // Group into clusters: a base char plus following transparent marks.
        var clusterStart = new List<int>();
        var clusterEnd = new List<int>();
        int i = 0;
        while (i < n)
        {
            int start = i;
            i++;
            while (i < n && Transparent.Contains(a[i])) i++;
            clusterStart.Add(start);
            clusterEnd.Add(i);
        }
        int cn = clusterStart.Count;

        // Per-cluster bidi type (base char decides; marks inherit).
        var type = new B[cn];
        for (int k = 0; k < cn; k++)
        {
            char baseChar = a[clusterStart[k]];
            type[k] = Transparent.Contains(baseChar) ? B.ON : Classify(baseChar);
        }

        // W7: EN preceded by L (skipping neutrals) becomes L.  W2: EN preceded by AN becomes AN.
        for (int k = 0; k < cn; k++)
        {
            if (type[k] != B.EN) continue;
            for (int j = k - 1; j >= 0; j--)
            {
                if (IsNeutral(type[j])) continue;
                if (type[j] == B.L) { type[k] = B.L; }
                else if (type[j] == B.AN) { type[k] = B.AN; }
                break;
            }
        }

        // Neutral resolution (simplified N1/N2). EN/AN act as R.
        for (int k = 0; k < cn; k++)
        {
            if (!IsNeutral(type[k])) continue;
            int j = k;
            while (j < cn && IsNeutral(type[j])) j++;
            B before = ResolveSide(type, k - 1);
            B after = ResolveSide(type, j);
            B dir = (before == after && before != B.ON) ? before : B.R; // paragraph dir = RTL
            if (dir == B.ON) dir = B.R;
            for (int m = k; m < j; m++) type[m] = dir;
            k = j - 1;
        }

        // Assign levels (base paragraph level = 1, RTL).
        var level = new int[cn];
        for (int k = 0; k < cn; k++)
            level[k] = (type[k] == B.L || type[k] == B.EN || type[k] == B.AN) ? 2 : 1;

        // L2: reorder by levels (highest to lowest odd level).
        int maxLevel = 0, minOdd = int.MaxValue;
        for (int k = 0; k < cn; k++)
        {
            if (level[k] > maxLevel) maxLevel = level[k];
            if (level[k] % 2 == 1 && level[k] < minOdd) minOdd = level[k];
        }
        if (minOdd == int.MaxValue) minOdd = 1;

        for (int lev = maxLevel; lev >= minOdd; lev--)
        {
            int k = 0;
            while (k < cn)
            {
                if (level[k] >= lev)
                {
                    int start = k;
                    while (k < cn && level[k] >= lev) k++;
                    ReverseClusters(clusterStart, clusterEnd, level, type, start, k - 1);
                }
                else k++;
            }
        }

        // Emit in visual order; mirror paired punctuation at odd levels (L4).
        var sb = new StringBuilder(n);
        for (int k = 0; k < cn; k++)
        {
            for (int p = clusterStart[k]; p < clusterEnd[k]; p++)
            {
                char ch = a[p];
                char mirror;
                if (level[k] % 2 == 1 && Mirror.TryGetValue(ch, out mirror)) ch = mirror;
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private static B ResolveSide(B[] type, int idx)
    {
        if (idx < 0 || idx >= type.Length) return B.ON;
        B t = type[idx];
        if (t == B.EN || t == B.AN) return B.R;
        return t;
    }

    private static void ReverseClusters(List<int> starts, List<int> ends, int[] level, B[] type, int lo, int hi)
    {
        while (lo < hi)
        {
            int ts = starts[lo], te = ends[lo], tl = level[lo]; B tt = type[lo];
            starts[lo] = starts[hi]; ends[lo] = ends[hi]; level[lo] = level[hi]; type[lo] = type[hi];
            starts[hi] = ts; ends[hi] = te; level[hi] = tl; type[hi] = tt;
            lo++; hi--;
        }
    }
}
