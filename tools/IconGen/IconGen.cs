using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

// Generates the YgoMaster application icon (a clean Yu-Gi-Oh style card) and writes a
// proper multi-size .ico file (16/24/32/48/64/128 BMP entries + a 256 PNG entry).
//
// Usage:  IconGen.exe <output.ico>
class IconGen
{
    static readonly Color Ink = Color.FromArgb(28, 33, 48);
    static readonly Color Gold = Color.FromArgb(255, 196, 84);
    static readonly Color BgTop = Color.FromArgb(45, 54, 88);
    static readonly Color BgBottom = Color.FromArgb(15, 18, 28);

    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "app.ico";
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };

        List<int> dims = new List<int>();
        List<byte[]> entries = new List<byte[]>();

        using (Bitmap master = Render(1024))
        {
            foreach (int s in sizes)
            {
                using (Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(master, new Rectangle(0, 0, s, s));
                    }
                    // BMP entries for every size (no PNG) so both the Windows shell and
                    // .NET's Icon class can read all of them.
                    entries.Add(BmpBytes(bmp));
                    dims.Add(s);
                }
            }
        }

        WriteIco(outPath, dims, entries);
        Console.WriteLine("Wrote " + outPath + " (" + dims.Count + " sizes)");
    }

    static Bitmap Render(int S)
    {
        float u = S / 1024f;
        Bitmap bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            // ---- rounded background ----
            RectangleF bgRect = new RectangleF(10 * u, 10 * u, 1004 * u, 1004 * u);
            using (GraphicsPath bg = RoundRect(bgRect, 205 * u))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(bgRect, BgTop, BgBottom, 90f))
                {
                    g.FillPath(b, bg);
                }
                using (Pen p = new Pen(Gold, 18 * u))
                {
                    g.DrawPath(p, bg);
                }
            }

            // ---- card ----
            GraphicsState state = g.Save();
            g.TranslateTransform(S / 2f, S / 2f);
            g.RotateTransform(-9f);

            using (GraphicsPath shadow = RoundRect(new RectangleF(-238 * u, -320 * u, 476 * u, 676 * u), 48 * u))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillPath(sb, shadow);
            }

            RectangleF card = new RectangleF(-235 * u, -335 * u, 470 * u, 670 * u);
            using (GraphicsPath cardPath = RoundRect(card, 44 * u))
            {
                using (LinearGradientBrush cb = new LinearGradientBrush(card, Color.White, Color.FromArgb(224, 229, 240), 90f))
                {
                    g.FillPath(cb, cardPath);
                }
                using (Pen bp = new Pen(Gold, 16 * u))
                {
                    g.DrawPath(bp, cardPath);
                }
            }

            using (GraphicsPath inner = RoundRect(new RectangleF(-211 * u, -311 * u, 422 * u, 622 * u), 30 * u))
            using (Pen ip = new Pen(Ink, 7 * u))
            {
                g.DrawPath(ip, inner);
            }

            // ---- top banner ----
            RectangleF banner = new RectangleF(-158 * u, -300 * u, 316 * u, 68 * u);
            using (GraphicsPath bp2 = RoundRect(banner, 16 * u))
            {
                using (LinearGradientBrush bb = new LinearGradientBrush(banner, Color.FromArgb(66, 116, 226), Color.FromArgb(38, 72, 168), 90f))
                {
                    g.FillPath(bb, bp2);
                }
                using (Pen op = new Pen(Gold, 7 * u))
                {
                    g.DrawPath(op, bp2);
                }
            }

            // ---- level star ----
            DrawStar(g, 0f, -266 * u, 26 * u, new SolidBrush(Gold));

            // ---- monogram ----
            using (Font font = new Font("Segoe UI", 250 * u, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush tb = new SolidBrush(Ink))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("Y", font, tb, new RectangleF(-200 * u, -190 * u, 400 * u, 300 * u), sf);
            }

            // ---- effect text lines ----
            using (SolidBrush lb = new SolidBrush(Color.FromArgb(188, 196, 214)))
            {
                g.FillPath(lb, RoundRect(new RectangleF(-150 * u, 150 * u, 300 * u, 22 * u), 11 * u));
                g.FillPath(lb, RoundRect(new RectangleF(-150 * u, 192 * u, 300 * u, 22 * u), 11 * u));
                g.FillPath(lb, RoundRect(new RectangleF(-150 * u, 234 * u, 210 * u, 22 * u), 11 * u));
            }

            g.Restore(state);
        }
        return bmp;
    }

    static void DrawStar(Graphics g, float cx, float cy, float r, Brush brush)
    {
        PointF[] pts = new PointF[10];
        double start = -Math.PI / 2;
        for (int i = 0; i < 10; i++)
        {
            double ang = start + i * Math.PI / 5;
            float rad = (i % 2 == 0) ? r : r * 0.44f;
            pts[i] = new PointF(cx + (float)(Math.Cos(ang) * rad), cy + (float)(Math.Sin(ang) * rad));
        }
        g.FillPolygon(brush, pts);
    }

    static GraphicsPath RoundRect(RectangleF rect, float radius)
    {
        float d = radius * 2f;
        GraphicsPath path = new GraphicsPath();
        if (radius <= 0.01f)
        {
            path.AddRectangle(rect);
            return path;
        }
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---------------------------------------------------------------- ico writing

    static byte[] PngBytes(Bitmap bmp)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    static byte[] BmpBytes(Bitmap bmp)
    {
        int w = bmp.Width;
        int h = bmp.Height;
        int stride = w * 4;
        int maskStride = ((w + 31) / 32) * 4;
        byte[] data = new byte[40 + stride * h + maskStride * h];

        WriteInt(data, 0, 40);
        WriteInt(data, 4, w);
        WriteInt(data, 8, h * 2);
        WriteShort(data, 12, 1);
        WriteShort(data, 14, 32);
        WriteInt(data, 16, 0);
        WriteInt(data, 20, stride * h);

        for (int y = 0; y < h; y++)
        {
            int srcY = h - 1 - y;
            for (int x = 0; x < w; x++)
            {
                Color c = bmp.GetPixel(x, srcY);
                int o = 40 + y * stride + x * 4;
                data[o] = c.B;
                data[o + 1] = c.G;
                data[o + 2] = c.R;
                data[o + 3] = c.A;
            }
        }
        return data;
    }

    static void WriteIco(string path, List<int> dims, List<byte[]> entries)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)dims.Count);
            int offset = 6 + 16 * dims.Count;
            for (int i = 0; i < dims.Count; i++)
            {
                bw.Write((byte)(dims[i] >= 256 ? 0 : dims[i]));
                bw.Write((byte)(dims[i] >= 256 ? 0 : dims[i]));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(entries[i].Length);
                bw.Write(offset);
                offset += entries[i].Length;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                bw.Write(entries[i]);
            }
        }
    }

    static void WriteInt(byte[] b, int o, int v)
    {
        b[o] = (byte)(v & 0xFF);
        b[o + 1] = (byte)((v >> 8) & 0xFF);
        b[o + 2] = (byte)((v >> 16) & 0xFF);
        b[o + 3] = (byte)((v >> 24) & 0xFF);
    }

    static void WriteShort(byte[] b, int o, int v)
    {
        b[o] = (byte)(v & 0xFF);
        b[o + 1] = (byte)((v >> 8) & 0xFF);
    }
}
