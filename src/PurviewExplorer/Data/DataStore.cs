// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.Data
{
    public enum LoadState
    {
        NotLoaded,
        Queued,
        Loading,
        Loaded,
        Unavailable,
        Failed,
    }

    public enum SourceKind
    {
        None,
        Live,
        Snapshot,
        Demo,
    }

    /// <summary>The items retrieved for one collection.</summary>
    public sealed class CollectionResult
    {
        public CollectionResult(string key, LoadState state = LoadState.NotLoaded)
        {
            Key = key;
            State = state;
        }

        public string Key { get; }
        public LoadState State { get; set; }
        public List<JsonMap> Items { get; set; } = new List<JsonMap>();

        /// <summary>Error or informational message (partial errors are kept even when items were returned).</summary>
        public string Message { get; set; }

        public List<string> Warnings { get; set; } = new List<string>();
        public DateTime? RetrievedUtc { get; set; }
        public double? DurationSeconds { get; set; }
        public string CommandLine { get; set; }

        public bool HasData => State == LoadState.Loaded;
    }

    /// <summary>Where the data currently shown came from.</summary>
    public sealed class SourceInfo
    {
        public SourceKind Kind { get; set; }
        public bool Connected { get; set; }
        public string Organization { get; set; }
        public string Account { get; set; }
        public string TenantId { get; set; }
        public string Environment { get; set; }
        public string ModuleVersion { get; set; }

        /// <summary>
        /// True for a search-only session (the workaround for guest accounts), false for a standard session,
        /// null when unknown (demo data, snapshots from before 0.5.0).
        /// </summary>
        public bool? SearchOnlySession { get; set; }

        public DateTime? CreatedUtc { get; set; }
        public string FilePath { get; set; }
        public string Application { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Organization)) return Organization;
                if (!string.IsNullOrEmpty(TenantId)) return TenantId;
                return Kind == SourceKind.None ? "" : "(unknown tenant)";
            }
        }
    }

    /// <summary>In-memory store of everything retrieved or loaded from a snapshot.</summary>
    public sealed class DataStore
    {
        private readonly Dictionary<string, CollectionResult> _results =
            new Dictionary<string, CollectionResult>(StringComparer.OrdinalIgnoreCase);

        public DataStore(SourceInfo source = null)
        {
            Source = source ?? new SourceInfo();
        }

        public SourceInfo Source { get; }

        public IEnumerable<CollectionResult> Results => _results.Values;

        public CollectionResult Get(string key)
        {
            if (!_results.TryGetValue(key, out var r))
            {
                r = new CollectionResult(key);
                _results[key] = r;
            }
            return r;
        }

        public bool Contains(string key) => _results.ContainsKey(key);

        public void Set(CollectionResult result) => _results[result.Key] = result;

        public int LoadedCount => _results.Values.Count(r => r.State == LoadState.Loaded);

        public bool AllLoaded(IEnumerable<CollectionDef> defs) =>
            defs.All(d => _results.TryGetValue(d.Key, out var r) && r.State != LoadState.NotLoaded && r.State != LoadState.Queued && r.State != LoadState.Loading);
    }
}
