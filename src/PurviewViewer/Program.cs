// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Reflection;
using System.Windows.Forms;
using SardaCloud.PurviewViewer.Data;
using SardaCloud.PurviewViewer.UI;

namespace SardaCloud.PurviewViewer
{
    internal static class AppInfo
    {
        public const string ProductName = "Purview Configuration Viewer";
        public const string Brand = "Sarda Cloud Community";
        public const string Author = "Francesco Valerio Buccoli";
        public const string ProjectUrl = "https://github.com/SardaCloud/PurviewViewer";
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

        public static string ProductAndVersion => ProductName + " " + Version;
    }

    internal static class Program
    {
        public static AppSettings Settings { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ReportUnhandled(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportUnhandled(e.ExceptionObject as Exception);

            Settings = AppSettings.Load();
            var startup = StartupOptions.Parse(args);
            Application.Run(new MainForm(Settings, startup));

            // The hosted runspace uses a foreground thread; if anything is still open (e.g. a sign-in that never
            // completed), it must not keep a windowless process alive.
            Environment.Exit(Environment.ExitCode);
        }

        private static void ReportUnhandled(Exception ex)
        {
            MessageBox.Show(
                "An unexpected error occurred:\r\n\r\n" + (ex?.ToString() ?? "(unknown error)"),
                AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
