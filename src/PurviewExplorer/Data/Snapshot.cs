// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>Reads and writes snapshot files (indented JSON, UTF-8).</summary>
    public static class Snapshot
    {
        public const string FormatName = "PurviewExplorer.Snapshot";

        /// <summary>Format name written by 0.1.0, when the application was called Purview Configuration Viewer.</summary>
        public const string LegacyFormatName = "PurviewViewer.Snapshot";

        public const int FormatVersion = 1;

        /// <summary>Values of <c>source.sessionType</c> (added in 0.5.0; absent in older snapshots and the demo data).</summary>
        public const string StandardSessionType = "Standard";
        public const string SearchOnlySessionType = "SearchOnly";

        public static string Serialize(DataStore store)
        {
            var src = store.Source;
            var source = new JsonMap
            {
                ["kind"] = (src.Kind == SourceKind.Demo ? SourceKind.Demo : SourceKind.Live).ToString(),
                ["organization"] = src.Organization,
                ["account"] = src.Account,
                ["tenantId"] = src.TenantId,
                ["environment"] = src.Environment,
                ["moduleVersion"] = src.ModuleVersion,
                ["sessionType"] = src.SearchOnlySession == null ? null : src.SearchOnlySession.Value ? SearchOnlySessionType : StandardSessionType,
            };

            var collections = new JsonMap();
            var ordered = PurviewCatalog.AllCollections.Select(d => d.Key)
                .Concat(store.Results.Select(r => r.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in ordered)
            {
                if (!store.Contains(key)) continue;
                var r = store.Get(key);
                if (r.State != LoadState.Loaded && r.State != LoadState.Unavailable && r.State != LoadState.Failed) continue;
                collections[key] = new JsonMap
                {
                    ["command"] = r.CommandLine ?? PurviewCatalog.Find(key)?.CommandLine,
                    ["state"] = r.State.ToString(),
                    ["retrievedUtc"] = r.RetrievedUtc.HasValue ? Json.FormatDate(DateTime.SpecifyKind(r.RetrievedUtc.Value, DateTimeKind.Utc)) : null,
                    ["durationSeconds"] = r.DurationSeconds.HasValue ? Math.Round(r.DurationSeconds.Value, 2) : (object)null,
                    ["message"] = r.Message,
                    ["warnings"] = r.Warnings.Count > 0 ? r.Warnings.Cast<object>().ToList() : null,
                    ["items"] = r.Items.Cast<object>().ToList(),
                };
            }

            var root = new JsonMap
            {
                ["format"] = FormatName,
                ["formatVersion"] = (long)FormatVersion,
                ["application"] = AppInfo.ProductAndVersion,
                ["createdUtc"] = Json.FormatDate(DateTime.SpecifyKind(src.CreatedUtc ?? DateTime.UtcNow, DateTimeKind.Utc)),
                ["source"] = source,
                ["collections"] = collections,
            };
            return Json.Serialize(root, true).Replace("\n", "\r\n") + "\r\n";
        }

        public static void Save(DataStore store, string path)
        {
            var text = Serialize(store);
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        public static DataStore Load(string path)
        {
            var store = Parse(File.ReadAllText(path, Encoding.UTF8), SourceKind.Snapshot);
            store.Source.FilePath = Path.GetFullPath(path);
            return store;
        }

        /// <summary>The fictional demo tenant embedded in the executable.</summary>
        public static DataStore LoadDemo()
        {
            using (var stream = typeof(Snapshot).Assembly.GetManifestResourceStream("PurviewExplorer.DemoSnapshot.json"))
            {
                if (stream == null) throw new InvalidDataException("The demo data is missing from the executable.");
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return Parse(reader.ReadToEnd(), SourceKind.Demo);
            }
        }

        public static DataStore Parse(string text, SourceKind kind)
        {
            var root = Json.Parse(text) as JsonMap;
            var format = root?["format"] as string;
            if (root == null || !(string.Equals(format, FormatName, StringComparison.Ordinal) || string.Equals(format, LegacyFormatName, StringComparison.Ordinal)))
                throw new InvalidDataException("This file isn't a Purview Explorer snapshot.");
            if (root["formatVersion"] is long v && v > FormatVersion)
                throw new InvalidDataException($"This snapshot uses format version {v}, which is newer than this application supports ({FormatVersion}).");

            var sourceMap = root["source"] as JsonMap ?? new JsonMap();
            var storedKind = sourceMap["kind"] as string;
            var info = new SourceInfo
            {
                Kind = string.Equals(storedKind, "Demo", StringComparison.OrdinalIgnoreCase) ? SourceKind.Demo : kind,
                Organization = sourceMap["organization"] as string,
                Account = sourceMap["account"] as string,
                TenantId = sourceMap["tenantId"] as string,
                Environment = sourceMap["environment"] as string,
                ModuleVersion = sourceMap["moduleVersion"] as string,
                SearchOnlySession = string.Equals(sourceMap["sessionType"] as string, SearchOnlySessionType, StringComparison.OrdinalIgnoreCase) ? true
                    : string.Equals(sourceMap["sessionType"] as string, StandardSessionType, StringComparison.OrdinalIgnoreCase) ? false
                    : (bool?)null,
                CreatedUtc = ParseDate(root["createdUtc"] as string),
                Application = root["application"] as string,
            };

            var store = new DataStore(info);
            if (root["collections"] is JsonMap collections)
            {
                foreach (var kv in collections)
                {
                    if (!(kv.Value is JsonMap c)) continue;
                    if (!Enum.TryParse(c["state"] as string, true, out LoadState state)) state = LoadState.Loaded;
                    if (state == LoadState.Queued || state == LoadState.Loading || state == LoadState.NotLoaded) continue;
                    var result = new CollectionResult(kv.Key, state)
                    {
                        CommandLine = c["command"] as string,
                        Message = c["message"] as string,
                        RetrievedUtc = ParseDate(c["retrievedUtc"] as string),
                        DurationSeconds = c["durationSeconds"] is double d ? d : c["durationSeconds"] is long l ? l : (double?)null,
                        Warnings = (c["warnings"] as List<object> ?? new List<object>()).Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)).ToList(),
                        Items = (c["items"] as List<object> ?? new List<object>()).OfType<JsonMap>().ToList(),
                    };
                    store.Set(result);
                }
            }
            return store;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
                ? dt
                : (DateTime?)null;
        }
    }
}
