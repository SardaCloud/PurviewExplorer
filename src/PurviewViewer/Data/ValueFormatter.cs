// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SardaCloud.PurviewViewer.Data
{
    /// <summary>Turns item values into display text.</summary>
    public static class ValueFormatter
    {
        public const int MaxShortLength = 1000;

        /// <summary>Single-line text for list cells (truncated).</summary>
        public static string ToShortText(object value)
        {
            string s = ToFlatText(value);
            return s.Length > MaxShortLength ? s.Substring(0, MaxShortLength) + "…" : s;
        }

        /// <summary>Single-line, untruncated text (used for CSV export and searching).</summary>
        public static string ToFlatText(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case string s:
                    return OneLine(s);
                case bool b:
                    return b ? "True" : "False";
                case double d:
                    return d.ToString("G", CultureInfo.InvariantCulture);
                case JsonMap map:
                    return Json.Serialize(map, false);
                case IList list:
                    if (list.Count == 0) return string.Empty;
                    if (list.Cast<object>().All(IsScalar))
                        return string.Join("; ", list.Cast<object>().Select(ToFlatText));
                    return Json.Serialize(list, false);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Multi-line text for the value viewer; JSON and XML strings are pretty-printed.</summary>
        public static string ToLongText(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case string s:
                    return PrettyString(s);
                case IList list when list.Cast<object>().All(IsScalar):
                    var parts = list.Cast<object>().Select(o => o is string str ? PrettyString(str) : ToFlatText(o)).ToList();
                    string separator = parts.Any(p => p.Contains("\n")) ? "\r\n\r\n" : "\r\n";
                    return string.Join(separator, parts);
                case JsonMap _:
                case IList _:
                    return NormalizeNewLines(Json.Serialize(value, true));
                default:
                    return ToFlatText(value);
            }
        }

        public static bool IsScalar(object value) => !(value is JsonMap) && !(value is IList);

        public static string PrettyString(string s)
        {
            string t = s.Trim();
            if (t.Length > 1 && ((t[0] == '{' && t[t.Length - 1] == '}') || (t[0] == '[' && t[t.Length - 1] == ']')))
            {
                try { return NormalizeNewLines(Json.Serialize(Json.Parse(t), true)); }
                catch (JsonException) { }
            }
            if (t.Length > 1 && t[0] == '<' && t[t.Length - 1] == '>')
            {
                try { return NormalizeNewLines(XDocument.Parse(t).ToString()); }
                catch (System.Xml.XmlException) { }
            }
            return NormalizeNewLines(s);
        }

        public static string NormalizeNewLines(string s) =>
            s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");

        private static string OneLine(string s)
        {
            if (s.IndexOfAny(new[] { '\r', '\n', '\t' }) < 0) return s;
            var sb = new StringBuilder(s.Length);
            bool lastWasSpace = false;
            foreach (char c in s)
            {
                bool ws = c == '\r' || c == '\n' || c == '\t' || c == ' ';
                if (ws)
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
