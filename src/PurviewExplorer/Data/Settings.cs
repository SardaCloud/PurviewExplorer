// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>User preferences, stored as JSON in %APPDATA%\SardaCloud\PurviewExplorer\settings.json. No secrets are stored.</summary>
    public sealed class AppSettings
    {
        public AuthMode AuthMode { get; set; }
        public string UserPrincipalName { get; set; }
        public bool DisableWam { get; set; }
        public string AppId { get; set; }
        public string CertificateThumbprint { get; set; }
        public string Organization { get; set; }

        /// <summary>The other organization last used with an interactive sign-in (partner or guest access).</summary>
        public string DelegatedOrganization { get; set; }

        /// <summary>Organizations connected to successfully, most recent first.</summary>
        public List<string> RecentOrganizations { get; private set; } = new List<string>();

        public const int MaxRecentOrganizations = 10;

        /// <summary>
        /// Other organizations where only a search-only session worked, with when it last did (UTC). Connecting to them
        /// again within <see cref="SearchOnlyMemory"/> starts with a search-only session, saving the failing attempt.
        /// </summary>
        public Dictionary<string, DateTime> SearchOnlyOrganizations { get; } = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>After this, a standard session is tried first again, in case the service no longer needs the workaround.</summary>
        public static readonly TimeSpan SearchOnlyMemory = TimeSpan.FromDays(14);

        public CloudEnvironment Environment { get; set; }
        public string LastSnapshotFolder { get; set; }
        public bool Maximized { get; set; }
        public int[] Bounds { get; set; }

        private static string AppData => System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);

        public static string FilePath => Path.Combine(AppData, "SardaCloud", "PurviewExplorer", "settings.json");

        // Earlier locations, newest first: 0.1.0 (Purview Configuration Viewer) and its first preview. Moved on first load.
        internal static string[] LegacyFilePaths => new[]
        {
            Path.Combine(AppData, "SardaCloud", "PurviewViewer", "settings.json"),
            Path.Combine(AppData, "PurviewViewer", "settings.json"),
        };

        public static AppSettings Load()
        {
            try
            {
                MigrateLegacyFile();
                if (!File.Exists(FilePath)) return new AppSettings();
                return FromJson(Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) as JsonMap);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                Trace.WriteLine("Couldn't read settings: " + ex.Message);
                return new AppSettings();
            }
        }

        internal static AppSettings FromJson(JsonMap m)
        {
            var s = new AppSettings();
            if (m == null) return s;
            if (Enum.TryParse(m["authMode"] as string, true, out AuthMode mode)) s.AuthMode = mode;
            if (Enum.TryParse(m["environment"] as string, true, out CloudEnvironment env)) s.Environment = env;
            s.UserPrincipalName = m["userPrincipalName"] as string;
            s.DisableWam = m["disableWam"] is bool b && b;
            s.AppId = m["appId"] as string;
            s.CertificateThumbprint = m["certificateThumbprint"] as string;
            s.Organization = m["organization"] as string;
            s.DelegatedOrganization = m["delegatedOrganization"] as string;
            if (m["recentOrganizations"] is List<object> recent)
            {
                foreach (var o in recent.OfType<string>().Reverse()) s.RememberOrganization(o);
            }
            else
            {
                s.RememberOrganization(s.Organization); // settings from 0.3.0 or earlier
            }
            if (m["searchOnlyOrganizations"] is JsonMap searchOnly)
            {
                var now = DateTime.UtcNow;
                foreach (var kv in searchOnly)
                {
                    if (kv.Value is string text && !string.IsNullOrWhiteSpace(kv.Key)
                        && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when)
                        && now - when < SearchOnlyMemory)
                    {
                        s.SearchOnlyOrganizations[kv.Key.Trim()] = when;
                    }
                }
            }
            s.LastSnapshotFolder = m["lastSnapshotFolder"] as string;
            s.Maximized = m["maximized"] is bool max && max;
            if (m["bounds"] is List<object> bounds && bounds.Count == 4 && bounds.TrueForAll(o => o is long))
                s.Bounds = bounds.ConvertAll(o => (int)(long)o).ToArray();
            return s;
        }

        /// <summary>Puts an organization first in <see cref="RecentOrganizations"/> (no duplicates, at most <see cref="MaxRecentOrganizations"/>).</summary>
        public void RememberOrganization(string organization)
        {
            if (string.IsNullOrWhiteSpace(organization)) return;
            var value = organization.Trim();
            RecentOrganizations.RemoveAll(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
            RecentOrganizations.Insert(0, value);
            if (RecentOrganizations.Count > MaxRecentOrganizations)
                RecentOrganizations.RemoveRange(MaxRecentOrganizations, RecentOrganizations.Count - MaxRecentOrganizations);
        }

        /// <summary>True when a search-only session was the one that worked for this organization within <see cref="SearchOnlyMemory"/>.</summary>
        public bool PrefersSearchOnly(string organization, DateTime nowUtc) =>
            !string.IsNullOrWhiteSpace(organization)
            && SearchOnlyOrganizations.TryGetValue(organization.Trim(), out var when)
            && nowUtc - when < SearchOnlyMemory;

        /// <summary>Records which kind of session worked for another organization.</summary>
        public void RecordSessionType(string organization, bool searchOnly, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(organization)) return;
            if (searchOnly) SearchOnlyOrganizations[organization.Trim()] = nowUtc;
            else SearchOnlyOrganizations.Remove(organization.Trim());
        }

        private static void MigrateLegacyFile()
        {
            if (File.Exists(FilePath)) return;
            var legacy = LegacyFilePaths.FirstOrDefault(File.Exists);
            if (legacy == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.Move(legacy, FilePath);
            var legacyDir = Path.GetDirectoryName(legacy);
            if (Directory.Exists(legacyDir) && Directory.GetFileSystemEntries(legacyDir).Length == 0) Directory.Delete(legacyDir);
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, Json.Serialize(ToJson(), true), new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine("Couldn't save settings: " + ex.Message);
            }
        }

        internal JsonMap ToJson() => new JsonMap
        {
            ["authMode"] = AuthMode.ToString(),
            ["environment"] = Environment.ToString(),
            ["userPrincipalName"] = UserPrincipalName,
            ["disableWam"] = DisableWam,
            ["appId"] = AppId,
            ["certificateThumbprint"] = CertificateThumbprint,
            ["organization"] = Organization,
            ["delegatedOrganization"] = DelegatedOrganization,
            ["recentOrganizations"] = RecentOrganizations.Cast<object>().ToList(),
            ["searchOnlyOrganizations"] = SearchOnlyJson(),
            ["lastSnapshotFolder"] = LastSnapshotFolder,
            ["maximized"] = Maximized,
            ["bounds"] = Bounds == null ? null : new List<object> { (long)Bounds[0], (long)Bounds[1], (long)Bounds[2], (long)Bounds[3] },
        };

        private JsonMap SearchOnlyJson()
        {
            if (SearchOnlyOrganizations.Count == 0) return null;
            var map = new JsonMap();
            foreach (var kv in SearchOnlyOrganizations.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                map[kv.Key] = Json.FormatDate(DateTime.SpecifyKind(kv.Value, DateTimeKind.Utc));
            return map;
        }
    }
}
