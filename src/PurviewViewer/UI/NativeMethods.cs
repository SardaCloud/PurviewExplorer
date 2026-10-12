// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SardaCloud.PurviewViewer.UI
{
    internal static class NativeMethods
    {
        public const uint SHGSI_ICON = 0x000000100;
        public const uint SHGSI_LARGEICON = 0x000000000;
        public const uint SHGSI_SMALLICON = 0x000000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SHSTOCKICONINFO
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        public static extern int SHGetStockIconInfo(int siid, uint uFlags, ref SHSTOCKICONINFO psii);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        public const uint PW_RENDERFULLCONTENT = 0x00000002;

        // ListView header sort arrows
        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETHEADER = LVM_FIRST + 31;
        private const int HDM_FIRST = 0x1200;
        private const int HDM_GETITEM = HDM_FIRST + 11;
        private const int HDM_SETITEM = HDM_FIRST + 12;
        private const int HDI_FORMAT = 0x0004;
        private const int HDF_SORTUP = 0x0400;
        private const int HDF_SORTDOWN = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct HDITEM
        {
            public int mask;
            public int cxy;
            public IntPtr pszText;
            public IntPtr hbm;
            public int cchTextMax;
            public int fmt;
            public IntPtr lParam;
            public int iImage;
            public int iOrder;
            public uint type;
            public IntPtr pvFilter;
            public uint state;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessageHdItem(IntPtr hWnd, int msg, IntPtr wParam, ref HDITEM lParam);

        public static void SetSortArrow(ListView listView, int column, SortOrder order)
        {
            if (!listView.IsHandleCreated) return;
            IntPtr header = SendMessage(listView.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (header == IntPtr.Zero) return;
            for (int i = 0; i < listView.Columns.Count; i++)
            {
                var item = new HDITEM { mask = HDI_FORMAT };
                SendMessageHdItem(header, HDM_GETITEM, (IntPtr)i, ref item);
                item.fmt &= ~(HDF_SORTUP | HDF_SORTDOWN);
                if (i == column && order == SortOrder.Ascending) item.fmt |= HDF_SORTUP;
                else if (i == column && order == SortOrder.Descending) item.fmt |= HDF_SORTDOWN;
                SendMessageHdItem(header, HDM_SETITEM, (IntPtr)i, ref item);
            }
        }

        public static Bitmap CaptureWindow(Control control)
        {
            var bounds = control.Bounds;
            var bmp = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try { PrintWindow(control.Handle, hdc, PW_RENDERFULLCONTENT); }
                finally { g.ReleaseHdc(hdc); }
            }
            return bmp;
        }
    }
}
