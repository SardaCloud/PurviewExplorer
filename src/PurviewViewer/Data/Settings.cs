// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SardaCloud.PurviewViewer.Data
{
    /// <summary>User preferences, stored as JSON in %APPDATA%\PurviewViewer\settings.json. No secrets are stored.</summary>
    public sealed class AppSettings
    {
        public AuthMode AuthMode { get; set; }
        public string UserPrincipalName { get; set; }
        public bool DisableWam { get; set; }
        public string AppId { get; set; }
        public string CertificateThumbprint { get; set; }
        public string Organization { get; set; }
        public CloudEnvironment Environment { get; set; }
        public string LastSnapshotFolder { get; set; }
        public bool Maximized { get; set; }
        public int[] Bounds { get; set; }

        public static string FilePath =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SardaCloud", "PurviewViewer", "settings.json");

        // Location used before the Sarda Cloud Community branding (0.1.0 previews); moved on first load.
        private static string LegacyFilePath =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "PurviewViewer", "settings.json");

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                MigrateLegacyFile();
                if (!File.Exists(FilePath)) return s;
                if (!(Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) is JsonMap m)) return s;
                if (Enum.TryParse(m["authMode"] as string, true, out AuthMode mode)) s.AuthMode = mode;
                if (Enum.TryParse(m["environment"] as string, true, out CloudEnvironment env)) s.Environment = env;
                s.UserPrincipalName = m["userPrincipalName"] as string;
                s.DisableWam = m["disableWam"] is bool b && b;
                s.AppId = m["appId"] as string;
                s.CertificateThumbprint = m["certificateThumbprint"] as string;
                s.Organization = m["organization"] as string;
                s.LastSnapshotFolder = m["lastSnapshotFolder"] as string;
                s.Maximized = m["maximized"] is bool max && max;
                if (m["bounds"] is System.Collections.Generic.List<object> bounds && bounds.Count == 4 && bounds.TrueForAll(o => o is long))
                    s.Bounds = bounds.ConvertAll(o => (int)(long)o).ToArray();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                Trace.WriteLine("Couldn't read settings: " + ex.Message);
            }
            return s;
        }

        private static void MigrateLegacyFile()
        {
            if (File.Exists(FilePath) || !File.Exists(LegacyFilePath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.Move(LegacyFilePath, FilePath);
            var legacyDir = Path.GetDirectoryName(LegacyFilePath);
            if (Directory.Exists(legacyDir) && Directory.GetFileSystemEntries(legacyDir).Length == 0) Directory.Delete(legacyDir);
        }

        public void Save()
        {
            try
            {
                var m = new JsonMap
                {
                    ["authMode"] = AuthMode.ToString(),
                    ["environment"] = Environment.ToString(),
                    ["userPrincipalName"] = UserPrincipalName,
                    ["disableWam"] = DisableWam,
                    ["appId"] = AppId,
                    ["certificateThumbprint"] = CertificateThumbprint,
                    ["organization"] = Organization,
                    ["lastSnapshotFolder"] = LastSnapshotFolder,
                    ["maximized"] = Maximized,
                    ["bounds"] = Bounds == null ? null : new System.Collections.Generic.List<object> { (long)Bounds[0], (long)Bounds[1], (long)Bounds[2], (long)Bounds[3] },
                };
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, Json.Serialize(m, true), new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Trace.WriteLine("Couldn't save settings: " + ex.Message);
            }
        }
    }
}
