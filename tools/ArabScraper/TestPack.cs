using System;
using System.IO;
using YgoMasterConfig;

class TestPack
{
    static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "arabpack_test_" + Guid.NewGuid().ToString("N"));
        string cardDir = Path.Combine(root, "Data", "CardData", "en-US");
        string arDir = Path.Combine(root, "Data", "ArabicPack", "ar");
        string origDir = Path.Combine(root, "Data", "ArabicPack", "original");
        Directory.CreateDirectory(cardDir);
        Directory.CreateDirectory(arDir);
        Directory.CreateDirectory(origDir);

        string[] files = { "CARD_Indx.bytes", "CARD_Name.bytes", "CARD_Desc.bytes" };
        foreach (string f in files)
        {
            File.WriteAllText(Path.Combine(cardDir, f), "ENGLISH-" + f);
            File.WriteAllText(Path.Combine(arDir, f), "ARABIC-" + f);
            File.WriteAllText(Path.Combine(origDir, f), "ORIGINAL-" + f);
        }

        int failures = 0;

        Check("available", ArabicPack.IsAvailable(root), true, ref failures);
        Check("installed before", ArabicPack.IsInstalled(root), false, ref failures);

        string error;
        Check("install ok", ArabicPack.Install(root, out error), true, ref failures);
        Check("installed after", ArabicPack.IsInstalled(root), true, ref failures);
        foreach (string f in files)
        {
            Check("live is arabic " + f, File.ReadAllText(Path.Combine(cardDir, f)), "ARABIC-" + f, ref failures);
        }

        // Re-install must not clobber the backup with the arabic text.
        Check("reinstall ok", ArabicPack.Install(root, out error), true, ref failures);
        Check("backup still english", File.ReadAllText(Path.Combine(root, "Data", "ArabicPack", "backup", "CARD_Indx.bytes")), "ENGLISH-CARD_Indx.bytes", ref failures);

        Check("uninstall ok", ArabicPack.Uninstall(root, out error), true, ref failures);
        Check("installed after uninstall", ArabicPack.IsInstalled(root), false, ref failures);
        foreach (string f in files)
        {
            Check("restored english " + f, File.ReadAllText(Path.Combine(cardDir, f)), "ENGLISH-" + f, ref failures);
        }

        // Uninstall without backup falls back to original.
        foreach (string f in files)
        {
            File.Delete(Path.Combine(root, "Data", "ArabicPack", "backup", f));
            File.WriteAllText(Path.Combine(cardDir, f), "ARABIC2-" + f);
        }
        Check("uninstall2 ok", ArabicPack.Uninstall(root, out error), true, ref failures);
        Check("restored original", File.ReadAllText(Path.Combine(cardDir, "CARD_Name.bytes")), "ORIGINAL-CARD_Name.bytes", ref failures);

        try { Directory.Delete(root, true); } catch { }

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURES"));
        return failures == 0 ? 0 : 1;
    }

    static void Check<T>(string label, T actual, T expected, ref int failures)
    {
        bool ok = Equals(actual, expected);
        if (!ok) { failures++; }
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + label + (ok ? "" : "  (got '" + actual + "' want '" + expected + "')"));
    }
}
