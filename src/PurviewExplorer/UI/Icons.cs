// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.UI
{
    /// <summary>Shell stock icons (the same icons Explorer uses), cached per size.</summary>
    internal static class Icons
    {
        private static readonly Dictionary<(StockIcon, bool), Icon> Cache = new Dictionary<(StockIcon, bool), Icon>();

        public static Icon Get(StockIcon id, bool small = true)
        {
            if (Cache.TryGetValue((id, small), out var icon)) return icon;
            var info = new NativeMethods.SHSTOCKICONINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.SHSTOCKICONINFO)) };
            int hr = NativeMethods.SHGetStockIconInfo((int)id, NativeMethods.SHGSI_ICON | (small ? NativeMethods.SHGSI_SMALLICON : NativeMethods.SHGSI_LARGEICON), ref info);
            if (hr == 0 && info.hIcon != IntPtr.Zero)
            {
                icon = (Icon)Icon.FromHandle(info.hIcon).Clone();
                NativeMethods.DestroyIcon(info.hIcon);
            }
            else
            {
                icon = small ? SystemIcons.Application : SystemIcons.Application;
            }
            Cache[(id, small)] = icon;
            return icon;
        }

        public static Bitmap Bitmap(StockIcon id, bool small = true) => Get(id, small).ToBitmap();

        /// <summary>The application icon at the requested size.</summary>
        public static Icon App(int size)
        {
            using (var stream = typeof(Icons).Assembly.GetManifestResourceStream("PurviewExplorer.AppIcon.ico"))
                return stream == null ? SystemIcons.Application : new Icon(stream, size, size);
        }

        /// <summary>Creates a small-icon image list whose keys are the <see cref="StockIcon"/> names.</summary>
        public static ImageList CreateSmallImageList(params StockIcon[] ids)
        {
            var list = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = SystemInformation.SmallIconSize };
            foreach (var id in ids)
                if (!list.Images.ContainsKey(id.ToString()))
                    list.Images.Add(id.ToString(), Get(id, true));
            return list;
        }

        public static IEnumerable<StockIcon> SolutionIcons()
        {
            yield return StockIcon.Info;
            foreach (var s in PurviewCatalog.Solutions) yield return s.Icon;
        }
    }
}
