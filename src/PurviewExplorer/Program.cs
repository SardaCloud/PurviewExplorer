// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Data;
using SardaCloud.PurviewExplorer.UI;

namespace SardaCloud.PurviewExplorer
{
    internal static class AppInfo
    {
        public const string ProductName = "Purview Explorer";
        public const string Brand = "Sarda Cloud Community";
        public const string Author = "Francesco Valerio Buccoli";
        public const string ProjectUrl = "https://github.com/SardaCloud/PurviewExplorer";
        public const string IssuesUrl = ProjectUrl + "/issues";
        public const string LicenseUrl = ProjectUrl + "/blob/main/LICENSE";

        public static string Version
        {
            get
            {
                var v = typeof(AppInfo).Assembly.GetName().Version;
                return $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        public static string Copyright =>
            typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "Copyright (C) 2026 " + Author;

        /// <summary>Architecture of this process: x64 or arm64 for the released builds.</summary>
        public static string Architecture => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        public static string OSArchitecture => System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        public static string ProductAndVersion => ProductName + " " + Version;
    }

    internal static class Program
    {
        public static AppSettings Settings { get; private set; }

        /// <summary>Lets Setup detect a running copy (Inno Setup AppMutex). Session-local and global names.</summary>
        public const string MutexName = "SardaCloud.PurviewExplorer";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ReportUnhandled(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportUnhandled(e.ExceptionObject as Exception);

            var startup = StartupOptions.Parse(args);
            if (startup.CheckPrerequisites)
            {
                Environment.Exit(RunPrerequisiteCheck(startup.CheckPrerequisitesReport));
                return;
            }

            var mutexes = CreateMutexes();
            // Screenshots and the self-test must never show (or change) the user's saved account and settings.
            Settings = startup.ScreenshotPath != null || startup.SelfTestLog != null ? new AppSettings() : AppSettings.Load();
            Application.Run(new MainForm(Settings, startup));
            GC.KeepAlive(mutexes);

            // The hosted runspace uses a foreground thread; if anything is still open (e.g. a sign-in that never
            // completed), it must not keep a windowless process alive.
            Environment.Exit(Environment.ExitCode);
        }

        /// <summary>
        /// "/CheckPrerequisites[:report.txt]": checks that the module loads in the hosted runspace, without signing in.
        /// With a file name the report is written there (Setup uses this) and nothing is shown; exit code 0 = ready.
        /// </summary>
        private static int RunPrerequisiteCheck(string reportPath)
        {
            string report;
            bool ok;
            try
            {
                report = PurviewSession.CheckPrerequisites(out ok);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                ok = false;
                report = "The check failed: " + ex.Message;
            }
            if (reportPath != null)
            {
                try
                {
                    File.WriteAllText(reportPath, report, new UTF8Encoding(true));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    return 2;
                }
            }
            else
            {
                ValueDialog.ShowValue(null, ok ? "Prerequisites: OK" : "Prerequisites: action needed", report);
            }
            return ok ? 0 : 1;
        }

        private static Mutex[] CreateMutexes()
        {
            var list = new List<Mutex>();
            foreach (var name in new[] { MutexName, @"Global\" + MutexName })
            {
                try
                {
                    list.Add(new Mutex(false, name));
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is WaitHandleCannotBeOpenedException)
                {
                    // Another session's copy owns the global name with different permissions; the local one is enough.
                }
            }
            return list.ToArray();
        }

        private static void ReportUnhandled(Exception ex)
        {
            MessageBox.Show(
                "An unexpected error occurred:\r\n\r\n" + (ex?.ToString() ?? "(unknown error)"),
                AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
