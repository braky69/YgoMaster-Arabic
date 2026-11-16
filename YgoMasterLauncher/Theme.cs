using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YgoMasterLauncher
{
    /// <summary>
    /// Shared colours, fonts and small control factories so every screen of the launcher
    /// looks consistent. Everything degrades gracefully when a font is not installed.
    /// </summary>
    static class Theme
    {
        public static readonly Color Ink = Color.FromArgb(28, 33, 48);
        public static readonly Color InkSoft = Color.FromArgb(74, 82, 106);
        public static readonly Color Muted = Color.FromArgb(128, 136, 156);
        public static readonly Color PageColor = Color.FromArgb(244, 246, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(216, 221, 232);
        public static readonly Color Header = Color.FromArgb(28, 33, 48);
        public static readonly Color HeaderSoft = Color.FromArgb(160, 175, 200);
        public static readonly Color Accent = Color.FromArgb(255, 196, 84);
        public static readonly Color Primary = Color.FromArgb(58, 92, 210);
        public static readonly Color PrimaryHover = Color.FromArgb(78, 112, 226);
        public static readonly Color PrimaryDown = Color.FromArgb(46, 76, 184);
        public static readonly Color Success = Color.FromArgb(30, 130, 70);
        public static readonly Color Danger = Color.FromArgb(184, 62, 62);
        public static readonly Color TabIdle = Color.FromArgb(233, 236, 244);
        public static readonly Color TabHover = Color.FromArgb(240, 242, 248);

        static readonly HashSet<string> Families = LoadFamilies();
        static readonly string UiFamily = Pick("Segoe UI", "Tahoma", "Arial");
        static readonly string SemiFamily = Pick("Segoe UI Semibold", "Segoe UI", "Tahoma", "Arial");

        static HashSet<string> LoadFamilies()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (InstalledFontCollection collection = new InstalledFontCollection())
                {
                    foreach (FontFamily family in collection.Families)
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

        static string Pick(params string[] names)
        {
            foreach (string name in names)
            {
                if (Families.Contains(name))
                {
                    return name;
                }
            }
            return names[names.Length - 1];
        }

        public static Font Ui(float size)
        {
            return new Font(UiFamily, size);
        }

        public static Font UiBold(float size)
        {
            return new Font(SemiFamily, size, FontStyle.Bold);
        }

        public static Label Label(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Font = Ui(9.75f);
            label.ForeColor = Ink;
            label.Margin = new Padding(0, 7, 8, 7);
            return label;
        }

        public static Label Hint(string text)
        {
            Label label = Label(text);
            label.ForeColor = Muted;
            return label;
        }

        public static Button Button(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(112, 34);
            button.Padding = new Padding(12, 0, 12, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = Ui(9.75f);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(4);
            button.UseVisualStyleBackColor = false;
            button.TextAlign = ContentAlignment.MiddleCenter;
            if (primary)
            {
                button.BackColor = Primary;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Primary;
                button.FlatAppearance.MouseOverBackColor = PrimaryHover;
                button.FlatAppearance.MouseDownBackColor = PrimaryDown;
            }
            else
            {
                button.BackColor = Surface;
                button.ForeColor = Ink;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(238, 242, 250);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(226, 232, 244);
            }
            return button;
        }

        public static GroupBox Group(string title)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            group.Font = UiBold(9.75f);
            group.ForeColor = Ink;
            group.BackColor = Surface;
            group.Padding = new Padding(12);
            return group;
        }

        public static TabPage Page(string title)
        {
            TabPage page = new TabPage(title);
            page.Padding = new Padding(14);
            page.BackColor = PageColor;
            page.UseVisualStyleBackColor = false;
            return page;
        }

        public static NumericUpDown ValueInput()
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = 0;
            input.Font = Ui(9.75f);
            input.Width = 170;
            input.ThousandsSeparator = true;
            return input;
        }

        public static NumericUpDown PortInput(int value)
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = 1;
            input.Maximum = 65535;
            input.Value = value;
            input.Font = Ui(9.75f);
            input.Width = 110;
            return input;
        }

        public static TextBox TextInput(int width)
        {
            TextBox box = new TextBox();
            box.Font = Ui(9.75f);
            box.Width = width;
            return box;
        }

        /// <summary>
        /// Dock=Top children are laid out from the last child to the first, so the child
        /// added last ends up on top. This reorders the z-order so the controls appear in
        /// the given top-to-bottom order (the first argument is drawn highest).
        /// </summary>
        public static void StackTopToBottom(Control parent, params Control[] topToBottom)
        {
            int count = topToBottom.Length;
            for (int i = 0; i < count; i++)
            {
                parent.Controls.SetChildIndex(topToBottom[i], count - 1 - i);
            }
        }
    }
}
