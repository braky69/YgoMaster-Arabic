using System;
using System.Collections.Generic;
using System.IO;
using MiniJSON;

namespace YgoMasterConfig
{
    /// <summary>
    /// Values which the launcher can push into a player's save file.
    /// A value of -1 means "leave unchanged".
    /// </summary>
    public class PlayerProfile
    {
        public string Name = "";
        public int Gems = -1;
        public int CraftNormal = -1;
        public int CraftRare = -1;
        public int CraftSuperRare = -1;
        public int CraftUltraRare = -1;
    }

    /// <summary>
    /// Reads / writes the game's Player.json files so the launcher can set the
    /// player name, gems and craft points (N/R/SR/UR dust).
    ///
    /// The server keeps the player in memory while it is running and rewrites
    /// Player.json on save, so these edits must be done while the game/server
    /// is closed.
    /// </summary>
    public static class PlayerDataEditor
    {
        // ItemID values used for the craft points (see Enums/ItemID.cs).
        public const int ItemCraftNormal = 3;    // N dust
        public const int ItemCraftRare = 4;      // R dust
        public const int ItemCraftSuperRare = 5; // SR dust
        public const int ItemCraftUltraRare = 6; // UR dust

        public const int MaxValue = 1000000000;

        public static string GetPlayersDirectory(string baseDir)
        {
            return Path.Combine(baseDir, "Data", "Players");
        }

        public static List<string> GetPlayerFiles(string playersDirectory)
        {
            List<string> files = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(playersDirectory) || !Directory.Exists(playersDirectory))
                {
                    return files;
                }
                foreach (string dir in Directory.GetDirectories(playersDirectory))
                {
                    string file = Path.Combine(dir, "Player.json");
                    if (File.Exists(file))
                    {
                        files.Add(file);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[PlayerDataEditor] Failed to list players - " + e.Message);
            }
            return files;
        }

        /// <summary>
        /// Reads the most recently used player save so the UI can be prefilled.
        /// </summary>
        public static bool TryReadProfile(string playersDirectory, out PlayerProfile profile)
        {
            profile = null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string file in GetPlayerFiles(playersDirectory))
            {
                try
                {
                    DateTime time = File.GetLastWriteTimeUtc(file);
                    if (time > bestTime)
                    {
                        bestTime = time;
                        best = file;
                    }
                }
                catch
                {
                }
            }
            if (best == null)
            {
                return false;
            }
            return TryReadProfileFile(best, out profile);
        }

        public static bool TryReadProfileFile(string file, out PlayerProfile profile)
        {
            profile = null;
            try
            {
                Dictionary<string, object> data = MiniJSON.Json.DeserializeStripped(File.ReadAllText(file)) as Dictionary<string, object>;
                if (data == null)
                {
                    return false;
                }
                PlayerProfile result = new PlayerProfile();
                result.Name = GetString(data, "Name");
                result.Gems = GetInt(data, "Gems", -1);
                Dictionary<string, object> craft = GetDictionary(data, "CraftPoints");
                result.CraftNormal = GetInt(craft, ItemCraftNormal.ToString(), -1);
                result.CraftRare = GetInt(craft, ItemCraftRare.ToString(), -1);
                result.CraftSuperRare = GetInt(craft, ItemCraftSuperRare.ToString(), -1);
                result.CraftUltraRare = GetInt(craft, ItemCraftUltraRare.ToString(), -1);
                profile = result;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[PlayerDataEditor] Failed to read " + file + " - " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Applies the profile to every saved player. Returns the number of
        /// files updated. If there are no save files yet the values are only
        /// applied through the defaults in Settings.json.
        /// </summary>
        public static int ApplyToAllPlayers(string playersDirectory, PlayerProfile profile, out int failed)
        {
            failed = 0;
            int updated = 0;
            foreach (string file in GetPlayerFiles(playersDirectory))
            {
                if (ApplyToPlayerFile(file, profile))
                {
                    updated++;
                }
                else
                {
                    failed++;
                }
            }
            return updated;
        }

        public static bool ApplyToPlayerFile(string file, PlayerProfile profile)
        {
            try
            {
                Dictionary<string, object> data = MiniJSON.Json.DeserializeStripped(File.ReadAllText(file)) as Dictionary<string, object>;
                if (data == null)
                {
                    data = new Dictionary<string, object>();
                }

                if (!string.IsNullOrEmpty(profile.Name))
                {
                    data["Name"] = profile.Name;
                }
                if (profile.Gems >= 0)
                {
                    data["Gems"] = (long)Clamp(profile.Gems);
                }

                Dictionary<string, object> craft = GetDictionary(data, "CraftPoints");
                if (craft == null)
                {
                    craft = new Dictionary<string, object>();
                    data["CraftPoints"] = craft;
                }
                if (profile.CraftNormal >= 0)
                {
                    craft[ItemCraftNormal.ToString()] = (long)Clamp(profile.CraftNormal);
                }
                if (profile.CraftRare >= 0)
                {
                    craft[ItemCraftRare.ToString()] = (long)Clamp(profile.CraftRare);
                }
                if (profile.CraftSuperRare >= 0)
                {
                    craft[ItemCraftSuperRare.ToString()] = (long)Clamp(profile.CraftSuperRare);
                }
                if (profile.CraftUltraRare >= 0)
                {
                    craft[ItemCraftUltraRare.ToString()] = (long)Clamp(profile.CraftUltraRare);
                }

                string json = MiniJSON.Json.Format(MiniJSON.Json.Serialize(data));
                File.WriteAllText(file, json);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[PlayerDataEditor] Failed to write " + file + " - " + e.Message);
                return false;
            }
        }

        static int Clamp(int value)
        {
            if (value < 0)
            {
                return 0;
            }
            if (value > MaxValue)
            {
                return MaxValue;
            }
            return value;
        }

        static string GetString(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value);
            }
            return "";
        }

        static int GetInt(Dictionary<string, object> data, string key, int defaultValue)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                }
            }
            return defaultValue;
        }

        static Dictionary<string, object> GetDictionary(Dictionary<string, object> data, string key)
        {
            object value;
            if (data != null && data.TryGetValue(key, out value))
            {
                return value as Dictionary<string, object>;
            }
            return null;
        }
    }
}
