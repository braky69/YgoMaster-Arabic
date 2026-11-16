using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// Scrapes arab-duelists.com card data (Arabic + English) into a local cache.
// Phase A: enumerate card passcodes from the paginated gallery.
// Phase B: fetch getcardbyid.php?id=<passcode> for each, caching raw JSON.
class Program
{
    const string Base = "https://arab-duelists.com/";
    const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) YgoMasterArabicPack/1.0";

    static string Root;
    static string CacheDir;
    static string PasscodesFile;

    static int Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
        ServicePointManager.DefaultConnectionLimit = 32;
        ServicePointManager.Expect100Continue = false;
        Root = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        CacheDir = Path.Combine(Root, "cache");
        PasscodesFile = Path.Combine(Root, "passcodes.txt");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(CacheDir);

        var passcodes = EnumeratePasscodes();
        Console.WriteLine("Enumerated passcodes: " + passcodes.Count);
        File.WriteAllLines(PasscodesFile, passcodes.ConvertAll(p => p.ToString()).ToArray());
        Console.WriteLine("Saved -> " + PasscodesFile);

        FetchAll(passcodes);
        Console.WriteLine("DONE");
        return 0;
    }

    static List<int> EnumeratePasscodes()
    {
        var set = new HashSet<int>();
        string[] sorts = { "name", "new", "" };
        foreach (string sort in sorts)
        {
            for (int page = 1; page <= 1000; page++)
            {
                string url = Base + "?p=gallery&max=40&page=" + page + (sort.Length > 0 ? "&sort=" + sort : "");
                string html = Get(url, 3);
                if (html == null) { Console.WriteLine("  [" + sort + "] page " + page + " FAILED"); continue; }
                int before = set.Count;
                var ms = Regex.Matches(html, @"\?p=card&id=(\d+)");
                foreach (Match m in ms)
                {
                    int id = int.Parse(m.Groups[1].Value);
                    if (id != 5) set.Add(id); // exclude the static nav link (?p=card&id=5)
                }
                int added = set.Count - before;
                if (page % 25 == 0 || added == 0)
                    Console.WriteLine("  [" + sort + "] page " + page + " added=" + added + " total=" + set.Count);
                if (added == 0) break;
            }
        }
        var list = new List<int>(set);
        list.Sort();
        return list;
    }

    static void FetchAll(List<int> passcodes)
    {
        int done = 0, fetched = 0, failed = 0, skipped = 0;
        int total = passcodes.Count;
        object gate = new object();

        Parallel.ForEach(passcodes, new ParallelOptions { MaxDegreeOfParallelism = 12 }, id =>
        {
            string file = Path.Combine(CacheDir, id + ".json");
            if (File.Exists(file) && new FileInfo(file).Length > 50)
            {
                lock (gate) { skipped++; done++; }
                return;
            }
            string json = Get(Base + "getcardbyid.php?id=" + id, 3);
            if (json != null && json.Length > 50 && json.IndexOf("name_official", StringComparison.Ordinal) >= 0)
            {
                File.WriteAllText(file, json, new UTF8Encoding(false));
                lock (gate) { fetched++; done++; }
            }
            else
            {
                lock (gate) { failed++; done++; }
            }
            lock (gate)
            {
                if (done % 500 == 0 || done == total)
                    Console.WriteLine("  " + done + "/" + total + "  fetched=" + fetched + " skipped=" + skipped + " failed=" + failed);
            }
        });
        Console.WriteLine("Fetch complete. fetched=" + fetched + " skipped=" + skipped + " failed=" + failed);
    }

    static string Get(string url, int retries)
    {
        for (int attempt = 0; attempt < retries; attempt++)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UA;
                req.Timeout = 40000;
                req.ReadWriteTimeout = 40000;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            catch (Exception)
            {
                Thread.Sleep(500 * (attempt + 1));
            }
        }
        return null;
    }
}
