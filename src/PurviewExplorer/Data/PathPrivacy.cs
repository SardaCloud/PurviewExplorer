// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>
    /// Replaces the user's own folders (which contain the user name, and with OneDrive the organization's name)
    /// with placeholders, so reports can be shared, for example in a GitHub issue.
    /// </summary>
    public static class PathPrivacy
    {
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (folder, placeholder) in Folders())
                text = ReplaceIgnoreCase(text, folder, placeholder);
            return text;
        }

        // Longest first, so Documents (often inside the profile or OneDrive) wins over the profile itself.
        internal static IEnumerable<(string Folder, string Placeholder)> Folders()
        {
            var list = new List<(string, string)>
            {
                (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "<Documents>"),
                (Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "<LocalAppData>"),
                (Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "<AppData>"),
                (Environment.GetEnvironmentVariable("OneDrive"), "<OneDrive>"),
                (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<UserProfile>"),
            };
            return list
                .Where(p => !string.IsNullOrEmpty(p.Item1) && p.Item1.Length > 3)
                .Select(p => (p.Item1.TrimEnd('\\'), p.Item2))
                .OrderByDescending(p => p.Item1.Length);
        }

        private static string ReplaceIgnoreCase(string text, string find, string replacement)
        {
            int i = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return text;
            var sb = new System.Text.StringBuilder(text.Length);
            int start = 0;
            while (i >= 0)
            {
                sb.Append(text, start, i - start).Append(replacement);
                start = i + find.Length;
                i = text.IndexOf(find, start, StringComparison.OrdinalIgnoreCase);
            }
            sb.Append(text, start, text.Length - start);
            return sb.ToString();
        }
    }
}
