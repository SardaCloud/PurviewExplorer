// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Catalog;
using SardaCloud.PurviewExplorer.Data;

namespace SardaCloud.PurviewExplorer.UI
{
    internal sealed class MainForm : Form
    {
        private readonly Workspace _ws = new Workspace();
        private readonly AppSettings _settings;
        private readonly StartupOptions _startup;
        private readonly TabControl _tabs;
        private readonly SummaryView _summary;
        private readonly List<SolutionView> _views = new List<SolutionView>();
        private readonly ToolBar _toolBar;
        private readonly StatusBar _statusBar;
        private readonly StatusBarPanel _stMain;
        private readonly StatusBarPanel _stItems;
        private readonly StatusBarPanel _stSource;
        private readonly StatusBarPanel _stAccount;
        private readonly Dictionary<string, ToolBarButton> _buttons = new Dictionary<string, ToolBarButton>();
        private readonly Dictionary<string, MenuItem> _menu = new Dictionary<string, MenuItem>();
        private bool _closingAfterDisconnect;
        private bool _closeRequested;

        public MainForm(AppSettings settings, StartupOptions startup)
        {
            _settings = settings;
            _startup = startup;
            Text = AppInfo.ProductName;
            Font = Ui.DialogFont;
            AutoScaleMode = AutoScaleMode.None;
            Icon = Icons.App(SystemInformation.IconSize.Width);
            Size = Ui.S(1180, 780);
            MinimumSize = Ui.S(760, 520);
            StartPosition = FormStartPosition.CenterScreen;
            RestoreWindow();

            Menu = BuildMenu();
            _toolBar = BuildToolBar();

            _stMain = new StatusBarPanel { AutoSize = StatusBarPanelAutoSize.Spring, Text = "Ready", BorderStyle = StatusBarPanelBorderStyle.None };
            _stItems = new StatusBarPanel { AutoSize = StatusBarPanelAutoSize.Contents, MinWidth = Ui.S(150) };
            _stSource = new StatusBarPanel { AutoSize = StatusBarPanelAutoSize.Contents, MinWidth = Ui.S(160) };
            _stAccount = new StatusBarPanel { AutoSize = StatusBarPanelAutoSize.Contents, MinWidth = Ui.S(120) };
            _statusBar = new StatusBar { ShowPanels = true, SizingGrip = true, Font = Ui.DialogFont };
            _statusBar.Panels.AddRange(new[] { _stMain, _stItems, _stSource, _stAccount });
            // The native status bar keeps a fixed 22 px height; size it to the font so text isn't clipped at high DPI.
            _statusBar.Height = Math.Max(_statusBar.Height, TextRenderer.MeasureText("Ag", Ui.DialogFont).Height + Ui.S(8));

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ImageList = Icons.CreateSmallImageList(Icons.SolutionIcons().ToArray()),
                Padding = new Point(Ui.S(8), Ui.S(4)),
            };
            _summary = new SummaryView(_ws);
            _summary.NavigateRequested += (s, key) => Navigate(key);
            AddTab("Summary", StockIcon.Info, _summary);
            foreach (var solution in PurviewCatalog.Solutions)
            {
                var view = new SolutionView(solution, _ws);
                view.SelectionChanged += (s, e) => UpdateUi();
                _views.Add(view);
                AddTab(solution.Name, solution.Icon, view);
            }
            _tabs.SelectedIndexChanged += (s, e) =>
            {
                CurrentView?.Activate();
                UpdateUi();
            };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(4), Ui.S(4), Ui.S(4), Ui.S(2)) };
            body.Controls.Add(_tabs);
            Controls.Add(body);
            Controls.Add(_toolBar);
            Controls.Add(_statusBar);

            _ws.StateChanged += (s, e) => UpdateUi();
            _ws.StoreReplaced += (s, e) => UpdateUi();
            _ws.CollectionChanged += (s, e) => UpdateUi();
            UpdateUi();
        }

        private SolutionView CurrentView => _tabs.SelectedTab?.Tag as SolutionView;

        private void AddTab(string text, StockIcon icon, Control content)
        {
            var page = new TabPage(text) { Tag = content, Padding = new Padding(Ui.S(2)), UseVisualStyleBackColor = true };
            page.Controls.Add(content);
            _tabs.TabPages.Add(page);
            page.ImageKey = icon.ToString(); // must be set after the page is added, or the image isn't shown
        }

        // ---------------------------------------------------------------- menus and toolbar

        private MainMenu BuildMenu()
        {
            MenuItem Item(string key, string text, EventHandler onClick, Shortcut shortcut = Shortcut.None)
            {
                var mi = new MenuItem(text, onClick, shortcut);
                _menu[key] = mi;
                return mi;
            }
            MenuItem Sep() => new MenuItem("-");

            var file = new MenuItem("&File", new[]
            {
                Item("connect", "&Connect to Tenant...", (s, e) => ConnectAsync()),
                Item("disconnect", "&Disconnect", (s, e) => DisconnectAsync()),
                Sep(),
                Item("open", "&Open Snapshot...", (s, e) => OpenSnapshotAsync(null), Shortcut.CtrlO),
                Item("save", "&Save Snapshot As...", (s, e) => SaveSnapshotAsync(), Shortcut.CtrlS),
                Sep(),
                Item("demo", "Open De&mo Data", (s, e) => OpenDemoAsync()),
                Sep(),
                Item("export", "&Export List to CSV...", (s, e) => CurrentView?.ExportCsv(_settings), Shortcut.CtrlE),
                Sep(),
                Item("exit", "E&xit", (s, e) => Close()),
            });
            var edit = new MenuItem("&Edit", new[]
            {
                Item("copy", "&Copy", (s, e) => CopySelection(), Shortcut.CtrlC),
                Item("selectAll", "Select &All", (s, e) => CurrentView?.SelectAll(), Shortcut.CtrlA),
                Sep(),
                Item("find", "&Filter...", (s, e) => CurrentView?.FocusFilter(), Shortcut.CtrlF),
            });
            var toolbar = Item("toolbar", "&Toolbar", (s, e) => ToggleBar(_toolBar, "toolbar"));
            var statusbar = Item("statusbar", "&Status Bar", (s, e) => ToggleBar(_statusBar, "statusbar"));
            toolbar.Checked = statusbar.Checked = true;
            var view = new MenuItem("&View", new[]
            {
                toolbar,
                statusbar,
                Sep(),
                Item("properties", "P&roperties\tAlt+Enter", (s, e) => CurrentView?.ShowItemProperties()),
                Sep(),
                Item("refresh", "&Refresh", (s, e) => CurrentView?.RefreshSelected(), Shortcut.F5),
                Item("refreshTab", "Refresh &Tab", (s, e) => CurrentView?.RefreshTab(), Shortcut.ShiftF5),
                Item("loadAll", "&Load All Collections", (s, e) => LoadAll(force: false), Shortcut.CtrlL),
                Item("refreshAll", "Refresh A&ll", (s, e) => LoadAll(force: true), Shortcut.CtrlF5),
                Item("stop", "St&op", (s, e) => _ws.CancelQueue()),
            });
            var help = new MenuItem("&Help", new[]
            {
                Item("docs", "Microsoft &Purview Documentation", (s, e) => Ui.OpenUrl(CurrentView?.Solution.LearnMoreUrl ?? "https://learn.microsoft.com/purview/")),
                Item("psDocs", "Security && Compliance &PowerShell", (s, e) => Ui.OpenUrl("https://learn.microsoft.com/powershell/exchange/scc-powershell")),
                Sep(),
                Item("check", "&Check Prerequisites...", (s, e) => CheckPrerequisitesAsync()),
                Sep(),
                Item("website", "Project &Website", (s, e) => Ui.OpenUrl(AppInfo.ProjectUrl)),
                Item("issue", "Report an &Issue", (s, e) => Ui.OpenUrl(AppInfo.IssuesUrl)),
                Sep(),
                Item("about", "&About " + AppInfo.ProductName.Replace("&", "&&"), (s, e) => ShowAbout()),
            });
            var menu = new MainMenu(new[] { file, edit, view, help });
            file.Popup += (s, e) => UpdateUi();
            edit.Popup += (s, e) => UpdateUi();
            view.Popup += (s, e) => UpdateUi();
            return menu;
        }

        private ToolBar BuildToolBar()
        {
            var size = SystemInformation.SmallIconSize;
            var images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = size };
            void Img(string key, Image image) => images.Images.Add(key, image);
            Img("connect", Icons.Bitmap(StockIcon.MyNetwork));
            Img("disconnect", Icons.Bitmap(StockIcon.DriveNetDisabled));
            Img("open", Icons.Bitmap(StockIcon.FolderOpen));
            Img("save", Icons.Bitmap(StockIcon.Drive35));
            Img("refresh", Ui.RefreshGlyph(size.Width));
            Img("loadAll", Ui.RefreshGlyph(size.Width, twoArrows: true));
            Img("stop", Icons.Bitmap(StockIcon.Delete));
            Img("properties", Icons.Bitmap(StockIcon.Software));
            Img("export", Icons.Bitmap(StockIcon.DocAssoc));

            var bar = new ToolBar
            {
                Appearance = ToolBarAppearance.Flat,
                TextAlign = ToolBarTextAlign.Right,
                ShowToolTips = true,
                Divider = true,
                Wrappable = false,
                ImageList = images,
                Dock = DockStyle.Top,
            };
            void Btn(string key, string text, string tip)
            {
                var b = new ToolBarButton(text) { ImageKey = key, ToolTipText = tip, Tag = key };
                _buttons[key] = b;
                bar.Buttons.Add(b);
            }
            void Sep() => bar.Buttons.Add(new ToolBarButton { Style = ToolBarButtonStyle.Separator });

            Btn("connect", "Connect", "Connect to a tenant");
            Btn("disconnect", "", "Disconnect");
            Sep();
            Btn("open", "Open", "Open a snapshot (Ctrl+O)");
            Btn("save", "Save", "Save a snapshot (Ctrl+S)");
            Sep();
            Btn("refresh", "Refresh", "Refresh the selected collection (F5)");
            Btn("loadAll", "Load All", "Load every collection (Ctrl+L)");
            Btn("stop", "Stop", "Stop loading");
            Sep();
            Btn("properties", "", "Properties (Alt+Enter)");
            Btn("export", "", "Export the list to CSV (Ctrl+E)");
            bar.ButtonClick += (s, e) => OnToolBarClick((string)e.Button.Tag);
            return bar;
        }

        private void OnToolBarClick(string key)
        {
            switch (key)
            {
                case "connect": ConnectAsync(); break;
                case "disconnect": DisconnectAsync(); break;
                case "open": OpenSnapshotAsync(null); break;
                case "save": SaveSnapshotAsync(); break;
                case "refresh": CurrentView?.RefreshSelected(); break;
                case "loadAll": LoadAll(force: false); break;
                case "stop": _ws.CancelQueue(); break;
                case "properties": CurrentView?.ShowItemProperties(); break;
                case "export": CurrentView?.ExportCsv(_settings); break;
            }
        }

        private void ToggleBar(Control bar, string key)
        {
            bar.Visible = !bar.Visible;
            _menu[key].Checked = bar.Visible;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Enter))
            {
                CurrentView?.ShowItemProperties();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------------------------------------------------------- state

        private void UpdateUi()
        {
            if (IsDisposed) return;
            var src = _ws.Store.Source;
            bool connected = _ws.IsConnected;
            bool connecting = _ws.IsConnecting;
            var view = CurrentView;
            bool hasItems = view != null && view.ShownCount > 0;
            bool hasSelection = view != null && view.HasSelectedItem;
            bool anyData = _ws.Store.Results.Any(r => r.State == LoadState.Loaded);

            Enable("connect", !connecting && !_ws.IsLoading);
            Enable("disconnect", _ws.Session != null && !connecting);
            Enable("open", !connecting);
            Enable("demo", !connecting);
            Enable("save", anyData && !connecting);
            Enable("export", hasItems);
            Enable("copy", true);
            Enable("selectAll", view != null);
            Enable("find", view != null);
            Enable("properties", hasSelection);
            Enable("refresh", connected && view != null);
            Enable("refreshTab", connected && view != null);
            Enable("loadAll", connected);
            Enable("refreshAll", connected);
            Enable("stop", _ws.IsLoading);

            string sourceText;
            switch (src.Kind)
            {
                case SourceKind.Live:
                    sourceText = (connected ? "Connected: " : "Disconnected: ") + src.DisplayName;
                    break;
                case SourceKind.Snapshot:
                    sourceText = "Snapshot: " + Path.GetFileName(src.FilePath) + (string.IsNullOrEmpty(src.Organization) ? "" : " (" + src.Organization + ")");
                    break;
                case SourceKind.Demo:
                    sourceText = "Demo data: " + src.DisplayName;
                    break;
                default:
                    sourceText = connecting ? "Connecting\u2026" : "Not connected";
                    break;
            }
            _stSource.Text = sourceText;
            _stAccount.Text = src.Account ?? "";
            _stItems.Text = view != null ? view.StatusText : _summary.StatusText;

            string activity = _ws.Activity;
            if (activity != null && _ws.IsLoading && _ws.BatchTotal > 1)
                activity += $"  ({Math.Min(_ws.BatchDone + 1, _ws.BatchTotal)} of {_ws.BatchTotal})";
            _stMain.Text = activity ?? "Ready";

            Text = src.Kind == SourceKind.None ? AppInfo.ProductName : AppInfo.ProductName + " - " + sourceText;
            Cursor = connecting ? Cursors.AppStarting : Cursors.Default;
        }

        private void Enable(string key, bool enabled)
        {
            if (_menu.TryGetValue(key, out var mi)) mi.Enabled = enabled;
            if (_buttons.TryGetValue(key, out var b)) b.Enabled = enabled;
        }

        // ---------------------------------------------------------------- commands

        private async void ConnectAsync()
        {
            if (_ws.IsBusy) return;
            ConnectOptions options;
            using (var dlg = new ConnectDialog(_settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                options = dlg.Options;
            }
            _settings.Save();
            try
            {
                await _ws.ConnectAsync(options);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                if (!_closeRequested) Ui.ShowError(this, "Couldn't connect to Security & Compliance PowerShell.\r\n\r\n" + PsConverter.FormatException(ex));
                return;
            }
            _settings.RememberOrganization(options.Mode == AuthMode.Certificate ? options.Organization : options.DelegatedOrganization);
            _settings.Save();
            if (_closeRequested) return;
            if (_tabs.SelectedIndex == 0 && _views.Count > 0) _tabs.SelectedIndex = 1;
            else CurrentView?.Activate();
        }

        private async void DisconnectAsync()
        {
            await _ws.DisconnectAsync();
            UpdateUi();
        }

        private async Task<bool> ConfirmLeaveLiveSessionAsync()
        {
            if (_ws.Session == null) return true;
            var answer = MessageBox.Show(this,
                $"You're connected to {_ws.Store.Source.DisplayName}. Opening other data disconnects this session.\r\n\r\nDo you want to continue?",
                AppInfo.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return false;
            await _ws.DisconnectAsync();
            return true;
        }

        private async void OpenSnapshotAsync(string path)
        {
            if (path == null)
            {
                using (var dlg = new OpenFileDialog
                {
                    Title = "Open Snapshot",
                    Filter = "Purview snapshots (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = _settings.LastSnapshotFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    path = dlg.FileName;
                }
            }
            DataStore store;
            try
            {
                store = Snapshot.Load(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is JsonException)
            {
                Ui.ShowError(this, $"Couldn't open {Path.GetFileName(path)}.\r\n\r\n{ex.Message}");
                return;
            }
            if (!await ConfirmLeaveLiveSessionAsync()) return;
            _settings.LastSnapshotFolder = Path.GetDirectoryName(Path.GetFullPath(path));
            _ws.ShowStore(store);
            CurrentView?.Activate();
        }

        private async void OpenDemoAsync()
        {
            if (!await ConfirmLeaveLiveSessionAsync()) return;
            _ws.ShowStore(Snapshot.LoadDemo());
            CurrentView?.Activate();
        }

        private async void SaveSnapshotAsync()
        {
            if (_ws.IsConnected && !_ws.Store.AllLoaded(PurviewCatalog.AllCollections))
            {
                var answer = MessageBox.Show(this,
                    "Some collections haven't been loaded yet.\r\n\r\nDo you want to load everything before saving? Choose No to save only what has been loaded.",
                    "Save Snapshot", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes)
                {
                    LoadAll(force: false);
                    await _ws.WhenIdleAsync();
                }
            }

            var src = _ws.Store.Source;
            var org = string.IsNullOrEmpty(src.Organization) ? "tenant" : src.Organization;
            var name = $"Purview-{org}-{DateTime.Now:yyyy-MM-dd-HHmm}.json";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            using (var dlg = new SaveFileDialog
            {
                Title = "Save Snapshot",
                Filter = "Purview snapshots (*.json)|*.json|All files (*.*)|*.*",
                FileName = src.Kind == SourceKind.Snapshot && src.FilePath != null ? Path.GetFileName(src.FilePath) : name,
                InitialDirectory = _settings.LastSnapshotFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true,
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (src.Kind == SourceKind.Live) src.CreatedUtc = DateTime.UtcNow;
                    Snapshot.Save(_ws.Store, dlg.FileName);
                    _settings.LastSnapshotFolder = Path.GetDirectoryName(dlg.FileName);
                    _stMain.Text = "Snapshot saved to " + dlg.FileName;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Ui.ShowError(this, "Couldn't save the snapshot.\r\n\r\n" + ex.Message);
                }
            }
        }

        private void LoadAll(bool force)
        {
            var current = CurrentView;
            var keys = new List<string>();
            if (current != null) keys.AddRange(current.Solution.Collections.Select(c => c.Key));
            keys.AddRange(PurviewCatalog.AllCollections.Select(c => c.Key).Where(k => !keys.Contains(k)));
            _ws.Enqueue(keys, force);
        }

        private void CopySelection()
        {
            if (CurrentView != null) CurrentView.Copy();
            else if (ActiveControl is TextBox tb) tb.Copy();
            else _summary.Copy();
        }

        private void ShowAbout()
        {
            using (var dlg = new AboutDialog(_ws)) dlg.ShowDialog(this);
        }

        private async void CheckPrerequisitesAsync()
        {
            var previous = Cursor;
            Cursor = Cursors.WaitCursor;
            _stMain.Text = "Checking prerequisites\u2026";
            bool ok = false;
            string report;
            try
            {
                report = await Task.Run(() => PurviewSession.CheckPrerequisites(out ok));
            }
            finally
            {
                Cursor = previous;
                UpdateUi();
            }
            ValueDialog.ShowValue(this, ok ? "Prerequisites: OK" : "Prerequisites: action needed", report);
        }

        private void Navigate(string key)
        {
            var def = PurviewCatalog.Find(key);
            if (def == null) return;
            var view = _views.FirstOrDefault(v => v.Solution == def.Solution);
            if (view == null) return;
            _tabs.SelectedTab = (TabPage)view.Parent;
            view.SelectCollection(key);
        }

        // ---------------------------------------------------------------- startup, window state, closing

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_startup.SelfTestLog != null)
            {
                Environment.ExitCode = await SelfTest.RunAsync(_ws, _tabs, _startup.SelfTestLog);
                _closingAfterDisconnect = true;
                Close();
                return;
            }
            try
            {
                if (_startup.Demo) _ws.ShowStore(Snapshot.LoadDemo());
                else if (_startup.SnapshotPath != null) OpenSnapshotAsync(_startup.SnapshotPath);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                Ui.ShowError(this, ex.Message);
            }
            ApplyStartupSelection();
        }

        private void ApplyStartupSelection()
        {
            if (_startup.Tab != null)
            {
                var view = _views.FirstOrDefault(v => string.Equals(v.Solution.Key, _startup.Tab, StringComparison.OrdinalIgnoreCase));
                if (view != null) _tabs.SelectedTab = (TabPage)view.Parent;
            }
            if (_startup.Collection != null) Navigate(_startup.Collection);
            if (_startup.Item.HasValue) CurrentView?.SelectItemAt(_startup.Item.Value);
            if (_startup.ScreenshotPath == null) return;

            var timer = new Timer { Interval = 700 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Form target = this;
                switch ((_startup.Dialog ?? "").ToLowerInvariant())
                {
                    case "properties":
                        var def = CurrentView?.SelectedCollection;
                        var item = CurrentView?.SelectedItem;
                        if (def != null && item != null) target = new ItemPropertiesDialog(def, item, _ws.Store);
                        break;
                    case "connect":
                        target = new ConnectDialog(_settings);
                        break;
                    case "about":
                        target = new AboutDialog(_ws);
                        break;
                }
                if (target != this)
                {
                    target.StartPosition = FormStartPosition.Manual;
                    target.Location = new Point(Left + Ui.S(120), Top + Ui.S(90));
                    if (_startup.DialogTab.HasValue && target.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<TabControl>()).FirstOrDefault() is TabControl tc)
                        tc.SelectedIndex = Math.Min(_startup.DialogTab.Value, tc.TabCount - 1);
                    target.Show(this);
                }
                var capture = new Timer { Interval = 500 };
                capture.Tick += (s2, e2) =>
                {
                    capture.Stop();
                    using (var bmp = NativeMethods.CaptureWindow(target))
                        bmp.Save(_startup.ScreenshotPath, ImageFormat.Png);
                    _closingAfterDisconnect = true;
                    Application.Exit();
                };
                capture.Start();
            };
            timer.Start();
        }

        private void RestoreWindow()
        {
            var b = _settings.Bounds;
            if (b != null && b[2] >= MinimumSize.Width && b[3] >= MinimumSize.Height)
            {
                var rect = new Rectangle(b[0], b[1], b[2], b[3]);
                if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect)))
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = rect;
                }
            }
            if (_settings.Maximized) WindowState = FormWindowState.Maximized;
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (_startup.ScreenshotPath == null && _startup.SelfTestLog == null)
            {
                var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                _settings.Bounds = new[] { bounds.X, bounds.Y, bounds.Width, bounds.Height };
                _settings.Maximized = WindowState == FormWindowState.Maximized;
                _settings.Save();
            }

            if (!_closingAfterDisconnect && (_ws.IsConnecting || _ws.Session != null))
            {
                // Never leave a signed-in runspace behind: wait for a sign-in in progress, then disconnect.
                e.Cancel = true;
                if (_closeRequested) return;
                _closeRequested = true;
                Enabled = false;
                if (_ws.IsConnecting)
                {
                    _stMain.Text = "Waiting for the sign-in to finish before closing\u2026";
                    await Task.WhenAny(_ws.WhenConnectFinishedAsync(), Task.Delay(TimeSpan.FromMinutes(5)));
                }
                _stMain.Text = "Disconnecting\u2026";
                if (_ws.Session != null) await Task.WhenAny(_ws.DisconnectAsync(), Task.Delay(8000));
                _closingAfterDisconnect = true;
                Close();
                return;
            }
            base.OnFormClosing(e);
        }
    }

    internal sealed class StartupOptions
    {
        public string SnapshotPath { get; set; }
        public bool Demo { get; set; }
        public string Tab { get; set; }
        public string Collection { get; set; }
        public int? Item { get; set; }
        public string Dialog { get; set; }
        public int? DialogTab { get; set; }
        public string ScreenshotPath { get; set; }
        public string SelfTestLog { get; set; }
        public bool CheckPrerequisites { get; set; }
        public string CheckPrerequisitesReport { get; set; }

        /// <summary>Parses "file.json", "/Demo" and the screenshot switches used to produce documentation images.</summary>
        public static StartupOptions Parse(string[] args)
        {
            var o = new StartupOptions();
            foreach (var arg in args)
            {
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    var body = arg.TrimStart('/', '-');
                    int colon = body.IndexOf(':');
                    var name = (colon < 0 ? body : body.Substring(0, colon)).ToLowerInvariant();
                    var value = colon < 0 ? null : body.Substring(colon + 1);
                    switch (name)
                    {
                        case "demo": o.Demo = true; break;
                        case "tab": o.Tab = value; break;
                        case "collection": o.Collection = value; break;
                        case "item": o.Item = int.TryParse(value, out var i) ? i : (int?)null; break;
                        case "dialog": o.Dialog = value; break;
                        case "dialogtab": o.DialogTab = int.TryParse(value, out var t) ? t : (int?)null; break;
                        case "screenshot": o.ScreenshotPath = value == null ? null : Path.GetFullPath(value); break;
                        case "selftest": o.SelfTestLog = Path.GetFullPath(value ?? "PurviewExplorer-selftest.log"); break;
                        case "checkprerequisites":
                            o.CheckPrerequisites = true;
                            o.CheckPrerequisitesReport = string.IsNullOrEmpty(value) ? null : Path.GetFullPath(value);
                            break;
                    }
                }
                else if (o.SnapshotPath == null)
                {
                    o.SnapshotPath = arg;
                }
            }
            return o;
        }
    }
}
