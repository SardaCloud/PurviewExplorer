// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Catalog;
using SardaCloud.PurviewExplorer.Data;

namespace SardaCloud.PurviewExplorer.UI
{
    /// <summary>Collects the Connect-IPPSSession options.</summary>
    internal sealed class ConnectDialog : Form
    {
        private readonly AppSettings _settings;
        private readonly ComboBox _environment;
        private readonly RadioButton _interactive;
        private readonly RadioButton _certificate;
        private readonly TextBox _upn;
        private readonly CheckBox _disableWam;
        private readonly TextBox _appId;
        private readonly TextBox _thumbprint;
        private readonly TextBox _organization;
        private readonly Control[] _interactiveControls;
        private readonly Control[] _certificateControls;

        public ConnectDialog(AppSettings settings)
        {
            _settings = settings;
            Text = "Connect to Tenant";
            Font = Ui.DialogFont;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            ClientSize = Ui.S(470, 500);

            var header = Ui.CreateHeader(Icons.Get(StockIcon.MyNetwork, false), "Connect to Tenant",
                "Sign in to Security & Compliance PowerShell (read-only access).");
            Controls.Add(header);

            int x = Ui.S(12), w = ClientSize.Width - Ui.S(24);
            int y = header.Bottom + Ui.S(10);

            // Environment
            var envGroup = new GroupBox { Text = "Environment", Location = new Point(x, y), Size = new Size(w, Ui.S(56)) };
            _environment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(Ui.S(12), Ui.S(22)), Width = w - Ui.S(24) };
            foreach (CloudEnvironment e in Enum.GetValues(typeof(CloudEnvironment)))
                _environment.Items.Add(new EnvironmentItem(e));
            envGroup.Controls.Add(_environment);
            Controls.Add(envGroup);
            y = envGroup.Bottom + Ui.S(8);

            // Authentication
            var authGroup = new GroupBox { Text = "Authentication", Location = new Point(x, y), Size = new Size(w, Ui.S(262)) };
            int gx = Ui.S(12), indent = Ui.S(30), gy = Ui.S(22);
            int labelWidth = Ui.S(150), fieldX = indent + labelWidth, fieldW = w - fieldX - Ui.S(12);

            _interactive = new RadioButton { Text = "&Interactive sign-in (delegated)", AutoSize = true, Location = new Point(gx, gy) };
            gy += Ui.S(26);
            var upnLabel = new Label { Text = "&User principal name:", AutoSize = true, Location = new Point(indent, gy + Ui.S(3)) };
            _upn = new TextBox { Location = new Point(fieldX, gy), Width = fieldW };
            gy += Ui.S(26);
            var upnHint = new Label { Text = "Optional, for example admin@contoso.com", AutoSize = false, AutoEllipsis = true, Size = new Size(fieldW, Ui.S(18)), ForeColor = SystemColors.GrayText, Location = new Point(fieldX, gy) };
            gy += Ui.S(22);
            _disableWam = new CheckBox { Text = "Sign in with a &browser window instead of Web Account Manager", AutoSize = true, Location = new Point(indent, gy) };
            gy += Ui.S(32);

            _certificate = new RadioButton { Text = "&Certificate (app-only, unattended)", AutoSize = true, Location = new Point(gx, gy) };
            gy += Ui.S(26);
            var appIdLabel = new Label { Text = "&Application (client) ID:", AutoSize = true, Location = new Point(indent, gy + Ui.S(3)) };
            _appId = new TextBox { Location = new Point(fieldX, gy), Width = fieldW };
            gy += Ui.S(28);
            var thumbLabel = new Label { Text = "Certificate &thumbprint:", AutoSize = true, Location = new Point(indent, gy + Ui.S(3)) };
            _thumbprint = new TextBox { Location = new Point(fieldX, gy), Width = fieldW };
            gy += Ui.S(28);
            var orgLabel = new Label { Text = "&Organization:", AutoSize = true, Location = new Point(indent, gy + Ui.S(3)) };
            _organization = new TextBox { Location = new Point(fieldX, gy), Width = fieldW };

            authGroup.Controls.AddRange(new Control[]
            {
                _interactive, upnLabel, _upn, upnHint, _disableWam,
                _certificate, appIdLabel, _appId, thumbLabel, _thumbprint, orgLabel, _organization,
            });
            Controls.Add(authGroup);
            _interactiveControls = new Control[] { upnLabel, _upn, upnHint, _disableWam };
            _certificateControls = new Control[] { appIdLabel, _appId, thumbLabel, _thumbprint, orgLabel, _organization };
            _interactive.CheckedChanged += (s, e) => UpdateEnabled();
            _certificate.CheckedChanged += (s, e) => UpdateEnabled();
            y = authGroup.Bottom + Ui.S(8);

            // Module status
            var module = ModuleLocator.FindBest();
            bool moduleOk = module != null && module.Version >= ModuleLocator.MinimumVersion;
            var moduleIcon = new PictureBox
            {
                Image = Icons.Bitmap(moduleOk ? StockIcon.Info : StockIcon.Warning),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(x, y),
            };
            var moduleText = new LinkLabel
            {
                AutoSize = false,
                Location = new Point(x + moduleIcon.Width + Ui.S(6), y),
                Size = new Size(w - moduleIcon.Width - Ui.S(6), Ui.S(34)),
                UseMnemonic = false,
            };
            if (moduleOk)
            {
                moduleText.Text = $"ExchangeOnlineManagement {module.Version} will be used.";
                moduleText.LinkArea = new LinkArea(0, 0);
            }
            else
            {
                string prefix = module == null
                    ? "The ExchangeOnlineManagement module isn't installed. Run: "
                    : $"ExchangeOnlineManagement {module.Version} is too old (3.2.0 or later is required). Run: ";
                string command = module == null ? ModuleLocator.InstallCommand : "Update-Module ExchangeOnlineManagement";
                moduleText.Text = prefix + command + "  (click to copy)";
                moduleText.LinkArea = new LinkArea(prefix.Length, command.Length);
                moduleText.LinkClicked += (s, e) => Clipboard.SetText(command);
            }
            Controls.Add(moduleIcon);
            Controls.Add(moduleText);

            // Buttons
            var ok = new Button { Text = "Connect", Size = Ui.S(88, 26), Enabled = moduleOk };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = Ui.S(88, 26) };
            cancel.Location = new Point(ClientSize.Width - Ui.S(12) - cancel.Width, ClientSize.Height - Ui.S(12) - cancel.Height);
            ok.Location = new Point(cancel.Left - Ui.S(8) - ok.Width, cancel.Top);
            var separator = new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Location = new Point(x, cancel.Top - Ui.S(12)), Width = w };
            ok.Click += (s, e) => OnOk();
            Controls.Add(separator);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            // Initial values
            _environment.SelectedIndex = Math.Max(0, (int)settings.Environment);
            _upn.Text = settings.UserPrincipalName ?? "";
            _disableWam.Checked = settings.DisableWam;
            _appId.Text = settings.AppId ?? "";
            _thumbprint.Text = settings.CertificateThumbprint ?? "";
            _organization.Text = settings.Organization ?? "";
            if (settings.AuthMode == AuthMode.Certificate) _certificate.Checked = true;
            else _interactive.Checked = true;
            UpdateEnabled();
        }

        public ConnectOptions Options { get; private set; }

        private void UpdateEnabled()
        {
            foreach (var c in _interactiveControls) c.Enabled = _interactive.Checked;
            foreach (var c in _certificateControls) c.Enabled = _certificate.Checked;
        }

        private void OnOk()
        {
            var options = new ConnectOptions
            {
                Environment = ((EnvironmentItem)_environment.SelectedItem).Value,
                Mode = _certificate.Checked ? AuthMode.Certificate : AuthMode.Interactive,
                UserPrincipalName = _upn.Text.Trim(),
                DisableWam = _disableWam.Checked,
                AppId = _appId.Text.Trim(),
                CertificateThumbprint = Regex.Replace(_thumbprint.Text, @"[\s:\u200e\u200f]", "").ToUpperInvariant(),
                Organization = _organization.Text.Trim(),
            };

            if (options.Mode == AuthMode.Interactive)
            {
                if (options.UserPrincipalName.Length > 0 && !options.UserPrincipalName.Contains("@"))
                {
                    Invalid(_upn, "Enter a user principal name such as admin@contoso.onmicrosoft.com, or leave it empty.");
                    return;
                }
            }
            else
            {
                if (!Guid.TryParse(options.AppId, out _))
                {
                    Invalid(_appId, "Enter the application (client) ID of the app registration, a GUID.");
                    return;
                }
                if (!Regex.IsMatch(options.CertificateThumbprint, "^[0-9A-F]{40}$"))
                {
                    Invalid(_thumbprint, "Enter the 40-character hexadecimal thumbprint of a certificate in your personal (CurrentUser\\My) or computer (LocalMachine\\My) store.");
                    return;
                }
                if (options.Organization.Length == 0 || !options.Organization.Contains("."))
                {
                    Invalid(_organization, "Enter the organization's initial domain, for example contoso.onmicrosoft.com.");
                    return;
                }
            }

            _settings.Environment = options.Environment;
            _settings.AuthMode = options.Mode;
            _settings.UserPrincipalName = options.UserPrincipalName;
            _settings.DisableWam = options.DisableWam;
            _settings.AppId = options.AppId;
            _settings.CertificateThumbprint = options.CertificateThumbprint;
            _settings.Organization = options.Organization;
            Options = options;
            DialogResult = DialogResult.OK;
        }

        private void Invalid(Control control, string message)
        {
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            control.Focus();
            if (control is TextBox tb) tb.SelectAll();
        }

        private sealed class EnvironmentItem
        {
            public EnvironmentItem(CloudEnvironment value) => Value = value;
            public CloudEnvironment Value { get; }
            public override string ToString() => ConnectOptions.EnvironmentName(Value);
        }
    }

    internal sealed class AboutDialog : Form
    {
        public AboutDialog(Workspace workspace)
        {
            Text = "About " + AppInfo.ProductName;
            Font = Ui.DialogFont;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;

            var icon = new PictureBox { Image = Icons.App(Ui.S(48)).ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Location = new Point(Ui.S(16), Ui.S(16)) };
            int x = Ui.S(16) + Ui.S(48) + Ui.S(16);
            int textWidth = Ui.S(360);

            Label Line(string text, Font font = null, bool gray = false, int top = 0) => new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(textWidth, 0),
                UseMnemonic = false,
                Font = font ?? Ui.DialogFont,
                ForeColor = gray ? SystemColors.GrayText : SystemColors.ControlText,
                Margin = new Padding(0, top, 0, 0),
            };
            LinkLabel Link(string text, string url, int top)
            {
                var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, top, 0, 0), UseMnemonic = false };
                link.LinkClicked += (s, e) => Ui.OpenUrl(url);
                return link;
            }

            var module = workspace.Session?.Module ?? ModuleLocator.FindBest();
            var column = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(x, Ui.S(14)),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            var license = new LinkLabel { Text = "Released under the MIT License.", AutoSize = true, Margin = Padding.Empty, UseMnemonic = false };
            license.LinkArea = new LinkArea("Released under the ".Length, "MIT License".Length);
            license.LinkClicked += (s, e) => Ui.OpenUrl(AppInfo.LicenseUrl);
            column.Controls.AddRange(new Control[]
            {
                Line(AppInfo.ProductName, Ui.TitleFont),
                Line($"Version {AppInfo.Version}  \u00b7  {AppInfo.Brand}", top: Ui.S(2)),
                Line(AppInfo.Copyright, top: Ui.S(10)),
                license,
                Link(AppInfo.ProjectUrl.Replace("https://", ""), AppInfo.ProjectUrl, Ui.S(2)),
                Line("A read-only viewer for Microsoft Purview configuration. It reads the configuration with " +
                     "Security & Compliance PowerShell Get-* cmdlets and never changes anything in the tenant.", top: Ui.S(14)),
                Line($"Windows PowerShell {WindowsPowerShellVersion()}\r\n" +
                     (module != null ? $"ExchangeOnlineManagement {module.Version}" : "ExchangeOnlineManagement: not installed") + "\r\n" +
                     $"{PurviewCatalog.Solutions.Count} solutions, {PurviewCatalog.AllCollections.Count()} collections", top: Ui.S(10)),
                Line("Microsoft and Microsoft Purview are trademarks of the Microsoft group of companies. This is an independent " +
                     "community tool, not affiliated with or endorsed by Microsoft. The Sarda Cloud name and logo are trademarks of Sarda Cloud Srl.",
                     gray: true, top: Ui.S(12)),
            });
            Controls.Add(icon);
            Controls.Add(column);

            var content = column.GetPreferredSize(Size.Empty);
            ClientSize = new Size(x + textWidth + Ui.S(20), Math.Max(icon.Bottom, column.Top + content.Height) + Ui.S(62));

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = Ui.S(88, 26) };
            ok.Location = new Point(ClientSize.Width - Ui.S(16) - ok.Width, ClientSize.Height - Ui.S(14) - ok.Height);
            var separator = new Label { BorderStyle = BorderStyle.Fixed3D, Height = 2, Location = new Point(Ui.S(16), ok.Top - Ui.S(12)), Width = ClientSize.Width - Ui.S(32) };
            Controls.AddRange(new Control[] { separator, ok });
            AcceptButton = ok;
            CancelButton = ok;
        }

        private static string WindowsPowerShellVersion()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine"))
                    if (key?.GetValue("PowerShellVersion") is string v && v.Length > 0) return v;
            }
            catch (System.Security.SecurityException) { }
            return "5.1";
        }
    }
}
