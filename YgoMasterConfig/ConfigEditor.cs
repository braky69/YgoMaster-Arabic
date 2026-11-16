using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace YgoMasterConfig
{
    /// <summary>
    /// Small line based JSON editor which preserves comments / formatting.
    /// This is intentionally not a full JSON parser. It only replaces simple
    /// "key": value pairs which live on their own line (which is how the
    /// YgoMaster settings files are written).
    /// </summary>
    public static class ConfigEditor
    {
        public static string GetValue(string filePath, string key)
        {
            string value;
            return TryGetValue(filePath, key, out value) ? value : null;
        }

        public static bool GetBool(string filePath, string key, bool defaultValue = false)
        {
            string value = GetValue(filePath, key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetInt(string filePath, string key, int defaultValue = 0)
        {
            string value = GetValue(filePath, key);
            int result;
            if (!string.IsNullOrEmpty(value) &&
                int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return defaultValue;
        }

        public static bool TryGetValue(string filePath, string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(key) || !File.Exists(filePath))
            {
                return false;
            }
            try
            {
                foreach (string line in File.ReadAllLines(filePath))
                {
                    string lineKey;
                    string lineValue;
                    int valueStart;
                    int valueEnd;
                    if (!TryParseLine(line, out lineKey, out lineValue, out valueStart, out valueEnd))
                    {
                        continue;
                    }
                    if (string.Equals(lineKey, key, StringComparison.Ordinal))
                    {
                        value = lineValue;
                        return true;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static bool SetValue(string filePath, string key, object value)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            values[key] = value;
            return SetValues(filePath, values).Count == 0;
        }

        /// <summary>
        /// Replaces multiple top level values in a single read/write cycle.
        /// Returns the names of keys which could not be found.
        /// </summary>
        public static List<string> SetValues(string filePath, IDictionary<string, object> values)
        {
            List<string> missing = new List<string>();
            if (values == null || values.Count == 0)
            {
                return missing;
            }
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                missing.AddRange(values.Keys);
                return missing;
            }

            string[] lines = File.ReadAllLines(filePath);
            HashSet<string> pending = new HashSet<string>(values.Keys, StringComparer.Ordinal);
            HashSet<string> replaced = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Length; i++)
            {
                string lineKey;
                string lineValue;
                int valueStart;
                int valueEnd;
                if (!TryParseLine(lines[i], out lineKey, out lineValue, out valueStart, out valueEnd))
                {
                    continue;
                }
                if (!pending.Contains(lineKey) || replaced.Contains(lineKey))
                {
                    continue;
                }
                object newValue;
                if (!values.TryGetValue(lineKey, out newValue))
                {
                    continue;
                }
                lines[i] = lines[i].Substring(0, valueStart) + FormatValue(newValue) + lines[i].Substring(valueEnd);
                pending.Remove(lineKey);
                replaced.Add(lineKey);
                if (pending.Count == 0)
                {
                    break;
                }
            }

            try
            {
                File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                missing.AddRange(pending);
                Console.WriteLine("[ConfigEditor] Failed to write " + filePath + " - " + e.Message);
                return missing;
            }

            missing.AddRange(pending);
            return missing;
        }

        /// <summary>
        /// Replaces the numeric child values of the DefaultCraftPoints block.
        /// The child keys ("3".."6") are only used inside that block so this
        /// stays scoped to the block instead of matching keys elsewhere.
        /// </summary>
        public static bool SetDefaultCraftPoints(string filePath, int normal, int rare, int superRare, int ultraRare)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("[ConfigEditor] Failed to read " + filePath + " - " + e.Message);
                return false;
            }

            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string lineKey;
                string lineValue;
                int valueStart;
                int valueEnd;
                if (TryParseLine(lines[i], out lineKey, out lineValue, out valueStart, out valueEnd) &&
                    string.Equals(lineKey, "DefaultCraftPoints", StringComparison.Ordinal))
                {
                    start = i;
                    break;
                }
            }
            if (start < 0)
            {
                return false;
            }

            Dictionary<string, object> values = new Dictionary<string, object>();
            values["3"] = normal;
            values["4"] = rare;
            values["5"] = superRare;
            values["6"] = ultraRare;

            int depth = CountBraceDelta(lines[start]);
            for (int i = start + 1; i < lines.Length && depth > 0; i++)
            {
                if (depth == 1)
                {
                    string lineKey;
                    string lineValue;
                    int valueStart;
                    int valueEnd;
                    if (TryParseLine(lines[i], out lineKey, out lineValue, out valueStart, out valueEnd) &&
                        values.ContainsKey(lineKey))
                    {
                        lines[i] = lines[i].Substring(0, valueStart) + FormatValue(values[lineKey]) + lines[i].Substring(valueEnd);
                        values.Remove(lineKey);
                        if (values.Count == 0)
                        {
                            break;
                        }
                    }
                }
                depth += CountBraceDelta(lines[i]);
            }

            try
            {
                File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[ConfigEditor] Failed to write " + filePath + " - " + e.Message);
                return false;
            }
        }

        static int CountBraceDelta(string line)
        {
            int delta = 0;
            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"')
                    {
                        inString = false;
                    }
                    continue;
                }
                if (c == '"')
                {
                    inString = true;
                    continue;
                }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                {
                    break;
                }
                if (c == '{' || c == '[')
                {
                    delta++;
                }
                else if (c == '}' || c == ']')
                {
                    delta--;
                }
            }
            return delta;
        }

        public static string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }
            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }
            if (value is int)
            {
                return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
            if (value is long)
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }
            if (value is float)
            {
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is double)
            {
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }
            return Quote(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        static string Quote(string value)
        {
            if (value == null)
            {
                return "null";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        static bool TryParseLine(string line, out string key, out string value, out int valueStart, out int valueEnd)
        {
            key = null;
            value = null;
            valueStart = -1;
            valueEnd = -1;
            if (line == null)
            {
                return false;
            }

            int i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }
            if (i >= line.Length || line[i] != '"')
            {
                return false;
            }

            i++;
            StringBuilder keyBuilder = new StringBuilder();
            while (i < line.Length)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length)
                {
                    char escaped = line[i + 1];
                    if (escaped == '"' || escaped == '\\' || escaped == '/')
                    {
                        keyBuilder.Append(escaped);
                        i += 2;
                        continue;
                    }
                    keyBuilder.Append(c);
                    i++;
                    continue;
                }
                if (c == '"')
                {
                    break;
                }
                keyBuilder.Append(c);
                i++;
            }
            if (i >= line.Length || line[i] != '"')
            {
                return false;
            }
            i++;

            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }
            if (i >= line.Length || line[i] != ':')
            {
                return false;
            }
            i++;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }
            if (i >= line.Length)
            {
                return false;
            }

            valueStart = i;
            if (line[i] == '"')
            {
                i++;
                StringBuilder valueBuilder = new StringBuilder();
                while (i < line.Length)
                {
                    char c = line[i];
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        char escaped = line[i + 1];
                        if (escaped == '"' || escaped == '\\' || escaped == '/')
                        {
                            valueBuilder.Append(escaped);
                            i += 2;
                            continue;
                        }
                        valueBuilder.Append(c);
                        i++;
                        continue;
                    }
                    if (c == '"')
                    {
                        i++;
                        break;
                    }
                    valueBuilder.Append(c);
                    i++;
                }
                value = valueBuilder.ToString();
                valueEnd = i;
            }
            else
            {
                int start = i;
                while (i < line.Length)
                {
                    char c = line[i];
                    if (c == ',' || c == '}' || c == ']')
                    {
                        break;
                    }
                    if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    {
                        break;
                    }
                    if (char.IsWhiteSpace(c))
                    {
                        break;
                    }
                    i++;
                }
                valueEnd = i;
                value = line.Substring(start, i - start).Trim();
            }

            key = keyBuilder.ToString();
            return key.Length > 0 && valueStart >= 0;
        }
    }
}
