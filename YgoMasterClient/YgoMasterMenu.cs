using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using YgoMasterConfig;
using YgomGame.Menu;
using YgomGame.SubMenu;

namespace YgoMasterClient
{
    /// <summary>
    /// In game YgoMaster sub menu. Everything here writes the same JSON files
    /// which the launcher edits, so both entry points stay in sync.
    /// </summary>
    static class YgoMasterMenu
    {
        const string ServerSettingsFile = "Settings.json";
        const string ClientSettingsFile = "ClientSettings.json";

        public static void AddMenuItems(IntPtr thisPtr)
        {
            try
            {
                SubMenuViewController.AddMenuItem(thisPtr, "YgoMaster: Host game", OnHostMenu);
                SubMenuViewController.AddMenuItem(thisPtr, "YgoMaster: Join game", OnJoinMenu);
                SubMenuViewController.AddMenuItem(thisPtr, "YgoMaster: Settings", OnSettingsMenu);
                SubMenuViewController.AddMenuItem(thisPtr, "YgoMaster: Open launcher", OnOpenLauncherMenu);
            }
            catch (Exception e)
            {
                Console.WriteLine("[YgoMasterMenu] Failed to add menu items: " + e);
            }
        }

        static string ServerSettingsPath
        {
            get { return Path.Combine(Program.DataDir, ServerSettingsFile); }
        }

        static string ClientSettingsPath
        {
            get { return Path.Combine(Program.ClientDataDir, ClientSettingsFile); }
        }

        static void OnHostMenu()
        {
            Win32Hooks.Invoke(ShowHostDialog);
        }

        static void ShowHostDialog()
        {
            try
            {
                string localIp = NetworkUtil.GetLocalIPv4();
                int httpPort = ConfigEditor.GetInt(ServerSettingsPath, "BasePort", NetworkSetup.DefaultHttpPort);
                int sessionPort = ConfigEditor.GetInt(ServerSettingsPath, "SessionServerPort", NetworkSetup.DefaultSessionPort);
                string message =
                    "Make this PC the host?\n\n" +
                    "LAN address: " + localIp + ":" + httpPort + "\n\n" +
                    "A friend on the same network chooses \"Join game\" and enters this address.\n" +
                    "For a friend outside your network, forward TCP " + httpPort + " and " + sessionPort + " on your router.\n\n" +
                    "The server will be started now. The game must be reopened afterwards.";
                CommonDialogViewController.OpenYesNoConfirmationDialog(
                    "YgoMaster Host", message,
                    delegate
                    {
                        Win32Hooks.Invoke(delegate
                        {
                            if (!NetworkSetup.ApplyHost(ServerSettingsPath, ClientSettingsPath, localIp, httpPort, sessionPort))
                            {
                                ShowInfo("YgoMaster Host",
                                    "Could not write the settings files. Run the game as administrator " +
                                    "or move the YgoMaster folder to a writable location.");
                                return;
                            }
                            StartServer();
                            ShowInfo("YgoMaster Host",
                                "Host settings saved and the server was started.\n\n" +
                                "LAN address: " + localIp + ":" + httpPort + "\n\n" +
                                "Close and reopen the game, then create a Duel Room (PvP).");
                        });
                    },
                    null, null, "Start host", "Cancel", true);
            }
            catch (Exception e)
            {
                ShowInfo("YgoMaster Host", "Failed to configure the host:\n\n" + e.Message);
            }
        }

        static void OnJoinMenu()
        {
            Win32Hooks.Invoke(ShowJoinPrompt);
        }

        static void ShowJoinPrompt()
        {
            try
            {
                string current = ConfigEditor.GetValue(ClientSettingsPath, "BaseIP");
                if (string.IsNullOrEmpty(current) || current == "localhost" || current == "127.0.0.1")
                {
                    current = "";
                }
                string input = Prompt("Join a friend", "Enter the host IP address (for example 192.168.1.20):", current);
                if (string.IsNullOrEmpty(input))
                {
                    return;
                }
                int pastedPort;
                string host = NetworkUtil.ParseHostAndPort(input, out pastedPort);
                if (string.IsNullOrEmpty(host))
                {
                    ShowInfo("YgoMaster Join", "The IP address is not valid.");
                    return;
                }
                int httpPort = pastedPort > 0
                    ? pastedPort
                    : ConfigEditor.GetInt(ClientSettingsPath, "BasePort", NetworkSetup.DefaultHttpPort);
                int sessionPort = ConfigEditor.GetInt(ClientSettingsPath, "SessionServerPort", NetworkSetup.DefaultSessionPort);
                if (!NetworkSetup.ApplyJoin(ClientSettingsPath, host, httpPort, sessionPort))
                {
                    ShowInfo("YgoMaster Join",
                        "Could not write the client settings file. Run the game as administrator " +
                        "or move the YgoMaster folder to a writable location.");
                    return;
                }
                ShowInfo("YgoMaster Join",
                    "Join settings saved for " + host + ":" + httpPort + ".\n\n" +
                    "Close and reopen the game to connect to your friend.");
            }
            catch (Exception e)
            {
                ShowInfo("YgoMaster Join", "Failed to configure the join:\n\n" + e.Message);
            }
        }

        static void OnSettingsMenu()
        {
            Win32Hooks.Invoke(ShowSettingsSheet);
        }

        static void ShowSettingsSheet()
        {
            List<string> entries = new List<string>();
            List<Action> actions = new List<Action>();

            AddToggle(entries, actions, "All cards (3x)", ServerSettingsPath, "UnlockAllCards");
            AddToggle(entries, actions, "All cards (highest rarity)", ServerSettingsPath, "UnlockAllCardsHighestRarity");
            AddToggle(entries, actions, "All items / avatars / sleeves", ServerSettingsPath, "UnlockAllItems");
            AddToggle(entries, actions, "All solo chapters", ServerSettingsPath, "UnlockAllSoloChapters");
            AddToggle(entries, actions, "Every card craftable", ServerSettingsPath, "CardCraftableAll");
            AddToggle(entries, actions, "Disable ban list", ServerSettingsPath, "DisableBanList");
            AddToggle(entries, actions, "Disable deck validation", ServerSettingsPath, "DisableDeckValidation");
            AddToggle(entries, actions, "Deck editor: no limits", ClientSettingsPath, "DeckEditorDisableLimits");
            AddToggle(entries, actions, "Duel: show remaining cards", ClientSettingsPath, "DuelClientShowRemainingCardsInDeck");

            entries.Add("Enable everything now");
            actions.Add(delegate
            {
                bool success = NetworkSetup.ApplyAllCards(ServerSettingsPath);
                if (success)
                {
                    success = ConfigEditor.SetValue(ClientSettingsPath, "DeckEditorDisableLimits", true);
                }
                Win32Hooks.Invoke(delegate
                {
                    if (!success)
                    {
                        ShowInfo("YgoMaster Settings",
                            "Could not write the settings files. Run the game as administrator " +
                            "or move the YgoMaster folder to a writable location.");
                        return;
                    }
                    ShowInfo("YgoMaster Settings",
                        "All cards, items, solo chapters, crafting and deck limits are now enabled.\n\n" +
                        "Restart the game to apply the changes.");
                });
            });

            entries.Add("Back");
            actions.Add(delegate { });

            ActionSheetViewController.Open("YgoMaster Settings", entries.ToArray(), delegate (IntPtr ptr, int index)
            {
                if (index >= 0 && index < actions.Count)
                {
                    actions[index]();
                }
            });
        }

        static void AddToggle(List<string> entries, List<Action> actions, string label, string path, string key)
        {
            bool value = ConfigEditor.GetBool(path, key);
            entries.Add((value ? "[ON]  " : "[OFF] ") + label);
            actions.Add(delegate
            {
                bool newValue = !ConfigEditor.GetBool(path, key);
                bool success = ConfigEditor.SetValue(path, key, newValue);
                Win32Hooks.Invoke(delegate
                {
                    if (!success)
                    {
                        ShowInfo("YgoMaster Settings",
                            "Could not update \"" + key + "\" in:\n" + path);
                        return;
                    }
                    ShowSettingsSheet();
                });
            });
        }

        static void OnOpenLauncherMenu()
        {
            Win32Hooks.Invoke(delegate
            {
                try
                {
                    string launcher = Path.Combine(Program.CurrentDir, "YgoMasterLauncher.exe");
                    if (!File.Exists(launcher))
                    {
                        ShowInfo("YgoMaster Launcher", "YgoMasterLauncher.exe was not found in:\n" + Program.CurrentDir);
                        return;
                    }
                    ProcessUtil.Start(launcher, Program.CurrentDir, false);
                }
                catch (Exception e)
                {
                    ShowInfo("YgoMaster Launcher", "Failed to open the launcher:\n\n" + e.Message);
                }
            });
        }

        static void StartServer()
        {
            try
            {
                if (ProcessUtil.IsRunning("YgoMaster"))
                {
                    return;
                }
                string server = Path.Combine(Program.CurrentDir, "YgoMaster.exe");
                if (File.Exists(server))
                {
                    ProcessUtil.Start(server, Program.CurrentDir, true);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[YgoMasterMenu] Failed to start the server: " + e.Message);
            }
        }

        static void ShowInfo(string title, string message)
        {
            try
            {
                CommonDialogViewController.OpenAlertDialog(title, message, delegate { }, "OK", null, true);
            }
            catch (Exception e)
            {
                Console.WriteLine("[YgoMasterMenu] Failed to open dialog: " + e.Message);
            }
        }

        static string Prompt(string title, string label, string initialValue)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.TopMost = true;
                form.ClientSize = new Size(430, 145);
                form.Font = new Font("Segoe UI", 9.75f);

                Label promptLabel = new Label();
                promptLabel.Text = label;
                promptLabel.AutoSize = true;
                promptLabel.MaximumSize = new Size(400, 0);
                promptLabel.Location = new Point(14, 14);
                form.Controls.Add(promptLabel);

                TextBox textBox = new TextBox();
                textBox.Text = initialValue;
                textBox.Location = new Point(14, 55);
                textBox.Width = 400;
                form.Controls.Add(textBox);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.Location = new Point(252, 100);
                okButton.Width = 75;
                form.Controls.Add(okButton);

                Button cancelButton = new Button();
                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.Location = new Point(335, 100);
                cancelButton.Width = 80;
                form.Controls.Add(cancelButton);

                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;
                textBox.Select();

                return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }
    }
}
