// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SardaCloud.PurviewViewer.Data
{
    public sealed class JsonException : FormatException
    {
        public JsonException(string message, int position) : base(message + " (at offset " + position + ")")
        {
            Position = position;
        }

        public int Position { get; }
    }

    /// <summary>
    /// Minimal JSON reader/writer. Objects map to <see cref="JsonMap"/>, arrays to
    /// <c>List&lt;object&gt;</c>, numbers to <c>long</c> or <c>double</c>.
    /// Kept dependency-free so the application ships as a single executable.
    /// </summary>
    public static class Json
    {
        private const int MaxDepth = 256;

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            p.SkipWhitespace();
            var value = p.ParseValue(0);
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("Unexpected characters after the JSON value");
            return value;
        }

        public static string Serialize(object value, bool indented = true)
        {
            var sb = new StringBuilder();
            Write(sb, value, indented, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, bool indented, int level)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d)) WriteString(sb, d.ToString(CultureInfo.InvariantCulture));
                    else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return;
                case float f:
                    Write(sb, (double)f, indented, level);
                    return;
                case decimal m:
                    sb.Append(m.ToString(CultureInfo.InvariantCulture));
                    return;
                case long _:
                case int _:
                case short _:
                case byte _:
                case sbyte _:
                case ushort _:
                case uint _:
                case ulong _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case DateTime dt:
                    WriteString(sb, FormatDate(dt));
                    return;
                case JsonMap map:
                    WriteObject(sb, map, indented, level);
                    return;
                case IDictionary dict:
                    var converted = new JsonMap();
                    foreach (DictionaryEntry e in dict) converted[Convert.ToString(e.Key, CultureInfo.InvariantCulture)] = e.Value;
                    WriteObject(sb, converted, indented, level);
                    return;
                case IEnumerable seq:
                    WriteArray(sb, seq, indented, level);
                    return;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        public static string FormatDate(DateTime dt)
        {
            switch (dt.Kind)
            {
                case DateTimeKind.Utc: return dt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
                case DateTimeKind.Local: return dt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
                default: return dt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
            }
        }

        private static void WriteObject(StringBuilder sb, JsonMap map, bool indented, int level)
        {
            if (map.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            bool first = true;
            foreach (var kv in map)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indented, level + 1);
                WriteString(sb, kv.Key);
                sb.Append(indented ? ": " : ":");
                Write(sb, kv.Value, indented, level + 1);
            }
            NewLine(sb, indented, level);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable seq, bool indented, int level)
        {
            bool first = true;
            sb.Append('[');
            foreach (var item in seq)
            {
                if (!first) sb.Append(',');
                first = false;
                NewLine(sb, indented, level + 1);
                Write(sb, item, indented, level + 1);
            }
            if (!first) NewLine(sb, indented, level);
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool indented, int level)
        {
            if (!indented) return;
            sb.Append('\n');
            sb.Append(' ', level * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c == '\u2028' || c == '\u2029')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
                _i = s.Length > 0 && s[0] == '\uFEFF' ? 1 : 0;
            }

            public bool AtEnd => _i >= _s.Length;

            public JsonException Error(string message) => new JsonException(message, _i);

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                    else break;
                }
            }

            public object ParseValue(int depth)
            {
                if (depth > MaxDepth) throw Error("JSON nesting is too deep");
                if (AtEnd) throw Error("Unexpected end of JSON");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject(depth);
                    case '[': return ParseArray(depth);
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private void Expect(string literal)
            {
                if (string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0) throw Error("Invalid literal");
                _i += literal.Length;
            }

            private JsonMap ParseObject(int depth)
            {
                var map = new JsonMap();
                _i++; // {
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return map; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Error("Expected a property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Error("Expected ':'");
                    _i++;
                    SkipWhitespace();
                    map[key] = ParseValue(depth + 1);
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return map; }
                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ParseArray(int depth)
            {
                var list = new List<object>();
                _i++; // [
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    SkipWhitespace();
                    list.Add(ParseValue(depth + 1));
                    SkipWhitespace();
                    if (AtEnd) throw Error("Unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("Expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("Unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd) throw Error("Unterminated escape sequence");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("Invalid \\u escape");
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("Invalid \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default:
                            throw Error("Invalid escape sequence '\\" + e + "'");
                    }
                }
            }

            private object ParseNumber()
            {
                int start = _i;
                bool isInteger = true;
                if (_s[_i] == '-') _i++;
                while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                if (!AtEnd && _s[_i] == '.')
                {
                    isInteger = false;
                    _i++;
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                if (!AtEnd && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    isInteger = false;
                    _i++;
                    if (!AtEnd && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                string token = _s.Substring(start, _i - start);
                if (isInteger && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
                throw new JsonException("Invalid number '" + token + "'", start);
            }
        }
    }
}
