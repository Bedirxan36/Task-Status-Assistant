using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TrayStatusHelper
{
    internal static class IconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon Build(bool numOn, bool capsOn, bool fanOn)
        {
            // 16x16 tray icon
            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Dark mode uyumlu arka rozet
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 20, 20)))
            using (var bgPen = new Pen(Color.FromArgb(60, 60, 60)))
            {
                var rect = new Rectangle(0, 0, 15, 15);
                g.FillEllipse(bgBrush, rect);
                g.DrawEllipse(bgPen, rect);
            }

            // 3 durum noktası (üst: Caps, orta: Num, alt: Fan)
            DrawDot(g, 8, 4, capsOn);
            DrawDot(g, 8, 8, numOn);
            DrawDot(g, 8, 12, fanOn);

            // C / N / F küçük harf ipucu (çok minik, ama görsel his verir)
            using var font = new Font("Segoe UI", 5f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.FromArgb(220, 220, 220));

            g.DrawString("C", font, textBrush, new PointF(1.2f, 1.0f));
            g.DrawString("N", font, textBrush, new PointF(1.2f, 6.0f));
            g.DrawString("F", font, textBrush, new PointF(1.2f, 11.0f));

            // Bitmap -> Icon
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Icon.FromHandle(hIcon);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        private static void DrawDot(Graphics g, int cx, int cy, bool on)
        {
            var color = on ? Color.FromArgb(0, 200, 90) : Color.FromArgb(220, 60, 60);
            using var b = new SolidBrush(color);
            using var p = new Pen(Color.FromArgb(20, 20, 20), 1);

            var r = new Rectangle(cx - 2, cy - 2, 5, 5);
            g.FillEllipse(b, r);
            g.DrawEllipse(p, r);
        }
    }
}

