using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NativeVcrdbCrosshair
{
    internal static class Optics
    {
        internal static readonly Color WarmInk = Color.FromArgb(253, 225, 212);
        private static readonly Color Shadow = Color.FromArgb(22, 12, 25);
        private static readonly Color Wine = Color.FromArgb(106, 30, 57);
        private static readonly Color PinkLight = Color.FromArgb(244, 133, 154);
        private static readonly Color Lavender = Color.FromArgb(138, 103, 200);

        internal static void Glow(Graphics g, Rectangle bounds, Color light, int alpha)
        {
            using (var shape = new GraphicsPath())
            {
                shape.AddEllipse(bounds);
                using (var glow = new PathGradientBrush(shape))
                {
                    glow.CenterColor = Color.FromArgb(alpha, light);
                    glow.SurroundColors = new[] { Color.FromArgb(0, light) };
                    g.FillPath(glow, shape);
                }
            }
        }

        internal static void PaintBackdrop(Graphics g, Rectangle area, int mode)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (mode == 1)
            {
                using (var a = new SolidBrush(Theme.BgCard))
                using (var b = new SolidBrush(Theme.BgPanel))
                {
                    g.FillRectangle(b, area);
                    for (int y = 0; y < area.Height; y += 20)
                        for (int x = y / 20 % 2 * 20; x < area.Width; x += 40) g.FillRectangle(a, x, y, 20, 20);
                }
                return;
            }
            if (mode == 2)
            {
                using (var fill = new LinearGradientBrush(area, Color.FromArgb(251, 241, 234), Color.FromArgb(171, 151, 168), 62f))
                    g.FillRectangle(fill, area);
                return;
            }

            bool gloss = Glass.Material == GlassMaterial.Gloss;
            bool technical = Glass.Material == GlassMaterial.Technical;
            using (var depth = new LinearGradientBrush(area, gloss ? Wine : Theme.BgPanelAlt, Shadow, 76f))
            {
                depth.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, 0.53f, 1f },
                    Colors = new[] { gloss ? Wine : Theme.BgPanelAlt, Shadow, Color.FromArgb(39, 25, 53) }
                };
                g.FillRectangle(depth, area);
            }
            if (gloss)
            {
                Glow(g, new Rectangle(-95, -112, area.Width + 71, area.Height + 53), PinkLight, 112);
                Glow(g, new Rectangle(area.Width / 2 - 70, area.Height / 2 - 6, area.Width, area.Height), Lavender, 91);
            }

            float cx = area.Width / 2f, cy = area.Height / 2f;
            float r = Math.Min(cx, cy) * 0.64f;
            var ring = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            if (gloss) PaintLens(g, ring);

            using (var guide = new Pen(Color.FromArgb(technical ? 89 : 47, WarmInk)))
            using (var rim = new Pen(Color.FromArgb(technical ? 169 : 101, WarmInk)))
            {
                guide.DashStyle = DashStyle.Dot;
                g.DrawLine(guide, 17, cy, area.Width - 17, cy);
                g.DrawLine(guide, cx, 57, cx, area.Height - 57);
                g.DrawEllipse(guide, ring.X - 15, ring.Y - 15, ring.Width + 30, ring.Height + 30);
                for (int i = 0; i < 48; i++)
                {
                    double angle = i * Math.PI / 24;
                    float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                    float inner = r + (i % 6 == 0 ? 7 : 12);
                    float outer = r + 18;
                    g.DrawLine(i % 6 == 0 ? rim : guide, cx + x * inner, cy + y * inner, cx + x * outer, cy + y * outer);
                }
                if (technical)
                {
                    for (float x = cx % 25; x < area.Width; x += 25) g.DrawLine(guide, x, 47, x, area.Height - 47);
                    for (float y = cy % 25; y < area.Height - 47; y += 25)
                        if (y >= 47) g.DrawLine(guide, 18, y, area.Width - 18, y);
                }
            }
        }

        private static void PaintLens(Graphics g, RectangleF ring)
        {
            var shade = ring;
            shade.Inflate(8, 8);
            shade.Offset(0, 12);
            Glow(g, Rectangle.Round(shade), Color.Black, 211);
            using (var plane = new LinearGradientBrush(ring, Color.FromArgb(113, 41, 69), Color.FromArgb(19, 15, 25), 74f))
            using (var rim = new LinearGradientBrush(ring, WarmInk, Color.FromArgb(59, 45, 78), 63f))
            using (var outer = new Pen(rim, 2.5f))
            {
                g.FillEllipse(plane, ring);
                g.DrawEllipse(outer, ring);
            }

            var bore = ring;
            bore.Inflate(-11, -11);
            using (var lens = new LinearGradientBrush(bore, Color.FromArgb(22, 15, 24), Color.FromArgb(58, 32, 55), 68f))
            using (var hairline = new Pen(Color.FromArgb(119, 226, 168, 181)))
            {
                g.FillEllipse(lens, bore);
                g.DrawEllipse(hairline, bore);
            }
            Glow(g, new Rectangle((int)bore.Left + 4, (int)bore.Top + 3, (int)(bore.Width * 0.84f), (int)(bore.Height * 0.46f)), PinkLight, 118);
            Glow(g, new Rectangle((int)bore.Left + 23, (int)(bore.Bottom - bore.Height * 0.39f), (int)(bore.Width * 0.78f), (int)(bore.Height * 0.41f)), Lavender, 86);

            using (var reflection = new Pen(Color.FromArgb(219, WarmInk), 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var reflected = new Pen(Color.FromArgb(145, PinkLight), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(reflection, ring, 209, 60);
                g.DrawArc(reflected, bore, 25, 41);
            }

            using (var edge = new Pen(Color.FromArgb(100, 246, 189, 200)))
            {
                bore.Inflate(-7, -7);
                g.DrawArc(edge, bore, 195, 96);
            }
        }
    }
}
