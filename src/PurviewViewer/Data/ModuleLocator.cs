// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SardaCloud.PurviewViewer.Data
{
    public sealed class ModuleInfo
    {
        public Version Version { get; set; }
        public string ManifestPath { get; set; }
        public override string ToString() => Version + " (" + ManifestPath + ")";
    }

    /// <summary>Finds the ExchangeOnlineManagement module, whether installed for Windows PowerShell or PowerShell 7.</summary>
    public static class ModuleLocator
    {
        public const string ModuleName = "ExchangeOnlineManagement";
        public static readonly Version MinimumVersion = new Version(3, 2, 0);

        public static IEnumerable<string> SearchPaths()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var paths = new List<string>
            {
                Path.Combine(docs, "WindowsPowerShell", "Modules"),
                Path.Combine(docs, "PowerShell", "Modules"),
                Path.Combine(programFiles, "WindowsPowerShell", "Modules"),
                Path.Combine(programFiles, "PowerShell", "Modules"),
            };
            var env = Environment.GetEnvironmentVariable("PSModulePath");
            if (!string.IsNullOrEmpty(env))
                paths.AddRange(env.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            return paths
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static List<ModuleInfo> FindAll()
        {
            var found = new List<ModuleInfo>();
            foreach (var root in SearchPaths())
            {
                try
                {
                    var moduleDir = Path.Combine(root, ModuleName);
                    if (!Directory.Exists(moduleDir)) continue;

                    var direct = Path.Combine(moduleDir, ModuleName + ".psd1");
                    if (File.Exists(direct))
                    {
                        var v = ReadManifestVersion(direct);
                        if (v != null) found.Add(new ModuleInfo { Version = v, ManifestPath = direct });
                    }
                    foreach (var dir in Directory.GetDirectories(moduleDir))
                    {
                        var psd1 = Path.Combine(dir, ModuleName + ".psd1");
                        if (!File.Exists(psd1)) continue;
                        if (!Version.TryParse(Path.GetFileName(dir), out var v)) v = ReadManifestVersion(psd1);
                        if (v != null) found.Add(new ModuleInfo { Version = v, ManifestPath = psd1 });
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return found
                .GroupBy(m => m.ManifestPath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(m => m.Version)
                .ToList();
        }

        public static ModuleInfo FindBest() => FindAll().FirstOrDefault();

        private static Version ReadManifestVersion(string psd1)
        {
            try
            {
                var m = Regex.Match(File.ReadAllText(psd1), @"ModuleVersion\s*=\s*['""]([\d\.]+)['""]");
                return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        public const string InstallCommand = "Install-Module ExchangeOnlineManagement -Scope CurrentUser";
    }
}
