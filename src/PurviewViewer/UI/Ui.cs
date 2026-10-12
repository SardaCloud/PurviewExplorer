// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SardaCloud.PurviewViewer.UI
{
    internal static class Ui
    {
        public static readonly float ScaleFactor = ComputeScale();

        public static int S(int pixels) => (int)Math.Round(pixels * ScaleFactor);

        public static Size S(int width, int height) => new Size(S(width), S(height));

        public static Font DialogFont { get; } = SystemFonts.MessageBoxFont;

        public static Font BoldFont { get; } = new Font(DialogFont, FontStyle.Bold);

        public static Font TitleFont { get; } = new Font(DialogFont.FontFamily, DialogFont.SizeInPoints + 2, FontStyle.Bold);

        public static Font MonoFont { get; } = CreateMonoFont();

        private static float ComputeScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                return Math.Max(1f, g.DpiX / 96f);
        }

        private static Font CreateMonoFont()
        {
            var f = new Font("Consolas", 9.75f);
            return f.Name == "Consolas" ? f : new Font(FontFamily.GenericMonospace, 9.75f);
        }

        /// <summary>White header band with icon, title and description, like classic wizard/property sheet headers.</summary>
        public static Panel CreateHeader(Icon icon, string title, string description, string learnMoreUrl = null)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = S(62), BackColor = SystemColors.Window };
            panel.Paint += (s, e) =>
            {
                var r = panel.ClientRectangle;
                e.Graphics.DrawLine(SystemPens.ControlDark, 0, r.Bottom - 2, r.Right, r.Bottom - 2);
                e.Graphics.DrawLine(SystemPens.ControlLightLight, 0, r.Bottom - 1, r.Right, r.Bottom - 1);
            };
            var picture = new PictureBox
            {
                Image = icon.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Location = new Point(S(12), S(8)),
                Size = new Size(Math.Max(icon.Width, S(32)) + S(4), S(44)),
            };
            var titleLabel = new Label
            {
                Text = title,
                Font = TitleFont,
                AutoSize = true,
                Location = new Point(picture.Right + S(8), S(9)),
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
            var descLabel = new Label
            {
                Text = description,
                AutoSize = true,
                Location = new Point(picture.Right + S(10), S(34)),
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
            panel.Controls.Add(picture);
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(descLabel);
            if (!string.IsNullOrEmpty(learnMoreUrl))
            {
                var link = new LinkLabel
                {
                    Text = "Learn more",
                    AutoSize = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BackColor = Color.Transparent,
                };
                link.LinkClicked += (s, e) => OpenUrl(learnMoreUrl);
                panel.Controls.Add(link);
                panel.Layout += (s, e) => link.Location = new Point(panel.ClientSize.Width - link.Width - S(12), S(10));
            }
            return panel;
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                MessageBox.Show("Couldn't open " + url + "\r\n\r\n" + ex.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static void ShowError(IWin32Window owner, string text, string caption = null) =>
            MessageBox.Show(owner, text, caption ?? AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);

        public static void ShowInfo(IWin32Window owner, string text, string caption = null) =>
            MessageBox.Show(owner, text, caption ?? AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);

        /// <summary>Circular-arrow "refresh" glyph, drawn at any size (no stock icon exists for it).</summary>
        public static Bitmap RefreshGlyph(int size, bool twoArrows = false)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float pad = size * 0.16f;
                var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
                float width = Math.Max(1.5f, size / 8f);
                var color = Color.FromArgb(0, 128, 0);
                using (var pen = new Pen(color, width))
                {
                    if (twoArrows)
                    {
                        g.DrawArc(pen, rect, 200, 120);
                        g.DrawArc(pen, rect, 20, 120);
                        DrawArrowHead(g, color, rect, 320, size);
                        DrawArrowHead(g, color, rect, 140, size);
                    }
                    else
                    {
                        g.DrawArc(pen, rect, 300, 300);
                        DrawArrowHead(g, color, rect, 240, size);
                    }
                }
            }
            return bmp;
        }

        private static void DrawArrowHead(Graphics g, Color color, RectangleF rect, float angleDegrees, int size)
        {
            double a = angleDegrees * Math.PI / 180;
            float cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
            float r = rect.Width / 2;
            var tip = new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)));
            // tangent direction (clockwise)
            var dir = new PointF((float)-Math.Sin(a), (float)Math.Cos(a));
            var normal = new PointF((float)Math.Cos(a), (float)Math.Sin(a));
            float len = size * 0.30f;
            var p1 = new PointF(tip.X + dir.X * len * 0.6f, tip.Y + dir.Y * len * 0.6f);
            var p2 = new PointF(tip.X - dir.X * len * 0.2f + normal.X * len * 0.55f, tip.Y - dir.Y * len * 0.2f + normal.Y * len * 0.55f);
            var p3 = new PointF(tip.X - dir.X * len * 0.2f - normal.X * len * 0.55f, tip.Y - dir.Y * len * 0.2f - normal.Y * len * 0.55f);
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, new[] { p1, p2, p3 });
        }

        public static string Plural(int count, string singular, string plural = null) =>
            count.ToString("N0") + " " + (count == 1 ? singular : plural ?? singular + "s");
    }
}
