using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace YgoMasterConfig
{
    public static class NetworkUtil
    {
        public static string GetLocalIPv4()
        {
            string fallback = null;
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }
                    foreach (UnicastIPAddressInformation addressInfo in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addressInfo.Address == null ||
                            addressInfo.Address.AddressFamily != AddressFamily.InterNetwork ||
                            IPAddress.IsLoopback(addressInfo.Address))
                        {
                            continue;
                        }
                        string ip = addressInfo.Address.ToString();
                        if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                        {
                            continue;
                        }
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                            ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                        {
                            return ip;
                        }
                        if (fallback == null)
                        {
                            fallback = ip;
                        }
                    }
                }
            }
            catch
            {
            }

            if (fallback != null)
            {
                return fallback;
            }

            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch
            {
            }

            return "127.0.0.1";
        }

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateToken()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Accepts "192.168.1.5", "192.168.1.5:4989", "http://host/" etc.
        /// </summary>
        public static string NormalizeHost(string input)
        {
            int port;
            return ParseHostAndPort(input, out port);
        }

        public static string ParseHostAndPort(string input, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }
            string text = input.Trim();
            if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(7);
            }
            else if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(8);
            }
            int slash = text.IndexOf('/');
            if (slash >= 0)
            {
                text = text.Substring(0, slash);
            }
            text = text.Trim().TrimEnd('.');
            if (text.Length == 0)
            {
                return null;
            }

            if (text.StartsWith("[", StringComparison.Ordinal) && text.Contains("]"))
            {
                int end = text.IndexOf(']');
                string hostPart = text.Substring(1, end - 1);
                string remainder = text.Substring(end + 1).TrimStart(':');
                int parsed;
                if (int.TryParse(remainder, out parsed))
                {
                    port = parsed;
                }
                return hostPart;
            }

            int firstColon = text.IndexOf(':');
            int lastColon = text.LastIndexOf(':');
            if (firstColon >= 0 && firstColon == lastColon)
            {
                int parsed;
                if (int.TryParse(text.Substring(lastColon + 1), out parsed))
                {
                    port = parsed;
                    text = text.Substring(0, lastColon);
                }
            }
            return text.Trim();
        }

        public static bool IsTcpPortOpen(string host, int port, int timeoutMilliseconds)
        {
            if (string.IsNullOrEmpty(host) || port <= 0 || port > 65535)
            {
                return false;
            }
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult result = client.BeginConnect(host, port, null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(Math.Max(100, timeoutMilliseconds));
                    if (!connected)
                    {
                        return false;
                    }
                    client.EndConnect(result);
                    return client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTcpPortAvailable(int port)
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Runs an elevated netsh command which creates a single inbound TCP rule
        /// covering all of the given ports.
        /// </summary>
        public static bool AddFirewallRules(IEnumerable<int> ports, out string message)
        {
            List<string> portStrings = new List<string>();
            foreach (int port in ports)
            {
                if (port > 0 && port <= 65535)
                {
                    portStrings.Add(port.ToString());
                }
            }
            if (portStrings.Count == 0)
            {
                message = "No valid ports were supplied.";
                return false;
            }

            string ruleName = "YgoMaster";
            string localPorts = string.Join(",", portStrings.ToArray());
            string arguments = string.Format(
                "advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport={1}",
                ruleName, localPorts);

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("netsh.exe", arguments);
                startInfo.UseShellExecute = true;
                if (!IsAdministrator())
                {
                    startInfo.Verb = "runas";
                }
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                Process process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(15000);
                }
                message = "Firewall rule \"" + ruleName + "\" added for TCP ports " + localPorts + ".";
                return true;
            }
            catch (Exception e)
            {
                message = "Failed to add the firewall rule (run the launcher as administrator).\n\n" + e.Message;
                return false;
            }
        }

        public static string BuildHostAddress(string ip, int port)
        {
            if (string.IsNullOrEmpty(ip))
            {
                ip = GetLocalIPv4();
            }
            return ip + ":" + port;
        }

        public static string DescribeLocalAddresses(int port)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GetLocalIPv4());
            sb.Append(':');
            sb.Append(port);
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }
                    foreach (UnicastIPAddressInformation addressInfo in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addressInfo.Address == null ||
                            addressInfo.Address.AddressFamily != AddressFamily.InterNetwork ||
                            IPAddress.IsLoopback(addressInfo.Address))
                        {
                            continue;
                        }
                        string ip = addressInfo.Address.ToString();
                        if (ip.StartsWith("169.254.", StringComparison.Ordinal) || sb.ToString().Contains(ip + ":"))
                        {
                            continue;
                        }
                        sb.Append('\n');
                        sb.Append(ip);
                        sb.Append(':');
                        sb.Append(port);
                    }
                }
            }
            catch
            {
            }
            return sb.ToString();
        }
    }
}
