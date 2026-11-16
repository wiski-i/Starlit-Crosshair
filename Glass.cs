using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NativeVcrdbCrosshair
{
    public enum GlassMaterial { Gloss, Quiet, Technical }

    internal static class Glass
    {
        internal static GlassMaterial Material = GlassMaterial.Gloss;
        internal static readonly Color Rose = Color.FromArgb(238, 176, 194);
        internal static readonly Color Success = Color.FromArgb(132, 225, 183);
        internal static readonly Color Warning = Color.FromArgb(255, 193, 146);

        internal static void Card(Graphics g, Rectangle bounds, int radius, Color fill, bool active)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            var previous = g.SmoothingMode;
            if (Material == GlassMaterial.Technical) radius = Math.Min(radius, 5);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bounds.Width--;
            bounds.Height--;
            using (var path = Draw.RoundRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, radius))
            {
                float highlight = Material == GlassMaterial.Gloss ? 0.13f : 0.025f;
                Color light = Anim.LerpColor(fill, Color.White, highlight);
                Color dark = Anim.LerpColor(fill, Color.Black, 0.21f);
                using (var shade = new LinearGradientBrush(bounds, light, dark, 88f)) g.FillPath(shade, path);

                Color edge = active ? Anim.LerpColor(fill, Theme.AccentInk, 0.66f) : Anim.LerpColor(fill, Rose, 0.26f);
                using (var rim = new LinearGradientBrush(bounds, edge, Anim.LerpColor(fill, edge, 0.37f), 98f))
                using (var pen = new Pen(rim)) g.DrawPath(pen, path);
            }
            g.SmoothingMode = previous;
        }

        internal static void FocusRing(Graphics g, Rectangle frame, int radius = 8)
        {
            frame.Inflate(-2, -2);
            if (frame.Width < 2 || frame.Height < 2) return;
            using (var pen = new Pen(Theme.AccentInk) { DashStyle = DashStyle.Dot })
            using (var path = Draw.RoundRect(frame.X, frame.Y, frame.Width - 1, frame.Height - 1, Math.Max(2, radius - 2)))
                g.DrawPath(pen, path);
        }
    }

    public abstract class GlassControl : Control
    {
        protected GlassControl()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Focus();
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Enter || base.IsInputKey(keyData); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space || e.KeyData == Keys.Enter) e.SuppressKeyPress = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space || e.KeyData == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyUp(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected virtual int FocusRadius { get { return 8; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Focused && ShowFocusCues) Glass.FocusRing(e.Graphics, ClientRectangle, FocusRadius);
        }
    }
    internal sealed class ColorSwatch : GlassControl
    {
        private readonly Color color;

        internal ColorSwatch(Color color)
        {
            this.color = color;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "Renk #" + (color.ToArgb() & 0xFFFFFF).ToString("X6");
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Draw.RoundRect(1, 1, Width - 3, Height - 3, 4))
            using (var fill = new SolidBrush(color))
            using (var rim = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(fill, shape);
                e.Graphics.DrawPath(rim, shape);
            }
            if (Focused)
            {
                using (var outer = Draw.RoundRect(2, 2, Width - 5, Height - 5, 3))
                using (var inner = Draw.RoundRect(3, 3, Width - 7, Height - 7, 2))
                using (var shadow = new Pen(Color.Black))
                using (var highlight = new Pen(Color.White))
                {
                    e.Graphics.DrawPath(shadow, outer);
                    e.Graphics.DrawPath(highlight, inner);
                }
            }
            base.OnPaint(e);
        }
    }
}