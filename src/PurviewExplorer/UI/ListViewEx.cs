// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Data;

namespace SardaCloud.PurviewExplorer.UI
{
    /// <summary>Details-view ListView with flicker-free drawing, click-to-sort columns and copy support.</summary>
    internal class ListViewEx : ListView
    {
        private int _sortColumn = -1;
        private SortOrder _sortOrder = SortOrder.None;

        public ListViewEx()
        {
            View = View.Details;
            FullRowSelect = true;
            HideSelection = false;
            DoubleBuffered = true;
            AllowColumnReorder = true;
        }

        /// <summary>When false, clicking a column header doesn't sort (e.g. for hierarchical lists in natural order).</summary>
        public bool SortOnColumnClick { get; set; } = true;

        public int SortColumn => _sortColumn;

        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);
            if (!SortOnColumnClick) return;
            if (_sortColumn == e.Column)
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : _sortOrder == SortOrder.Descending ? SortOrder.None : SortOrder.Ascending;
            else
            {
                _sortColumn = e.Column;
                _sortOrder = SortOrder.Ascending;
            }
            ApplySort();
        }

        public void ResetSort()
        {
            _sortColumn = -1;
            _sortOrder = SortOrder.None;
            ListViewItemSorter = null;
            NativeMethods.SetSortArrow(this, -1, SortOrder.None);
        }

        public void ApplySort()
        {
            if (_sortColumn < 0 || _sortColumn >= Columns.Count || _sortOrder == SortOrder.None)
            {
                ListViewItemSorter = null;
                NativeMethods.SetSortArrow(this, -1, SortOrder.None);
                SortRequested?.Invoke(this, EventArgs.Empty); // owner restores natural order
                return;
            }
            ListViewItemSorter = new ItemComparer(_sortColumn, _sortOrder);
            Sort();
            NativeMethods.SetSortArrow(this, _sortColumn, _sortOrder);
        }

        /// <summary>Raised when sorting is turned off, so the owner can repopulate in natural order.</summary>
        public event EventHandler SortRequested;

        /// <summary>Tab-separated text of the selected rows (with a header line).</summary>
        public string SelectedRowsAsText(bool includeHeader = true)
        {
            var sb = new StringBuilder();
            var columns = Columns.Cast<ColumnHeader>().OrderBy(c => c.DisplayIndex).ToList();
            if (includeHeader) sb.AppendLine(string.Join("\t", columns.Select(c => c.Text)));
            foreach (ListViewItem item in SelectedItems)
                sb.AppendLine(string.Join("\t", columns.Select(c => c.Index < item.SubItems.Count ? item.SubItems[c.Index].Text : "")));
            return sb.ToString();
        }

        public void SelectAll()
        {
            BeginUpdate();
            foreach (ListViewItem item in Items) item.Selected = true;
            EndUpdate();
        }

        private sealed class ItemComparer : IComparer
        {
            private readonly int _column;
            private readonly SortOrder _order;

            public ItemComparer(int column, SortOrder order)
            {
                _column = column;
                _order = order;
            }

            public int Compare(object x, object y)
            {
                var a = (ListViewItem)x;
                var b = (ListViewItem)y;
                string ta = _column < a.SubItems.Count ? a.SubItems[_column].Text : "";
                string tb = _column < b.SubItems.Count ? b.SubItems[_column].Text : "";
                int r = ValueComparer.Instance.Compare(ta.Trim(), tb.Trim());
                return _order == SortOrder.Descending ? -r : r;
            }
        }
    }
}
