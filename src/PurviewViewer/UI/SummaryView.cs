// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SardaCloud.PurviewViewer.Catalog;
using SardaCloud.PurviewViewer.Data;

namespace SardaCloud.PurviewViewer.UI
{
    /// <summary>First tab: the data source and the status of every collection, grouped by solution.</summary>
    internal sealed class SummaryView : UserControl
    {
        private readonly Workspace _ws;
        private readonly ListViewEx _list;
        private readonly Dictionary<string, Label> _values = new Dictionary<string, Label>();
        private readonly Dictionary<string, ListViewItem> _rows = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);

        public SummaryView(Workspace workspace)
        {
            _ws = workspace;
            Dock = DockStyle.Fill;

            var header = Ui.CreateHeader(Icons.Get(StockIcon.Info, false), "Summary",
                "Where the configuration comes from, and what was retrieved for each solution. Double-click a row to open it.");

            var sourceGroup = new GroupBox
            {
                Text = "Source",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(Ui.S(10), Ui.S(6), Ui.S(10), Ui.S(6)),
            };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var fields = new[] { "Source", "Organization", "Account", "Tenant ID", "Environment", "Taken", "File", "Module" };
            for (int i = 0; i < fields.Length; i++)
            {
                var label = new Label { Text = fields[i] + ":", AutoSize = true, Margin = new Padding(0, Ui.S(3), Ui.S(8), Ui.S(3)) };
                var value = new Label { AutoSize = true, AutoEllipsis = false, Margin = new Padding(0, Ui.S(3), Ui.S(24), Ui.S(3)), UseMnemonic = false };
                _values[fields[i]] = value;
                table.Controls.Add(label, (i % 2) * 2, i / 2);
                table.Controls.Add(value, (i % 2) * 2 + 1, i / 2);
            }
            sourceGroup.Controls.Add(table);

            _list = new ListViewEx { Dock = DockStyle.Fill, MultiSelect = false, ShowGroups = true };
            _list.SmallImageList = Icons.CreateSmallImageList(PurviewCatalog.Solutions.Select(s => s.Icon).Concat(new[] { StockIcon.Warning, StockIcon.Error }).ToArray());
            _list.Columns.Add("Collection", Ui.S(250));
            _list.Columns.Add("Items", Ui.S(60), HorizontalAlignment.Right);
            _list.Columns.Add("Status", Ui.S(130));
            _list.Columns.Add("Command", Ui.S(380));
            _list.Columns.Add("Retrieved", Ui.S(130));
            _list.Columns.Add("Seconds", Ui.S(64), HorizontalAlignment.Right);
            _list.SortOnColumnClick = false;
            foreach (var s in PurviewCatalog.Solutions)
            {
                var group = new ListViewGroup(s.Key, s.Name);
                _list.Groups.Add(group);
                foreach (var def in s.Collections)
                {
                    var row = new ListViewItem(new[] { def.Name, "", "", def.CommandLine, "", "" }, s.Icon.ToString()) { Group = group, Tag = def };
                    _rows[def.Key] = row;
                    _list.Items.Add(row);
                }
            }
            _list.ItemActivate += (s, e) =>
            {
                if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is CollectionDef def)
                    NavigateRequested?.Invoke(this, def.Key);
            };

            var listLabel = new Label { Text = "Collections", Font = Ui.BoldFont, Dock = DockStyle.Top, Height = Ui.S(26), TextAlign = ContentAlignment.BottomLeft };
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(8), Ui.S(8), Ui.S(8), Ui.S(8)) };
            body.Controls.Add(_list);
            body.Controls.Add(listLabel);
            body.Controls.Add(sourceGroup);

            Controls.Add(body);
            Controls.Add(header);

            _ws.StoreReplaced += (s, e) => RefreshAll();
            _ws.StateChanged += (s, e) => UpdateSource();
            _ws.CollectionChanged += (s, key) => UpdateRow(key);
            RefreshAll();
        }

        public event EventHandler<string> NavigateRequested;

        public CollectionDef SelectedCollection => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as CollectionDef : null;

        public string StatusText
        {
            get
            {
                var defs = PurviewCatalog.AllCollections.ToList();
                int loaded = defs.Count(d => _ws.Store.Contains(d.Key) && _ws.Store.Get(d.Key).State == LoadState.Loaded);
                int items = defs.Where(d => _ws.Store.Contains(d.Key)).Sum(d => _ws.Store.Get(d.Key).Items.Count);
                return $"{loaded} of {defs.Count} collections loaded, {Ui.Plural(items, "item")}";
            }
        }

        public void Copy()
        {
            if (_list.SelectedItems.Count > 0) Clipboard.SetText(_list.SelectedRowsAsText());
        }

        public void RefreshAll()
        {
            UpdateSource();
            _list.BeginUpdate();
            foreach (var key in _rows.Keys.ToList()) UpdateRow(key);
            _list.EndUpdate();
        }

        private void UpdateSource()
        {
            var src = _ws.Store.Source;
            string kind;
            switch (src.Kind)
            {
                case SourceKind.Live: kind = src.Connected && _ws.IsConnected ? "Connected (live)" : "Disconnected (data retrieved earlier)"; break;
                case SourceKind.Snapshot: kind = "Snapshot file"; break;
                case SourceKind.Demo: kind = "Demo data (fictional)"; break;
                default: kind = _ws.IsConnecting ? "Connecting\u2026" : "Not connected"; break;
            }
            Set("Source", kind);
            Set("Organization", src.Organization);
            Set("Account", src.Account);
            Set("Tenant ID", src.TenantId);
            Set("Environment", src.Environment);
            Set("Taken", src.Kind == SourceKind.Live || src.Kind == SourceKind.None
                ? (src.CreatedUtc.HasValue ? "connected " + src.CreatedUtc.Value.ToLocalTime().ToString("g") : null)
                : src.CreatedUtc?.ToLocalTime().ToString("g"));
            Set("File", src.FilePath);
            Set("Module", string.IsNullOrEmpty(src.ModuleVersion) ? null : "ExchangeOnlineManagement " + src.ModuleVersion);
        }

        private void Set(string field, string value) => _values[field].Text = string.IsNullOrEmpty(value) ? "\u2014" : value;

        private void UpdateRow(string key)
        {
            if (!_rows.TryGetValue(key, out var row)) return;
            var def = (CollectionDef)row.Tag;
            var r = _ws.Store.Contains(key) ? _ws.Store.Get(key) : new CollectionResult(key);
            string status;
            string image = def.Solution.Icon.ToString();
            switch (r.State)
            {
                case LoadState.Queued: status = "Queued"; break;
                case LoadState.Loading: status = "Loading\u2026"; break;
                case LoadState.Loaded:
                    status = string.IsNullOrEmpty(r.Message) ? (r.Warnings.Count > 0 ? "Loaded (warnings)" : "Loaded") : "Loaded with errors";
                    if (!string.IsNullOrEmpty(r.Message)) image = StockIcon.Warning.ToString();
                    break;
                case LoadState.Unavailable: status = "Not available"; break;
                case LoadState.Failed: status = "Error"; image = StockIcon.Error.ToString(); break;
                default: status = "Not loaded"; break;
            }
            bool hasCount = r.State == LoadState.Loaded || (r.Items.Count > 0 && r.RetrievedUtc.HasValue);
            row.SubItems[1].Text = hasCount ? r.Items.Count.ToString("N0") : "";
            row.SubItems[2].Text = status;
            row.SubItems[3].Text = r.CommandLine ?? def.CommandLine;
            row.SubItems[4].Text = r.RetrievedUtc.HasValue && r.State != LoadState.Loading && r.State != LoadState.Queued ? r.RetrievedUtc.Value.ToLocalTime().ToString("g") : "";
            row.SubItems[5].Text = r.DurationSeconds.HasValue ? r.DurationSeconds.Value.ToString("0.0") : "";
            row.ImageKey = image;
            row.ForeColor = r.State == LoadState.Loaded || r.State == LoadState.Failed || r.State == LoadState.Loading ? SystemColors.WindowText : SystemColors.GrayText;
        }
    }
}
