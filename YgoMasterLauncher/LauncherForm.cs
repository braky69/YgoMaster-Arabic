using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using YgoMasterConfig;

namespace YgoMasterLauncher
{
    public class LauncherForm : Form
    {
        static readonly string[] ServerSettingKeys = new string[]
        {
            "UnlockAllCards",
            "UnlockAllCardsHighestRarity",
            "UnlockAllItems",
            "UnlockAllSoloChapters",
            "CardCraftableAll",
            "DisableBanList",
            "DisableDeckValidation"
        };

        static readonly string[] ClientSettingKeys = new string[]
        {
            "DeckEditorDisableLimits",
            "DuelClientShowRemainingCardsInDeck"
        };

        readonly string baseDir;
        readonly string serverSettingsPath;
        readonly string clientSettingsPath;
        readonly string serverExePath;
        readonly string clientExePath;

        Icon appIcon;

        TabControl tabs;
        Label localIpValue;
        NumericUpDown hostHttpPort;
        NumericUpDown hostSessionPort;
        Button hostStartButton;
        Button hostCopyButton;
        Button hostFirewallButton;
        TextBox logBox;

        TextBox joinIp;
        NumericUpDown joinHttpPort;
        NumericUpDown joinSessionPort;
        Button joinStartButton;

        CheckBox[] serverChecks;
        CheckBox[] clientChecks;
        Button saveSettingsButton;
        Button enableAllButton;
        Button soloButton;
        Button openFolderButton;
        StatusStrip statusStrip;
        ToolStripStatusLabel statusLabel;

        TextBox playerNameBox;
        NumericUpDown gemsBox;
        NumericUpDown dustNormalBox;
        NumericUpDown dustRareBox;
        NumericUpDown dustSuperRareBox;
        NumericUpDown dustUltraRareBox;
        Label playerStatus;
        Button applyPlayerButton;

        CheckBox arabicToggle;
        ComboBox arabicFontCombo;
        Button arabicApplyButton;
        Label arabicStatus;

        public LauncherForm()
        {
            baseDir = ResolveBaseDirectory();
            serverSettingsPath = Path.Combine(baseDir, "Data", "Settings.json");
            clientSettingsPath = Path.Combine(baseDir, "Data", "ClientData", "ClientSettings.json");
            serverExePath = Path.Combine(baseDir, "YgoMaster.exe");
            clientExePath = Path.Combine(baseDir, "YgoMasterClient.exe");

            appIcon = LoadAppIcon();
            BuildUi();
            LoadSettingsIntoUi();
            LoadPlayerIntoUi();
            LoadArabicIntoUi();
        }

        static string ResolveBaseDirectory()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (Directory.Exists(Path.Combine(exeDir, "Data")))
            {
                return exeDir;
            }
            string nested = Path.Combine(exeDir, "YgoMaster");
            if (Directory.Exists(Path.Combine(nested, "Data")))
            {
                return nested;
            }
            return exeDir;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!File.Exists(Path.Combine(baseDir, "YgoMasterLoader.dll")))
            {
                MessageBox.Show(this,
                    "YgoMasterLoader.dll is missing from:\n" + baseDir + "\n\n" +
                    "Copy the complete YgoMaster folder from the release zip. " +
                    "The loader is required to start the game.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static Icon LoadAppIcon()
        {
            // Prefer the icon embedded in this assembly (crisp at any size), then fall back
            // to the icon attached to the executable, then to the generic Windows icon.
            try
            {
                using (Stream stream = typeof(LauncherForm).Assembly
                    .GetManifestResourceStream("YgoMasterLauncher.app.ico"))
                {
                    if (stream != null)
                    {
                        return new Icon(stream, 64, 64);
                    }
                }
            }
            catch
            {
            }
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                {
                    return icon;
                }
            }
            catch
            {
            }
            return SystemIcons.Application;
        }

        void BuildUi()
        {
            Text = "YgoMaster Launcher - DEV BY BRAKY";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 640);
            MinimumSize = new Size(760, 600);
            Font = Theme.Ui(9.75f);
            BackColor = Theme.PageColor;
            ForeColor = Theme.Ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.BackColor = Theme.PageColor;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Theme.Ui(9.75f);
            tabs.Padding = new Point(16, 6);
            tabs.BackColor = Theme.PageColor;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Normal;
            tabs.ItemSize = new Size(160, 38);
            tabs.DrawItem += TabDrawItem;
            root.Controls.Add(tabs, 0, 1);

            statusStrip = new StatusStrip();
            statusStrip.BackColor = Theme.Surface;
            statusStrip.ForeColor = Theme.Ink;
            statusStrip.SizingGrip = false;
            statusLabel = new ToolStripStatusLabel("Ready");
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.ForeColor = Theme.InkSoft;
            ToolStripStatusLabel creditLabel = new ToolStripStatusLabel("DEV BY BRAKY");
            creditLabel.Font = Theme.UiBold(9f);
            creditLabel.ForeColor = Theme.Ink;
            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(creditLabel);
            root.Controls.Add(statusStrip, 0, 2);

            BuildHostTab();
            BuildJoinTab();
            BuildPlayerTab();
            BuildArabicTab();
            BuildSettingsTab();

            StyleTabs();
        }

        Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Theme.Header;

            PictureBox logo = new PictureBox();
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.Size = new Size(46, 46);
            logo.Location = new Point(18, 14);
            logo.BackColor = Color.Transparent;
            if (appIcon != null)
            {
                try
                {
                    logo.Image = appIcon.ToBitmap();
                }
                catch
                {
                }
            }
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "YgoMaster";
            title.ForeColor = Color.White;
            title.Font = Theme.UiBold(17f);
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.Location = new Point(76, 12);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Offline Master Duel  |  All Cards  |  Host / Join";
            subtitle.ForeColor = Theme.HeaderSoft;
            subtitle.Font = Theme.Ui(9.75f);
            subtitle.AutoSize = true;
            subtitle.BackColor = Color.Transparent;
            subtitle.Location = new Point(78, 43);
            header.Controls.Add(subtitle);

            Label credit = new Label();
            credit.Text = "DEV BY BRAKY";
            credit.ForeColor = Theme.Accent;
            credit.Font = Theme.UiBold(12f);
            credit.AutoSize = true;
            credit.BackColor = Color.Transparent;
            credit.Location = new Point(header.Width - credit.PreferredWidth - 22, 26);
            header.Controls.Add(credit);
            header.Resize += delegate
            {
                credit.Location = new Point(Math.Max(20, header.Width - credit.PreferredWidth - 22), 26);
            };

            return header;
        }

        void StyleTabs()
        {
            // Tabs keep the native auto width (SizeMode.Normal) so every title is fully
            // visible in one row; we only ask the control to repaint with our owner draw.
            tabs.Invalidate();
        }

        void TabDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabs.TabPages.Count)
            {
                return;
            }
            TabPage page = tabs.TabPages[e.Index];
            Rectangle bounds = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (SolidBrush background = new SolidBrush(selected ? Theme.Surface : Theme.TabIdle))
            {
                e.Graphics.FillRectangle(background, bounds);
            }
            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(Theme.Primary))
                {
                    e.Graphics.FillRectangle(accent, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3));
                }
            }

            Font font = selected ? Theme.UiBold(9.75f) : Theme.Ui(9.75f);
            TextRenderer.DrawText(e.Graphics, page.Text, font, bounds,
                selected ? Theme.Ink : Theme.InkSoft,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            font.Dispose();
        }

        void BuildPlayerTab()
        {
            TabPage page = Theme.Page("Player (Name / Gems / Dust)");
            tabs.TabPages.Add(page);

            GroupBox group = Theme.Group("Set your player name, gems and craft points");
            group.Dock = DockStyle.Top;
            group.Height = 300;
            page.Controls.Add(group);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(14);
            table.ColumnCount = 2;
            table.RowCount = 7;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            group.Controls.Add(table);

            table.Controls.Add(MakeLabel("Player name:"), 0, 0);
            playerNameBox = new TextBox();
            playerNameBox.Width = 300;
            playerNameBox.MaxLength = 32;
            table.Controls.Add(playerNameBox, 1, 0);

            table.Controls.Add(MakeLabel("Gems:"), 0, 1);
            gemsBox = MakeValueInput();
            table.Controls.Add(gemsBox, 1, 1);

            table.Controls.Add(MakeLabel("N dust (Normal):"), 0, 2);
            dustNormalBox = MakeValueInput();
            table.Controls.Add(dustNormalBox, 1, 2);

            table.Controls.Add(MakeLabel("R dust (Rare):"), 0, 3);
            dustRareBox = MakeValueInput();
            table.Controls.Add(dustRareBox, 1, 3);

            table.Controls.Add(MakeLabel("SR dust (Super Rare):"), 0, 4);
            dustSuperRareBox = MakeValueInput();
            table.Controls.Add(dustSuperRareBox, 1, 4);

            table.Controls.Add(MakeLabel("UR dust (Ultra Rare):"), 0, 5);
            dustUltraRareBox = MakeValueInput();
            table.Controls.Add(dustUltraRareBox, 1, 5);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.WrapContents = false;
            actions.Margin = new Padding(0, 10, 0, 0);

            applyPlayerButton = MakePrimaryButton("Apply to player");
            applyPlayerButton.Click += ApplyPlayerClick;
            actions.Controls.Add(applyPlayerButton);

            Button reloadButton = MakeButton("Reload from game");
            reloadButton.Click += delegate { LoadPlayerIntoUi(); Log("Player values reloaded from the save file."); };
            actions.Controls.Add(reloadButton);

            table.Controls.Add(actions, 1, 6);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(680, 0);
            note.Dock = DockStyle.Bottom;
            note.Padding = new Padding(4, 10, 4, 4);
            note.ForeColor = Theme.Muted;
            note.Text = "These values are written to your player save (Data\\Players\\...\\Player.json) and to the " +
                "defaults for new players. The game and the server must be closed while applying, then reopened. " +
                "Gems and dust are capped at 1,000,000,000.";
            page.Controls.Add(note);

            playerStatus = new Label();
            playerStatus.AutoSize = true;
            playerStatus.Dock = DockStyle.Bottom;
            playerStatus.Padding = new Padding(4, 4, 4, 4);
            playerStatus.ForeColor = Theme.Ink;
            page.Controls.Add(playerStatus);
        }

        static NumericUpDown MakeValueInput()
        {
            NumericUpDown input = Theme.ValueInput();
            input.Maximum = PlayerDataEditor.MaxValue;
            return input;
        }

        void LoadPlayerIntoUi()
        {
            string playersDir = PlayerDataEditor.GetPlayersDirectory(baseDir);
            PlayerProfile profile;
            if (PlayerDataEditor.TryReadProfile(playersDir, out profile))
            {
                playerNameBox.Text = profile.Name;
                gemsBox.Value = ClampValue(profile.Gems);
                dustNormalBox.Value = ClampValue(profile.CraftNormal);
                dustRareBox.Value = ClampValue(profile.CraftRare);
                dustSuperRareBox.Value = ClampValue(profile.CraftSuperRare);
                dustUltraRareBox.Value = ClampValue(profile.CraftUltraRare);
                if (playerStatus != null)
                {
                    playerStatus.Text = "Loaded from your saved player.";
                }
            }
            else
            {
                gemsBox.Value = ClampValue(ConfigEditor.GetInt(serverSettingsPath, "DefaultGems", 0));
                if (playerStatus != null)
                {
                    playerStatus.Text = "No saved player found yet. Values will be used as defaults for a new player.";
                }
            }
        }

        static decimal ClampValue(int value)
        {
            if (value < 0)
            {
                return 0;
            }
            if (value > PlayerDataEditor.MaxValue)
            {
                return PlayerDataEditor.MaxValue;
            }
            return value;
        }

        void ApplyPlayerClick(object sender, EventArgs e)
        {
            if (!ConfirmCloseRunning("YgoMaster", "YgoMasterClient", "masterduel"))
            {
                return;
            }

            PlayerProfile profile = new PlayerProfile();
            profile.Name = playerNameBox.Text.Trim();
            profile.Gems = (int)gemsBox.Value;
            profile.CraftNormal = (int)dustNormalBox.Value;
            profile.CraftRare = (int)dustRareBox.Value;
            profile.CraftSuperRare = (int)dustSuperRareBox.Value;
            profile.CraftUltraRare = (int)dustUltraRareBox.Value;

            bool settingsOk = ConfigEditor.SetValue(serverSettingsPath, "DefaultGems", profile.Gems);
            settingsOk = ConfigEditor.SetDefaultCraftPoints(serverSettingsPath,
                profile.CraftNormal, profile.CraftRare, profile.CraftSuperRare, profile.CraftUltraRare) && settingsOk;

            string playersDir = PlayerDataEditor.GetPlayersDirectory(baseDir);
            int failed;
            int updated = PlayerDataEditor.ApplyToAllPlayers(playersDir, profile, out failed);

            if (!settingsOk)
            {
                playerStatus.Text = "Updated " + updated + " player file(s), but could not write the defaults.";
                MessageBox.Show(this,
                    "The player files were updated but the defaults could not be written.\n\n" +
                    "Run YgoMasterLauncher.exe as administrator, or move the YgoMaster folder to a writable location.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string message;
            if (updated == 0)
            {
                message = "No saved player was found yet.\n\nThe values were saved as defaults and will be " +
                    "applied when you create/enter the game.";
            }
            else
            {
                message = "Updated " + updated + " player file(s)." +
                    (failed > 0 ? "\nFailed: " + failed : "") +
                    "\n\nThe values are applied the next time you start the game.";
            }
            playerStatus.Text = message.Replace("\n", " ");
            Log("Player profile applied (" + updated + " file(s) updated).");
            MessageBox.Show(this, message, "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void BuildHostTab()
        {
            TabPage page = Theme.Page("Host (this PC)");
            tabs.TabPages.Add(page);

            GroupBox group = Theme.Group("Start a game that your friend can join");
            group.Dock = DockStyle.Top;
            group.Height = 250;
            page.Controls.Add(group);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(12);
            table.ColumnCount = 3;
            table.RowCount = 6;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            group.Controls.Add(table);

            table.Controls.Add(MakeLabel("Your LAN address:"), 0, 0);
            localIpValue = MakeLabel("");
            localIpValue.Font = new Font(Font, FontStyle.Bold);
            table.Controls.Add(localIpValue, 1, 0);
            Button refreshIpButton = MakeButton("Refresh");
            refreshIpButton.Width = 90;
            refreshIpButton.Click += delegate { UpdateLocalIp(); };
            table.Controls.Add(refreshIpButton, 2, 0);

            table.Controls.Add(MakeLabel("HTTP port:"), 0, 1);
            hostHttpPort = MakePortInput(NetworkSetup.DefaultHttpPort);
            table.Controls.Add(hostHttpPort, 1, 1);
            table.Controls.Add(MakeHint("Friend connects to this port (default 4989)"), 2, 1);

            table.Controls.Add(MakeLabel("Session port:"), 0, 2);
            hostSessionPort = MakePortInput(NetworkSetup.DefaultSessionPort);
            table.Controls.Add(hostSessionPort, 1, 2);
            table.Controls.Add(MakeHint("Used while dueling (default 4988)"), 2, 2);

            hostStartButton = MakePrimaryButton("Set up && start host");
            hostStartButton.Click += HostStartClick;
            table.Controls.Add(hostStartButton, 1, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.WrapContents = false;
            hostCopyButton = MakeButton("Copy address");
            hostCopyButton.Click += HostCopyClick;
            hostFirewallButton = MakeButton("Allow firewall");
            hostFirewallButton.Click += HostFirewallClick;
            buttons.Controls.Add(hostCopyButton);
            buttons.Controls.Add(hostFirewallButton);
            table.Controls.Add(buttons, 2, 3);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(620, 0);
            note.Text = "The launcher will write the host settings, add the firewall rule and start the server + game.\n" +
                "The server may ask for administrator permission (Windows UAC) so it can accept LAN connections.\n" +
                "For a friend outside your network, port forward TCP 4989 and 4988 on your router.";
            table.Controls.Add(note, 0, 4);
            table.SetColumnSpan(note, 3);

            GroupBox logGroup = Theme.Group("Log");
            logGroup.Dock = DockStyle.Fill;
            logGroup.Padding = new Padding(8);
            page.Controls.Add(logGroup);
            logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;
            logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Consolas", 9f);
            logBox.BackColor = Color.White;
            logBox.ForeColor = Theme.InkSoft;
            logGroup.Controls.Add(logBox);
            Theme.StackTopToBottom(page, group, logGroup);
        }

        void BuildJoinTab()
        {
            TabPage page = Theme.Page("Join a friend");
            tabs.TabPages.Add(page);

            GroupBox group = Theme.Group("Connect to your friend's host");
            group.Dock = DockStyle.Top;
            group.Height = 250;
            page.Controls.Add(group);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.Padding = new Padding(12);
            table.ColumnCount = 2;
            table.RowCount = 5;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            group.Controls.Add(table);

            table.Controls.Add(MakeLabel("Friend's IP address:"), 0, 0);
            joinIp = new TextBox();
            joinIp.Width = 260;
            joinIp.Text = "127.0.0.1";
            table.Controls.Add(joinIp, 1, 0);

            table.Controls.Add(MakeLabel("HTTP port:"), 0, 1);
            joinHttpPort = MakePortInput(NetworkSetup.DefaultHttpPort);
            table.Controls.Add(joinHttpPort, 1, 1);

            table.Controls.Add(MakeLabel("Session port:"), 0, 2);
            joinSessionPort = MakePortInput(NetworkSetup.DefaultSessionPort);
            table.Controls.Add(joinSessionPort, 1, 2);

            joinStartButton = MakePrimaryButton("Set up && join game");
            joinStartButton.Click += JoinStartClick;
            table.Controls.Add(joinStartButton, 1, 3);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(620, 0);
            note.Text = "Enter the address shown by the host's launcher.\n" +
                "LAN example: 192.168.1.20    WAN example: your friend's public IP (port forwarding required).\n" +
                "The launcher writes ClientSettings.json and then starts the game. It does not start a server.";
            table.Controls.Add(note, 0, 4);
            table.SetColumnSpan(note, 2);

            Label warn = new Label();
            warn.AutoSize = true;
            warn.MaximumSize = new Size(660, 0);
            warn.ForeColor = Theme.Danger;
            warn.Dock = DockStyle.Bottom;
            warn.Padding = new Padding(4, 8, 4, 4);
            warn.Text = "Every PC needs a different MultiplayerToken. This launcher generates one automatically if the field is empty.";
            page.Controls.Add(warn);
        }

        void BuildArabicTab()
        {
            TabPage page = Theme.Page("العربية (Arabic)");
            page.AutoScroll = true;
            tabs.TabPages.Add(page);

            GroupBox group = Theme.Group("Arabic card text (تعريب البطاقات)");
            group.Dock = DockStyle.Top;
            group.Height = 230;
            page.Controls.Add(group);

            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false;
            panel.Padding = new Padding(14);
            group.Controls.Add(panel);

            arabicToggle = new CheckBox();
            arabicToggle.Text = "Enable Arabic card names and effects (تفعيل أسماء وتأثيرات البطاقات بالعربية)";
            arabicToggle.AutoSize = true;
            arabicToggle.Margin = new Padding(0, 4, 0, 10);
            panel.Controls.Add(arabicToggle);

            FlowLayoutPanel fontRow = new FlowLayoutPanel();
            fontRow.AutoSize = true;
            fontRow.WrapContents = false;
            fontRow.Margin = new Padding(0, 0, 0, 10);

            Label fontLabel = new Label();
            fontLabel.Text = "Font (نوع الخط):";
            fontLabel.AutoSize = true;
            fontLabel.Margin = new Padding(0, 6, 8, 0);
            fontRow.Controls.Add(fontLabel);

            arabicFontCombo = new ComboBox();
            arabicFontCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            arabicFontCombo.Width = 260;
            arabicFontCombo.Margin = new Padding(0, 3, 0, 0);
            fontRow.Controls.Add(arabicFontCombo);
            panel.Controls.Add(fontRow);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.WrapContents = false;

            arabicApplyButton = MakePrimaryButton("Apply (تطبيق)");
            arabicApplyButton.Click += ApplyArabicClick;
            actions.Controls.Add(arabicApplyButton);

            Button openPackButton = MakeButton("Open pack folder");
            openPackButton.Click += delegate
            {
                try
                {
                    string dir = ArabicPack.GetPackDir(baseDir);
                    Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", dir);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actions.Controls.Add(openPackButton);

            panel.Controls.Add(actions);

            arabicStatus = new Label();
            arabicStatus.AutoSize = true;
            arabicStatus.Margin = new Padding(0, 8, 0, 0);
            arabicStatus.ForeColor = Color.FromArgb(28, 33, 48);
            panel.Controls.Add(arabicStatus);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(660, 0);
            note.Dock = DockStyle.Top;
            note.Padding = new Padding(2, 8, 2, 2);
            note.ForeColor = Theme.Muted;
            note.Text =
                "The game has no Arabic language slot, so the Arabic text is written over the English card data. " +
                "The original English files are backed up automatically and restored when you turn this off, so the " +
                "game can never break.\n\n" +
                "Card text is display only - the duel engine still runs the real effects, so playing works exactly the " +
                "same. Close the game and server before applying, then start the game again.";
            page.Controls.Add(note);
            Theme.StackTopToBottom(page, group, note);
        }

        void LoadArabicIntoUi()
        {
            bool available = ArabicPack.IsAvailable(baseDir);
            bool installed = ArabicPack.IsInstalled(baseDir);
            arabicToggle.Checked = installed;
            arabicToggle.Enabled = available;
            arabicApplyButton.Enabled = available;
            LoadFontIntoUi(available);
            if (!available)
            {
                arabicStatus.Text = "Arabic pack files were not found (Data\\ArabicPack\\ar).";
                arabicStatus.ForeColor = Theme.Danger;
            }
            else if (installed)
            {
                arabicStatus.Text = "Arabic card text is currently installed.";
                arabicStatus.ForeColor = Theme.Success;
            }
            else
            {
                arabicStatus.Text = "Arabic card text is not installed (English is shown).";
                arabicStatus.ForeColor = Theme.Ink;
            }
        }

        void LoadFontIntoUi(bool enabled)
        {
            if (arabicFontCombo == null)
            {
                return;
            }
            List<FontFace> fonts = FontCatalog.GetAvailable();
            string family, style;
            FontFace selected = ArabicPack.TryGetFont(baseDir, out family, out style)
                ? new FontFace(family, style)
                : FontCatalog.Default;

            arabicFontCombo.BeginUpdate();
            arabicFontCombo.Items.Clear();
            int index = -1;
            foreach (FontFace face in fonts)
            {
                int i = arabicFontCombo.Items.Add(face);
                if (string.Equals(face.Family, selected.Family, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                }
            }
            if (index < 0)
            {
                index = arabicFontCombo.Items.Add(selected);
            }
            arabicFontCombo.SelectedIndex = index;
            arabicFontCombo.EndUpdate();
            arabicFontCombo.Enabled = enabled;
        }

        void ApplyArabicClick(object sender, EventArgs e)
        {
            if (!ArabicPack.IsAvailable(baseDir))
            {
                MessageBox.Show(this,
                    "The Arabic pack files are missing:\n\n" + ArabicPack.GetArabicDir(baseDir),
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ConfirmCloseRunning("YgoMaster", "YgoMasterClient", "masterduel"))
            {
                return;
            }

            bool want = arabicToggle.Checked;
            string error;
            bool ok;
            if (want)
            {
                ok = ArabicPack.Install(baseDir, out error);
                if (ok && arabicFontCombo != null && arabicFontCombo.SelectedItem is FontFace)
                {
                    FontFace face = (FontFace)arabicFontCombo.SelectedItem;
                    ArabicPack.SetFont(baseDir, face.Family, face.Style);
                }
            }
            else
            {
                ok = ArabicPack.Uninstall(baseDir, out error);
            }
            if (!ok)
            {
                MessageBox.Show(this, error, "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("Arabic pack change failed: " + error);
                return;
            }

            Log(want ? "Arabic card text installed." : "Arabic card text removed (English restored).");
            LoadArabicIntoUi();
            MessageBox.Show(this,
                want
                    ? "Arabic card text installed.\n\nStart the game again to see the Arabic names and effects."
                    : "Arabic card text removed.\n\nThe original English text will be used again.",
                "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void BuildSettingsTab()
        {
            TabPage page = Theme.Page("Settings");
            page.AutoScroll = true;
            tabs.TabPages.Add(page);

            GroupBox serverGroup = Theme.Group("Server / card settings (Data/Settings.json)");
            serverGroup.Dock = DockStyle.Top;
            serverGroup.Height = 235;
            page.Controls.Add(serverGroup);

            FlowLayoutPanel serverPanel = new FlowLayoutPanel();
            serverPanel.Dock = DockStyle.Fill;
            serverPanel.FlowDirection = FlowDirection.TopDown;
            serverPanel.WrapContents = false;
            serverPanel.Padding = new Padding(12);
            serverGroup.Controls.Add(serverPanel);

            serverChecks = new CheckBox[ServerSettingKeys.Length];
            string[] serverLabels = new string[]
            {
                "Unlock all cards (3x of every card)",
                "Unlock all cards with highest rarity",
                "Unlock all items / avatars / sleeves",
                "Unlock all solo chapters",
                "Make every card craftable",
                "Disable the ban list",
                "Disable deck validation"
            };
            for (int i = 0; i < ServerSettingKeys.Length; i++)
            {
                CheckBox checkBox = new CheckBox();
                checkBox.Text = serverLabels[i];
                checkBox.AutoSize = true;
                checkBox.Margin = new Padding(0, 3, 0, 3);
                serverPanel.Controls.Add(checkBox);
                serverChecks[i] = checkBox;
            }

            GroupBox clientGroup = Theme.Group("Client settings (Data/ClientData/ClientSettings.json)");
            clientGroup.Dock = DockStyle.Top;
            clientGroup.Height = 110;
            page.Controls.Add(clientGroup);

            FlowLayoutPanel clientPanel = new FlowLayoutPanel();
            clientPanel.Dock = DockStyle.Fill;
            clientPanel.FlowDirection = FlowDirection.TopDown;
            clientPanel.WrapContents = false;
            clientPanel.Padding = new Padding(12);
            clientGroup.Controls.Add(clientPanel);

            clientChecks = new CheckBox[ClientSettingKeys.Length];
            string[] clientLabels = new string[]
            {
                "Disable deck editor limits",
                "Show remaining cards in deck during a duel"
            };
            for (int i = 0; i < ClientSettingKeys.Length; i++)
            {
                CheckBox checkBox = new CheckBox();
                checkBox.Text = clientLabels[i];
                checkBox.AutoSize = true;
                checkBox.Margin = new Padding(0, 3, 0, 3);
                clientPanel.Controls.Add(checkBox);
                clientChecks[i] = checkBox;
            }

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Top;
            actions.Height = 90;
            actions.Padding = new Padding(0, 10, 0, 0);
            page.Controls.Add(actions);

            saveSettingsButton = MakePrimaryButton("Save settings");
            saveSettingsButton.Click += SaveSettingsClick;
            actions.Controls.Add(saveSettingsButton);

            enableAllButton = MakeButton("Enable everything");
            enableAllButton.Click += EnableAllClick;
            actions.Controls.Add(enableAllButton);

            soloButton = MakeButton("Offline / Solo mode");
            soloButton.Click += SoloClick;
            actions.Controls.Add(soloButton);

            openFolderButton = MakeButton("Open game folder");
            openFolderButton.Click += delegate
            {
                try
                {
                    Process.Start("explorer.exe", baseDir);
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            actions.Controls.Add(openFolderButton);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(660, 0);
            note.Dock = DockStyle.Top;
            note.Padding = new Padding(2, 8, 2, 2);
            note.Text = "Changes are written to the JSON files and applied the next time the game starts.\n" +
                "Card effects are provided by Master Duel's own duel engine; this tool unlocks every card which exists in the installed game data.";
            page.Controls.Add(note);
            Theme.StackTopToBottom(page, serverGroup, clientGroup, actions, note);
        }

        static Label MakeLabel(string text)
        {
            return Theme.Label(text);
        }

        static Label MakeHint(string text)
        {
            return Theme.Hint(text);
        }

        static NumericUpDown MakePortInput(int value)
        {
            return Theme.PortInput(value);
        }

        static Button MakeButton(string text)
        {
            return Theme.Button(text, false);
        }

        static Button MakePrimaryButton(string text)
        {
            return Theme.Button(text, true);
        }

        void UpdateLocalIp()
        {
            string ip = NetworkUtil.GetLocalIPv4();
            int port = (int)hostHttpPort.Value;
            localIpValue.Text = ip + ":" + port;
            statusLabel.Text = "LAN address: " + localIpValue.Text;
        }

        void LoadSettingsIntoUi()
        {
            hostHttpPort.Value = ClampPort(ConfigEditor.GetInt(serverSettingsPath, "BasePort", NetworkSetup.DefaultHttpPort));
            hostSessionPort.Value = ClampPort(ConfigEditor.GetInt(serverSettingsPath, "SessionServerPort", NetworkSetup.DefaultSessionPort));
            joinHttpPort.Value = hostHttpPort.Value;
            joinSessionPort.Value = hostSessionPort.Value;

            for (int i = 0; i < ServerSettingKeys.Length; i++)
            {
                serverChecks[i].Checked = ConfigEditor.GetBool(serverSettingsPath, ServerSettingKeys[i]);
            }
            for (int i = 0; i < ClientSettingKeys.Length; i++)
            {
                clientChecks[i].Checked = ConfigEditor.GetBool(clientSettingsPath, ClientSettingKeys[i]);
            }

            string lastHost = ConfigEditor.GetValue(clientSettingsPath, "BaseIP");
            if (!string.IsNullOrEmpty(lastHost) && lastHost != "127.0.0.1" && lastHost != "localhost")
            {
                joinIp.Text = lastHost;
            }
            UpdateLocalIp();
        }

        static decimal ClampPort(int port)
        {
            if (port < 1)
            {
                return NetworkSetup.DefaultHttpPort;
            }
            if (port > 65535)
            {
                return 65535;
            }
            return port;
        }

        void HostStartClick(object sender, EventArgs e)
        {
            int httpPort = (int)hostHttpPort.Value;
            int sessionPort = (int)hostSessionPort.Value;
            if (httpPort == sessionPort)
            {
                MessageBox.Show(this, "The HTTP port and the session port must be different.", "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!File.Exists(serverExePath) || !File.Exists(clientExePath))
            {
                MessageBox.Show(this, "YgoMaster.exe / YgoMasterClient.exe were not found in:\n" + baseDir, "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!ConfirmCloseRunning("YgoMaster", "YgoMasterClient", "masterduel"))
            {
                return;
            }

            string localIp = NetworkUtil.GetLocalIPv4();
            bool written = NetworkSetup.ApplyHost(serverSettingsPath, clientSettingsPath, localIp, httpPort, sessionPort);
            if (!written)
            {
                Log("Failed to write the settings files.");
                MessageBox.Show(this,
                    "Could not write the settings files:\n\n" + serverSettingsPath + "\n" + clientSettingsPath + "\n\n" +
                    "Run YgoMasterLauncher.exe as administrator (right click -> Run as administrator), " +
                    "or move the YgoMaster folder to a writable location such as your Desktop.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log("Host settings written. LAN address: " + localIp + ":" + httpPort);
            UpdateLocalIp();

            Log("Starting YgoMaster.exe (accept the Windows UAC prompt if it appears)...");
            if (!ProcessUtil.Start(serverExePath, baseDir, true))
            {
                MessageBox.Show(this, "Failed to start YgoMaster.exe.", "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            bool ready = WaitForPort("127.0.0.1", httpPort, 30000);
            if (ready)
            {
                Log("Server is listening on port " + httpPort + ".");
            }
            else
            {
                Log("Server did not open port " + httpPort + " in time.");
                MessageBox.Show(this,
                    "The server did not start listening on port " + httpPort + ".\n\n" +
                    "Try right clicking YgoMasterLauncher.exe and choosing \"Run as administrator\", " +
                    "or start YgoMaster.exe manually and accept the UAC prompt.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Log("Starting YgoMasterClient.exe...");
            if (!ProcessUtil.Start(clientExePath, baseDir, false))
            {
                MessageBox.Show(this, "Failed to start YgoMasterClient.exe.", "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Settings are only read when a process starts. If the game or the
        /// server is already running the new configuration would be ignored,
        /// so ask the user before closing the old processes.
        /// </summary>
        bool ConfirmCloseRunning(params string[] processNames)
        {
            bool anyRunning = false;
            foreach (string name in processNames)
            {
                if (ProcessUtil.IsRunning(name))
                {
                    anyRunning = true;
                    break;
                }
            }
            if (!anyRunning)
            {
                return true;
            }

            DialogResult result = MessageBox.Show(this,
                "YgoMaster is already running.\n\n" +
                "The new configuration is only applied when the game and server are restarted.\n\n" +
                "Close the running game/server and continue?",
                "YgoMaster", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return false;
            }

            foreach (string name in processNames)
            {
                int count = ProcessUtil.KillProcesses(name);
                if (count > 0)
                {
                    Log("Closed " + count + " running " + name + " process(es).");
                }
            }
            Thread.Sleep(700);
            return true;
        }

        bool WaitForPort(string host, int port, int timeoutMilliseconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
            {
                if (NetworkUtil.IsTcpPortOpen(host, port, 400))
                {
                    return true;
                }
                Application.DoEvents();
                Thread.Sleep(200);
            }
            return false;
        }

        void HostCopyClick(object sender, EventArgs e)
        {
            string address = NetworkUtil.BuildHostAddress(NetworkUtil.GetLocalIPv4(), (int)hostHttpPort.Value);
            try
            {
                Clipboard.SetText(address);
                Log("Copied " + address + " to the clipboard.");
            }
            catch (Exception ex)
            {
                Log("Failed to copy address: " + ex.Message);
            }
        }

        void HostFirewallClick(object sender, EventArgs e)
        {
            string message;
            bool success = NetworkUtil.AddFirewallRules(
                new int[] { (int)hostHttpPort.Value, (int)hostSessionPort.Value }, out message);
            Log(message);
            MessageBox.Show(this, message, "Firewall", MessageBoxButtons.OK,
                success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        void JoinStartClick(object sender, EventArgs e)
        {
            int pastedPort;
            string host = NetworkUtil.ParseHostAndPort(joinIp.Text, out pastedPort);
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show(this, "Enter your friend's IP address.", "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int httpPort = pastedPort > 0 ? pastedPort : (int)joinHttpPort.Value;
            int sessionPort = (int)joinSessionPort.Value;

            if (!File.Exists(clientExePath))
            {
                MessageBox.Show(this, "YgoMasterClient.exe was not found in:\n" + baseDir, "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!ConfirmCloseRunning("YgoMasterClient", "masterduel"))
            {
                return;
            }

            if (!NetworkUtil.IsTcpPortOpen(host, httpPort, 2500))
            {
                DialogResult result = MessageBox.Show(this,
                    "Could not reach " + host + ":" + httpPort + ".\n\n" +
                    "Check the host address, the firewall and port forwarding.\n\nTry to join anyway?",
                    "YgoMaster", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            if (!NetworkSetup.ApplyJoin(clientSettingsPath, host, httpPort, sessionPort))
            {
                Log("Failed to write the client settings file.");
                MessageBox.Show(this,
                    "Could not write the settings file:\n\n" + clientSettingsPath + "\n\n" +
                    "Run YgoMasterLauncher.exe as administrator, or move the YgoMaster folder to a writable location.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log("Join settings written. Connecting to " + host + ":" + httpPort + ".");
            Log("Starting YgoMasterClient.exe...");
            if (!ProcessUtil.Start(clientExePath, baseDir, false))
            {
                MessageBox.Show(this, "Failed to start YgoMasterClient.exe.", "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void SaveSettingsClick(object sender, EventArgs e)
        {
            bool success = true;
            for (int i = 0; i < ServerSettingKeys.Length; i++)
            {
                if (!ConfigEditor.SetValue(serverSettingsPath, ServerSettingKeys[i], serverChecks[i].Checked))
                {
                    success = false;
                }
            }
            for (int i = 0; i < ClientSettingKeys.Length; i++)
            {
                if (!ConfigEditor.SetValue(clientSettingsPath, ClientSettingKeys[i], clientChecks[i].Checked))
                {
                    success = false;
                }
            }
            if (!success)
            {
                Log("Failed to write all settings.");
                MessageBox.Show(this,
                    "Some settings could not be written.\n\nRun YgoMasterLauncher.exe as administrator, " +
                    "or move the YgoMaster folder to a writable location.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log("Settings saved. Restart the game to apply them.");
            MessageBox.Show(this, "Settings saved.\n\nRestart the game for the changes to take effect.",
                "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void EnableAllClick(object sender, EventArgs e)
        {
            foreach (CheckBox checkBox in serverChecks)
            {
                checkBox.Checked = true;
            }
            if (!NetworkSetup.ApplyAllCards(serverSettingsPath))
            {
                Log("Failed to write the card settings.");
                MessageBox.Show(this,
                    "Could not write the settings file.\n\nRun YgoMasterLauncher.exe as administrator, " +
                    "or move the YgoMaster folder to a writable location.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            ConfigEditor.SetValue(clientSettingsPath, "DeckEditorDisableLimits", true);
            clientChecks[0].Checked = true;
            Log("All cards / items / solo chapters enabled.");
            MessageBox.Show(this,
                "All cards, items and solo chapters are now enabled.\n\nRestart the game for the changes to take effect.",
                "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void SoloClick(object sender, EventArgs e)
        {
            if (!NetworkSetup.ApplySolo(serverSettingsPath, clientSettingsPath))
            {
                Log("Failed to write the offline configuration.");
                MessageBox.Show(this,
                    "Could not write the settings files.\n\nRun YgoMasterLauncher.exe as administrator, " +
                    "or move the YgoMaster folder to a writable location.",
                    "YgoMaster", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log("Offline / solo configuration written.");
            DialogResult result = MessageBox.Show(this,
                "Offline / solo mode configured.\n\nStart the game now?",
                "YgoMaster", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes && File.Exists(clientExePath))
            {
                if (ConfirmCloseRunning("YgoMaster", "YgoMasterClient", "masterduel"))
                {
                    ProcessUtil.Start(clientExePath, baseDir, false);
                }
            }
        }

        void Log(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            if (logBox.TextLength > 0)
            {
                logBox.AppendText(Environment.NewLine);
            }
            logBox.AppendText(line);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
            statusLabel.Text = message;
        }
    }
}
