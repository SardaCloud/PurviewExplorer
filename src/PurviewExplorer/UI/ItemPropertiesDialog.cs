// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Catalog;
using SardaCloud.PurviewExplorer.Data;

namespace SardaCloud.PurviewExplorer.UI
{
    /// <summary>Property sheet for one item: all properties, related items (e.g. a policy's rules) and the raw JSON.</summary>
    internal sealed class ItemPropertiesDialog : Form
    {
        private readonly DataStore _store;

        public ItemPropertiesDialog(CollectionDef def, JsonMap item, DataStore store)
        {
            _store = store;
            Text = ItemHelper.Title(item) + " Properties";
            Font = Ui.DialogFont;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            MaximizeBox = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            Size = Ui.S(720, 600);
            MinimumSize = Ui.S(480, 380);
            Icon = Icons.Get(def.Solution.Icon);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // General
            var general = new TabPage("General") { Padding = new Padding(Ui.S(8)) };
            var summary = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 0, Ui.S(8)) };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var picture = new PictureBox { Image = Icons.Get(def.Solution.Icon, false).ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 0, Ui.S(12), 0) };
            var title = new Label { Text = ItemHelper.Title(item), Font = Ui.TitleFont, AutoSize = true, Anchor = AnchorStyles.Left, UseMnemonic = false };
            summary.Controls.Add(picture, 0, 0);
            summary.Controls.Add(title, 1, 0);
            var typeLabel = new Label
            {
                Text = $"{def.Solution.Name} \u2013 {def.Name}\r\nSource: {def.Cmdlet}",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, Ui.S(4), 0, 0),
                UseMnemonic = false,
            };
            summary.Controls.Add(typeLabel, 1, 1);
            var props = CreatePropertyList(item);
            general.Controls.Add(props);
            general.Controls.Add(new Label { Dock = DockStyle.Top, Height = Ui.S(6) });
            general.Controls.Add(summary);
            tabs.TabPages.Add(general);

            // Related collections
            foreach (var relation in def.Relations)
            {
                var childDef = PurviewCatalog.Find(relation.CollectionKey);
                if (childDef == null || !store.Contains(childDef.Key)) continue;
                var childResult = store.Get(childDef.Key);
                if (childResult.State != LoadState.Loaded) continue;
                var related = ItemHelper.FindRelated(item, childResult.Items, relation);
                if (related.Count == 0 && relation.CollectionKey == def.Key) continue;
                tabs.TabPages.Add(CreateRelatedPage(relation.Title, childDef, related));
            }

            // JSON
            var jsonPage = new TabPage("JSON") { Padding = new Padding(Ui.S(8)) };
            var json = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = Ui.MonoFont,
                BackColor = SystemColors.Window,
                Text = ValueFormatter.NormalizeNewLines(Json.Serialize(item, true)),
            };
            jsonPage.Controls.Add(json);
            tabs.TabPages.Add(jsonPage);

            // Buttons
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = Ui.S(80, 26), Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            var copy = new Button { Text = "&Copy JSON", Size = Ui.S(100, 26), Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            copy.Click += (s, e) => Clipboard.SetText(json.Text);
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(42) };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(copy);
            buttons.Layout += (s, e) =>
            {
                ok.Location = new Point(buttons.ClientSize.Width - ok.Width - Ui.S(10), Ui.S(8));
                copy.Location = new Point(Ui.S(10), Ui.S(8));
            };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(8), Ui.S(8), Ui.S(8), 0) };
            body.Controls.Add(tabs);
            Controls.Add(body);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = ok;
        }

        private ListViewEx CreatePropertyList(JsonMap item)
        {
            var list = new ListViewEx { Dock = DockStyle.Fill, GridLines = true };
            list.Columns.Add("Property", Ui.S(200));
            list.Columns.Add("Value", Ui.S(420));
            list.Items.AddRange(item.Select(kv => new ListViewItem(new[] { kv.Key, ValueFormatter.ToShortText(kv.Value) }) { Tag = kv }).ToArray());
            list.ItemActivate += (s, e) =>
            {
                if (list.SelectedItems.Count > 0 && list.SelectedItems[0].Tag is KeyValuePair<string, object> kv)
                    ValueDialog.ShowValue(this, kv.Key, kv.Value);
            };
            list.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C && list.SelectedItems.Count > 0)
                {
                    Clipboard.SetText(list.SelectedRowsAsText(includeHeader: false));
                    e.Handled = true;
                }
            };
            return list;
        }

        private TabPage CreateRelatedPage(string title, CollectionDef childDef, List<JsonMap> related)
        {
            var page = new TabPage($"{title} ({related.Count})") { Padding = new Padding(Ui.S(8)) };
            if (related.Count == 0)
            {
                page.Controls.Add(new Label { Text = "No related items.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SystemColors.GrayText });
                return page;
            }
            var columns = ItemHelper.DisplayColumns(childDef, related).Where(c => c != ItemHelper.ParentColumn).ToList();
            var list = new ListViewEx { Dock = DockStyle.Fill, MultiSelect = false };
            list.SmallImageList = Icons.CreateSmallImageList(childDef.Solution.Icon);
            foreach (var c in columns) list.Columns.Add(c, c == columns[0] ? Ui.S(220) : Ui.S(110));
            foreach (var r in ItemHelper.Order(childDef, related))
                list.Items.Add(new ListViewItem(columns.Select(c => ValueFormatter.ToShortText(r[c])).ToArray(), 0) { Tag = r });
            list.ItemActivate += (s, e) =>
            {
                if (list.SelectedItems.Count == 0) return;
                using (var dlg = new ItemPropertiesDialog(childDef, (JsonMap)list.SelectedItems[0].Tag, _store))
                    dlg.ShowDialog(this);
            };
            var hint = new Label { Text = "Double-click an item to see its properties.", Dock = DockStyle.Bottom, Height = Ui.S(22), TextAlign = ContentAlignment.BottomLeft, ForeColor = SystemColors.GrayText };
            page.Controls.Add(list);
            page.Controls.Add(hint);
            return page;
        }
    }

    /// <summary>Shows a single (possibly long) value, pretty-printing JSON and XML.</summary>
    internal sealed class ValueDialog : Form
    {
        private ValueDialog(string title, string text)
        {
            Text = title;
            Font = Ui.DialogFont;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Size = Ui.S(680, 480);
            MinimumSize = Ui.S(360, 240);
            SizeGripStyle = SizeGripStyle.Show;
            ShowIcon = false;

            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = Ui.MonoFont,
                BackColor = SystemColors.Window,
                Text = text,
            };
            var wrap = new CheckBox { Text = "&Word wrap", AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom };
            wrap.CheckedChanged += (s, e) =>
            {
                box.WordWrap = wrap.Checked;
                box.ScrollBars = wrap.Checked ? ScrollBars.Vertical : ScrollBars.Both;
            };
            var copy = new Button { Text = "&Copy", Size = Ui.S(80, 26), Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            copy.Click += (s, e) =>
            {
                if (box.TextLength > 0) Clipboard.SetText(box.SelectionLength > 0 ? box.SelectedText : box.Text);
            };
            var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Size = Ui.S(80, 26), Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = Ui.S(42) };
            buttons.Controls.AddRange(new Control[] { wrap, copy, close });
            buttons.Layout += (s, e) =>
            {
                close.Location = new Point(buttons.ClientSize.Width - close.Width - Ui.S(10), Ui.S(8));
                copy.Location = new Point(close.Left - copy.Width - Ui.S(8), Ui.S(8));
                wrap.Location = new Point(Ui.S(10), Ui.S(12));
            };
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Ui.S(8), Ui.S(8), Ui.S(8), 0) };
            body.Controls.Add(box);
            Controls.Add(body);
            Controls.Add(buttons);
            CancelButton = close;
            Shown += (s, e) =>
            {
                box.SelectionStart = 0;
                box.SelectionLength = 0;
            };
        }

        public static void ShowValue(IWin32Window owner, string title, object value)
        {
            var text = value is string s && s.Length == 0 ? "" : ValueFormatter.ToLongText(value);
            using (var dlg = new ValueDialog(title, text.Length == 0 ? "(empty)" : text))
                dlg.ShowDialog(owner);
        }
    }
}
