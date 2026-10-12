// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>Exports a collection to CSV (UTF-8 with BOM so Excel detects the encoding).</summary>
    public static class CsvExport
    {
        public static void Write(string path, CollectionDef def, IReadOnlyCollection<JsonMap> items)
        {
            File.WriteAllText(path, ToCsv(def, items), new UTF8Encoding(true));
        }

        public static string ToCsv(CollectionDef def, IReadOnlyCollection<JsonMap> items)
        {
            // Preferred columns first, then every other property in first-seen order.
            var columns = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in ItemHelper.DisplayColumns(def, items).Where(c => c != ItemHelper.ParentColumn))
                if (seen.Add(c)) columns.Add(c);
            foreach (var item in items)
                foreach (var k in item.Keys)
                    if (seen.Add(k)) columns.Add(k);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columns.Select(Escape)));
            foreach (var item in items)
                sb.AppendLine(string.Join(",", columns.Select(c => Escape(ValueFormatter.ToFlatText(item[c])))));
            return sb.ToString();
        }

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            // Neutralize spreadsheet formula injection.
            if ("=+-@".IndexOf(s[0]) >= 0 && !double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                s = "'" + s;
            return s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
