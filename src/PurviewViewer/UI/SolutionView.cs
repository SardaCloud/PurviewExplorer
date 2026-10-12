// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SardaCloud.PurviewViewer.Catalog;
using SardaCloud.PurviewViewer.Data;

namespace SardaCloud.PurviewViewer.UI
{
    /// <summary>One tab: the collections of a solution (left), their items (top right) and the selected item's properties (bottom right).</summary>
    internal sealed class SolutionView : UserControl
    {
        private readonly Workspace _ws;
        private readonly TreeView _tree;
        private readonly ListViewEx _items;
        private readonly ListViewEx _props;
        private readonly Label _listTitle;
        private readonly Label _listInfo;
        private readonly Label _message;
        private readonly Label _propsTitle;
        private readonly LinkLabel _infoBar;
        private readonly TextBox _filter;
        private readonly SplitContainer _split;
        private readonly SplitContainer _rightSplit;
        private readonly Dictionary<JsonMap, string> _searchCache = new Dictionary<JsonMap, string>();
        private readonly Dictionary<string, TreeNode> _nodes = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        private List<JsonMap> _shown = new List<JsonMap>();
        private bool _detailsPending;
        private bool _layoutApplied;

        public SolutionView(SolutionDef solution, Workspace workspace)
        {
            Solution = solution;
            _ws = workspace;
            Dock = DockStyle.Fill;

            var header = Ui.CreateHeader(Icons.Get(solution.Icon, small: false), solution.Name, solution.Description, solution.LearnMoreUrl);

            _tree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowLines = false,
                ShowRootLines = false,
                ShowPlusMinus = false,
                FullRowSelect = true,
                ImageList = Icons.CreateSmallImageList(StockIcon.Folder, StockIcon.FolderOpen, StockIcon.Warning, StockIcon.Error),
                ItemHeight = Math.Max(SystemInformation.SmallIconSize.Height + Ui.S(4), Ui.S(20)),
            };
            foreach (var def in solution.Collections)
            {
                var node = new TreeNode(def.Name) { Tag = def, ImageKey = "Folder", SelectedImageKey = "FolderOpen" };
                _nodes[def.Key] = node;
                _tree.Nodes.Add(node);
            }
            _tree.AfterSelect += (s, e) => OnCollectionSelected();

            // Items list with its header (title, info, filter box) and an info bar for partial errors.
            _listTitle = new Label { AutoSize = true, UseMnemonic = false, Font = Ui.BoldFont, Anchor = AnchorStyles.Left, Margin = new Padding(Ui.S(2), 0, Ui.S(6), 0) };
            _listInfo = new Label { AutoSize = true, UseMnemonic = false, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, AutoEllipsis = true };
            var filterLabel = new Label { Text = "&Filter:", AutoSize = true, Anchor = AnchorStyles.Right };
            _filter = new TextBox { Width = Ui.S(180), Anchor = AnchorStyles.Right };
            var filterTimer = new Timer { Interval = 250 };
            filterTimer.Tick += (s, e) =>
            {
                filterTimer.Stop();
                PopulateItems();
            };
            _filter.TextChanged += (s, e) =>
            {
                filterTimer.Stop();
                filterTimer.Start();
            };
            _filter.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && _filter.TextLength > 0)
                {
                    _filter.Clear();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Down && _items.Items.Count > 0)
                {
                    _items.Focus();
                    if (_items.SelectedItems.Count == 0) _items.Items[0].Selected = true;
                    e.SuppressKeyPress = true;
                }
            };

            var listHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = _filter.PreferredHeight + Ui.S(8),
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(Ui.S(2), Ui.S(3), Ui.S(2), Ui.S(3)),
            };
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            listHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            listHeader.Controls.Add(_listTitle, 0, 0);
            listHeader.Controls.Add(_listInfo, 1, 0);
            listHeader.Controls.Add(filterLabel, 2, 0);
            listHeader.Controls.Add(_filter, 3, 0);

            _infoBar = new LinkLabel
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = Ui.S(24),
                BackColor = SystemColors.Info,
                ForeColor = SystemColors.InfoText,
                LinkColor = SystemColors.HotTrack,
                TextAlign = ContentAlignment.MiddleLeft,
                Image = Icons.Bitmap(StockIcon.Warning),
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(Ui.S(4), 0, 0, 0),
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle,
                UseMnemonic = false,
            };
            _infoBar.LinkClicked += (s, e) => ShowCollectionMessage();

            _items = new ListViewEx { Dock = DockStyle.Fill, MultiSelect = true };
            _items.SmallImageList = Icons.CreateSmallImageList(solution.Icon);
            _items.SelectedIndexChanged += (s, e) => ScheduleDetails();
            _items.ItemActivate += (s, e) => ShowItemProperties();
            _items.ColumnClick += (s, e) =>
            {
                foreach (ListViewItem item in _items.Items) item.IndentCount = 0;
            };
            _items.SortRequested += (s, e) => PopulateItems();
            _items.ContextMenu = CreateItemsMenu();

            _message = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(Ui.S(24)),
                Visible = false,
                BorderStyle = BorderStyle.Fixed3D,
                UseMnemonic = false,
            };

            var listArea = new Panel { Dock = DockStyle.Fill };
            listArea.Controls.Add(_items);
            listArea.Controls.Add(_message);
            listArea.Controls.Add(_infoBar);
            listArea.Controls.Add(listHeader);

            // Properties of the selected item.
            _propsTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = Ui.S(22),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Ui.BoldFont,
                Text = "Properties",
                AutoEllipsis = true,
                UseMnemonic = false,
            };
            _props = new ListViewEx { Dock = DockStyle.Fill, GridLines = true, MultiSelect = true };
            _props.Columns.Add("Property", Ui.S(220));
            _props.Columns.Add("Value", Ui.S(640));
            _props.ItemActivate += (s, e) => ShowPropertyValue();
            _props.ContextMenu = CreatePropsMenu();

            var propsArea = new Panel { Dock = DockStyle.Fill };
            propsArea.Controls.Add(_props);
            propsArea.Controls.Add(_propsTitle);

            _rightSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _rightSplit.Panel1.Controls.Add(listArea);
            _rightSplit.Panel2.Controls.Add(propsArea);

            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, FixedPanel = FixedPanel.Panel1 };
            _split.Panel1.Controls.Add(_tree);
            _split.Panel2.Controls.Add(_rightSplit);
            _split.Panel1.Padding = new Padding(0, Ui.S(4), 0, 0);
            _split.Panel2.Padding = new Padding(0, Ui.S(1), 0, 0);

            Controls.Add(_split);
            Controls.Add(header);

            _ws.CollectionChanged += OnWorkspaceCollectionChanged;
            _ws.StoreReplaced += (s, e) =>
            {
                _searchCache.Clear();
                RefreshAllNodes();
                PopulateItems();
            };
            RefreshAllNodes();
        }

        public SolutionDef Solution { get; }

        public event EventHandler SelectionChanged;

        public CollectionDef SelectedCollection => _tree.SelectedNode?.Tag as CollectionDef;

        public JsonMap SelectedItem => _items.SelectedItems.Count > 0 ? _items.SelectedItems[0].Tag as JsonMap : null;

        public int ShownCount => _shown.Count;

        public bool HasSelectedItem => _items.SelectedItems.Count > 0;

        public string StatusText
        {
            get
            {
                var def = SelectedCollection;
                if (def == null) return "";
                var r = _ws.Store.Get(def.Key);
                if (r.Items.Count == 0) return "";
                var text = _shown.Count == r.Items.Count ? Ui.Plural(r.Items.Count, "item") : $"{_shown.Count:N0} of {Ui.Plural(r.Items.Count, "item")}";
                if (_items.SelectedItems.Count > 0) text += $", {_items.SelectedItems.Count:N0} selected";
                return text;
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_layoutApplied || Width < Ui.S(500) || Height < Ui.S(300)) return;
            _layoutApplied = true;
            _split.Panel1MinSize = Ui.S(120);
            _split.SplitterDistance = Ui.S(230);
            _rightSplit.Panel1MinSize = Ui.S(120);
            _rightSplit.Panel2MinSize = Ui.S(80);
            _rightSplit.SplitterDistance = Math.Max(Ui.S(160), (int)(_rightSplit.Height * 0.58));
        }

        /// <summary>Called when the tab becomes visible: selects a collection and queues the tab's collections for loading.</summary>
        public void Activate()
        {
            if (_tree.SelectedNode == null && _tree.Nodes.Count > 0) _tree.SelectedNode = _tree.Nodes[0];
            var selected = SelectedCollection;
            var keys = Solution.Collections.Select(c => c.Key).ToList();
            if (selected != null)
            {
                keys.Remove(selected.Key);
                keys.Insert(0, selected.Key);
            }
            _ws.Enqueue(keys);
        }

        public void SelectCollection(string key)
        {
            if (_nodes.TryGetValue(key, out var node)) _tree.SelectedNode = node;
        }

        public void SelectItemAt(int index)
        {
            if (index < 0 || index >= _items.Items.Count) return;
            _items.SelectedItems.Clear();
            _items.Items[index].Selected = true;
            _items.Items[index].Focused = true;
            _items.EnsureVisible(index);
            UpdateDetails();
        }

        public void FocusFilter()
        {
            _filter.Focus();
            _filter.SelectAll();
        }

        /// <summary>Sets the filter text and applies it immediately.</summary>
        public void SetFilter(string text)
        {
            _filter.Text = text ?? "";
            PopulateItems();
        }

        internal string MessageText => _message.Visible ? _message.Text : null;

        public void RefreshSelected()
        {
            var def = SelectedCollection;
            if (def != null) _ws.Enqueue(new[] { def.Key }, force: true, front: true);
        }

        public void RefreshTab() => _ws.Enqueue(Solution.Collections.Select(c => c.Key), force: true);

        /// <summary>Copies the focused list's selection (or the filter box text) to the clipboard.</summary>
        public void Copy()
        {
            if (ActiveControlIsTextBox(out var tb))
            {
                tb.Copy();
                return;
            }
            var list = _props.Focused ? _props : _items;
            if (list.SelectedItems.Count == 0) return;
            Clipboard.SetText(list.SelectedRowsAsText(includeHeader: list == _items));
        }

        public void SelectAll()
        {
            if (ActiveControlIsTextBox(out var tb))
            {
                tb.SelectAll();
                return;
            }
            (_props.Focused ? _props : _items).SelectAll();
        }

        private bool ActiveControlIsTextBox(out TextBox textBox)
        {
            textBox = FindForm()?.ActiveControl as TextBox ?? (_filter.Focused ? _filter : null);
            return textBox != null && textBox.Focused;
        }

        public void ExportCsv(AppSettings settings)
        {
            var def = SelectedCollection;
            if (def == null || _shown.Count == 0) return;
            var org = _ws.Store.Source.DisplayName;
            var name = $"{Solution.Name} - {def.Name}{(string.IsNullOrEmpty(org) ? "" : " - " + org)} - {DateTime.Now:yyyy-MM-dd}.csv";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            using (var dlg = new SaveFileDialog
            {
                Title = "Export to CSV",
                Filter = "CSV (comma-separated values) (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = name,
                InitialDirectory = settings.LastSnapshotFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true,
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    CsvExport.Write(dlg.FileName, def, _shown);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Ui.ShowError(FindForm(), "Couldn't export the list:\r\n\r\n" + ex.Message);
                }
            }
        }

        public void ShowItemProperties()
        {
            var def = SelectedCollection;
            var item = SelectedItem;
            if (def == null || item == null) return;
            using (var dlg = new ItemPropertiesDialog(def, item, _ws.Store))
                dlg.ShowDialog(FindForm());
        }

        private void ShowPropertyValue()
        {
            if (_props.SelectedItems.Count == 0 || !(_props.SelectedItems[0].Tag is KeyValuePair<string, object> kv)) return;
            ValueDialog.ShowValue(FindForm(), kv.Key, kv.Value);
        }

        private void ShowCollectionMessage()
        {
            var def = SelectedCollection;
            if (def == null) return;
            var r = _ws.Store.Get(def.Key);
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(r.Message)) text.Append(r.Message);
            if (r.Warnings.Count > 0)
            {
                if (text.Length > 0) text.Append("\r\n\r\n");
                text.Append("Warnings:\r\n").Append(string.Join("\r\n", r.Warnings.Distinct()));
            }
            ValueDialog.ShowValue(FindForm(), def.Name + " \u2013 messages", text.ToString());
        }

        private ContextMenu CreateItemsMenu()
        {
            var props = new MenuItem("P&roperties", (s, e) => ShowItemProperties()) { DefaultItem = true };
            var copy = new MenuItem("&Copy", (s, e) => Copy());
            var selectAll = new MenuItem("Select &All", (s, e) => _items.SelectAll());
            var export = new MenuItem("&Export List to CSV...", (s, e) => ExportCsv(Program.Settings));
            var refresh = new MenuItem("Re&fresh", (s, e) => RefreshSelected());
            var menu = new ContextMenu(new[] { props, new MenuItem("-"), copy, selectAll, new MenuItem("-"), export, new MenuItem("-"), refresh });
            menu.Popup += (s, e) =>
            {
                props.Enabled = HasSelectedItem;
                copy.Enabled = HasSelectedItem;
                export.Enabled = _shown.Count > 0;
                refresh.Enabled = _ws.IsConnected;
            };
            return menu;
        }

        private ContextMenu CreatePropsMenu()
        {
            var view = new MenuItem("&View Value...", (s, e) => ShowPropertyValue()) { DefaultItem = true };
            var copy = new MenuItem("&Copy", (s, e) =>
            {
                if (_props.SelectedItems.Count > 0) Clipboard.SetText(_props.SelectedRowsAsText(includeHeader: false));
            });
            var copyValue = new MenuItem("Copy &Value", (s, e) =>
            {
                if (_props.SelectedItems.Count > 0 && _props.SelectedItems[0].Tag is KeyValuePair<string, object> kv)
                {
                    var text = ValueFormatter.ToLongText(kv.Value);
                    if (text.Length > 0) Clipboard.SetText(text);
                }
            });
            var menu = new ContextMenu(new[] { view, new MenuItem("-"), copy, copyValue });
            menu.Popup += (s, e) =>
            {
                bool any = _props.SelectedItems.Count > 0;
                view.Enabled = copy.Enabled = copyValue.Enabled = any;
            };
            return menu;
        }

        private void OnWorkspaceCollectionChanged(object sender, string key)
        {
            if (!_nodes.ContainsKey(key)) return;
            UpdateNode(key);
            if (string.Equals(SelectedCollection?.Key, key, StringComparison.OrdinalIgnoreCase)) PopulateItems();
        }

        private void RefreshAllNodes()
        {
            _tree.BeginUpdate();
            foreach (var key in _nodes.Keys.ToList()) UpdateNode(key);
            _tree.EndUpdate();
        }

        private void UpdateNode(string key)
        {
            var node = _nodes[key];
            var def = (CollectionDef)node.Tag;
            var r = _ws.Store.Contains(key) ? _ws.Store.Get(key) : null;
            var state = r?.State ?? LoadState.NotLoaded;
            string text = def.Name;
            string image = "Folder";
            Color color = SystemColors.WindowText;
            switch (state)
            {
                case LoadState.Queued:
                    text += r.Items.Count > 0 ? $" ({r.Items.Count:N0})" : "";
                    color = SystemColors.GrayText;
                    break;
                case LoadState.Loading:
                    text += " (loading\u2026)";
                    break;
                case LoadState.Loaded:
                    text += $" ({r.Items.Count:N0})";
                    if (!string.IsNullOrEmpty(r.Message)) image = "Warning";
                    break;
                case LoadState.Unavailable:
                    text += " (not available)";
                    color = SystemColors.GrayText;
                    break;
                case LoadState.Failed:
                    text += " (error)";
                    image = "Error";
                    break;
                default:
                    color = SystemColors.GrayText;
                    break;
            }
            if (node.Text != text) node.Text = text;
            node.ImageKey = image;
            node.SelectedImageKey = image == "Folder" ? "FolderOpen" : image;
            node.ForeColor = color;
        }

        private void OnCollectionSelected()
        {
            _items.ResetSort();
            PopulateItems();
            var def = SelectedCollection;
            if (def != null) _ws.Enqueue(new[] { def.Key }, front: true);
        }

        private void PopulateItems()
        {
            var def = SelectedCollection;
            var selectedTitles = new HashSet<string>(_items.SelectedItems.Cast<ListViewItem>().Select(i => ItemHelper.Title(i.Tag as JsonMap)));
            bool sameCollection = Equals(_items.Tag, def);
            int topIndex = sameCollection && _items.TopItem != null ? _items.TopItem.Index : 0;

            _items.BeginUpdate();
            try
            {
                _items.Items.Clear();
                _items.Tag = def;
                _shown = new List<JsonMap>();
                if (def == null)
                {
                    _items.Columns.Clear();
                    ShowMessage("");
                    return;
                }

                var r = _ws.Store.Get(def.Key);
                var all = r.Items;
                _listTitle.Text = def.Name;
                _listInfo.Text = BuildInfoText(def, r);
                UpdateInfoBar(r);

                var ordered = ItemHelper.Order(def, all);
                var filter = _filter.Text.Trim();
                _shown = filter.Length == 0 ? ordered : ordered.Where(i => SearchText(i).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                var columns = ItemHelper.DisplayColumns(def, all);
                if (!sameCollection || !ColumnsMatch(columns)) RebuildColumns(columns, all);

                var parents = ItemHelper.BuildParentLookup(def, all);
                bool indent = def.ParentProperty != null && filter.Length == 0 && _items.ListViewItemSorter == null;
                var rows = new List<ListViewItem>(_shown.Count);
                foreach (var item in _shown)
                {
                    var cells = columns.Select(c => c == ItemHelper.ParentColumn
                        ? (parents.TryGetValue(item, out var p) ? ItemHelper.Title(p) : "")
                        : ValueFormatter.ToShortText(item[c])).ToArray();
                    var row = new ListViewItem(cells, 0) { Tag = item };
                    if (indent) row.IndentCount = ItemHelper.Depth(item, parents);
                    if (selectedTitles.Contains(ItemHelper.Title(item))) row.Selected = true;
                    rows.Add(row);
                }
                _items.Items.AddRange(rows.ToArray());
                if (_items.ListViewItemSorter != null) _items.Sort();

                ShowMessage(BuildMessage(def, r, all.Count, _shown.Count));
                if (_items.Items.Count > 0 && sameCollection && topIndex > 0 && topIndex < _items.Items.Count)
                    _items.TopItem = _items.Items[topIndex];
            }
            finally
            {
                _items.EndUpdate();
            }
            UpdateDetails();
        }

        private bool ColumnsMatch(List<string> columns) =>
            _items.Columns.Count == columns.Count && columns.Select((c, i) => _items.Columns[i].Text == c).All(b => b);

        private void RebuildColumns(List<string> columns, List<JsonMap> items)
        {
            _items.Columns.Clear();
            var sample = items.Take(200).ToList();
            foreach (var c in columns)
            {
                int width = TextRenderer.MeasureText(c, _items.Font).Width + Ui.S(24);
                foreach (var item in sample)
                {
                    var text = c == ItemHelper.ParentColumn ? "Parent label name" : ValueFormatter.ToShortText(item[c]);
                    if (text.Length > 80) text = text.Substring(0, 80);
                    width = Math.Max(width, TextRenderer.MeasureText(text, _items.Font).Width + Ui.S(16));
                }
                int min = _items.Columns.Count == 0 ? Ui.S(160) : Ui.S(60);
                if (_items.Columns.Count == 0) width += SystemInformation.SmallIconSize.Width + Ui.S(8);
                _items.Columns.Add(c, Math.Max(min, Math.Min(width, Ui.S(360))));
            }
        }

        private string BuildInfoText(CollectionDef def, CollectionResult r)
        {
            var parts = new List<string> { r.CommandLine ?? def.CommandLine };
            if (r.RetrievedUtc.HasValue && (r.State == LoadState.Loaded || r.State == LoadState.Failed || r.State == LoadState.Unavailable))
                parts.Add("retrieved " + r.RetrievedUtc.Value.ToLocalTime().ToString("g"));
            if (def.Undocumented) parts.Add("undocumented cmdlet");
            return string.Join("  \u00b7  ", parts);
        }

        private void UpdateInfoBar(CollectionResult r)
        {
            bool show = r.State == LoadState.Loaded && (!string.IsNullOrEmpty(r.Message) || r.Warnings.Count > 0);
            if (show)
            {
                var first = (r.Message ?? r.Warnings.FirstOrDefault() ?? "").Split('\n')[0].Trim();
                if (first.Length > 160) first = first.Substring(0, 160) + "\u2026";
                var label = string.IsNullOrEmpty(r.Message) ? "Completed with warnings: " : "Completed with errors: ";
                var link = "Details...";
                _infoBar.Text = "      " + label + first + "  " + link;
                _infoBar.LinkArea = new LinkArea(_infoBar.Text.Length - link.Length, link.Length);
            }
            _infoBar.Visible = show;
        }

        private string BuildMessage(CollectionDef def, CollectionResult r, int total, int shown)
        {
            switch (r.State)
            {
                case LoadState.Loaded:
                    if (total == 0) return $"No items.\r\n\r\n{def.Cmdlet} returned nothing, so this isn't configured in the tenant.";
                    if (shown == 0) return "No items match the filter.";
                    return null;
                case LoadState.Queued:
                    return total > 0 ? null : "Waiting to load\u2026";
                case LoadState.Loading:
                    return total > 0 ? null : $"Loading\u2026\r\n\r\nRunning {def.CommandLine}";
                case LoadState.Unavailable:
                    return r.Message ?? $"{def.Cmdlet} isn't available.";
                case LoadState.Failed:
                    return "Couldn't retrieve this collection.\r\n\r\n" + (r.Message ?? "");
                default:
                    if (_ws.IsConnected) return "Not loaded yet. Press F5 to load it.";
                    switch (_ws.Store.Source.Kind)
                    {
                        case SourceKind.Snapshot:
                        case SourceKind.Demo:
                            return "This collection isn't included in the snapshot.";
                        case SourceKind.Live:
                            return "Not loaded before disconnecting. Connect again to load it.";
                        default:
                            return "Not connected.\r\n\r\nChoose File > Connect to Tenant to view your configuration,\r\nor File > Open Snapshot to view a saved one.";
                    }
            }
        }

        private void ShowMessage(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            _message.Text = text ?? "";
            _message.Visible = show;
            _items.Visible = !show;
        }

        private string SearchText(JsonMap item)
        {
            if (_searchCache.TryGetValue(item, out var s)) return s;
            var sb = new StringBuilder();
            foreach (var kv in item) sb.Append(kv.Key).Append('=').Append(ValueFormatter.ToFlatText(kv.Value)).Append('\n');
            s = sb.ToString();
            _searchCache[item] = s;
            return s;
        }

        private void ScheduleDetails()
        {
            if (_detailsPending || !IsHandleCreated) return;
            _detailsPending = true;
            BeginInvoke((Action)(() =>
            {
                _detailsPending = false;
                UpdateDetails();
            }));
        }

        private void UpdateDetails()
        {
            var item = SelectedItem;
            _props.BeginUpdate();
            try
            {
                _props.Items.Clear();
                if (item == null)
                {
                    _propsTitle.Text = "Properties";
                }
                else
                {
                    _propsTitle.Text = _items.SelectedItems.Count > 1
                        ? $"Properties of {ItemHelper.Title(item)} (first of {_items.SelectedItems.Count:N0} selected)"
                        : "Properties of " + ItemHelper.Title(item);
                    var rows = item.Select(kv => new ListViewItem(new[] { kv.Key, ValueFormatter.ToShortText(kv.Value) }) { Tag = kv }).ToArray();
                    _props.Items.AddRange(rows);
                }
            }
            finally
            {
                _props.EndUpdate();
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
