// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SardaCloud.PurviewExplorer.Catalog
{
    /// <summary>Shell stock icon identifiers (SHSTOCKICONID).</summary>
    public enum StockIcon
    {
        DocNoAssoc = 0,
        DocAssoc = 1,
        Application = 2,
        Folder = 3,
        FolderOpen = 4,
        Drive35 = 6,
        DriveNetDisabled = 10,
        World = 13,
        Server = 15,
        MyNetwork = 17,
        Find = 22,
        Help = 23,
        Recycler = 31,
        Lock = 47,
        AutoList = 49,
        ServerShare = 51,
        Stack = 55,
        StuffedFolder = 57,
        Shield = 77,
        Warning = 78,
        Info = 79,
        Error = 80,
        Key = 81,
        Software = 82,
        Delete = 84,
        Users = 96,
        NetworkConnect = 103,
        Internet = 104,
        ZipFile = 105,
    }

    /// <summary>A Purview solution, shown as one tab.</summary>
    public sealed class SolutionDef
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string LearnMoreUrl { get; set; }
        public StockIcon Icon { get; set; }
        public List<CollectionDef> Collections { get; } = new List<CollectionDef>();
    }

    /// <summary>A type of configuration object, retrieved with one read-only cmdlet.</summary>
    public sealed class CollectionDef
    {
        /// <summary>Default properties used to identify an item when matching parents and relations.</summary>
        public static readonly string[] IdentityProperties =
            { "Guid", "ImmutableId", "Identity", "Name", "ExchangeObjectId", "Id", "DistinguishedName" };

        public SolutionDef Solution { get; internal set; }

        /// <summary>Unique key, "Solution.Collection".</summary>
        public string Key { get; internal set; }

        public string LocalKey { get; set; }
        public string Name { get; set; }
        public string Cmdlet { get; set; }
        public List<KeyValuePair<string, object>> Parameters { get; } = new List<KeyValuePair<string, object>>();

        /// <summary>Preferred list columns; columns absent from every item are hidden.</summary>
        public string[] Columns { get; set; } = new string[0];

        public string SortProperty { get; set; }

        /// <summary>Property that points to a parent item in the same collection (e.g. sublabels).</summary>
        public string ParentProperty { get; set; }

        /// <summary>When set, the cmdlet runs once per item of these collections.</summary>
        public string[] ForEachCollections { get; set; }
        public string ForEachParameter { get; set; }
        public string ForEachProperty { get; set; } = "Name";
        public string ForEachLabel { get; set; }

        public List<RelationDef> Relations { get; } = new List<RelationDef>();
        public string Notes { get; set; }
        public bool Undocumented { get; set; }

        public CollectionDef Param(string name, object value = null)
        {
            Parameters.Add(new KeyValuePair<string, object>(name, value ?? true));
            return this;
        }

        public CollectionDef Related(string title, string collectionKey, string[] childProperties, string[] parentProperties = null)
        {
            Relations.Add(new RelationDef
            {
                Title = title,
                CollectionKey = collectionKey,
                ChildProperties = childProperties,
                ParentProperties = parentProperties ?? IdentityProperties,
            });
            return this;
        }

        /// <summary>The command as it would be typed in PowerShell.</summary>
        public string CommandLine
        {
            get
            {
                var sb = new StringBuilder(Cmdlet);
                foreach (var p in Parameters)
                {
                    sb.Append(" -").Append(p.Key);
                    if (!(p.Value is bool b && b)) sb.Append(' ').Append(QuoteArgument(p.Value));
                }
                if (ForEachCollections != null && ForEachParameter != null)
                    sb.Append(" -").Append(ForEachParameter).Append(" <each ").Append(ForEachLabel ?? ForEachProperty).Append('>');
                return sb.ToString();
            }
        }

        internal static string QuoteArgument(object value)
        {
            string s = value?.ToString() ?? "";
            return s.Length > 0 && s.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_')
                ? s
                : "'" + s.Replace("'", "''") + "'";
        }

        public override string ToString() => Key;
    }

    /// <summary>Items in another collection that reference an item of this collection.</summary>
    public sealed class RelationDef
    {
        public string Title { get; set; }
        public string CollectionKey { get; set; }
        public string[] ChildProperties { get; set; }
        public string[] ParentProperties { get; set; }
    }
}
