// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell;

namespace SardaCloud.PurviewExplorer.Data
{
    public enum AuthMode
    {
        Interactive,
        Certificate,
    }

    public enum CloudEnvironment
    {
        Commercial,
        GccHigh,
        DoD,
        China,
    }

    public sealed class ConnectOptions
    {
        public AuthMode Mode { get; set; }
        public string UserPrincipalName { get; set; }
        public bool DisableWam { get; set; }
        public string AppId { get; set; }
        public string CertificateThumbprint { get; set; }

        /// <summary>The organization to connect to with a certificate (app-only).</summary>
        public string Organization { get; set; }

        /// <summary>
        /// With an interactive sign-in, another organization to connect to: a customer organization for a partner
        /// (CSP or GDAP) or an organization where the account is a guest. Empty for the account's own organization.
        /// </summary>
        public string DelegatedOrganization { get; set; }

        public CloudEnvironment Environment { get; set; }

        /// <summary>
        /// Start with a search-only session (Connect-IPPSSession -EnableSearchOnlySession) instead of a standard one, because
        /// only a search-only session worked for this other organization recently. Used only with <see cref="IsDelegated"/>.
        /// </summary>
        public bool PreferSearchOnlySession { get; set; }

        public bool IsDelegated => Mode == AuthMode.Interactive && !string.IsNullOrWhiteSpace(DelegatedOrganization);

        /// <summary>The organization's domain when the options name one (another organization, or a certificate's organization); otherwise null.</summary>
        public string TargetOrganization =>
            IsDelegated ? DelegatedOrganization.Trim()
            : Mode == AuthMode.Certificate && !string.IsNullOrWhiteSpace(Organization) ? Organization.Trim()
            : null;

        public static string EnvironmentName(CloudEnvironment e)
        {
            switch (e)
            {
                case CloudEnvironment.GccHigh: return "Microsoft 365 GCC High";
                case CloudEnvironment.DoD: return "Microsoft 365 DoD";
                case CloudEnvironment.China: return "Office 365 operated by 21Vianet";
                default: return "Microsoft 365 (Commercial / GCC)";
            }
        }

        /// <summary>Connection and authorization endpoints, per the Connect-IPPSSession documentation.</summary>
        public static void Endpoints(CloudEnvironment e, out string connectionUri, out string authorizationUri)
        {
            switch (e)
            {
                case CloudEnvironment.GccHigh:
                    connectionUri = "https://ps.compliance.protection.office365.us/powershell-liveid/";
                    break;
                case CloudEnvironment.DoD:
                    connectionUri = "https://l5.ps.compliance.protection.office365.us/powershell-liveid/";
                    break;
                case CloudEnvironment.China:
                    connectionUri = "https://ps.compliance.protection.partner.outlook.cn/powershell-liveid";
                    break;
                default:
                    connectionUri = null;
                    break;
            }
            authorizationUri = connectionUri == null ? null : SignInHost(e) + "/organizations";
        }

        /// <summary>The Microsoft Entra sign-in address of an environment, without the tenant.</summary>
        public static string SignInHost(CloudEnvironment e)
        {
            switch (e)
            {
                case CloudEnvironment.GccHigh:
                case CloudEnvironment.DoD:
                    return "https://login.microsoftonline.us";
                case CloudEnvironment.China:
                    return "https://login.chinacloudapi.cn";
                default:
                    return "https://login.microsoftonline.com";
            }
        }

        /// <summary>True for a domain name such as contoso.onmicrosoft.com (what Organization and DelegatedOrganization expect).</summary>
        public static bool IsOrganizationName(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(value.Trim(), @"\A(?=.{4,253}\z)(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}\z");

        /// <summary>
        /// Checks what the Connect dialog collected. Returns null when the options can be used; otherwise a message,
        /// with <paramref name="field"/> set to the name of the property to correct.
        /// </summary>
        public string Validate(out string field)
        {
            field = null;
            if (Mode == AuthMode.Interactive)
            {
                if (!string.IsNullOrWhiteSpace(UserPrincipalName) && !UserPrincipalName.Contains("@"))
                {
                    field = nameof(UserPrincipalName);
                    return "Enter a user principal name such as admin@contoso.onmicrosoft.com, or leave it empty.";
                }
                if (!string.IsNullOrWhiteSpace(DelegatedOrganization) && !IsOrganizationName(DelegatedOrganization))
                {
                    field = nameof(DelegatedOrganization);
                    return "Enter the other organization's .onmicrosoft.com domain, for example adatum.onmicrosoft.com, " +
                           "or leave it empty to connect to your own organization.";
                }
                return null;
            }
            if (!Guid.TryParse(AppId, out _))
            {
                field = nameof(AppId);
                return "Enter the application (client) ID of the app registration, a GUID.";
            }
            if (CertificateThumbprint == null || !Regex.IsMatch(CertificateThumbprint, "^[0-9A-F]{40}$"))
            {
                field = nameof(CertificateThumbprint);
                return "Enter the 40-character hexadecimal thumbprint of a certificate in your personal (CurrentUser\\My) or computer (LocalMachine\\My) store.";
            }
            if (!IsOrganizationName(Organization))
            {
                field = nameof(Organization);
                return "Enter the organization's initial domain, for example contoso.onmicrosoft.com.";
            }
            return null;
        }

        /// <summary>
        /// The Connect-IPPSSession parameters for these options, in order. <paramref name="supported"/> tells whether
        /// the installed module has an optional parameter (ShowBanner, CommandName, DisableWAM, EnableSearchOnlySession).
        /// <paramref name="searchOnly"/> asks for a search-only session, which some guest accounts need.
        /// </summary>
        internal static List<KeyValuePair<string, object>> ConnectParameters(ConnectOptions o, string[] commandNames, bool disableWam, Func<string, bool> supported, bool searchOnly = false)
        {
            var list = new List<KeyValuePair<string, object>>();
            void Add(string name, object value) => list.Add(new KeyValuePair<string, object>(name, value));

            if (supported("ShowBanner")) Add("ShowBanner", false);
            if (commandNames != null && commandNames.Length > 0 && supported("CommandName")) Add("CommandName", commandNames);
            Endpoints(o.Environment, out var connectionUri, out var authorizationUri);
            if (connectionUri != null) Add("ConnectionUri", connectionUri);
            if (o.IsDelegated)
            {
                // Partner and guest access: sign in at the other organization and map the session to it.
                var organization = o.DelegatedOrganization.Trim();
                Add("AzureADAuthorizationEndpointUri", SignInHost(o.Environment) + "/" + organization);
                Add("DelegatedOrganization", organization);
            }
            else if (authorizationUri != null)
            {
                Add("AzureADAuthorizationEndpointUri", authorizationUri);
            }
            if (o.Mode == AuthMode.Certificate)
            {
                Add("AppId", o.AppId);
                Add("CertificateThumbprint", o.CertificateThumbprint);
                Add("Organization", o.Organization);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(o.UserPrincipalName)) Add("UserPrincipalName", o.UserPrincipalName.Trim());
                if (disableWam && supported("DisableWAM")) Add("DisableWAM", true);
                if (searchOnly && o.IsDelegated && supported("EnableSearchOnlySession")) Add("EnableSearchOnlySession", true);
            }
            Add("ErrorAction", "Stop");
            return list;
        }

        /// <summary>
        /// Compares the tenant the organization's domain belongs to with the tenant of the session. Returns null when
        /// they match or either is unknown; otherwise the message to show.
        /// </summary>
        internal static string CheckTenant(string expectedTenantId, string sessionTenantId, string organization)
        {
            if (string.IsNullOrWhiteSpace(expectedTenantId) || string.IsNullOrWhiteSpace(sessionTenantId)) return null;
            if (string.Equals(expectedTenantId.Trim(), sessionTenantId.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
            return $"The session belongs to tenant {sessionTenantId.Trim()}, but {organization} is tenant {expectedTenantId.Trim()}. " +
                   "Purview Explorer disconnected without reading anything, so nothing from the wrong organization is shown or saved.\r\n\r\n" +
                   "Check the account and the organization, and connect again.";
        }

        /// <summary>
        /// The organization, account and tenant ID of a connection, from Get-ConnectionInformation (may be null)
        /// with the options as fallback. With partner or guest access the organization is the other organization.
        /// </summary>
        internal static (string Organization, string Account, string TenantId) ResolveIdentity(JsonMap info, ConnectOptions options)
        {
            string Value(string name) => info == null ? null : NullIfEmpty(ValueFormatter.ToFlatText(info[name]));
            string organization = Value("DelegatedOrganization") ?? Value("Organization");
            string account = Value("UserPrincipalName");
            string tenantId = Value("TenantID");
            if (options.Mode == AuthMode.Certificate)
            {
                organization = organization ?? NullIfEmpty(options.Organization);
                account = account ?? "App " + options.AppId;
            }
            else
            {
                if (options.IsDelegated) organization = organization ?? options.DelegatedOrganization.Trim();
                account = account ?? NullIfEmpty(options.UserPrincipalName);
            }
            if (organization == null && account != null && account.Contains("@"))
                organization = account.Substring(account.IndexOf('@') + 1);
            return (organization, account, tenantId);
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    public sealed class InvokeResult
    {
        public List<JsonMap> Items { get; } = new List<JsonMap>();
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public string TerminatingError { get; set; }
        public int RawCount { get; set; }
        public bool Stopped { get; set; }
        public bool Succeeded => TerminatingError == null && !Stopped;
    }

    public sealed class PurviewException : Exception
    {
        public PurviewException(string message) : base(message) { }
    }

    /// <summary>The operations the workspace needs from a connected session (a fake implements it in tests).</summary>
    public interface IPurviewSession : IDisposable
    {
        ModuleInfo Module { get; }
        bool IsConnected { get; }

        /// <summary>True/false when known; null when the probe was stopped or failed (not cached).</summary>
        Task<bool?> CommandExistsAsync(string name);

        Task<InvokeResult> InvokeAsync(string cmdlet, IEnumerable<KeyValuePair<string, object>> parameters);
        void Stop();
        Task DisconnectAsync();
    }

    /// <summary>
    /// A Security &amp; Compliance PowerShell session hosted in-process (Windows PowerShell 5.1 runspace
    /// with the ExchangeOnlineManagement module). Calls are serialized; the runspace runs on its own STA thread
    /// so the sign-in UI of the module works.
    /// </summary>
    public sealed class PurviewSession : IPurviewSession
    {
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, bool> _commandCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private Runspace _runspace;
        private volatile PowerShell _current;
        private HashSet<string> _connectParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private PurviewSession() { }

        public ModuleInfo Module { get; private set; }
        public string Organization { get; private set; }
        public string Account { get; private set; }
        public string TenantId { get; private set; }
        public CloudEnvironment Environment { get; private set; }
        public bool IsConnected { get; private set; }

        /// <summary>
        /// The session was made with Connect-IPPSSession -EnableSearchOnlySession, because a standard session failed for
        /// the account (or did recently). Some cmdlets may not be offered in such a session.
        /// </summary>
        public bool IsSearchOnlySession { get; private set; }

        public static async Task<PurviewSession> ConnectAsync(ConnectOptions options, IEnumerable<string> commandNames, IProgress<string> progress)
        {
            var session = new PurviewSession();
            try
            {
                await session._gate.WaitAsync().ConfigureAwait(false);
                try
                {
                    var names = commandNames.ToArray();
                    await Task.Run(() => session.Connect(options, names, progress)).ConfigureAwait(false);
                }
                finally
                {
                    session._gate.Release();
                }
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        private void Connect(ConnectOptions options, string[] commandNames, IProgress<string> progress)
        {
            Environment = options.Environment;
            Module = ModuleLocator.FindBest();
            if (Module == null)
                throw new PurviewException(
                    "The ExchangeOnlineManagement PowerShell module isn't installed.\r\n\r\n" +
                    "Install it from an elevated or regular PowerShell prompt:\r\n    " + ModuleLocator.InstallCommand);
            if (Module.Version < ModuleLocator.MinimumVersion)
                throw new PurviewException(
                    $"ExchangeOnlineManagement {Module.Version} is too old; version {ModuleLocator.MinimumVersion} or later is required.\r\n\r\n" +
                    "Update it with:\r\n    Update-Module ExchangeOnlineManagement");

            // Before signing in, make sure the organization exists: a typo then fails at once, with a clear message,
            // and the tenant ID found here is compared with the session's afterwards.
            string expectedTenantId = null;
            var organization = options.TargetOrganization;
            if (organization != null)
            {
                progress?.Report($"Looking up {organization}...");
                var lookup = TenantLookup.Resolve(options.Environment, organization);
                if (lookup.Status == TenantLookupStatus.NotFound)
                    throw new PurviewException(
                        $"No organization has the domain {organization} in {ConnectOptions.EnvironmentName(options.Environment)}. " +
                        "Check the spelling, and enter the organization's primary .onmicrosoft.com domain (for example adatum.onmicrosoft.com).");
                if (lookup.Status == TenantLookupStatus.Found) expectedTenantId = lookup.TenantId;
                else System.Diagnostics.Trace.WriteLine($"Couldn't look up {organization}: {lookup.Message}");
            }

            progress?.Report("Starting Windows PowerShell...");
            OpenRunspace();

            progress?.Report($"Loading ExchangeOnlineManagement {Module.Version}...");
            var importError = ImportModule();
            if (importError != null)
                throw new PurviewException("Couldn't load the ExchangeOnlineManagement module:\r\n" + importError);

            progress?.Report("Signing in to Security & Compliance PowerShell...");
            bool disableWam = options.DisableWam;
            bool canSearchOnly = options.IsDelegated && Supports("EnableSearchOnlySession");

            InvokeResult Attempt(bool searchOnly)
            {
                var r = RunConnect(options, commandNames, disableWam, searchOnly);
                if (!r.Succeeded && !r.Stopped && options.Mode == AuthMode.Interactive && !disableWam && LooksLikeWamFailure(r.TerminatingError))
                {
                    progress?.Report("Web Account Manager isn't available here; signing in with the browser instead...");
                    disableWam = true;
                    r = RunConnect(options, commandNames, disableWam, searchOnly);
                }
                return r;
            }

            bool usedSearchOnly = canSearchOnly && options.PreferSearchOnlySession;
            var result = Attempt(usedSearchOnly);
            string searchOnlyError = null;
            var failure = result.Succeeded || result.Stopped ? ConnectFailure.Other : ConnectionErrors.Classify(result.TerminatingError);
            if (!result.Succeeded && !result.Stopped && failure != ConnectFailure.Cancelled)
            {
                if (usedSearchOnly)
                {
                    // Only a search-only session worked for this organization recently, but not this time: try a standard one.
                    searchOnlyError = result.TerminatingError ?? "The connection was cancelled.";
                    progress?.Report("The search-only session failed; trying a standard session...");
                    usedSearchOnly = false;
                    result = Attempt(false);
                }
                else if (canSearchOnly && failure == ConnectFailure.ServiceSetupFailed)
                {
                    // Some guest accounts get this from the service with a standard session but not with a search-only one.
                    progress?.Report("The standard session failed for this account; trying a search-only session...");
                    var retry = Attempt(true);
                    if (retry.Succeeded)
                    {
                        result = retry;
                        usedSearchOnly = true;
                    }
                    else
                    {
                        searchOnlyError = retry.TerminatingError ?? "The connection was cancelled.";
                    }
                }
            }
            if (!result.Succeeded)
                throw new PurviewException(ConnectionErrors.Describe(result.TerminatingError, options, searchOnlyError,
                    searchOnlySupported: !options.IsDelegated || Supports("EnableSearchOnlySession")));

            IsConnected = true;
            IsSearchOnlySession = usedSearchOnly;
            if (!usedSearchOnly) progress?.Report("Reading connection information...");
            ReadConnectionInformation(options, expectedTenantId, usedSearchOnly);

            var mismatch = ConnectOptions.CheckTenant(expectedTenantId, TenantId, organization);
            if (mismatch != null)
            {
                Run(ps => ps.AddCommand("Disconnect-ExchangeOnline").AddParameter("Confirm", false).AddParameter("ErrorAction", "SilentlyContinue"), convert: false);
                IsConnected = false;
                throw new PurviewException(mismatch);
            }
        }

        private bool Supports(string parameter) => _connectParameters.Count == 0 || _connectParameters.Contains(parameter);

        private void OpenRunspace()
        {
            UseWindowsPowerShellModulePath();
            var iss = InitialSessionState.CreateDefault();
            iss.ExecutionPolicy = ExecutionPolicy.RemoteSigned;
            _runspace = RunspaceFactory.CreateRunspace(iss);
            _runspace.ThreadOptions = PSThreadOptions.ReuseThread;
            _runspace.ApartmentState = ApartmentState.STA;
            _runspace.Open();
        }

        /// <summary>
        /// When the app is started from PowerShell 7, it inherits a PSModulePath that lists PowerShell 7 module folders
        /// first; Windows PowerShell would then try to load PowerShell 7's PackageManagement (a dependency of
        /// ExchangeOnlineManagement) and fail. Keep only Windows PowerShell module folders for this process.
        /// </summary>
        internal static string UseWindowsPowerShellModulePath()
        {
            var paths = new List<string>
            {
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "WindowsPowerShell", "Modules"),
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules"),
                System.IO.Path.Combine(System.Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "Modules"),
            };
            var inherited = System.Environment.GetEnvironmentVariable("PSModulePath") ?? "";
            foreach (var p in inherited.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
            {
                if (p.Length > 0 && !IsPowerShell7Path(p) && !paths.Contains(p.TrimEnd('\\'), StringComparer.OrdinalIgnoreCase))
                    paths.Add(p.TrimEnd('\\'));
            }
            var value = string.Join(";", paths);
            System.Environment.SetEnvironmentVariable("PSModulePath", value);
            return value;
        }

        internal static bool IsPowerShell7Path(string path)
        {
            var p = path.Trim().TrimEnd('\\') + "\\";
            var programFilesPs = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "PowerShell") + "\\";
            var documentsPs = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "PowerShell") + "\\";
            return p.StartsWith(programFilesPs, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(documentsPs, StringComparison.OrdinalIgnoreCase)
                || p.EndsWith(@"\Documents\PowerShell\Modules\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Imports the module and reads the Connect-IPPSSession parameters. Returns an error message, or null.</summary>
        private string ImportModule()
        {
            var import = Run(ps => ps.AddCommand("Import-Module")
                .AddParameter("Name", Module.ManifestPath)
                .AddParameter("DisableNameChecking")
                .AddParameter("ErrorAction", "Stop"), convert: false);
            if (!import.Succeeded) return import.TerminatingError ?? "Import-Module was stopped.";

            var parameters = Run(ps => ps.AddScript("(Get-Command -Name Connect-IPPSSession -ErrorAction Stop).Parameters.Keys"), convert: false);
            if (!parameters.Succeeded) return parameters.TerminatingError;
            _connectParameters = new HashSet<string>(parameters.Items.Select(i => i["Value"] as string).Where(s => s != null), StringComparer.OrdinalIgnoreCase);
            return null;
        }

        /// <summary>
        /// Verifies that the module can be found and loaded in the hosted runspace, without signing in.
        /// Returns a human-readable report, with the user's own folders replaced by placeholders so it can be
        /// shared; <paramref name="ok"/> tells whether connecting should work.
        /// </summary>
        public static string CheckPrerequisites(out bool ok) => PathPrivacy.Redact(BuildPrerequisiteReport(out ok));

        private static string BuildPrerequisiteReport(out bool ok)
        {
            ok = false;
            var report = new System.Text.StringBuilder();
            report.AppendLine($"{AppInfo.ProductAndVersion}, {AppInfo.Architecture} process on {AppInfo.OSArchitecture} Windows");
            report.AppendLine($"Windows: {WindowsDescription()}");
            report.AppendLine($".NET Framework: {DotNetFrameworkDescription()} (CLR {System.Environment.Version})");
            report.AppendLine($"Windows PowerShell engine: System.Management.Automation {System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(PSObject).Assembly.Location).ProductVersion}");
            report.AppendLine();

            var modules = ModuleLocator.FindAll();
            if (modules.Count == 0)
            {
                report.AppendLine("ExchangeOnlineManagement: NOT FOUND in:");
                foreach (var p in ModuleLocator.SearchPaths()) report.AppendLine("    " + p);
                report.AppendLine();
                report.AppendLine("Install it with:  " + ModuleLocator.InstallCommand);
                return report.ToString();
            }
            foreach (var m in modules) report.AppendLine($"ExchangeOnlineManagement {m.Version}: {m.ManifestPath}");
            report.AppendLine();

            var session = new PurviewSession { Module = modules[0] };
            try
            {
                if (session.Module.Version < ModuleLocator.MinimumVersion)
                {
                    report.AppendLine($"Version {session.Module.Version} is too old; {ModuleLocator.MinimumVersion} or later is required (Update-Module ExchangeOnlineManagement).");
                    return report.ToString();
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                session.OpenRunspace();
                var policy = session.Run(ps => ps.AddCommand("Get-ExecutionPolicy"), convert: false);
                report.AppendLine("Runspace: opened (STA), execution policy " + (policy.Items.FirstOrDefault()?["Value"] ?? "unknown"));
                var error = session.ImportModule();
                if (error != null)
                {
                    report.AppendLine("Import-Module: FAILED");
                    report.AppendLine(error);
                    return report.ToString();
                }
                report.AppendLine($"Import-Module: OK ({sw.Elapsed.TotalSeconds:0.0} s)");
                report.AppendLine("Connect-IPPSSession parameters: " + string.Join(", ", session._connectParameters.Where(p =>
                    p == "UserPrincipalName" || p == "DisableWAM" || p == "AccessToken" || p == "CertificateThumbprint" || p == "CommandName" ||
                    p == "DelegatedOrganization" || p == "EnableSearchOnlySession").OrderBy(p => p)));
                ok = true;
                report.AppendLine();
                report.AppendLine("Everything needed to connect is in place.");
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                report.AppendLine("FAILED: " + PsConverter.FormatException(ex));
            }
            finally
            {
                session.Dispose();
            }
            return report.ToString();
        }

        private static string WindowsDescription()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var name = key?.GetValue("ProductName") as string ?? "Windows";
                    var display = key?.GetValue("DisplayVersion") as string;
                    var build = key?.GetValue("CurrentBuildNumber") as string;
                    var ubr = key?.GetValue("UBR");
                    // ProductName still says "Windows 10" on Windows 11; the build number tells them apart.
                    if (int.TryParse(build, out var b) && b >= 22000 && name.StartsWith("Windows 10", StringComparison.Ordinal))
                        name = "Windows 11" + name.Substring("Windows 10".Length);
                    return $"{name}{(display != null ? " " + display : "")}, build {build}{(ubr != null ? "." + ubr : "")}";
                }
            }
            catch (System.Security.SecurityException)
            {
                return System.Environment.OSVersion.VersionString;
            }
        }

        private static string DotNetFrameworkDescription()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                {
                    var version = key?.GetValue("Version") as string;
                    var release = key?.GetValue("Release");
                    return version != null ? $"{version} (release {release})" : "unknown";
                }
            }
            catch (System.Security.SecurityException)
            {
                return "unknown";
            }
        }

        private InvokeResult RunConnect(ConnectOptions o, string[] commandNames, bool disableWam, bool searchOnly)
        {
            var parameters = ConnectOptions.ConnectParameters(o, commandNames, disableWam, Supports, searchOnly);
            return Run(ps =>
            {
                ps.AddCommand("Connect-IPPSSession");
                foreach (var p in parameters) ps.AddParameter(p.Key, p.Value);
            }, convert: false);
        }

        private static bool LooksLikeWamFailure(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            return message.IndexOf("window handle", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("WAM", StringComparison.Ordinal) >= 0
                || message.IndexOf("broker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ReadConnectionInformation(ConnectOptions options, string lookedUpTenantId, bool searchOnly)
        {
            // In a search-only session Get-ConnectionInformation takes minutes and reports the session as Broken while
            // its cmdlets work, so the identity comes from the options and the tenant lookup instead.
            JsonMap current = null;
            if (!searchOnly)
            {
                var info = Run(ps => ps.AddCommand("Get-ConnectionInformation").AddParameter("ErrorAction", "SilentlyContinue"), convert: true);
                current = info.Items.LastOrDefault(i => string.Equals(ValueFormatter.ToFlatText(i["State"]), "Connected", StringComparison.OrdinalIgnoreCase))
                          ?? info.Items.LastOrDefault();
            }
            var identity = ConnectOptions.ResolveIdentity(current, options);
            Organization = identity.Organization;
            Account = identity.Account;
            TenantId = identity.TenantId ?? lookedUpTenantId;
        }

        public async Task<bool?> CommandExistsAsync(string name)
        {
            lock (_commandCache)
            {
                if (_commandCache.TryGetValue(name, out var known)) return known;
            }
            var r = await RunGatedAsync(ps => ps.AddCommand("Get-Command").AddParameter("Name", name).AddParameter("ErrorAction", "SilentlyContinue"), convert: false)
                .ConfigureAwait(false);
            if (r.Stopped || r.TerminatingError != null) return null; // undetermined: don't cache
            bool exists = r.RawCount > 0;
            lock (_commandCache) _commandCache[name] = exists;
            return exists;
        }

        public Task<InvokeResult> InvokeAsync(string cmdlet, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var args = parameters.ToList();
            if (!cmdlet.StartsWith("Get-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only read-only Get-* cmdlets are allowed.");
            return RunGatedAsync(ps =>
            {
                ps.AddCommand(cmdlet);
                foreach (var p in args)
                {
                    if (p.Value is bool b && b) ps.AddParameter(p.Key);
                    else ps.AddParameter(p.Key, p.Value);
                }
                ps.AddParameter("WarningAction", "SilentlyContinue");
            }, convert: true);
        }

        /// <summary>Stops the command currently running, if any.</summary>
        public void Stop()
        {
            var ps = _current;
            if (ps == null) return;
            try { ps.BeginStop(null, null); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public async Task DisconnectAsync()
        {
            if (_runspace == null) return;
            Stop();
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsConnected && _runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                {
                    await Task.Run(() => Run(ps => ps.AddCommand("Disconnect-ExchangeOnline")
                        .AddParameter("Confirm", false)
                        .AddParameter("ErrorAction", "SilentlyContinue"), convert: false)).ConfigureAwait(false);
                }
                IsConnected = false;
            }
            finally
            {
                _gate.Release();
            }
            Dispose();
        }

        private async Task<InvokeResult> RunGatedAsync(Action<PowerShell> build, bool convert)
        {
            if (_runspace == null || !IsConnected) throw new InvalidOperationException("Not connected.");
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Task.Run(() => Run(build, convert)).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        private InvokeResult Run(Action<PowerShell> build, bool convert)
        {
            var result = new InvokeResult();
            using (var ps = PowerShell.Create())
            {
                ps.Runspace = _runspace;
                build(ps);
                _current = ps;
                try
                {
                    var output = ps.Invoke();
                    result.RawCount = output.Count;
                    foreach (var o in output)
                    {
                        if (o == null) continue;
                        result.Items.Add(convert ? PsConverter.ToItem(o) : new JsonMap { ["Value"] = o.BaseObject is string s ? s : o.ToString() });
                    }
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    result.TerminatingError = PsConverter.FormatException(ex);
                }
                finally
                {
                    _current = null;
                }
                result.Stopped = ps.InvocationStateInfo.State == PSInvocationState.Stopped;
                foreach (var e in ps.Streams.Error) result.Errors.Add(PsConverter.FormatError(e));
                foreach (var w in ps.Streams.Warning) result.Warnings.Add(w.Message);
            }
            return result;
        }

        public void Dispose()
        {
            IsConnected = false;
            var rs = _runspace;
            _runspace = null;
            if (rs == null) return;
            try { rs.Dispose(); }
            catch (Exception) { }
        }
    }
}
