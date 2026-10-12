// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>Helpers to interpret generic Purview items.</summary>
    public static class ItemHelper
    {
        private static readonly string[] TitleProperties =
            { "DisplayName", "Name", "FilterName", "RuleCollectionName", "Identity" };

        public static string Title(JsonMap item)
        {
            if (item == null) return "";
            foreach (var p in TitleProperties)
            {
                var s = ValueFormatter.ToFlatText(item[p]);
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            foreach (var kv in item)
            {
                if (kv.Value is string s && !string.IsNullOrWhiteSpace(s)) return s;
            }
            return "(unnamed)";
        }

        /// <summary>All non-empty identity values of an item (for matching references).</summary>
        public static HashSet<string> IdentityValues(JsonMap item, IEnumerable<string> properties)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in properties)
            {
                foreach (var v in ScalarValues(item[p]))
                {
                    if (v.Length > 0) set.Add(v);
                }
            }
            return set;
        }

        /// <summary>Flattens a value into its scalar string values.</summary>
        public static IEnumerable<string> ScalarValues(object value)
        {
            switch (value)
            {
                case null:
                    yield break;
                case JsonMap _:
                    yield break;
                case IList list:
                    foreach (var o in list)
                        foreach (var s in ScalarValues(o))
                            yield return s;
                    yield break;
                default:
                    yield return ValueFormatter.ToFlatText(value).Trim();
                    yield break;
            }
        }

        /// <summary>Items of <paramref name="children"/> that reference <paramref name="parent"/>.</summary>
        public static List<JsonMap> FindRelated(JsonMap parent, IEnumerable<JsonMap> children, RelationDef relation)
        {
            var ids = IdentityValues(parent, relation.ParentProperties);
            if (ids.Count == 0) return new List<JsonMap>();
            return children
                .Where(c => !ReferenceEquals(c, parent))
                .Where(c => relation.ChildProperties.Any(p => ScalarValues(c[p]).Any(ids.Contains)))
                .ToList();
        }

        /// <summary>Columns to display: preferred columns that exist in the data, or a sensible default.</summary>
        public static List<string> DisplayColumns(CollectionDef def, IReadOnlyCollection<JsonMap> items)
        {
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
                foreach (var k in item.Keys)
                    present.Add(k);

            var columns = def.Columns.Where(present.Contains).ToList();
            if (def.ParentProperty != null && def.Key != null && items.Any(i => !string.IsNullOrEmpty(ValueFormatter.ToFlatText(i[def.ParentProperty]))))
            {
                int pos = Math.Min(1, columns.Count);
                columns.Insert(pos, ParentColumn);
            }
            if (columns.Count > 0) return columns;

            var first = items.FirstOrDefault();
            if (first == null) return def.Columns.Length > 0 ? def.Columns.ToList() : new List<string> { "Name" };
            return first.Where(kv => ValueFormatter.IsScalar(kv.Value)).Select(kv => kv.Key).Take(7).ToList();
        }

        /// <summary>Synthetic column showing the parent item title for hierarchical collections.</summary>
        public const string ParentColumn = "(Parent)";

        /// <summary>Orders items: by sort property (numeric-aware) and, for hierarchies, children right after parents.</summary>
        public static List<JsonMap> Order(CollectionDef def, IEnumerable<JsonMap> items)
        {
            var list = items.ToList();
            if (def.SortProperty != null)
            {
                list = list
                    .Select((item, index) => new { item, index })
                    .OrderBy(x => x.item[def.SortProperty] == null ? 1 : 0)
                    .ThenBy(x => x.item[def.SortProperty], ValueComparer.Instance)
                    .ThenBy(x => x.index)
                    .Select(x => x.item)
                    .ToList();
            }
            if (def.ParentProperty == null) return list;

            var lookup = BuildParentLookup(def, list);
            var result = new List<JsonMap>(list.Count);
            var added = new HashSet<JsonMap>();
            void AddWithChildren(JsonMap item, int depth)
            {
                if (!added.Add(item) || depth > 10) return;
                result.Add(item);
                foreach (var child in list.Where(c => ReferenceEquals(lookup.TryGetValue(c, out var p) ? p : null, item)))
                    AddWithChildren(child, depth + 1);
            }
            foreach (var item in list.Where(i => !lookup.ContainsKey(i)))
                AddWithChildren(item, 0);
            foreach (var item in list)
                AddWithChildren(item, 0); // orphans whose parent wasn't found or cycles
            return result;
        }

        /// <summary>Maps each child item to its parent item (same collection).</summary>
        public static Dictionary<JsonMap, JsonMap> BuildParentLookup(CollectionDef def, IReadOnlyCollection<JsonMap> items)
        {
            var map = new Dictionary<JsonMap, JsonMap>();
            if (def.ParentProperty == null) return map;
            var byId = new Dictionary<string, JsonMap>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
                foreach (var id in IdentityValues(item, CollectionDef.IdentityProperties))
                    if (!byId.ContainsKey(id)) byId[id] = item;
            foreach (var item in items)
            {
                var parentRef = ValueFormatter.ToFlatText(item[def.ParentProperty]).Trim();
                if (parentRef.Length > 0 && byId.TryGetValue(parentRef, out var parent) && !ReferenceEquals(parent, item))
                    map[item] = parent;
            }
            return map;
        }

        public static int Depth(JsonMap item, Dictionary<JsonMap, JsonMap> parents)
        {
            int depth = 0;
            while (parents.TryGetValue(item, out var p) && depth < 10)
            {
                depth++;
                item = p;
            }
            return depth;
        }
    }

    /// <summary>Numeric-aware, case-insensitive comparison of item values and cell text.</summary>
    public sealed class ValueComparer : IComparer<object>, IComparer<string>
    {
        public static readonly ValueComparer Instance = new ValueComparer();

        public int Compare(object x, object y) =>
            Compare(ValueFormatter.ToFlatText(x), ValueFormatter.ToFlatText(y));

        public int Compare(string x, string y)
        {
            x = x ?? "";
            y = y ?? "";
            if (double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var dx) &&
                double.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out var dy))
                return dx.CompareTo(dy);
            if (x.Length == 0 && y.Length > 0) return 1;
            if (y.Length == 0 && x.Length > 0) return -1;
            return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
