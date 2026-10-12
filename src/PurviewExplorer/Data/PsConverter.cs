// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Security;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>Converts PowerShell output objects into plain JSON-like values.</summary>
    public static class PsConverter
    {
        public const int MaxDepth = 5;
        public const int MaxListItems = 10000;

        private static readonly HashSet<string> SkippedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RunspaceId", "PSComputerName", "PSShowComputerName", "PSSourceJobInstanceId",
        };

        public static JsonMap ToItem(PSObject obj)
        {
            if (obj == null) return new JsonMap();
            var baseObj = obj.BaseObject;
            if (baseObj is IDictionary dict && !(baseObj is PSObject))
                return DictionaryToMap(dict, 1);

            if (IsScalar(baseObj))
                return new JsonMap { ["Value"] = ConvertValue(obj, 1) };

            return PropertiesToMap(obj, 1);
        }

        public static object ConvertValue(object value, int depth)
        {
            if (value == null) return null;

            if (value is PSObject pso)
            {
                var b = pso.BaseObject;
                if (b == null) return null;
                if (b is PSObject || b is PSCustomObject)
                {
                    if (!IsPropertyBag(pso))
                    {
                        // Deserialized types (ADObjectId, Unlimited<T>, ...) keep their original ToString(); it's only
                        // useful when it isn't just the type name (types that don't override ToString()).
                        var text = SafeToString(pso);
                        if (!string.IsNullOrEmpty(text) && !text.StartsWith("@{", StringComparison.Ordinal) && !IsTypeName(pso, text))
                            return text;
                    }
                    return depth >= MaxDepth ? (object)SafeToString(pso) : PropertiesToMap(pso, depth + 1);
                }
                value = b;
            }

            switch (value)
            {
                case string s: return s;
                case bool b: return b;
                case char c: return c.ToString();
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case ulong ul:
                    return ul <= long.MaxValue ? (object)(long)ul : ul.ToString(CultureInfo.InvariantCulture);
                case float f: return (double)f;
                case double d: return d;
                case decimal m: return (double)m;
                case DateTime dt: return Json.FormatDate(dt);
                case DateTimeOffset dto: return dto.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
                case Guid _:
                case Enum _:
                case TimeSpan _:
                case Uri _:
                case Version _:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                case SecureString _:
                    return "(secure string)";
                case byte[] bytes:
                    return "(" + bytes.Length + " bytes)";
                case ScriptBlock sb:
                    return sb.ToString();
                case IDictionary dict:
                    return depth >= MaxDepth ? (object)SafeToString(value) : DictionaryToMap(dict, depth + 1);
                case IEnumerable seq:
                    if (depth >= MaxDepth) return SafeToString(value);
                    var list = new List<object>();
                    foreach (var o in seq)
                    {
                        if (list.Count >= MaxListItems)
                        {
                            list.Add("(list truncated)");
                            break;
                        }
                        list.Add(ConvertValue(o, depth + 1));
                    }
                    return list;
                default:
                    return SafeToString(value);
            }
        }

        private static bool IsScalar(object o) =>
            o is string || o is ValueType || o is Guid || o is Uri || o is Version;

        private static bool IsTypeName(PSObject pso, string text)
        {
            const string prefix = "Deserialized.";
            foreach (var t in pso.TypeNames)
            {
                if (t == text) return true;
                if (t.StartsWith(prefix, StringComparison.Ordinal) && t.Substring(prefix.Length) == text) return true;
            }
            return false;
        }

        private static bool IsPropertyBag(PSObject pso)
        {
            var first = pso.TypeNames.Count > 0 ? pso.TypeNames[0] : null;
            return first == null
                || first == "System.Management.Automation.PSCustomObject"
                || first == "Deserialized.System.Management.Automation.PSCustomObject"
                || first == "System.Object";
        }

        private static JsonMap PropertiesToMap(PSObject obj, int depth)
        {
            var map = new JsonMap();
            foreach (var p in obj.Properties)
            {
                if (SkippedProperties.Contains(p.Name) || map.ContainsKey(p.Name)) continue;
                object v;
                try
                {
                    v = p.Value;
                }
                catch (Exception ex)
                {
                    map[p.Name] = "(error reading property: " + ex.Message + ")";
                    continue;
                }
                map[p.Name] = ConvertValue(v, depth);
            }
            return map;
        }

        private static JsonMap DictionaryToMap(IDictionary dict, int depth)
        {
            var map = new JsonMap();
            foreach (DictionaryEntry e in dict)
                map[Convert.ToString(e.Key, CultureInfo.InvariantCulture) ?? ""] = ConvertValue(e.Value, depth);
            return map;
        }

        private static string SafeToString(object o)
        {
            try
            {
                return Convert.ToString(o, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                return "(" + ex.GetType().Name + ")";
            }
        }

        /// <summary>Readable message for an ErrorRecord.</summary>
        public static string FormatError(ErrorRecord er)
        {
            if (er == null) return "";
            var msg = er.ErrorDetails?.Message;
            if (string.IsNullOrWhiteSpace(msg)) msg = er.Exception?.Message;
            if (string.IsNullOrWhiteSpace(msg)) msg = er.ToString();
            return msg.Trim();
        }

        public static string FormatException(Exception ex)
        {
            if (ex is RuntimeException re && re.ErrorRecord != null && !(re.ErrorRecord.Exception is ParentContainsErrorRecordException))
                return FormatError(re.ErrorRecord);
            var inner = ex;
            while (inner.InnerException != null && (inner is CmdletInvocationException || inner is AggregateException))
                inner = inner.InnerException;
            return inner.Message;
        }
    }
}
