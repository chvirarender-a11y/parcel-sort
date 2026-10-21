using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParcelSort.Core
{
    /// <summary>
    /// A level as stored on disk. Example:
    /// { "id": 1, "name": "Heart", "bays": 5,
    ///   "field": [".rr.rr.", "rrrrrrr", ".rrrrr.", "..rrr..", "...r..."],
    ///   "lanes": ["r6 r5", "r7"] }
    /// Field rows run top to bottom; '.' is bare soil. Each lane lists harvesters front first,
    /// as a color letter followed by how many crops it can carry.
    /// </summary>
    [Serializable]
    public class FieldLevel
    {
        public int id;
        public string name = "";
        public int bays = 5;
        public string[] field = new string[0];
        public string[] lanes = new string[0];

        public int Width { get { return field.Length == 0 ? 0 : field[0].Length; } }
        public int Height { get { return field.Length; } }

        /// <summary>Crop at (x, y) with y = 0 the bottom row, or Crops.None.</summary>
        public int CropAt(int x, int y)
        {
            char c = field[Height - 1 - y][x];
            return c == Crops.Empty ? Crops.None : Crops.FromLetter(c);
        }

        public static List<Harvester> ParseLane(string lane)
        {
            var result = new List<Harvester>();
            foreach (var token in lane.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int color = Crops.FromLetter(token[0]);
                int capacity = int.Parse(token.Substring(1), CultureInfo.InvariantCulture);
                result.Add(new Harvester(color, capacity));
            }
            return result;
        }

        public static string FormatLane(IEnumerable<Harvester> lane)
        {
            var sb = new StringBuilder();
            foreach (var h in lane)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Crops.ToLetter(h.Color)).Append(h.Capacity.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Returns a list of problems; empty means the level is well formed.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            if (bays < 1) errors.Add("bays must be at least 1");
            if (Height == 0) errors.Add("field is empty");
            if (lanes.Length == 0) errors.Add("level has no lanes");
            if (errors.Count > 0) return errors;

            var crops = new int[Crops.Count];
            foreach (var row in field)
            {
                if (row.Length != Width) errors.Add("field rows must all be " + Width + " wide");
                foreach (char c in row)
                {
                    if (c == Crops.Empty) continue;
                    int crop = Crops.FromLetter(c);
                    if (crop < 0) errors.Add("unknown crop letter '" + c + "'");
                    else crops[crop]++;
                }
            }

            var capacity = new int[Crops.Count];
            foreach (var lane in lanes)
            {
                try
                {
                    foreach (var h in ParseLane(lane))
                    {
                        if (h.Color < 0 || h.Capacity < 1) errors.Add("bad harvester in lane '" + lane + "'");
                        else capacity[h.Color] += h.Capacity;
                    }
                }
                catch (FormatException)
                {
                    errors.Add("cannot read lane '" + lane + "'");
                }
            }

            for (int c = 0; c < Crops.Count; c++)
            {
                if (crops[c] != capacity[c])
                    errors.Add(string.Format("{0}: {1} crops but harvesters carry {2}", Crops.Names[c], crops[c], capacity[c]));
            }
            return errors;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"id\": {0},\n", id);
            sb.AppendFormat("  \"name\": \"{0}\",\n", name);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  \"bays\": {0},\n", bays);
            AppendArray(sb, "field", field, true);
            AppendArray(sb, "lanes", lanes, false);
            sb.Append("}\n");
            return sb.ToString();
        }

        static void AppendArray(StringBuilder sb, string key, string[] items, bool more)
        {
            sb.AppendFormat("  \"{0}\": [\n", key);
            for (int i = 0; i < items.Length; i++)
            {
                sb.Append("    \"").Append(items[i]).Append('"');
                sb.Append(i < items.Length - 1 ? ",\n" : "\n");
            }
            sb.Append(more ? "  ],\n" : "  ]\n");
        }

        public static FieldLevel FromJson(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("level JSON must be an object");

            var level = new FieldLevel();
            object value;
            if (root.TryGetValue("id", out value)) level.id = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (root.TryGetValue("name", out value)) level.name = (string)value;
            if (root.TryGetValue("bays", out value)) level.bays = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (root.TryGetValue("field", out value)) level.field = ToStrings(value);
            if (root.TryGetValue("lanes", out value)) level.lanes = ToStrings(value);
            return level;
        }

        static string[] ToStrings(object value)
        {
            var list = (List<object>)value;
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = (string)list[i];
            return result;
        }
    }
}
