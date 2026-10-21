using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ParcelSort.Core
{
    /// <summary>
    /// Minimal JSON reader so the core has no Unity dependency.
    /// Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int pos = 0;
            object value = ReadValue(json, ref pos);
            SkipWhitespace(json, ref pos);
            if (pos != json.Length) throw Error(json, pos, "unexpected trailing text");
            return value;
        }

        static object ReadValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) throw Error(s, pos, "unexpected end of input");
            char c = s[pos];
            if (c == '{') return ReadObject(s, ref pos);
            if (c == '[') return ReadArray(s, ref pos);
            if (c == '"') return ReadString(s, ref pos);
            if (Match(s, ref pos, "true")) return true;
            if (Match(s, ref pos, "false")) return false;
            if (Match(s, ref pos, "null")) return null;
            return ReadNumber(s, ref pos);
        }

        static Dictionary<string, object> ReadObject(string s, ref int pos)
        {
            var obj = new Dictionary<string, object>();
            pos++;
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return obj; }
            while (true)
            {
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != '"') throw Error(s, pos, "expected key");
                string key = ReadString(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw Error(s, pos, "expected ':'");
                pos++;
                obj[key] = ReadValue(s, ref pos);
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                if (pos < s.Length && s[pos] == '}') { pos++; return obj; }
                throw Error(s, pos, "expected ',' or '}'");
            }
        }

        static List<object> ReadArray(string s, ref int pos)
        {
            var list = new List<object>();
            pos++;
            SkipWhitespace(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref pos));
                SkipWhitespace(s, ref pos);
                if (pos < s.Length && s[pos] == ',') { pos++; continue; }
                if (pos < s.Length && s[pos] == ']') { pos++; return list; }
                throw Error(s, pos, "expected ',' or ']'");
            }
        }

        static string ReadString(string s, ref int pos)
        {
            var sb = new StringBuilder();
            pos++;
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (pos >= s.Length) break;
                char e = s[pos++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber));
                        pos += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            throw Error(s, pos, "unterminated string");
        }

        static double ReadNumber(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length && "+-0123456789.eE".IndexOf(s[pos]) >= 0) pos++;
            if (start == pos) throw Error(s, pos, "unexpected character");
            return double.Parse(s.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }

        static bool Match(string s, ref int pos, string word)
        {
            if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0) return false;
            pos += word.Length;
            return true;
        }

        static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        }

        static FormatException Error(string s, int pos, string message)
        {
            return new FormatException("JSON error at " + pos + ": " + message);
        }
    }
}
