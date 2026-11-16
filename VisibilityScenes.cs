using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace NativeVcrdbCrosshair
{
    internal static class VisibilityScenes
    {
        internal const int Sky = 3, Dim = 4, Motion = 5;

        internal static void Paint(Graphics g, Rectangle frame, int scene)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            switch (scene)
            {
                case Sky:
                    FillGradient(g, frame, Color.FromArgb(98, 164, 218), Color.FromArgb(248, 238, 215));
                    Optics.Glow(g, new Rectangle(-54, 27, frame.Width * 3 / 4, frame.Height / 2), Color.White, 240);
                    Optics.Glow(g, new Rectangle(frame.Width / 3, frame.Height / 3, frame.Width, frame.Height / 2), Color.White, 200);
                    int ground = frame.Height * 4 / 5;
                    using (var ridge = new SolidBrush(Color.FromArgb(96, 119, 131)))
                        g.FillPolygon(ridge, new[] { new Point(0, ground), new Point(frame.Width / 5, ground - 34),
                            new Point(frame.Width / 3, ground - 2), new Point(frame.Width * 3 / 5, ground - 26),
                            new Point(frame.Width, ground + 15), new Point(frame.Width, frame.Height), new Point(0, frame.Height) });
                    break;

                case Dim:
                    FillGradient(g, frame, Color.FromArgb(45, 44, 50), Color.FromArgb(12, 16, 22));
                    Optics.Glow(g, new Rectangle(frame.Width / 8, 0, frame.Width * 3 / 4, frame.Height * 2 / 3),
                        Color.FromArgb(210, 153, 109), 103);
                    using (var wall = new SolidBrush(Color.FromArgb(31, 37, 43)))
                    using (var edge = new Pen(Color.FromArgb(79, 76, 74)))
                    {
                        int y = frame.Height * 2 / 3;
                        g.FillRectangle(wall, 0, y, frame.Width, frame.Height - y);
                        g.DrawLine(edge, 0, y, frame.Width, y);
                        g.DrawLine(edge, frame.Width / 3, y, frame.Width / 5, frame.Height);
                        g.DrawLine(edge, frame.Width * 2 / 3, y, frame.Width * 4 / 5, frame.Height);
                    }
                    break;

                case Motion:
                    FillGradient(g, frame, Color.FromArgb(87, 126, 138), Color.FromArgb(39, 44, 52));
                    Optics.Glow(g, new Rectangle(0, 0, frame.Width, frame.Height), Color.FromArgb(227, 181, 129), 150);
                    break;
            }
        }

        internal static void PaintMotion(Graphics g, Rectangle frame, float phase)
        {
            const int spacing = 148;
            int shift = (int)(phase / (2 * Math.PI) * spacing);
            int y = frame.Height * 3 / 5;
            using (var road = new SolidBrush(Color.FromArgb(50, 42, 49)))
            using (var shadow = new SolidBrush(Color.FromArgb(153, 33, 35, 48)))
            using (var marker = new Pen(Color.FromArgb(159, 235, 207, 154), 3))
            {
                g.FillRectangle(road, 0, y, frame.Width, frame.Height - y);
                for (int x = -spacing - shift; x < frame.Width; x += spacing)
                {
                    g.FillPolygon(shadow, new[] { new Point(x, y), new Point(x + 21, y - 72),
                        new Point(x + 63, y - 72), new Point(x + 107, y) });
                    g.DrawLine(marker, x, frame.Height * 4 / 5, x + 69, frame.Height * 4 / 5);
                }
            }
        }

        private static void FillGradient(Graphics g, Rectangle frame, Color top, Color bottom)
        {
            using (var brush = new LinearGradientBrush(frame, top, bottom, 90f))
                g.FillRectangle(brush, frame);
        }
    }
}
