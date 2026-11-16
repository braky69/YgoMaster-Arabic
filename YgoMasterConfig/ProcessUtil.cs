using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace YgoMasterConfig
{
    public static class ProcessUtil
    {
        public static bool IsRunning(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool Start(string fileName, string workingDirectory, bool elevated, string arguments = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
                {
                    return false;
                }
                ProcessStartInfo startInfo = new ProcessStartInfo(fileName);
                if (!string.IsNullOrEmpty(arguments))
                {
                    startInfo.Arguments = arguments;
                }
                startInfo.WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
                    ? Path.GetDirectoryName(fileName)
                    : workingDirectory;
                startInfo.UseShellExecute = true;
                if (elevated && !NetworkUtil.IsAdministrator())
                {
                    startInfo.Verb = "runas";
                }
                Process process = Process.Start(startInfo);
                return process != null;
            }
            catch (Exception e)
            {
                Console.WriteLine("[ProcessUtil] Failed to start " + fileName + " - " + e.Message);
                return false;
            }
        }

        public static int KillProcesses(string processName)
        {
            int count = 0;
            try
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                        count++;
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }
            return count;
        }
    }

    /// <summary>
    /// Shared host / join / solo configuration which is used by both the
    /// launcher and the in game YgoMaster menu.
    /// </summary>
    public static class NetworkSetup
    {
        public const int DefaultHttpPort = 4989;
        public const int DefaultSessionPort = 4988;

        public static bool ApplyHost(string serverSettingsPath, string clientSettingsPath,
            string localIP, int httpPort, int sessionPort)
        {
            if (string.IsNullOrEmpty(localIP))
            {
                localIP = NetworkUtil.GetLocalIPv4();
            }

            Dictionary<string, object> server = new Dictionary<string, object>();
            server["MultiplayerEnabled"] = true;
            server["BaseIP"] = localIP;
            server["BasePort"] = httpPort;
            server["SessionServerPort"] = sessionPort;
            server["SessionServerIP"] = "0.0.0.0";
            server["MultiplayerPvpClientConnectIP"] = localIP;
            server["MultiplayerPvpClientConnectPort"] = 0;
            server["BindIP"] = "http://*:{BasePort}/";
            List<string> serverMissing = ConfigEditor.SetValues(serverSettingsPath, server);

            Dictionary<string, object> client = new Dictionary<string, object>();
            client["BaseIP"] = "127.0.0.1";
            client["BasePort"] = httpPort;
            client["SessionServerPort"] = sessionPort;
            client["SessionServerIP"] = "127.0.0.1";
            client["DontAutoRunServerExe"] = true;
            if (string.IsNullOrEmpty(ConfigEditor.GetValue(clientSettingsPath, "MultiplayerToken")))
            {
                client["MultiplayerToken"] = NetworkUtil.GenerateToken();
            }
            List<string> clientMissing = ConfigEditor.SetValues(clientSettingsPath, client);
            return serverMissing.Count == 0 && clientMissing.Count == 0;
        }

        public static bool ApplyJoin(string clientSettingsPath, string hostIP,
            int httpPort, int sessionPort)
        {
            Dictionary<string, object> client = new Dictionary<string, object>();
            client["BaseIP"] = hostIP;
            client["BasePort"] = httpPort;
            client["SessionServerPort"] = sessionPort;
            client["SessionServerIP"] = hostIP;
            client["DontAutoRunServerExe"] = true;
            if (string.IsNullOrEmpty(ConfigEditor.GetValue(clientSettingsPath, "MultiplayerToken")))
            {
                client["MultiplayerToken"] = NetworkUtil.GenerateToken();
            }
            return ConfigEditor.SetValues(clientSettingsPath, client).Count == 0;
        }

        public static bool ApplySolo(string serverSettingsPath, string clientSettingsPath)
        {
            Dictionary<string, object> server = new Dictionary<string, object>();
            server["MultiplayerEnabled"] = false;
            server["BaseIP"] = "localhost";
            server["BindIP"] = "http://{BaseIP}:{BasePort}/";
            server["SessionServerIP"] = "{BaseIP}";
            server["MultiplayerPvpClientConnectIP"] = "{SessionServerIP}";
            server["MultiplayerPvpClientConnectPort"] = 0;
            List<string> serverMissing = ConfigEditor.SetValues(serverSettingsPath, server);

            Dictionary<string, object> client = new Dictionary<string, object>();
            client["BaseIP"] = "localhost";
            client["BasePort"] = DefaultHttpPort;
            client["SessionServerPort"] = DefaultSessionPort;
            client["SessionServerIP"] = "{BaseIP}";
            client["MultiplayerToken"] = "";
            client["DontAutoRunServerExe"] = false;
            List<string> clientMissing = ConfigEditor.SetValues(clientSettingsPath, client);
            return serverMissing.Count == 0 && clientMissing.Count == 0;
        }

        public static bool ApplyAllCards(string serverSettingsPath)
        {
            Dictionary<string, object> server = new Dictionary<string, object>();
            server["UnlockAllCards"] = true;
            server["UnlockAllItems"] = true;
            server["UnlockAllSoloChapters"] = true;
            server["CardCraftableAll"] = true;
            server["DisableBanList"] = true;
            server["DisableDeckValidation"] = true;
            return ConfigEditor.SetValues(serverSettingsPath, server).Count == 0;
        }

        public static bool IsHostConfigured(string serverSettingsPath)
        {
            return ConfigEditor.GetBool(serverSettingsPath, "MultiplayerEnabled");
        }
    }
}
