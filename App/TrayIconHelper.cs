using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    internal static class TrayIconHelper
    {
        private static Icon? _green;
        private static Icon? _red;

        public static Icon GreenDot => _green ??= CreateCircleIcon(Color.LimeGreen);
        public static Icon RedDot => _red ??= CreateCircleIcon(Color.Red);

        private static Icon CreateCircleIcon(Color color)
        {
            using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(color))
            using (var pen = new Pen(Color.FromArgb(160, 0, 0, 0), 1f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.FillEllipse(brush, 2, 2, 12, 12);
                g.DrawEllipse(pen, 2, 2, 12, 12);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var tmp = Icon.FromHandle(hIcon);
                return (Icon)tmp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
