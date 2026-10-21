using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParcelSort.Core
{
    /// <summary>
    /// A level as stored on disk. Example:
    /// { "id": 1, "bufferSize": 5, "dockCount": 2, "truckCapacity": 3,
    ///   "trucks": "rgbrgb", "stacks": ["rgb", "bGr", "gbr"] }
    /// Stacks list parcels bottom to top. Upper-case letters start hidden.
    /// Trucks arrive in the order given.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int id;
        public int bufferSize = 5;
        public int dockCount = 2;
        public int truckCapacity = 3;
        public string trucks = "";
        public string[] stacks = new string[0];

        /// <summary>Returns a list of problems; empty means the level is well formed.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (bufferSize < 1) errors.Add("bufferSize must be at least 1");
            if (dockCount < 1) errors.Add("dockCount must be at least 1");
            if (truckCapacity < 1) errors.Add("truckCapacity must be at least 1");
            if (stacks == null || stacks.Length == 0) errors.Add("level has no stacks");
            if (string.IsNullOrEmpty(trucks)) errors.Add("level has no trucks");
            if (errors.Count > 0) return errors;

            var parcelCount = new int[ParcelColors.Count];
            var truckCount = new int[ParcelColors.Count];
            foreach (var stack in stacks)
            {
                foreach (char c in stack)
                {
                    int color = ParcelColors.FromLetter(c);
                    if (color < 0) errors.Add("unknown parcel letter '" + c + "'");
                    else parcelCount[color]++;
                }
            }
            foreach (char c in trucks)
            {
                int color = ParcelColors.FromLetter(c);
                if (color < 0 || char.IsUpper(c)) errors.Add("bad truck letter '" + c + "'");
                else truckCount[color]++;
            }
            for (int color = 0; color < ParcelColors.Count; color++)
            {
                if (parcelCount[color] != truckCount[color] * truckCapacity)
                {
                    errors.Add(string.Format("{0}: {1} parcels but {2} trucks x {3}",
                        ParcelColors.Names[color], parcelCount[color], truckCount[color], truckCapacity));
                }
            }
            return errors;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"id\": {0},\n", id);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"bufferSize\": {0},\n", bufferSize);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"dockCount\": {0},\n", dockCount);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"truckCapacity\": {0},\n", truckCapacity);
            sb.AppendFormat("  \"trucks\": \"{0}\",\n", trucks);
            sb.Append("  \"stacks\": [");
            for (int i = 0; i < stacks.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(stacks[i]).Append('"');
            }
            sb.Append("]\n}\n");
            return sb.ToString();
        }

        public static LevelData FromJson(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("level JSON must be an object");

            var level = new LevelData();
            level.id = GetInt(root, "id", 0);
            level.bufferSize = GetInt(root, "bufferSize", level.bufferSize);
            level.dockCount = GetInt(root, "dockCount", level.dockCount);
            level.truckCapacity = GetInt(root, "truckCapacity", level.truckCapacity);
            object trucks;
            if (root.TryGetValue("trucks", out trucks)) level.trucks = (string)trucks;
            object stacks;
            if (root.TryGetValue("stacks", out stacks))
            {
                var list = (List<object>)stacks;
                level.stacks = new string[list.Count];
                for (int i = 0; i < list.Count; i++) level.stacks[i] = (string)list[i];
            }
            return level;
        }

        static int GetInt(Dictionary<string, object> obj, string key, int fallback)
        {
            object value;
            return obj.TryGetValue(key, out value) ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
        }
    }
}
