using System;
using System.IO;
using System.Text;

class TestArabic
{
    static void Main(string[] args)
    {
        string[] samples =
        {
            "التنين الأبيض أزرق العينين",
            "هذا التنين الأسطوري هو محرك دمار قوي. لا يقهر عملياً، قليلون جداً واجهوا هذا الكائن الرائع وعاشوا ليرووا الحكاية.",
            "(1) ضع بطاقة 1 \"القمر الطالع\" من مجموعتك في أعلى مجموعتك، باستثناء \"نجوم القمر الطالع\"، لكن لا يمكنك وضع بطاقة أخرى بنفس الاسم بهذا التأثير.",
            "ATK/2000 DEF/1500",
            "يسحب كل لاعب بطاقة 1.",
            "إذا كنت تتحكم بوحش إكسيز \"القمر الطالع\" من نوع ساحر: اسحب بطاقة 1."
        };

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        html.Append("<link href='https://fonts.googleapis.com/css2?family=Amiri&family=Noto+Naskh+Arabic:wght@400;700&display=swap' rel='stylesheet'>");
        html.Append("<style>body{background:#111;color:#eee;font-family:'Noto Naskh Arabic','Amiri',serif;font-size:22px;padding:20px}");
        html.Append("h3{color:#7fd;font-size:16px;margin:18px 0 4px}");
        html.Append(".box{border:1px solid #444;padding:10px;margin:6px 0;border-radius:6px}");
        html.Append(".ltr{direction:ltr;unicode-bidi:bidi-override;font-family:'Noto Naskh Arabic','Amiri',monospace}");
        html.Append(".logical{direction:rtl;unicode-bidi:normal}");
        html.Append(".mono{font-family:Consolas,monospace;font-size:12px;color:#9ad;word-break:break-all}");
        html.Append("</style></head><body>");

        Console.OutputEncoding = Encoding.UTF8;
        foreach (var s in samples)
        {
            string shaped = ArabicText.Shape(s);
            string prep = ArabicText.Prepare(s);
            Console.WriteLine("LOGICAL : " + s);
            Console.WriteLine("SHAPED  : " + shaped);
            Console.WriteLine("PREPARED: " + prep);
            Console.WriteLine();

            html.Append("<h3>Original (browser-rendered)</h3><div class='box logical'>").Append(Esc(s)).Append("</div>");
            html.Append("<h3>PREPARED, rendered LTR bidi-override (what the game shows)</h3><div class='box ltr'>").Append(Esc(prep)).Append("</div>");
            html.Append("<div class='mono'>").Append(Cps(prep)).Append("</div>");
        }
        html.Append("</body></html>");

        string outPath = args.Length > 0 ? args[0] : "preview.html";
        File.WriteAllText(outPath, html.ToString(), new UTF8Encoding(false));
        Console.WriteLine("Wrote " + outPath);
    }

    static string Esc(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }

    static string Cps(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s) sb.Append(((int)c).ToString("X4")).Append(' ');
        return sb.ToString();
    }
}
