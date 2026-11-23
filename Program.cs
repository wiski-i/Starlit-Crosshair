using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using Mutex = System.Threading.Mutex;
using AbandonedMutexException = System.Threading.AbandonedMutexException;
using Microsoft.Win32;

namespace NativeVcrdbCrosshair
{
    public static class Program
    {
        // --- DPI farkındalığı ---
        // Overlay tam ekran bir penceredir. Süreç DPI-aware olmazsa Windows
        // pencereyi mantıksal çözünürlükte oluşturup fiziksel piksellere
        // bitmap olarak GERER (%125/%150 ölçeklemede ×1.25/×1.5). Bu kesirli
        // germe, crosshair çizgilerini yarım piksele oturtur; çizgiler bulanır,
        // merkezden kayar ve karşılıklı kollar birbirine göre kayık görünür.
        // Aşağıdaki çağrılar süreci fiziksel piksellerle 1:1 eşler.
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        // shcore.dll (Windows 8.1+) -- PROCESS_PER_MONITOR_DPI_AWARE = 2
        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static void EnableDpiAwareness()
        {
            // En iyi destekten en eskiye doğru dene; biri tutarsa çık.
            try { if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)) return; }
            catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; }
            catch { }
            try { SetProcessDPIAware(); }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            EnableDpiAwareness();      // pencere oluşturulmadan ÖNCE çağrılmalı
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var instance = new Mutex(false, @"Local\VCRDB.Crosshair"))
            {
                bool acquired;
                try { acquired = instance.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired)
                {
                    UiText.UseEnglish = ConfigManager.Load().EnglishUi;
                    DarkMessage.Show(UiText.Get("Crosshair zaten açık. Bildirim alanındaki simgeyi kullan."), "VCRDB Crosshair");
                    return;
                }
                try { using (var form = new MainForm()) Application.Run(form); }
                finally { instance.ReleaseMutex(); }
            }
        }
    }

    // ============================================================
    //  TEMA / RENK
    // ============================================================
    public enum AccentTheme { Orange, Purple, Cyan, Burgundy }

    public static class Theme
    {
        public static Color Accent;
        public static Color AccentInk;
        public static Color AccentHover;
        public static Color AccentPressed;
        public static Color Bg;
        public static Color BgPanel;
        public static Color BgPanelAlt;
        public static Color BgCard;
        public static Color Text;
        public static Color TextDim;
        public static Color Track;
        public static Color Border;
        public static AccentTheme Current;

        static Theme()
        {
            ApplyAccent(AccentTheme.Burgundy);
        }

        public static void ApplyAccent(AccentTheme t)
        {
            Current = t;
            Bg = Color.FromArgb(27, 20, 31);
            BgPanel = Color.FromArgb(32, 23, 36);
            BgPanelAlt = Color.FromArgb(55, 36, 52);
            BgCard = Color.FromArgb(61, 41, 58);
            Text = Color.FromArgb(250, 241, 247);
            TextDim = Color.FromArgb(198, 181, 198);
            Track = Color.FromArgb(74, 54, 75);
            Border = Color.FromArgb(97, 70, 91);
            Accent = AccentColor(t);
            AccentInk = Anim.LerpColor(Accent, Color.White, 0.67f);
            AccentHover = Anim.LerpColor(Accent, Color.White, 0.13f);
            AccentPressed = Anim.LerpColor(Accent, Color.Black, 0.16f);
        }

        internal static void ApplyDialogFrame(IntPtr window)
        {
            if (SystemInformation.HighContrast) return;

            const int ImmersiveDarkMode = 20, BorderColor = 34, CaptionColor = 35, TextColor = 36;
            int dark = 1;
            if (DwmSetWindowAttribute(window, ImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(window, 19, ref dark, sizeof(int));
            SetFrameColor(window, BorderColor, Border);
            SetFrameColor(window, CaptionColor, BgPanel);
            SetFrameColor(window, TextColor, Text);
        }

        private static void SetFrameColor(IntPtr window, int attribute, Color color)
        {
            int value = ColorTranslator.ToWin32(color);
            DwmSetWindowAttribute(window, attribute, ref value, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subApp, string subIdList);

        /// <summary>Gives a scrolling container dark Windows scrollbars instead of the light default.</summary>
        internal static void DarkScrollbars(Control control)
        {
            control.HandleCreated += delegate
            {
                if (!SystemInformation.HighContrast) SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            };
        }

        /// <summary>Draws a drop-down list in the app palette; the themed default stays white.</summary>
        internal static void DarkPicker(ComboBox box)
        {
            box.FlatStyle = FlatStyle.Flat;
            box.DrawMode = DrawMode.OwnerDrawFixed;
            box.BackColor = BgPanelAlt;
            box.ForeColor = Text;
            box.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                Color fill = (e.State & DrawItemState.Selected) != 0 ? Accent : BgPanelAlt;
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0)
                    TextRenderer.DrawText(e.Graphics, box.Items[e.Index].ToString(), box.Font, e.Bounds, Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            // FlatStyle still paints the border and arrow button in system colors.
            box.HandleCreated += delegate { new PickerChrome(box).AssignHandle(box.Handle); };
        }

        private sealed class PickerChrome : NativeWindow
        {
            private readonly ComboBox box;
            internal PickerChrome(ComboBox box) { this.box = box; }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F || SystemInformation.HighContrast) return;
                Rectangle r = box.ClientRectangle;
                int buttonWidth = SystemInformation.VerticalScrollBarWidth + 2;
                var button = new Rectangle(r.Right - buttonWidth, r.Top, buttonWidth, r.Height);
                using (Graphics g = Graphics.FromHwnd(box.Handle))
                using (var fill = new SolidBrush(BgPanelAlt))
                using (var edge = new Pen(box.Focused ? Accent : Border))
                using (var ink = new Pen(TextDim, 1.6f))
                {
                    g.FillRectangle(fill, button);
                    g.DrawRectangle(edge, r.X, r.Y, r.Width - 1, r.Height - 1);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    float cx = button.X + button.Width / 2f, cy = r.Height / 2f;
                    g.DrawLines(ink, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
                }
            }
        }

        public static Color AccentColor(AccentTheme t)
        {
            switch (t)
            {
                case AccentTheme.Orange: return Color.FromArgb(143, 60, 25);
                case AccentTheme.Purple: return Color.FromArgb(103, 62, 157);
                case AccentTheme.Cyan: return Color.FromArgb(16, 100, 104);
                default: return Color.FromArgb(113, 27, 54);
            }
        }
    }

    // ============================================================
    //  ANIMASYON YARDIMCISI
    // ============================================================
    public static class Anim
    {
        public static Color LerpColor(Color a, Color b, float t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    // ============================================================
    //  ÇİZİM YARDIMCILARI
    // ============================================================
    public static class Draw
    {
        public static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            GraphicsPath path = new GraphicsPath();
            if (r <= 0 || w <= 0 || h <= 0) { path.AddRectangle(new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h))); return path; }
            int d = r * 2;
            if (d > w) d = w;
            if (d > h) d = h;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawGlyph(Graphics g, string key, Rectangle r, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            using (SolidBrush b = new SolidBrush(c))
            using (Pen pen = new Pen(c, 1.9f))
            {
                if (key == "cross")
                {
                    g.FillRectangle(b, cx - 1, r.Y + 1, 2, r.Height - 2);
                    g.FillRectangle(b, r.X + 1, cy - 1, r.Width - 2, 2);
                }
                else if (key == "grid")
                {
                    int s = (r.Width - 4) / 2;
                    g.DrawRectangle(pen, r.X + 1, r.Y + 1, s - 2, s - 2);
                    g.DrawRectangle(pen, r.X + 1 + s, r.Y + 1, s - 2, s - 2);
                    g.DrawRectangle(pen, r.X + 1, r.Y + 1 + s, s - 2, s - 2);
                    g.DrawRectangle(pen, r.X + 1 + s, r.Y + 1 + s, s - 2, s - 2);
                }
                else if (key == "move")
                {
                    g.DrawLine(pen, cx, r.Y + 3, cx, r.Y + r.Height - 3);
                    g.DrawLine(pen, r.X + 3, cy, r.X + r.Width - 3, cy);
                    g.FillPolygon(b, new Point[] { new Point(cx, r.Y), new Point(cx - 4, r.Y + 5), new Point(cx + 4, r.Y + 5) });
                    g.FillPolygon(b, new Point[] { new Point(cx, r.Y + r.Height), new Point(cx - 4, r.Y + r.Height - 5), new Point(cx + 4, r.Y + r.Height - 5) });
                    g.FillPolygon(b, new Point[] { new Point(r.X, cy), new Point(r.X + 5, cy - 4), new Point(r.X + 5, cy + 4) });
                    g.FillPolygon(b, new Point[] { new Point(r.X + r.Width, cy), new Point(r.X + r.Width - 5, cy - 4), new Point(r.X + r.Width - 5, cy + 4) });
                }
                else if (key == "monitor")
                {
                    g.DrawRectangle(pen, r.X + 1, r.Y + 2, r.Width - 3, r.Height - 8);
                    g.FillRectangle(b, cx - 5, r.Y + r.Height - 3, 10, 2);
                }
                else if (key == "gear")
                {
                    int rad = r.Width / 2 - 4;
                    g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                    g.FillEllipse(b, cx - 2, cy - 2, 4, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        int x1 = cx + (int)(Math.Cos(a) * rad);
                        int y1 = cy + (int)(Math.Sin(a) * rad);
                        int x2 = cx + (int)(Math.Cos(a) * (rad + 3));
                        int y2 = cy + (int)(Math.Sin(a) * (rad + 3));
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
                else if (key == "help")
                {
                    g.DrawEllipse(pen, r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3);
                    using (Font f = new Font("Segoe UI", 9f, FontStyle.Bold))
                        TextRenderer.DrawText(g, "?", f, new Rectangle(r.X, r.Y - 1, r.Width, r.Height), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    // ============================================================
    //  AYAR MODELİ
    // ============================================================
    public enum CenterShape { Square, Ring, Diamond }

    public class VcrdbSettings
    {
        public Color Color;
        public int Opacity;
        public bool OutlineShow;
        public int OutlineThick;

        public bool DotShow;
        public int DotThick;

        public bool InShow;
        public int InLen;
        public int InThick;
        public int InOff;

        public bool OutShow;
        public int OutLen;
        public int OutThick;
        public int OutOff;

        public int OffsetX;
        public int OffsetY;
        public int Scale;
        public bool TShape;
        public bool Pulse;
        public CenterShape Marker;
        public bool RoundedArms;
        public bool DoubleOutline;
        public int TopLength = 100, RightLength = 100, BottomLength = 100, LeftLength = 100;

        private static readonly HashSet<string> ShapeFields = new HashSet<string>
        {
            "Marker", "RoundedArms", "DoubleOutline", "TopLength", "RightLength", "BottomLength", "LeftLength"
        };

        public bool HasVisibleArms
        {
            get
            {
                return (InShow && InLen > 0 || OutShow && OutLen > 0)
                    && (LeftLength > 0 || RightLength > 0 || BottomLength > 0 || (!TShape && TopLength > 0));
            }
        }

        public VcrdbSettings()
        {
            Color = Color.FromArgb(255, 229, 220); Opacity = 255;
            OutlineShow = true; OutlineThick = 1;
            DotShow = false; DotThick = 2;
            InShow = true; InLen = 6; InThick = 2; InOff = 3;
            OutShow = false; OutLen = 2; OutThick = 2; OutOff = 10;
            OffsetX = 0; OffsetY = 0; Scale = 100; TShape = false; Pulse = false;
        }

        public VcrdbSettings Clone()
        {
            VcrdbSettings c = new VcrdbSettings();
            c.CopyFrom(this);
            return c;
        }

        public void CopyFrom(VcrdbSettings s)
        {
            Color = s.Color; Opacity = s.Opacity;
            OutlineShow = s.OutlineShow; OutlineThick = s.OutlineThick;
            DotShow = s.DotShow; DotThick = s.DotThick;
            InShow = s.InShow; InLen = s.InLen; InThick = s.InThick; InOff = s.InOff;
            OutShow = s.OutShow; OutLen = s.OutLen; OutThick = s.OutThick; OutOff = s.OutOff;
            OffsetX = s.OffsetX; OffsetY = s.OffsetY; Scale = s.Scale; TShape = s.TShape; Pulse = s.Pulse;
            Marker = s.Marker; RoundedArms = s.RoundedArms; DoubleOutline = s.DoubleOutline;
            TopLength = s.TopLength; RightLength = s.RightLength;
            BottomLength = s.BottomLength; LeftLength = s.LeftLength;
        }

        public string ToConfigString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Color=" + Color.ToArgb());
            sb.AppendLine("Opacity=" + Opacity);
            sb.AppendLine("OutlineShow=" + (OutlineShow ? 1 : 0));
            sb.AppendLine("OutlineThick=" + OutlineThick);
            sb.AppendLine("DotShow=" + (DotShow ? 1 : 0));
            sb.AppendLine("DotThick=" + DotThick);
            sb.AppendLine("InShow=" + (InShow ? 1 : 0));
            sb.AppendLine("InLen=" + InLen);
            sb.AppendLine("InThick=" + InThick);
            sb.AppendLine("InOff=" + InOff);
            sb.AppendLine("OutShow=" + (OutShow ? 1 : 0));
            sb.AppendLine("OutLen=" + OutLen);
            sb.AppendLine("OutThick=" + OutThick);
            sb.AppendLine("OutOff=" + OutOff);
            sb.AppendLine("OffsetX=" + OffsetX);
            sb.AppendLine("OffsetY=" + OffsetY);
            sb.AppendLine("Scale=" + Scale);
            sb.AppendLine("TShape=" + (TShape ? 1 : 0));
            sb.AppendLine("Pulse=" + (Pulse ? 1 : 0));
            sb.AppendLine("Marker=" + (int)Marker);
            sb.AppendLine("DoubleOutline=" + (DoubleOutline ? 1 : 0));
            sb.AppendLine("RoundedArms=" + (RoundedArms ? 1 : 0));
            sb.AppendLine("TopLength=" + TopLength);
            sb.AppendLine("RightLength=" + RightLength);
            sb.AppendLine("BottomLength=" + BottomLength);
            sb.AppendLine("LeftLength=" + LeftLength);
            return sb.ToString();
        }

        public static Dictionary<string, string> ParseKV(string txt)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (txt == null) return d;
            string[] lines = txt.Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                d[k] = v;
            }
            return d;
        }

        public static int GetInt(Dictionary<string, string> d, string key, int def)
        {
            string v;
            if (d.TryGetValue(key, out v))
            {
                int n;
                if (int.TryParse(v, out n)) return n;
            }
            return def;
        }

        public static VcrdbSettings FromConfigString(string txt)
        {
            VcrdbSettings s = new VcrdbSettings();
            Dictionary<string, string> d = ParseKV(txt);
            s.Color = Color.FromArgb(255, Color.FromArgb(GetInt(d, "Color", s.Color.ToArgb())));

            s.Opacity = Clamp(GetInt(d, "Opacity", 255), 0, 255);
            s.OutlineShow = GetInt(d, "OutlineShow", 1) == 1;
            s.OutlineThick = Clamp(GetInt(d, "OutlineThick", 1), 1, 6);
            s.DotShow = GetInt(d, "DotShow", 0) == 1;
            s.DotThick = Clamp(GetInt(d, "DotThick", 2), 1, 24);
            s.InShow = GetInt(d, "InShow", 1) == 1;
            s.InLen = Clamp(GetInt(d, "InLen", 6), 0, 30);
            s.InThick = Clamp(GetInt(d, "InThick", 2), 1, 12);
            s.InOff = Clamp(GetInt(d, "InOff", 3), 0, 30);
            s.OutShow = GetInt(d, "OutShow", 0) == 1;
            s.OutLen = Clamp(GetInt(d, "OutLen", 2), 0, 30);
            s.OutThick = Clamp(GetInt(d, "OutThick", 2), 1, 12);
            s.OutOff = Clamp(GetInt(d, "OutOff", 10), 0, 60);
            s.OffsetX = Clamp(GetInt(d, "OffsetX", 0), -300, 300);
            s.OffsetY = Clamp(GetInt(d, "OffsetY", 0), -300, 300);
            s.Scale = Clamp(GetInt(d, "Scale", 100), 50, 300);
            s.TShape = GetInt(d, "TShape", 0) == 1;
            s.Pulse = GetInt(d, "Pulse", 0) == 1;
            s.Marker = (CenterShape)Clamp(GetInt(d, "Marker", 0), 0, 2);
            s.RoundedArms = GetInt(d, "RoundedArms", 0) == 1;
            s.DoubleOutline = GetInt(d, "DoubleOutline", 0) == 1;
            s.TopLength = Clamp(GetInt(d, "TopLength", 100), 0, 200);
            s.RightLength = Clamp(GetInt(d, "RightLength", 100), 0, 200);
            s.BottomLength = Clamp(GetInt(d, "BottomLength", 100), 0, 200);
            s.LeftLength = Clamp(GetInt(d, "LeftLength", 100), 0, 200);
            return s;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public string ToShareCode()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ToConfigString());
            return "VCRDB-" + Convert.ToBase64String(bytes);
        }

        public static bool TryFromShareCode(string code, out VcrdbSettings result)
        {
            result = null;
            if (String.IsNullOrWhiteSpace(code) || code.Length > 2048) return false;
            code = code.Trim();
            if (code.StartsWith("VCRDB-", StringComparison.Ordinal)) code = code.Substring(6);
            try
            {
                string txt = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(code));
                Dictionary<string, string> values = ParseKV(txt);
                VcrdbSettings parsed = FromConfigString(txt);
                // Compare every decoded value against its normalized representation.
                foreach (var setting in ParseKV(parsed.ToConfigString()))
                {
                    int value;
                    string raw;
                    if (!values.TryGetValue(setting.Key, out raw) && ShapeFields.Contains(setting.Key)) continue;
                    if (!Int32.TryParse(raw, out value)
                        || value != Int32.Parse(setting.Value)) return false;
                }
                result = parsed;
                return true;
            }
            catch (FormatException) { return false; }
            catch (DecoderFallbackException) { return false; }
        }
    }

    // ============================================================
    //  ORTAK CROSSHAIR ÇİZİCİ
    // ============================================================
    public static class CrosshairRenderer
    {
        // Geriye dönük uyumlu sarmalayıcı (tam sayı merkez).
        public static void Render(Graphics g, int cx, int cy, VcrdbSettings s, float extraScale)
        {
            Render(g, (double)cx, (double)cy, s, extraScale);
        }

        // ASIL ÇİZİCİ — merkez ONDALIKLI verilir (cxf = Width/2.0 + offset).
        //
        // Neden ondalıklı? Çift genişlikli ekranda (1920, 2560, 3840 ...) gerçek
        // merkez piksel SINIRINDADIR (1920 -> x=960.0). Tam sayı Width/2 ile tek
        // kalınlıklı çizgi 960. piksele oturur ve merkez çizgisinin SAĞINDA kalır
        // -> "hafif sağa kayık". Tek genişlikte ise Width/2 küsuratı atar.
        //
        // Çözüm: her şey gerçek merkez (cxf,cyf) etrafında YANSIMA ile çizilir:
        //   - sağ kol = sol kolun aynası, alt kol = üst kolun aynası (Kx-x-w / Ky-y-h),
        //   - her çizginin kalınlık bandı ekran paritesine göre hizalanır (SymBand),
        // böylece HER çözünürlükte piksel-mükemmel ortalanır. Bant simetrisi için
        // gerekirse kalınlık 1px artırılır (gözle fark edilmez).
        public static void Render(Graphics g, double cxf, double cyf, VcrdbSettings s, float extraScale, int opacity = -1)
        {
            g.SmoothingMode = SmoothingMode.None;
            float sc = extraScale * (s.Scale / 100f);
            if (sc <= 0) sc = 0.01f;

            if (opacity < 0) opacity = s.Opacity;
            SolidBrush mainBrush = new SolidBrush(Color.FromArgb(opacity, s.Color.R, s.Color.G, s.Color.B));
            SolidBrush outlineBrush = new SolidBrush(Color.FromArgb(opacity, 0, 0, 0));

            int ot = Math.Max(1, (int)Math.Round(s.OutlineThick * sc));
            int dotT = Math.Max(1, (int)Math.Round(s.DotThick * sc));
            int inL = (int)Math.Round(s.InLen * sc);
            int inT = Math.Max(1, (int)Math.Round(s.InThick * sc));
            int inO = (int)Math.Round(s.InOff * sc);
            int outL = (int)Math.Round(s.OutLen * sc);
            int outT = Math.Max(1, (int)Math.Round(s.OutThick * sc));
            int outO = (int)Math.Round(s.OutOff * sc);

            // Yansıma sabitleri: piksel p, (K-1-p) pikseline yansır; 2*cxf tam sayıdır.
            int Kx = (int)Math.Round(cxf * 2.0);
            int Ky = (int)Math.Round(cyf * 2.0);

            // Önce OUTLINE (siyah, genişletilmiş) sonra FILL — fill üstte kalır.
            using (var halo = s.OutlineShow && s.DoubleOutline ? new SolidBrush(Color.FromArgb(opacity, 255, 255, 255)) : null)
            for (int pass = halo != null ? -1 : 0; pass < 2; pass++)
            {
                if (pass == 0 && !s.OutlineShow) continue;
                Brush b = pass < 0 ? halo : pass == 0 ? outlineBrush : mainBrush;
                int ex = pass < 0 ? ot + Math.Max(1, (int)Math.Round(sc)) : pass == 0 ? ot : 0;   // outline genişlemesi

                if (s.DotShow)
                {
                    int tdx = s.Marker == CenterShape.Ring ? Math.Max(5, dotT) : dotT, tdy = tdx;
                    int x0 = SymBand(Kx, ref tdx);
                    int y0 = SymBand(Ky, ref tdy);
                    Marker(g, b, x0, y0, tdx, tdy, ex, s.Marker);
                }
                if (s.InShow) Arms(g, b, cxf, cyf, Kx, Ky, inT, inL, inO, s, ex);
                if (s.OutShow) Arms(g, b, cxf, cyf, Kx, Ky, outT, outL, outO, s, ex);
            }

            mainBrush.Dispose();
            outlineBrush.Dispose();
        }

        // Dört kolu (T-şekilde üçü) gerçek merkez etrafında simetrik çizer.
        internal static void RenderPreview(Graphics g, Rectangle bounds, double x, double y, VcrdbSettings settings, float scale)
        {
            if (settings.Opacity == 0) return;
            if (settings.Opacity == 255) { Render(g, x, y, settings, scale, 255); return; }
            using (var pixels = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var canvas = Graphics.FromImage(pixels))
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                Render(canvas, x, y, settings, scale, 255);
                var alpha = new System.Drawing.Imaging.ColorMatrix();
                alpha.Matrix33 = settings.Opacity / 255f;
                attributes.SetColorMatrix(alpha);
                g.DrawImage(pixels, bounds, 0, 0, pixels.Width, pixels.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private static void Marker(Graphics g, Brush brush, int x, int y, int width, int height, int ex, CenterShape shape)
        {
            if (shape == CenterShape.Square)
            {
                g.FillRectangle(brush, x - ex, y - ex, width + ex * 2, height + ex * 2);
                return;
            }

            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (shape == CenterShape.Diamond)
            {
                float cx = x + width / 2f, cy = y + height / 2f;
                g.FillPolygon(brush, new[]
                {
                    new PointF(cx, y - ex), new PointF(x + width + ex, cy),
                    new PointF(cx, y + height + ex), new PointF(x - ex, cy)
                });
            }
            else
            {
                int rim = Math.Min((Math.Min(width, height) - 1) / 2, Math.Max(1, Math.Min(width, height) / 5) + ex);
                using (var path = new GraphicsPath(FillMode.Alternate))
                {
                    path.AddEllipse(x - ex, y - ex, width + ex * 2, height + ex * 2);
                    if (width > rim * 2 && height > rim * 2)
                        path.AddEllipse(x + rim, y + rim, width - rim * 2, height - rim * 2);
                    g.FillPath(brush, path);
                }
            }
            g.SmoothingMode = mode;
        }

        private static void Arms(Graphics g, Brush brush, double cx, double cy, int kx, int ky,
                                 int thickness, int length, int gap, VcrdbSettings settings, int outline)
        {
            if (length <= 0) return;
            int width = thickness, height = thickness;
            int x = SymBand(kx, ref width);
            int y = SymBand(ky, ref height);
            int top = (int)Math.Round(cy - gap - length) + length;
            int left = (int)Math.Round(cx - gap - length) + length;
            int topLength = settings.TShape ? 0 : ScaleArm(length, settings.TopLength);
            int bottomLength = ScaleArm(length, settings.BottomLength);
            int leftLength = ScaleArm(length, settings.LeftLength);
            int rightLength = ScaleArm(length, settings.RightLength);

            FillArm(g, brush, x, top - topLength, width, topLength, outline, settings.RoundedArms);
            FillArm(g, brush, x, ky - top, width, bottomLength, outline, settings.RoundedArms);
            FillArm(g, brush, left - leftLength, y, leftLength, height, outline, settings.RoundedArms);
            FillArm(g, brush, kx - left, y, rightLength, height, outline, settings.RoundedArms);
        }

        private static int ScaleArm(int length, int percent)
        {
            return percent == 0 ? 0 : Math.Max(1, (int)Math.Round(length * percent / 100.0));
        }

        // [start, start+t-1] bandını, X=K/2 (gerçek merkez) etrafında SİMETRİK yapar.
        // K-t tek ise (parite uyuşmazsa) kalınlığı 1 artırır ki bant tam ortalansın.
        private static int SymBand(int K, ref int t)
        {
            if (((K - t) & 1) != 0) t++;
            return (K - t) / 2;
        }

        private static void FillArm(Graphics g, Brush brush, int x, int y, int width, int height, int ex, bool rounded)
        {
            if (width <= 0 || height <= 0) return;
            var bounds = new Rectangle(x - ex, y - ex, width + ex * 2, height + ex * 2);
            int radius = Math.Min(bounds.Width, bounds.Height) / 2;
            if (!rounded || radius == 0)
            {
                g.FillRectangle(brush, bounds);
                return;
            }

            var mode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Draw.RoundRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, radius))
                g.FillPath(brush, path);
            g.SmoothingMode = mode;
        }
    }

    // ============================================================
    //  KALICILIK
    // ============================================================
    public class AppConfig
    {
        public VcrdbSettings Settings;
        public AccentTheme Accent;
        public GlassMaterial Material;
        internal bool EnglishUi;
        public bool HkCtrl, HkAlt, HkShift;
        public Keys HkKey;
        public string MonitorDevice = "";
        public bool BackgroundPulse;
        public bool HoldVisibility;
        internal readonly ProfileLibrary Library = new ProfileLibrary();
        public readonly Dictionary<string, string> GameProfiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal string ProfileForGame(string executable)
        {
            string profile;
            return executable != null && GameProfiles.TryGetValue(executable, out profile) ? profile : null;
        }

        public AppConfig()
        {
            Settings = new VcrdbSettings();
            Accent = AccentTheme.Burgundy;
            HkCtrl = true; HkAlt = true; HkShift = false;
            HkKey = Keys.C;

        }
    }

    public static class ConfigManager
    {
        public static string Dir;
        public static string ConfigPath;
        public static string ProfilesDir;

        static ConfigManager()
        {
            Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VCRDBCrosshair");
            ConfigPath = Path.Combine(Dir, "config.ini");
            ProfilesDir = Path.Combine(Dir, "profiles");
            try
            {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                if (!Directory.Exists(ProfilesDir)) Directory.CreateDirectory(ProfilesDir);
            }
            catch { }
        }

        public static AppConfig Load()
        {
            AppConfig c = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string txt = ReadSettings(ConfigPath, 512 * 1024);
                    c.Settings = VcrdbSettings.FromConfigString(txt);
                    Dictionary<string, string> d = VcrdbSettings.ParseKV(txt);
                    int accent = VcrdbSettings.GetInt(d, "Accent", (int)c.Accent);
                    if (Enum.IsDefined(typeof(AccentTheme), accent)) c.Accent = (AccentTheme)accent;
                    c.HkCtrl = VcrdbSettings.GetInt(d, "HkCtrl", 1) == 1;
                    c.HkAlt = VcrdbSettings.GetInt(d, "HkAlt", 1) == 1;
                    c.HkShift = VcrdbSettings.GetInt(d, "HkShift", 0) == 1;
                    int key = VcrdbSettings.GetInt(d, "HkKey", (int)Keys.C);
                    if (HotkeyBox.IsSupportedKey((Keys)key)) c.HkKey = (Keys)key;
                    else { c.HkCtrl = true; c.HkAlt = true; c.HkShift = false; }
                    d.TryGetValue("MonitorDevice", out c.MonitorDevice);
                    c.BackgroundPulse = VcrdbSettings.GetInt(d, "BackgroundPulse", 0) == 1;
                    c.HoldVisibility = VcrdbSettings.GetInt(d, "HoldVisibility", 0) == 1;
                    c.EnglishUi = VcrdbSettings.GetInt(d, "EnglishUi", 0) == 1;
                    c.Library.Load(d);
                    int material = VcrdbSettings.GetInt(d, "Material", 0);
                    foreach (var item in d)
                    {
                        if (!item.Key.StartsWith("Game.", StringComparison.Ordinal)) continue;
                        string executable = item.Key.Substring(5);
                        if (GameProfileWatcher.IsExecutableName(executable) && ConfigManager.IsValidProfileName(item.Value))
                            c.GameProfiles[executable] = item.Value;
                    }
                    if (Enum.IsDefined(typeof(GlassMaterial), material)) c.Material = (GlassMaterial)material;
                    if (!d.ContainsKey("Material") && c.Accent == AccentTheme.Orange) c.Accent = AccentTheme.Burgundy;
                }
            }
            catch { }
            return c;
        }

        public static bool Save(AppConfig c)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(c.Settings.ToConfigString());
                sb.AppendLine("Accent=" + (int)c.Accent);
                sb.AppendLine("HkCtrl=" + (c.HkCtrl ? 1 : 0));
                sb.AppendLine("HkAlt=" + (c.HkAlt ? 1 : 0));
                sb.AppendLine("HkShift=" + (c.HkShift ? 1 : 0));
                sb.AppendLine("HkKey=" + (int)c.HkKey);
                sb.AppendLine("MonitorDevice=" + c.MonitorDevice);
                sb.AppendLine("BackgroundPulse=" + (c.BackgroundPulse ? 1 : 0));
                sb.AppendLine("HoldVisibility=" + (c.HoldVisibility ? 1 : 0));
                sb.AppendLine("EnglishUi=" + (c.EnglishUi ? 1 : 0));
                c.Library.Append(sb);
                sb.AppendLine("Material=" + (int)c.Material);
                foreach (var item in c.GameProfiles) sb.AppendLine("Game." + item.Key + "=" + item.Value);
                Directory.CreateDirectory(Dir);
                WriteAtomically(ConfigPath, sb.ToString());
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        internal static void WriteAtomically(string path, string content, bool replace = true)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (replace && File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { File.Delete(temporary); }
        }

        public static List<string> ListProfiles()
        {
            List<string> list = new List<string>();
            try
            {
                if (Directory.Exists(ProfilesDir))
                {
                    string[] files = Directory.GetFiles(ProfilesDir, "*.txt");
                    foreach (string f in files) list.Add(Path.GetFileNameWithoutExtension(f));
                    list.Sort(StringComparer.CurrentCultureIgnoreCase);
                }
            }
            catch { }
            return list;
        }

        public static bool IsValidProfileName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 40 || name.EndsWith(".")
                || name.EndsWith(" ") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            string stem = name.Split('.')[0].TrimEnd();
            return !System.Text.RegularExpressions.Regex.IsMatch(stem,
                "^(CON|PRN|AUX|NUL|CONIN\\$|CONOUT\\$|COM[0-9¹²³]|LPT[0-9¹²³])$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        public static bool ProfileExists(string name)
        {
            return IsValidProfileName(name) && File.Exists(Path.Combine(ProfilesDir, name + ".txt"));
        }

        public static void SaveProfile(string name, VcrdbSettings s)
        {
            if (!IsValidProfileName(name)) throw new ArgumentException("Invalid profile name.");
            Directory.CreateDirectory(ProfilesDir);
            WriteAtomically(Path.Combine(ProfilesDir, name + ".txt"), s.ToConfigString());
        }

        internal static bool TryAddProfile(string name, VcrdbSettings settings)
        {
            if (!IsValidProfileName(name)) throw new ArgumentException("Invalid profile name.");
            Directory.CreateDirectory(ProfilesDir);
            string path = Path.Combine(ProfilesDir, name + ".txt");
            if (File.Exists(path)) return false;
            try { WriteAtomically(path, settings.ToConfigString(), false); }
            catch (IOException)
            {
                if (File.Exists(path)) return false;
                throw;
            }
            return true;
        }

        public static VcrdbSettings LoadProfile(string name)
        {
            if (!ProfileExists(name)) return null;
            try { return VcrdbSettings.FromConfigString(ReadSettings(Path.Combine(ProfilesDir, name + ".txt"))); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public static void DeleteProfile(string name)
        {
            if (!IsValidProfileName(name)) throw new ArgumentException("Invalid profile name.");
            File.Delete(Path.Combine(ProfilesDir, name + ".txt"));
        }

        private static string ReadSettings(string path, int maxBytes = 8192)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > maxBytes) throw new IOException("Settings file is too large.");
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }
    }

    // ============================================================
    //  TOGGLE SWITCH
    // ============================================================
    public class ToggleSwitch : GlassControl
    {
        private bool _checked = false;
        private float anim = 0f;
        private System.Windows.Forms.Timer timer;
        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    StartAnim();
                    if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public void SetCheckedSilent(bool value)
        {
            _checked = value;
            anim = value ? 1f : 0f;
            timer.Stop();
            Invalidate();
        }

        public ToggleSwitch()
        {
            AccessibleRole = AccessibleRole.CheckButton;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.Size = new Size(48, 26);
            this.Cursor = Cursors.Hand;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
            anim = _checked ? 1f : 0f;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            float target = _checked ? 1f : 0f;
            anim = anim + (target - anim) * 0.35f;
            if (Math.Abs(target - anim) < 0.01f) { anim = target; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Checked = !_checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;
            int trackH = 20;
            int trackY = (h - trackH) / 2;
            Color track = Anim.LerpColor(Theme.Track, Theme.Accent, anim);
            using (SolidBrush tb = new SolidBrush(track))
            using (GraphicsPath p = Draw.RoundRect(0, trackY, w, trackH, trackH / 2))
                g.FillPath(tb, p);

            int knob = 16;
            int travel = w - knob - 4;
            int kx = 2 + (int)(travel * anim);
            int ky = (h - knob) / 2;
            using (SolidBrush kb = new SolidBrush(Color.White))
                g.FillEllipse(kb, kx, ky, knob, knob);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  MODERN SLIDER
    // ============================================================
    public class ModernSlider : GlassControl
    {
        private int _value = 50;
        private int _min = 0;
        private int _max = 100;
        private bool isDragging = false;
        private bool hovered = false;
        private float thumbAnim = 0f;
        private System.Windows.Forms.Timer timer;
        public bool ShowValue = true;
        public string Suffix = "";
        public event EventHandler ValueChanged;

        public int Minimum { get { return _min; } set { _min = value; Invalidate(); } }
        public int Maximum { get { return _max; } set { _max = value; Invalidate(); } }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(_min, Math.Min(_max, value));
                if (_value != v) { _value = v; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
            }
        }

        public void SetValueSilent(int value)
        {
            int v = Math.Max(_min, Math.Min(_max, value));
            _value = v; Invalidate();
        }

        public ModernSlider()
        {
            AccessibleRole = AccessibleRole.Slider;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.Size = new Size(220, 28);
            this.Cursor = Cursors.Hand;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
        }

        private int TrackLeft() { return 8; }
        private int TrackRight() { return ShowValue ? this.Width - 44 : this.Width - 8; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            float target = (hovered || isDragging) ? 1f : 0f;
            thumbAnim = thumbAnim + (target - thumbAnim) * 0.3f;
            if (Math.Abs(target - thumbAnim) < 0.01f) { thumbAnim = target; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; StartAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; StartAnim(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Capture = true; isDragging = true; StartAnim(); UpdateValueFromMouse(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (isDragging) UpdateValueFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Capture = false; base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) { isDragging = false; StartAnim(); }
            base.OnMouseCaptureChanged(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down
                || keyData == Keys.Home || keyData == Keys.End || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value--;
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value++;
            else if (e.KeyCode == Keys.Home) Value = Minimum;
            else if (e.KeyCode == Keys.End) Value = Maximum;
            else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }

        private void UpdateValueFromMouse(int x)
        {
            int tl = TrackLeft();
            int tr = TrackRight();
            if (tr <= tl) return;
            float pct = (float)(x - tl) / (tr - tl);
            pct = Math.Max(0, Math.Min(1, pct));
            Value = _min + (int)Math.Round(pct * (_max - _min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int tl = TrackLeft();
            int tr = TrackRight();
            int trackH = 5;
            int trackY = (this.Height - trackH) / 2;
            float pct = _max == _min ? 0 : (float)(_value - _min) / (_max - _min);
            int fillW = (int)((tr - tl) * pct);

            using (SolidBrush bg = new SolidBrush(Theme.Track))
            using (GraphicsPath p = Draw.RoundRect(tl, trackY, tr - tl, trackH, trackH / 2))
                g.FillPath(bg, p);

            if (fillW > 0)
                using (SolidBrush fb = new SolidBrush(Theme.AccentInk))
                using (GraphicsPath p = Draw.RoundRect(tl, trackY, fillW, trackH, trackH / 2))
                    g.FillPath(fb, p);

            int baseR = 11;
            int r = baseR + (int)(4 * thumbAnim);
            int thumbX = tl + fillW - r / 2;
            int thumbY = (this.Height - r) / 2;
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
                g.FillEllipse(glow, thumbX - 3, thumbY - 3, r + 6, r + 6);
            using (SolidBrush tb = new SolidBrush(Color.White))
                g.FillEllipse(tb, thumbX, thumbY, r, r);
            using (Pen pen = new Pen(Theme.Accent, 2f))
                g.DrawEllipse(pen, thumbX, thumbY, r, r);

            if (ShowValue)
            {
                string txt = _value.ToString() + Suffix;
                Rectangle vr = new Rectangle(this.Width - 42, 0, 42, this.Height);
                TextRenderer.DrawText(g, txt, this.Font, vr, Theme.Text, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  MODERN BUTON
    // ============================================================
    public class ModernButton : GlassControl
    {
        private bool hovered = false;
        private bool pressed = false;
        private Color current;
        private bool hasInit = false;
        private System.Windows.Forms.Timer timer;
        public bool Ghost = false;
        public int CornerRadius = 10;
        protected override int FocusRadius { get { return Glass.Material == GlassMaterial.Technical ? Math.Min(CornerRadius, 5) : CornerRadius; } }

        public ModernButton()
        {
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.Size = new Size(150, 40);
            this.Font = new Font("Segoe UI Semibold", 9.75F);
            this.ForeColor = Color.White;
            this.Cursor = Cursors.Hand;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
        }

        private bool selected;
        public bool Selected
        {
            get { return selected; }
            set { selected = value; ResetTheme(); }
        }

        private Color TargetColor()
        {
            if (!Enabled) return Theme.BgPanel;
            if (Ghost && !Selected) return hovered ? Theme.BgCard : Theme.BgPanelAlt;
            return pressed ? Theme.AccentPressed : (hovered ? Theme.AccentHover : Theme.Accent);
        }

        public void ResetTheme() { hasInit = false; Invalidate(); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            ResetTheme();
            base.OnEnabledChanged(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            Color target = TargetColor();
            current = Anim.LerpColor(current, target, 0.3f);
            if (Math.Abs(current.R - target.R) < 3 && Math.Abs(current.G - target.G) < 3 && Math.Abs(current.B - target.B) < 3)
            { current = target; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; StartAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; StartAnim(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; StartAnim(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; StartAnim(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!hasInit) { current = TargetColor(); hasInit = true; }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Glass.Card(g, ClientRectangle, CornerRadius, current, Selected || !Ghost);
            Color tc = !Enabled ? Theme.TextDim : Ghost ? Theme.Text : Color.White;
            TextRenderer.DrawText(g, this.Text, this.Font, this.ClientRectangle, tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  NAV ITEM (kenar menü öğesi)
    // ============================================================
    public class NavItem : GlassControl
    {
        private bool hovered = false;
        private bool selected = false;
        private float hoverAnim = 0f;
        private float selAnim = 0f;
        private System.Windows.Forms.Timer timer;
        private string glyph;
        public int PageIndex;

        public bool Selected
        {
            get { return selected; }
            set { if (selected != value) { selected = value; StartAnim(); } }
        }

        public NavItem(string text, string glyphKey, int pageIndex)
        {
            AccessibleRole = AccessibleRole.PageTab;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.Text = text;
            this.glyph = glyphKey;
            this.PageIndex = pageIndex;
            this.Size = new Size(160, 42);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9.75F);
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            float ht = hovered ? 1f : 0f;
            float st = selected ? 1f : 0f;
            hoverAnim = hoverAnim + (ht - hoverAnim) * 0.3f;
            selAnim = selAnim + (st - selAnim) * 0.3f;
            bool done = Math.Abs(ht - hoverAnim) < 0.01f && Math.Abs(st - selAnim) < 0.01f;
            if (done) { hoverAnim = ht; selAnim = st; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; StartAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; StartAnim(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float hi = Math.Max(hoverAnim * 0.5f, selAnim);
            if (hi > 0.01f)
            {
                Color bg = Anim.LerpColor(Theme.BgPanel, Anim.LerpColor(Theme.BgCard, Theme.Accent, selAnim * 0.54f), hi);
                Glass.Card(g, new Rectangle(0, 3, Width, Height - 6), 10, bg, Selected);
            }

            Color iconColor = Anim.LerpColor(Theme.TextDim, Theme.AccentInk, selAnim);
            Color textColor = Anim.LerpColor(Theme.TextDim, Theme.Text, Math.Max(selAnim, hoverAnim));

            Rectangle ir = new Rectangle(14, (this.Height - 18) / 2, 18, 18);
            Draw.DrawGlyph(g, glyph, ir, iconColor);

            Rectangle tr = new Rectangle(44, 0, this.Width - 48, this.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, tr, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  TAB STRIP
    // ============================================================
    public class TabStrip : GlassControl
    {
        private string[] tabs;
        private int selected = 0;
        private int hoverIndex = -1;
        private int[] starts;
        private int[] widths;
        private float indX, indW;
        private bool indInit = false;
        private System.Windows.Forms.Timer timer;
        public event EventHandler SelectedIndexChanged;

        public int SelectedIndex { get { return selected; } }

        public TabStrip(string[] tabs)
        {
            AccessibleRole = AccessibleRole.PageTabList;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.tabs = tabs;
            this.Size = new Size(500, 42);
            this.Font = new Font("Segoe UI Semibold", 9.75F);
            this.Cursor = Cursors.Hand;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
            AccessibleName = UiText.Get("Nişangâh bileşenleri");
            Layout2();
        }

        private void Layout2()
        {
            starts = new int[tabs.Length];
            widths = new int[tabs.Length];
            int x = 0;
            for (int i = 0; i < tabs.Length; i++)
            {
                Size sz = TextRenderer.MeasureText(tabs[i], this.Font);
                int w = sz.Width + 28;
                starts[i] = x;
                widths[i] = w;
                x += w;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            float tx = starts[selected] + 14;
            float tw = widths[selected] - 28;
            indX = indX + (tx - indX) * 0.3f;
            indW = indW + (tw - indW) * 0.3f;
            if (Math.Abs(tx - indX) < 0.5f && Math.Abs(tw - indW) < 0.5f) { indX = tx; indW = tw; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitTest(e.X);
            if (idx != hoverIndex) { hoverIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) SelectTab(HitTest(e.X));
            base.OnMouseClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                int delta = e.KeyCode == Keys.Left ? -1 : 1;
                SelectTab((selected + delta + tabs.Length) % tabs.Length);
                e.Handled = true;
            }
            else base.OnKeyDown(e);
        }

        private void SelectTab(int index)
        {
            if (index < 0 || index == selected) return;
            selected = index;
            indInit = false;
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }

        private int HitTest(int x)
        {
            for (int i = 0; i < tabs.Length; i++)
                if (x >= starts[i] && x < starts[i] + widths[i]) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!indInit) { indX = starts[selected] + 14; indW = widths[selected] - 28; indInit = true; }
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, 0, this.Height - 1, this.Width, this.Height - 1);

            for (int i = 0; i < tabs.Length; i++)
            {
                if (i == selected)
                    Glass.Card(g, new Rectangle(starts[i] + 1, 1, widths[i] - 2, Height - 6), 10, Theme.BgCard, true);
                Color c = (i == selected) ? Theme.Text : (i == hoverIndex ? Theme.Text : Theme.TextDim);
                Rectangle tr = new Rectangle(starts[i], 0, widths[i], this.Height - 4);
                TextRenderer.DrawText(g, tabs[i], this.Font, tr, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using (SolidBrush b = new SolidBrush(Theme.Accent))
            using (GraphicsPath p = Draw.RoundRect((int)indX, this.Height - 3, (int)indW, 3, 2))
                g.FillPath(b, p);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  CANLI ÖNİZLEME
    // ============================================================
    public class PreviewPanel : Control
    {
        private VcrdbSettings settings;
        private Bitmap background;
        private int backdropMode = -1;
        private GlassMaterial backdropMaterial;
        private readonly Timer pulseTimer;
        private float phase;
        internal int BgMode { get; private set; }

        internal void SetBackgroundMode(int mode)
        {
            BgMode = mode;
            SyncAnimation();
        }
        public float Zoom = 2f;

        public PreviewPanel(VcrdbSettings s)
        {
            settings = s.Clone();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Size = new Size(230, 200);
            pulseTimer = new Timer { Interval = 33 };
            pulseTimer.Tick += delegate { phase = (phase + 0.18f) % ((float)Math.PI * 2); Invalidate(); };
            VisibleChanged += delegate { SyncAnimation(); };
            AccessibleName = UiText.Get("Canlı nişangâh önizlemesi");
            AccessibleRole = AccessibleRole.Graphic;
        }

        public void SetSettings(VcrdbSettings s)
        {
            settings = s.Clone();
            SyncAnimation();
            Invalidate();
        }

        internal void SyncAnimation()
        {
            if (Disposing || IsDisposed) return;
            var form = FindForm();
            bool active = Visible && (form == null || form.WindowState != FormWindowState.Minimized);
            pulseTimer.Enabled = active && (BgMode == VisibilityScenes.Motion ||
                (settings.Pulse && settings.Opacity > 0 && (settings.DotShow || settings.HasVisibleArms)));
            if (!pulseTimer.Enabled) phase = 0;
            if (!active && background != null)
            {
                background.Dispose();
                background = null;
            }
            if (active) Invalidate();
        }

        private void UpdateBackdrop()
        {
            if (background != null && background.Size == ClientSize && backdropMode == BgMode && backdropMaterial == Glass.Material) return;
            if (background != null) background.Dispose();
            background = new Bitmap(Width, Height);
            backdropMode = BgMode;
            backdropMaterial = Glass.Material;
            using (var g = Graphics.FromImage(background))
            {
                if (BgMode < VisibilityScenes.Sky) Optics.PaintBackdrop(g, ClientRectangle, BgMode);
                else VisibilityScenes.Paint(g, ClientRectangle, BgMode);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2) return;
            UpdateBackdrop();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var state = g.Save();
            using (var shape = Draw.RoundRect(0, 0, Width - 1, Height - 1, 22))
            {
                g.SetClip(shape);
                g.DrawImageUnscaled(background, 0, 0);
                if (BgMode == VisibilityScenes.Motion) VisibilityScenes.PaintMotion(g, ClientRectangle, phase);
                double x = Width / 2.0 + settings.OffsetX * 0.18 * Zoom;
                double y = Height / 2.0 + settings.OffsetY * 0.18 * Zoom;
                float scale = settings.Pulse ? (float)(1 + 0.07 * Math.Sin(phase)) : 1;
                CrosshairRenderer.RenderPreview(g, ClientRectangle, x, y, settings, Zoom * scale);
                Color tag = BgMode == 2 || BgMode == VisibilityScenes.Sky ? Theme.Bg : Optics.WarmInk;
                using (var font = new Font("Consolas", 9.5f))
                {
                    TextRenderer.DrawText(g, "X/Y", font, new Point(17, Height - 34), tag);
                    TextRenderer.DrawText(g, String.Format("{0:0.0}×", Zoom), font,
                        new Rectangle(0, Height - 34, Width - 16, 21), tag, TextFormatFlags.Right);
                }
                g.Restore(state);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var edge = new LinearGradientBrush(ClientRectangle, Color.FromArgb(183, 137, 149), Theme.Border, 66f))
                using (var pen = new Pen(edge)) g.DrawPath(pen, shape);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pulseTimer.Dispose();
                if (background != null) { background.Dispose(); background = null; }
            }
            base.Dispose(disposing);
        }
    }


    // ============================================================
    //  PRESET KARTI
    // ============================================================
    public class PresetCard : GlassControl
    {
        private VcrdbSettings preview;
        private string title;
        private float hoverAnim = 0f;
        private System.Windows.Forms.Timer timer;
        public string ProfileName;   // null => yerleşik preset
        public event EventHandler Activated;
        public event EventHandler DeleteRequested;
        public string SecondaryText;

        public PresetCard(string title, VcrdbSettings s, string profileName)
        {
            AccessibleRole = AccessibleRole.PushButton;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.title = title;
            AccessibleName = title;
            this.preview = s.Clone();
            this.ProfileName = profileName;
            this.Size = new Size(116, 114);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 8.5F);
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 15;
            timer.Tick += new EventHandler(OnTick);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void StartAnim() { if (!timer.Enabled) timer.Start(); }

        private void OnTick(object s, EventArgs e)
        {
            float t = (ClientRectangle.Contains(PointToClient(Control.MousePosition))) ? 1f : 0f;
            hoverAnim = hoverAnim + (t - hoverAnim) * 0.3f;
            if (Math.Abs(t - hoverAnim) < 0.01f) { hoverAnim = t; timer.Stop(); }
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { StartAnim(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { StartAnim(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            if (Activated != null) Activated(this, EventArgs.Empty);
            base.OnClick(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && ContextMenuStrip == null && ProfileName != null && DeleteRequested != null)
                DeleteRequested(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && ProfileName != null && DeleteRequested != null)
                DeleteRequested(this, EventArgs.Empty);
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = Anim.LerpColor(Theme.BgCard, Theme.Accent, hoverAnim * 0.38f);
            Glass.Card(g, ClientRectangle, 14, bg, hoverAnim > 0.25f);
            bool hasTags = !String.IsNullOrEmpty(SecondaryText);
            int footer = hasTags ? 36 : 22;
            CrosshairRenderer.RenderPreview(g, ClientRectangle, Width / 2.0, (Height - footer) / 2.0, preview, 1.9f);

            Rectangle tr = new Rectangle(3, Height - footer, Width - 6, 20);
            if (hasTags)
                TextRenderer.DrawText(g, SecondaryText, Font, new Rectangle(3, Height - 18, Width - 6, 16),
                    Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, title, this.Font, tr, Anim.LerpColor(Theme.TextDim, Theme.Text, hoverAnim),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  RENK SPEKTRUMU
    // ============================================================
    public class ColorSpectrumControl : Control
    {
        private Bitmap spectrum;
        public event EventHandler ColorChanged;
        private Color _selectedColor;
        private int markerX = -1, markerY = -1;

        public Color SelectedColor { get { return _selectedColor; } }

        public ColorSpectrumControl()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(200, 22);
            this.Cursor = Cursors.Cross;
            _selectedColor = Color.Lime;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            GenerateSpectrum();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && spectrum != null) { spectrum.Dispose(); spectrum = null; }
            base.Dispose(disposing);
        }

        private void GenerateSpectrum()
        {
            if (this.Width == 0 || this.Height == 0) return;
            if (spectrum != null) spectrum.Dispose();
            spectrum = new Bitmap(this.Width, this.Height);
            using (Graphics g = Graphics.FromImage(spectrum))
            {
                Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
                Color[] colors = { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red };
                float[] positions = { 0.0f, 0.16f, 0.33f, 0.50f, 0.66f, 0.83f, 1.0f };
                ColorBlend blend = new ColorBlend();
                blend.Colors = colors;
                blend.Positions = positions;
                using (LinearGradientBrush b = new LinearGradientBrush(rect, Color.Black, Color.Black, 0f))
                {
                    b.InterpolationColors = blend;
                    g.FillRectangle(b, rect);
                }
                // üst/alt: beyaz->renk->siyah dikey karışım
                using (LinearGradientBrush wb = new LinearGradientBrush(new Rectangle(0, 0, this.Width, this.Height / 2 + 1), Color.FromArgb(180, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    g.FillRectangle(wb, 0, 0, this.Width, this.Height / 2);
                using (LinearGradientBrush bb = new LinearGradientBrush(new Rectangle(0, this.Height / 2 - 1, this.Width, this.Height / 2 + 2), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(180, 0, 0, 0), 90f))
                    g.FillRectangle(bb, 0, this.Height / 2, this.Width, this.Height / 2 + 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (spectrum != null) e.Graphics.DrawImageUnscaled(spectrum, 0, 0);
            if (markerX >= 0)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.White, 2f)) e.Graphics.DrawEllipse(p, markerX - 5, markerY - 5, 10, 10);
                using (Pen p = new Pen(Color.Black, 1f)) e.Graphics.DrawEllipse(p, markerX - 6, markerY - 6, 12, 12);
            }
        }

        private void PickColor(int x, int y)
        {
            if (spectrum == null) return;
            x = Math.Max(0, Math.Min(this.Width - 1, x));
            y = Math.Max(0, Math.Min(this.Height - 1, y));
            markerX = x; markerY = y;
            _selectedColor = spectrum.GetPixel(x, y);
            Invalidate();
            if (ColorChanged != null) ColorChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { PickColor(e.X, e.Y); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) PickColor(e.X, e.Y); }
    }

    // ============================================================
    //  HOTKEY KUTUSU
    // ============================================================
    public class HotkeyBox : Control
    {
        public Keys KeyValue = Keys.C;
        public bool Ctrl = true, Alt = true, Shift = false;
        private bool focusedBox = false;
        public event EventHandler HotkeyChanged;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            this.TabStop = true;
            this.Size = new Size(180, 36);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI Semibold", 9.75F);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key != Keys.Tab && key != Keys.Escape && keyData != (Keys.Alt | Keys.F4);
        }

        internal static bool IsSupportedKey(Keys key)
        {
            int value = (int)key;
            return value > 7 && value <= 254 && key != Keys.Tab && key != Keys.Escape && key != Keys.F12 &&
                key != Keys.ControlKey && key != Keys.Menu && key != Keys.ShiftKey && key != Keys.LWin && key != Keys.RWin &&
                key != Keys.LControlKey && key != Keys.RControlKey && key != Keys.LMenu && key != Keys.RMenu &&
                key != Keys.LShiftKey && key != Keys.RShiftKey;
        }
        protected override void OnMouseDown(MouseEventArgs e) { this.Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { focusedBox = true; Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { focusedBox = false; Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys kc = e.KeyCode;
            if (!IsSupportedKey(kc) || (!e.Control && !e.Alt && !e.Shift && (kc < Keys.F1 || kc > Keys.F24)))
            {
                Invalidate();
                e.SuppressKeyPress = true;
                return;
            }
            Ctrl = e.Control; Alt = e.Alt; Shift = e.Shift;
            KeyValue = kc;
            e.SuppressKeyPress = true;
            Invalidate();
            if (HotkeyChanged != null) HotkeyChanged(this, EventArgs.Empty);
        }

        public string ComboText()
        {
            StringBuilder sb = new StringBuilder();
            if (Ctrl) sb.Append("CTRL + ");
            if (Alt) sb.Append("ALT + ");
            if (Shift) sb.Append("SHIFT + ");
            sb.Append(KeyValue.ToString().ToUpper());
            return sb.ToString();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            using (SolidBrush b = new SolidBrush(Theme.BgPanelAlt))
            using (GraphicsPath p = Draw.RoundRect(0, 0, this.Width - 1, this.Height - 1, 8))
                g.FillPath(b, p);
            using (Pen pen = new Pen(focusedBox ? Theme.Accent : Theme.Border, focusedBox ? 1.6f : 1f))
            using (GraphicsPath p = Draw.RoundRect(0, 0, this.Width - 1, this.Height - 1, 8))
                g.DrawPath(pen, p);
            string txt = focusedBox ? UiText.Get("Tuş kombinasyonuna bas...") : ComboText();
            TextRenderer.DrawText(g, txt, this.Font, this.ClientRectangle, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (focusedBox) Glass.FocusRing(g, ClientRectangle);
        }
    }

    // ============================================================
    //  KÜÇÜK GİRİŞ DIYALOĞU
    // ============================================================
    public class PromptForm : Form
    {
        private TextBox box;
        public string Value = "";

        public PromptForm(string title, string def)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(360, 152);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Theme.BgPanel;

            Panel border = new Panel();
            border.Dock = DockStyle.Fill;
            border.BackColor = Theme.BgPanel;
            this.Controls.Add(border);

            Label t = new Label();
            t.Text = title; t.ForeColor = Theme.Text; t.Font = new Font("Segoe UI Semibold", 11F);
            t.Location = new Point(20, 18); t.AutoSize = true;
            border.Controls.Add(t);

            box = new TextBox();
            box.Text = def; box.Location = new Point(20, 52); box.Width = 320;
            box.BackColor = Theme.BgPanelAlt; box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.FixedSingle; box.Font = new Font("Segoe UI", 10F);
            box.KeyDown += new KeyEventHandler(OnBoxKey);
            border.Controls.Add(box);

            ModernButton ok = new ModernButton();
            ok.Text = UiText.Get("Tamam"); ok.Location = new Point(178, 100); ok.Size = new Size(76, 34);
            ok.Click += new EventHandler(OnOk);
            border.Controls.Add(ok);

            ModernButton cancel = new ModernButton();
            cancel.Text = UiText.Get("İptal"); cancel.Ghost = true; cancel.Location = new Point(262, 100); cancel.Size = new Size(76, 34);
            cancel.Click += new EventHandler(OnCancel);
            border.Controls.Add(cancel);

            this.Shown += new EventHandler(OnShownFocus);
        }

        private void OnShownFocus(object s, EventArgs e) { box.Focus(); box.SelectAll(); }
        private void OnBoxKey(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) OnOk(s, e); else if (e.KeyCode == Keys.Escape) OnCancel(s, e); }
        private void OnOk(object s, EventArgs e) { Value = box.Text; this.DialogResult = DialogResult.OK; this.Close(); }
        private void OnCancel(object s, EventArgs e) { this.DialogResult = DialogResult.Cancel; this.Close(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Accent, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
        }

        public static string ShowDialog(IWin32Window owner, string title, string def)
        {
            using (PromptForm f = new PromptForm(title, def))
            {
                if (f.ShowDialog(owner) == DialogResult.OK) return f.Value;
                return null;
            }
        }
    }

    // ============================================================
    //  STATUS PILL (üst bar durum göstergesi)
    // ============================================================
    public class StatusPill : GlassControl
    {
        private bool on = true;
        public event EventHandler Toggled;
        protected override int FocusRadius { get { return Height / 2; } }

        public bool On { get { return on; } set { on = value; Invalidate(); } }

        public StatusPill()
        {
            AccessibleRole = AccessibleRole.CheckButton;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            this.Size = new Size(108, 28);
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI Semibold", 8.5F);
        }

        protected override void OnClick(EventArgs e) { if (Toggled != null) Toggled(this, EventArgs.Empty); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color dot = on ? Glass.Success : Theme.TextDim;
            using (SolidBrush b = new SolidBrush(Theme.BgCard))
            using (GraphicsPath p = Draw.RoundRect(0, 0, this.Width, this.Height, this.Height / 2))
                g.FillPath(b, p);
            using (SolidBrush gl = new SolidBrush(Color.FromArgb(70, dot)))
                g.FillEllipse(gl, 11, this.Height / 2 - 7, 14, 14);
            using (SolidBrush db = new SolidBrush(dot))
                g.FillEllipse(db, 14, this.Height / 2 - 4, 8, 8);
            Rectangle tr = new Rectangle(28, 0, this.Width - 32, this.Height);
            TextRenderer.DrawText(g, UiText.Get(on ? "GÖRÜNÜR" : "GİZLİ"), this.Font, tr, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            base.OnPaint(e);
        }
    }

    // ============================================================
    //  ANA FORM
    // ============================================================
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xB001;
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        private const int FORM_W = 1152;
        private const int FORM_H = 738;
        private const int TITLE_H = 68;
        private const int SIDEBAR_W = 172;
        private const int PREVIEW_W = 434;
        private int BODY_H { get { return FORM_H - TITLE_H; } }
        private const int CONTENT_W = FORM_W - SIDEBAR_W - PREVIEW_W;
        private readonly bool compact;
        private int LayoutWidth { get { return compact ? SIDEBAR_W + CONTENT_W : FORM_W; } }

        private AppConfig config;
        private VcrdbSettings settings;
        private OverlayForm overlay;
        private NotifyIcon trayIcon;
        private GameProfileWatcher gameProfiles;
        private VcrdbSettings automaticProfile;
        private ToolStripMenuItem activeGameMenu;
        private ModernButton[] markerButtons;
        private ToggleSwitch roundedArms;
        private ModernSlider topLength, rightLength, bottomLength, leftLength;
        private ToolStripMenuItem overlayMenuItem;
        private Icon appIcon;
        private Bitmap titleLogo;
        private PreviewPanel preview;
        private StatusPill statusPill;

        private bool isUpdatingUI = false;
        private bool crosshairVisible = true;

        private Panel contentHost;
        private Panel[] pages;
        private NavItem[] navItems;
        private Panel slideIndicator;
        private int currentPage = 0;

        private System.Windows.Forms.Timer saveTimer;
        private System.Windows.Forms.Timer fadeTimer;
        private System.Windows.Forms.Timer navAnimTimer;
        private System.Windows.Forms.Timer pageAnimTimer;
        private int navTargetY = 0;

        // editör kontrol referansları
        private ColorSpectrumControl spectrum;
        private Panel colorPreviewBar;
        private TextBox hexBox;
        private ModernSlider slOpacity, slOutThick, slDotThick, slInLen, slInThick, slInOff, slOutLen, slOutThick2, slOutOff, slScale, slOffX, slOffY;
        private ToggleSwitch tgOutline, tgDot, tgIn, tgOut, tgTShape, tgPulse;

        private Panel galleryPanel;
        private HotkeyBox hotkeyBox;
        private ComboBox monitorSelect;
        private Label notice, previewReadout, hotkeyMessage, colorMessage, shareMessage;
        private ModernButton[] accentButtons, materialButtons;
        private bool hotkeyRegistered, closed, dirty;
        private TextBox shareOutBox, shareInBox;

        private Font uiFont = new Font("Segoe UI", 9.75F);
        private Font sliderFont = new Font("Segoe UI Semibold", 8.5F);

        public MainForm() : this(Screen.FromPoint(Cursor.Position).WorkingArea.Size) { }

        internal MainForm(Size available)
        {
            config = ConfigManager.Load();
            UiText.UseEnglish = config.EnglishUi;
            settings = config.Settings;
            Theme.ApplyAccent(config.Accent);
            Glass.Material = config.Material;

            this.FormBorderStyle = FormBorderStyle.None;
            compact = available.Width < FORM_W;
            Size canvas = new Size(LayoutWidth, compact ? FORM_H + BODY_H : FORM_H);
            int scrollWidth = available.Height < canvas.Height ? SystemInformation.VerticalScrollBarWidth : 0;
            ClientSize = new Size(Math.Min(canvas.Width + scrollWidth, available.Width), Math.Min(canvas.Height, available.Height));
            AutoScroll = ClientSize.Width < canvas.Width || ClientSize.Height < canvas.Height;
            if (AutoScroll) AutoScrollMinSize = canvas;
            else
            {
                using (var chrome = Draw.RoundRect(0, 0, FORM_W, FORM_H, 19)) Region = new Region(chrome);
            }
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Theme.Bg;
            this.Text = "Crosshair Studio";
            appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (appIcon != null) Icon = appIcon;
            using (Stream artwork = typeof(MainForm).Assembly.GetManifestResourceStream("Crosshair.Title.png"))
            {
                if (artwork != null)
                    using (Image image = Image.FromStream(artwork)) titleLogo = new Bitmap(image);
            }
            this.AutoScaleMode = AutoScaleMode.None;
            this.KeyPreview = false;

            overlay = new OverlayForm(settings, config.MonitorDevice);
            gameProfiles = new GameProfileWatcher(config, ApplyGameProfile);
            InitializeWorkflow();
            overlay.ScreensChanged += delegate { LoadMonitors(); UpdateScreenOffsetLabel(); };
            overlay.RenderFailed += delegate { SetNotice(UiText.Get("Katman çizilemedi. Ekran seçimini veya Windows görüntü ayarlarını kontrol et.")); };


            BuildTitleBar();
            BuildSidebar();
            BuildPreviewArea();
            BuildContent();

            saveTimer = new System.Windows.Forms.Timer();
            saveTimer.Interval = 450;
            saveTimer.Tick += new EventHandler(OnSaveTick);

            navAnimTimer = new System.Windows.Forms.Timer();
            navAnimTimer.Interval = 15;
            navAnimTimer.Tick += new EventHandler(OnNavAnim);

            pageAnimTimer = new System.Windows.Forms.Timer();
            pageAnimTimer.Interval = 15;
            pageAnimTimer.Tick += new EventHandler(OnPageAnim);

            BuildTray();
            RefreshUIFromSettings();
            SelectPage(0, false);

            VisibleChanged += OnEditorActivityChanged;
            Resize += OnEditorActivityChanged;
            this.Load += new EventHandler(OnFormLoad);
            Shown += delegate { if (AutoScroll) AutoScrollPosition = Point.Empty; };
            this.FormClosing += new FormClosingEventHandler(OnFormClosing);

            // fade-in
            this.Opacity = 0;
            fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 15;
            fadeTimer.Tick += new EventHandler(OnFadeTick);
        }

        private void OnFormLoad(object s, EventArgs e)
        {
            trayIcon.Visible = true;
            hotkeyRegistered = RegisterCurrentHotkey();
            if (!hotkeyRegistered) ShowHotkeyFailure(UiText.Get("Kısayol kaydedilemedi. Ayarlardan başka bir kombinasyon seç."));
            FadeIn();
            overlay.SetEditorActivity(Visible && WindowState != FormWindowState.Minimized, config.BackgroundPulse);
            if (config.HoldVisibility) SetHoldVisibility(true);
            overlay.Start();
            RegisterFavoriteKeys();
            RefreshGameRules();
        }

        private void FadeIn()
        {
            if (SystemInformation.IsMenuAnimationEnabled) fadeTimer.Start();
            else Opacity = 1;
        }

        private void OnFadeTick(object s, EventArgs e)
        {
            if (this.Opacity < 1) { this.Opacity = Math.Min(1, this.Opacity + 0.16); }
            else { fadeTimer.Stop(); }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            PersistNow();
            if (trayIcon != null) trayIcon.Visible = false;
        }

        // ---------- ÜST BAR ----------
        protected override void Dispose(bool disposing)
        {
            if (disposing && !closed)
            {
                closed = true;
                if (hotkeyRegistered && IsHandleCreated) UnregisterHotKey(Handle, HOTKEY_ID);
                VisibleChanged -= OnEditorActivityChanged;
                Resize -= OnEditorActivityChanged;
                DisposeWorkflow();
                if (gameProfiles != null) gameProfiles.Dispose();
                if (overlay != null) overlay.Dispose();
                if (trayIcon != null)
                {
                    ContextMenuStrip menu = trayIcon.ContextMenuStrip;
                    trayIcon.Dispose();
                    if (menu != null) menu.Dispose();
                }
                foreach (var timer in new[] { saveTimer, fadeTimer, navAnimTimer, pageAnimTimer })
                    if (timer != null) timer.Dispose();
                uiFont.Dispose();
                sliderFont.Dispose();
                if (appIcon != null) appIcon.Dispose();
                if (titleLogo != null) titleLogo.Dispose();
            }
            base.Dispose(disposing);
        }

        private void BuildTitleBar()
        {
            Panel titleBar = new Panel();
            titleBar.Location = new Point(0, 0);
            titleBar.Size = new Size(LayoutWidth, TITLE_H);
            titleBar.BackColor = Theme.BgPanelAlt;
            titleBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                var bounds = titleBar.ClientRectangle;
                Color left = Glass.Material == GlassMaterial.Gloss ? Color.FromArgb(78, 35, 57) : Theme.BgPanelAlt;
                using (var titleFill = new LinearGradientBrush(bounds, left, Theme.BgPanel, 8f))
                using (var seam = new Pen(Theme.Border))
                {
                    e.Graphics.FillRectangle(titleFill, bounds);
                    e.Graphics.DrawLine(seam, 0, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                }
            };
            titleBar.MouseDown += new MouseEventHandler(TitleBar_MouseDown);
            this.Controls.Add(titleBar);

            // logo + başlık
            Panel logo = new Panel();
            logo.Location = new Point(13, 10);
            logo.Size = new Size(48, 48);
            logo.BackColor = Color.Transparent;
            logo.Paint += new PaintEventHandler(LogoPaint);
            logo.MouseDown += new MouseEventHandler(TitleBar_MouseDown);
            titleBar.Controls.Add(logo);

            Label title = new Label();
            title.Text = "Crosshair Studio";
            title.BackColor = Color.Transparent;
            title.ForeColor = Theme.Text;
            title.Font = new Font("Bahnschrift", 16F, FontStyle.Regular);
            title.AutoSize = true;
            title.Location = new Point(68, 8);
            title.MouseDown += new MouseEventHandler(TitleBar_MouseDown);
            titleBar.Controls.Add(title);
            var edition = new Label
            {
                Text = "VCRDB   /   WINDOWS", AutoSize = true, Location = new Point(70, 41),
                ForeColor = Theme.TextDim, BackColor = Color.Transparent, Font = sliderFont
            };
            edition.MouseDown += TitleBar_MouseDown;
            titleBar.Controls.Add(edition);

            statusPill = new StatusPill();
            statusPill.Location = new Point(LayoutWidth - 235, (TITLE_H - 28) / 2);
            statusPill.Toggled += new EventHandler(OnStatusToggle);
            statusPill.AccessibleName = UiText.Get("Nişangâhı göster veya gizle");
            titleBar.Controls.Add(statusPill);

            ModernButton btnClose = MakeWinBtn("✕", UiText.Get("Çıkış"), LayoutWidth - 49);
            btnClose.Name = "WindowClose";
            btnClose.Click += new EventHandler(OnCloseClick);
            titleBar.Controls.Add(btnClose);

            ModernButton btnMin = MakeWinBtn("—", UiText.Get("Simge durumuna küçült"), LayoutWidth - 91);
            btnMin.Name = "WindowMinimize";
            btnMin.Click += new EventHandler(OnMinClick);
            titleBar.Controls.Add(btnMin);
        }

        private void LogoPaint(object sender, PaintEventArgs e)
        {
            var tile = (Control)sender;
            if (titleLogo != null) e.Graphics.DrawImageUnscaled(titleLogo, Point.Empty);
            else e.Graphics.DrawIcon(appIcon ?? SystemIcons.Application, tile.ClientRectangle);
        }


        private ModernButton MakeWinBtn(string text, string name, int x)
        {
            return new ModernButton
            {
                Text = text, AccessibleName = name, Ghost = true, CornerRadius = 9,
                Size = new Size(35, 30), Location = new Point(x, (TITLE_H - 30) / 2)
            };
        }

        private void OnCloseClick(object s, EventArgs e) { Close(); }
        private void OnMinClick(object s, EventArgs e) { this.WindowState = FormWindowState.Minimized; }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        }

        // ---------- KENAR MENÜ ----------
        private void BuildSidebar()
        {
            Panel sidebar = new Panel();
            sidebar.Location = new Point(0, TITLE_H);
            sidebar.Size = new Size(SIDEBAR_W, BODY_H);
            sidebar.BackColor = Theme.BgPanel;
            this.Controls.Add(sidebar);

            slideIndicator = new Panel();
            slideIndicator.Size = new Size(4, 22);
            slideIndicator.BackColor = Theme.AccentInk;
            slideIndicator.Location = new Point(0, 40);

            sidebar.Controls.Add(slideIndicator);

            AddSidebarHeader(sidebar, UiText.Get("STÜDYO"), 26);

            navItems = new NavItem[6];
            string[] names =
            {
                UiText.Get("Editör"), UiText.Get("Profiller"), UiText.Get("Konum"),
                UiText.Get("Ekran"), UiText.Get("Ayarlar"), UiText.Get("Yardım")
            };
            string[] glyphs = { "cross", "grid", "move", "monitor", "gear", "help" };
            int[] ys = { 51, 94, 137, 180, 266, 309 };
            for (int i = 0; i < 6; i++)
            {
                NavItem ni = new NavItem(names[i], glyphs[i], i);
                ni.Location = new Point(10, ys[i]);
                ni.Width = SIDEBAR_W - 20;
                ni.Click += new EventHandler(OnNavClick);
                sidebar.Controls.Add(ni);
                navItems[i] = ni;
            }

            AddSidebarHeader(sidebar, UiText.Get("UYGULAMA"), 240);

            ModernButton hideBtn = new ModernButton();
            hideBtn.Text = UiText.Get("Tepsiye Gizle");
            hideBtn.Location = new Point(16, compact ? 420 : BODY_H - 60);
            if (compact)
            {
                var jump = OptionButton(sidebar, UiText.Get("Vizöre Git"), 16, 370, SIDEBAR_W - 32);
                jump.Click += delegate { AutoScrollPosition = new Point(0, FORM_H - 18); };
            }
            hideBtn.Size = new Size(SIDEBAR_W - 32, 44);
            hideBtn.CornerRadius = 13;
            hideBtn.Ghost = true;
            hideBtn.Click += new EventHandler(OnHidePlay);
            sidebar.Controls.Add(hideBtn);
        }

        private Label AddSidebarHeader(Panel host, string text, int y)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = Theme.TextDim;
            l.Font = new Font("Segoe UI Semibold", 7.5F);
            l.AutoSize = true; l.Location = new Point(18, y);
            host.Controls.Add(l);
            return l;
        }

        private void OnNavClick(object sender, EventArgs e)
        {
            NavItem ni = (NavItem)sender;
            SelectPage(ni.PageIndex, true);
        }

        private void OnEditorActivityChanged(object sender, EventArgs e)
        {
            if (closed || preview == null || !IsHandleCreated) return;
            bool active = Visible && WindowState != FormWindowState.Minimized;
            preview.SyncAnimation();
            overlay.SetEditorActivity(active, config.BackgroundPulse);
            if (active) return;
            if (fadeTimer != null && fadeTimer.Enabled) { fadeTimer.Stop(); Opacity = 1; }
            if (pageAnimTimer != null) { pageAnimTimer.Stop(); if (pages != null) pages[currentPage].Left = 0; }
            if (navAnimTimer != null) { navAnimTimer.Stop(); if (slideIndicator != null) slideIndicator.Top = navTargetY; }
        }

        private void OnHidePlay(object s, EventArgs e)
        {
            if (saveTimer != null) saveTimer.Stop();
            PersistNow();
            this.Hide();
            trayIcon.Visible = true;
        }

        private void OnNavAnim(object s, EventArgs e)
        {
            int cur = slideIndicator.Top;
            int d = navTargetY - cur;
            if (Math.Abs(d) <= 2) { slideIndicator.Top = navTargetY; navAnimTimer.Stop(); }
            else slideIndicator.Top = cur + (int)Math.Round(d * 0.3, MidpointRounding.AwayFromZero);
        }

        // ---------- ÖNİZLEME ALANI ----------
        private void BuildPreviewArea()
        {
            var pv = new Panel
            {
                Location = compact ? new Point(SIDEBAR_W + 14, FORM_H) : new Point(FORM_W - PREVIEW_W, TITLE_H),
                Size = new Size(PREVIEW_W, BODY_H), BackColor = Theme.BgPanel
            };
            pv.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var edge = new Pen(Theme.Border)) e.Graphics.DrawLine(edge, 0, 0, 0, BODY_H);
                using (var edge = new Pen(Theme.Border)) e.Graphics.DrawLine(edge, 24, 474, PREVIEW_W - 24, 474);
            };
            Controls.Add(pv);

            AddSidebarHeader(pv, UiText.Get("CANLI ÖNİZLEME"), 23);
            var heading = new Label
            {
                Text = UiText.Get("Optik vizör"), Font = new Font("Bahnschrift", 26f),
                AutoSize = true, ForeColor = Theme.Text, Location = new Point(22, 41)
            };
            pv.Controls.Add(heading);
            preview = new PreviewPanel(settings) { Location = new Point(23, 104), Size = new Size(PREVIEW_W - 46, 326) };
            pv.Controls.Add(preview);
            previewReadout = new Label
            {
                Font = new Font("Consolas", 10F), ForeColor = Optics.WarmInk,
                Location = new Point(24, 440), AutoSize = true
            };
            pv.Controls.Add(previewReadout);
            UpdateReadout();

            AddSidebarHeader(pv, UiText.Get("YAKINLAŞTIRMA"), 490);
            var backgroundHeading = AddSidebarHeader(pv, UiText.Get("ZEMİN"), 558);
            AddPreviewChoices(pv, new[] { "1×", "2×", "3×" }, 513, 1,
                delegate(int i) { preview.Zoom = i + 1; preview.Invalidate(); });

            // Starts right of the 1px edge line painted at x = 0 so the divider stays unbroken.
            var backdropControls = new Panel { Bounds = new Rectangle(1, 581, PREVIEW_W - 1, 36), BackColor = Theme.BgPanel };
            var testControls = new Panel { Bounds = backdropControls.Bounds, BackColor = Theme.BgPanel, Visible = false };
            pv.Controls.Add(backdropControls);
            pv.Controls.Add(testControls);
            int normalScene = 0, testScene = VisibilityScenes.Sky;
            AddPreviewChoices(backdropControls, new[] { UiText.Get("Optik"), UiText.Get("Dama"), UiText.Get("Açık") }, 0, 0,
                delegate(int i) { normalScene = i; preview.SetBackgroundMode(i); }, 23);
            AddPreviewChoices(testControls, new[] { UiText.Get("Gökyüzü"), UiText.Get("Loş"), UiText.Get("Hareket") }, 0, 0,
                delegate(int i) { testScene = VisibilityScenes.Sky + i; preview.SetBackgroundMode(testScene); }, 23);

            var visibility = OptionButton(pv, UiText.Get("Görünürlük Testi"), PREVIEW_W - 169, 51, 146);
            visibility.Name = "VisibilityTest";
            visibility.Click += delegate
            {
                visibility.Selected = !visibility.Selected;
                testControls.Visible = visibility.Selected;
                backdropControls.Visible = !visibility.Selected;
                backgroundHeading.Text = visibility.Selected ? UiText.Get("SINAMA SAHNESİ") : UiText.Get("ZEMİN");
                preview.SetBackgroundMode(visibility.Selected ? testScene : normalScene);
            };

            notice = new Label
            {
                Text = "", Font = sliderFont, ForeColor = Theme.TextDim,
                Location = new Point(24, 627), Size = new Size(PREVIEW_W - 48, 36)
            };
            pv.Controls.Add(notice);
        }

        private void AddPreviewChoices(Panel host, string[] names, int y, int selected, Action<int> apply, int left = 24)
        {
            var buttons = new ModernButton[names.Length];
            int width = (PREVIEW_W - 62) / buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                var button = new ModernButton
                {
                    Text = names[i], Ghost = true, Selected = i == selected, CornerRadius = 10,
                    Location = new Point(left + (width + 8) * i, y), Size = new Size(width, 36)
                };
                buttons[i] = button;
                button.Click += delegate
                {
                    foreach (var item in buttons) item.Selected = item == button;
                    apply(index);
                };
                host.Controls.Add(button);
            }
        }


        private void UpdateReadout()
        {
            if (previewReadout == null) return;
            previewReadout.Text = String.Format("#{0:X6}   |   {1}%   |   {2:+0;-0;0}, {3:+0;-0;0} px",
                settings.Color.ToArgb() & 0xFFFFFF, settings.Scale, settings.OffsetX, settings.OffsetY);
        }

        // ---------- İÇERİK ----------
        private void BuildContent()
        {
            contentHost = new Panel();
            contentHost.Location = new Point(SIDEBAR_W, TITLE_H);
            contentHost.Size = new Size(CONTENT_W, BODY_H);
            contentHost.BackColor = Theme.Bg;
            this.Controls.Add(contentHost);

            pages = new Panel[6];
            pages[0] = BuildEditorPage();
            pages[1] = BuildPresetsPage();
            pages[2] = BuildPositionPage();
            pages[3] = BuildDisplayPage();
            pages[4] = BuildSettingsPage();
            pages[5] = BuildHelpPage();

            for (int i = 0; i < 6; i++)
            {
                pages[i].Location = new Point(0, 0);
                pages[i].Size = new Size(CONTENT_W, BODY_H);
                pages[i].BackColor = Theme.Bg;
                pages[i].Visible = false;
                contentHost.Controls.Add(pages[i]);
            }
        }

        private Panel NewPage() { Panel p = new Panel(); p.BackColor = Theme.Bg; return p; }

        private Label MakeHeader(Panel host, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = Theme.Text; l.Font = new Font("Segoe UI Semibold", 20F);
            l.AutoSize = true; l.Location = new Point(x, y);
            host.Controls.Add(l);
            return l;
        }

        private Label MakeSection(Panel host, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = Theme.AccentInk;
            l.Tag = "accent-text"; l.Font = new Font("Segoe UI Semibold", 8.5F);
            l.AutoSize = true; l.Location = new Point(x, y);
            host.Controls.Add(l);
            return l;
        }

        private Label MakeRowLabel(Panel host, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text; l.ForeColor = Theme.TextDim; l.Font = uiFont;
            l.AutoSize = true; l.Location = new Point(x, y + 5);
            host.Controls.Add(l);
            return l;
        }

        private ModernSlider AddSlider(Panel host, string label, int x, int y, int min, int max, string suffix)
        {
            MakeRowLabel(host, label, x, y);
            ModernSlider sl = new ModernSlider();
            sl.AccessibleName = label;
            sl.Location = new Point(x + 132, y);
            sl.Width = CONTENT_W - x - 156; sl.Height = 28;
            sl.Minimum = min; sl.Maximum = max; sl.Suffix = suffix;
            sl.Font = sliderFont;
            host.Controls.Add(sl);
            return sl;
        }

        private ToggleSwitch AddToggle(Panel host, string label, int x, int y)
        {
            MakeRowLabel(host, label, x, y);
            ToggleSwitch t = new ToggleSwitch();
            t.AccessibleName = label;
            t.Location = new Point(x + 132, y);
            host.Controls.Add(t);
            return t;
        }

        // ----- Sayfa 0: Editör -----
        private Panel BuildEditorPage()
        {
            Panel page = NewPage();
            int px = 28;
            MakeSection(page, UiText.Get("NİŞANGÂH STÜDYOSU"), px, 25);
            Label title = MakeHeader(page, UiText.Get("Tasarım masası"), px - 2, 42);
            title.Font = new Font("Bahnschrift", 26f);
            MakeRowLabel(page, UiText.Get("Rengi, boyutu ve nişangâhın parçalarını ayarla."), px, 88);

            TabStrip tabs = new TabStrip(new[]
            {
                UiText.Get("Genel"), UiText.Get("Merkez"), UiText.Get("İç çizgiler"),
                UiText.Get("Dış çizgiler"), UiText.Get("Şekil")
            });
            tabs.Location = new Point(px, 128);
            tabs.Size = new Size(CONTENT_W - px * 2, 42);
            page.Controls.Add(tabs);

            Panel[] sub = new Panel[5];
            for (int i = 0; i < sub.Length; i++)
            {
                sub[i] = new Panel();
                sub[i].Location = new Point(0, 190);
                sub[i].Size = new Size(CONTENT_W, 375);
                sub[i].BackColor = Theme.Bg;
                sub[i].Visible = (i == 0);
                page.Controls.Add(sub[i]);
            }
            tabs.SelectedIndexChanged += delegate (object s, EventArgs e)
            {
                for (int i = 0; i < sub.Length; i++) sub[i].Visible = (i == tabs.SelectedIndex);
            };

            // --- GENEL ---
            Panel g0 = sub[0];
            g0.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var outline = Draw.RoundRect(17, 0, CONTENT_W - 34, 364, 16))
                using (var rim = new Pen(Color.FromArgb(57, 41, 57))) e.Graphics.DrawPath(rim, outline);
                using (var divider = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(divider, 28, 120, CONTENT_W - 28, 120);
                    e.Graphics.DrawLine(divider, 28, 341, CONTENT_W - 28, 341);
                }
            };
            MakeRowLabel(g0, UiText.Get("Renk"), px, 12);
            spectrum = new ColorSpectrumControl();
            spectrum.Location = new Point(px + 132, 10); spectrum.Size = new Size(300, 26);
            spectrum.ColorChanged += new EventHandler(OnSpectrumChanged);
            g0.Controls.Add(spectrum);

            colorPreviewBar = new Panel();
            colorPreviewBar.Location = new Point(px + 132, 40); colorPreviewBar.Size = new Size(300, 5);
            colorPreviewBar.BackColor = settings.Color;
            g0.Controls.Add(colorPreviewBar);

            Color[] sw = { Color.White, Color.Black, Color.Red, Color.Lime, Color.Cyan, Color.DeepSkyBlue, Color.Yellow, Color.Magenta, Color.FromArgb(255,132,24), Color.HotPink };
            for (int i = 0; i < sw.Length; i++)
            {
                var s = new ColorSwatch(sw[i]);
                s.Location = new Point(px + 132 + i * 28, 56); s.Size = new Size(22, 22);

                Color cc = sw[i];
                s.Click += delegate (object o, EventArgs e) { SetColor(cc); };
                g0.Controls.Add(s);
            }

            MakeRowLabel(g0, UiText.Get("Renk kodu"), px, 92);
            hexBox = new TextBox();
            hexBox.Location = new Point(px + 132, 90); hexBox.Width = 100;
            hexBox.BackColor = Theme.BgPanelAlt; hexBox.ForeColor = Theme.Text;
            hexBox.BorderStyle = BorderStyle.FixedSingle; hexBox.Font = uiFont;
            hexBox.AccessibleName = UiText.Get("Onaltılık renk kodu");
            hexBox.MaxLength = 7;
            hexBox.KeyDown += new KeyEventHandler(OnHexKey);
            g0.Controls.Add(hexBox);
            colorMessage = new Label
            {
                Font = sliderFont, ForeColor = Glass.Warning, AutoSize = true,
                Location = new Point(px + 242, 92)
            };
            g0.Controls.Add(colorMessage);

            slOpacity = AddSlider(g0, UiText.Get("Opaklık"), px, 130, 0, 255, "");
            slOpacity.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.Opacity = slOpacity.Value; Pushed(); } };

            tgOutline = AddToggle(g0, UiText.Get("Dış Hat"), px, 174);
            tgOutline.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutlineShow = tgOutline.Checked; Pushed(); } };

            slOutThick = AddSlider(g0, UiText.Get("Hat Kalınlığı"), px, 214, 1, 6, "");
            slOutThick.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutlineThick = slOutThick.Value; Pushed(); } };

            slScale = AddSlider(g0, UiText.Get("Global Boyut"), px, 254, 50, 300, "%");
            slScale.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.Scale = slScale.Value; if (positionScaleSlider != null) positionScaleSlider.SetValueSilent(slScale.Value); Pushed(); } };

            tgTShape = AddToggle(g0, UiText.Get("T-Şekli"), px, 296);
            tgTShape.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.TShape = tgTShape.Checked; Pushed(); } };

            tgPulse = AddToggle(g0, UiText.Get("Nabız efekti"), px + 230, 296);
            tgPulse.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.Pulse = tgPulse.Checked; Pushed(); } };
            var quickSave = OptionButton(page, UiText.Get("＋  Profil Olarak Kaydet"), px, 581, 192);
            quickSave.Ghost = false;
            quickSave.Click += OnSaveProfile;
            quickSave.BringToFront();
            AddEditorActions(page, px);

            // --- MERKEZ ---
            Panel g1 = sub[1];
            MakeSection(g1, UiText.Get("MERKEZ NOKTA"), px, 10);
            tgDot = AddToggle(g1, UiText.Get("Göster"), px, 40);
            tgDot.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.DotShow = tgDot.Checked; Pushed(); } };
            slDotThick = AddSlider(g1, UiText.Get("Boyut"), px, 84, 1, 24, "px");
            slDotThick.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.DotThick = slDotThick.Value; Pushed(); } };

            // --- İÇ ---
            Panel g2 = sub[2];
            MakeSection(g2, UiText.Get("İÇ ÇİZGİLER"), px, 10);
            tgIn = AddToggle(g2, UiText.Get("Göster"), px, 40);
            tgIn.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.InShow = tgIn.Checked; Pushed(); } };
            slInLen = AddSlider(g2, UiText.Get("Uzunluk"), px, 84, 0, 30, "");
            slInLen.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.InLen = slInLen.Value; Pushed(); } };
            slInThick = AddSlider(g2, UiText.Get("Kalınlık"), px, 124, 1, 12, "");
            slInThick.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.InThick = slInThick.Value; Pushed(); } };
            slInOff = AddSlider(g2, UiText.Get("Boşluk"), px, 164, 0, 30, "");
            slInOff.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.InOff = slInOff.Value; Pushed(); } };

            // --- DIŞ ---
            Panel g3 = sub[3];
            MakeSection(g3, UiText.Get("DIŞ ÇİZGİLER"), px, 10);
            tgOut = AddToggle(g3, UiText.Get("Göster"), px, 40);
            tgOut.CheckedChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutShow = tgOut.Checked; Pushed(); } };
            slOutLen = AddSlider(g3, UiText.Get("Uzunluk"), px, 84, 0, 30, "");
            slOutLen.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutLen = slOutLen.Value; Pushed(); } };
            slOutThick2 = AddSlider(g3, UiText.Get("Kalınlık"), px, 124, 1, 12, "");
            slOutThick2.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutThick = slOutThick2.Value; Pushed(); } };
            slOutOff = AddSlider(g3, UiText.Get("Boşluk"), px, 164, 0, 60, "");
            slOutOff.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OutOff = slOutOff.Value; Pushed(); } };

            BuildShapeEditor(sub[4], px);
            return page;
        }

        private void BuildShapeEditor(Panel host, int x)
        {
            MakeSection(host, UiText.Get("MERKEZ İŞARETİ"), x, 11);
            string[] names = { UiText.Get("Kare"), UiText.Get("Halka"), UiText.Get("Elmas") };
            markerButtons = new ModernButton[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var option = OptionButton(host, names[i], x + 87 * i, 38, 79);
                markerButtons[i] = option;
                option.Selected = (int)settings.Marker == index;
                option.Click += delegate
                {
                    settings.Marker = (CenterShape)index;
                    settings.DotShow = true;
                    tgDot.SetCheckedSilent(true);
                    UpdateMarkerButtons();
                    Pushed();
                };
            }

            roundedArms = AddToggle(host, UiText.Get("Yuvarlak uçlar"), x + 268, 41);
            roundedArms.CheckedChanged += delegate { settings.RoundedArms = roundedArms.Checked; Pushed(); };

            MakeSection(host, UiText.Get("DÖRT KOLUN UZUNLUĞU"), x, 98);
            MakeRowLabel(host, UiText.Get("İç ve dış çizgilere uygulanır. %0 ilgili kolu gizler."), x, 121);
            topLength = AddArmSlider(host, UiText.Get("Üst"), x, 164, value => settings.TopLength = value);
            rightLength = AddArmSlider(host, UiText.Get("Sağ"), x, 203, value => settings.RightLength = value);
            bottomLength = AddArmSlider(host, UiText.Get("Alt"), x, 242, value => settings.BottomLength = value);
            leftLength = AddArmSlider(host, UiText.Get("Sol"), x, 281, value => settings.LeftLength = value);
            doubleOutline = AddToggle(host, UiText.Get("Çift renk dış hat"), x + 166, 329);
            doubleOutline.CheckedChanged += delegate
            {
                settings.DoubleOutline = doubleOutline.Checked;
                if (doubleOutline.Checked)
                {
                    settings.OutlineShow = true;
                    tgOutline.SetCheckedSilent(true);
                }
                Pushed();
            };
            var reset = OptionButton(host, UiText.Get("Kolları Eşitle"), x, 325, 141);
            reset.Click += delegate
            {
                settings.TopLength = settings.RightLength = settings.BottomLength = settings.LeftLength = 100;
                RefreshUIFromSettings();
                Pushed();
            };
        }

        private void UpdateMarkerButtons()
        {
            if (markerButtons == null) return;
            for (int i = 0; i < markerButtons.Length; i++)
                markerButtons[i].Selected = (int)settings.Marker == i;
        }

        private ModernSlider AddArmSlider(Panel host, string label, int x, int y, Action<int> apply)
        {
            var slider = AddSlider(host, label, x, y, 0, 200, "%");
            slider.ValueChanged += delegate
            {
                if (isUpdatingUI) return;
                apply(slider.Value);
                Pushed();
            };
            return slider;
        }

        private void OnSpectrumChanged(object s, EventArgs e)
        {
            if (isUpdatingUI) return;
            SetColor(spectrum.SelectedColor);
        }

        private void OnHexKey(object s, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            string hex = hexBox.Text.Trim().TrimStart('#');
            if (hex.Length == 3) hex = String.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            int rgb;
            if (hex.Length == 6 && Int32.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out rgb))
            {
                SetColor(Color.FromArgb(unchecked((int)0xFF000000) | rgb));
                return;
            }
            colorMessage.Text = UiText.Get("Örnek: #FFE5DC");
            hexBox.SelectAll();
        }


        private void SetColor(Color c)
        {
            settings.Color = c;
            colorMessage.Text = "";
            isUpdatingUI = true;
            if (colorPreviewBar != null) colorPreviewBar.BackColor = c;
            if (hexBox != null) hexBox.Text = "#" + (c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
            isUpdatingUI = false;
            Pushed();
        }

        // ----- Sayfa 1: Presetler -----
        private Panel BuildPresetsPage()
        {
            Panel page = NewPage();
            MakeHeader(page, UiText.Get("Profil galerisi"), 28, 16);
            OptionButton(page, UiText.Get("＋  Profili Kaydet"), CONTENT_W - 171, 17, 146).Click += OnSaveProfile;
            Label sub = new Label();
            sub.Text = UiText.Get("Tıklayarak uygula, sağ tıklayarak profil işlemlerini aç.");
            sub.ForeColor = Theme.TextDim; sub.Font = uiFont; sub.AutoSize = true; sub.Location = new Point(28, 59);
            page.Controls.Add(sub);

            galleryPanel = new Panel();
            OptionButton(page, UiText.Get("Oyuna Özel"), 28, 89, 142).Click += EditGameRules;
            OptionButton(page, UiText.Get("Paket İçe Aktar"), 178, 89, 140).Click += ImportProfilePack;
            OptionButton(page, UiText.Get("Paketi Dışa Aktar"), 326, 89, 163).Click += ExportProfilePack;

            AddLibraryTools(page);
            galleryPanel.Location = new Point(28, 223);
            galleryPanel.Size = new Size(CONTENT_W - 45, BODY_H - 232);
            galleryPanel.BackColor = Theme.Bg;
            galleryPanel.AutoScroll = true;
            Theme.DarkScrollbars(galleryPanel);
            page.Controls.Add(galleryPanel);
            return page;
        }

        private void RebuildGallery()
        {
            if (galleryPanel == null) return;
            galleryStale = false;
            string query = profileSearch == null ? "" : profileSearch.Text;
            bool favoritesOnly = favoritesFilter != null && favoritesFilter.Selected;
            const int cardWidth = 111, cardHeight = 110, gap = 9;
            int columns = Math.Max(1, (galleryPanel.Width - SystemInformation.VerticalScrollBarWidth + gap) / (cardWidth + gap));
            int y = 0, column = 0, shown = 0;
            galleryPanel.SuspendLayout();
            try
            {
                galleryPanel.AutoScrollPosition = Point.Empty;
                while (galleryPanel.Controls.Count > 0) galleryPanel.Controls[0].Dispose();

                Action<string> startGroup = label =>
                {
                    if (column > 0) { column = 0; y += cardHeight + gap; }
                    MakeSection(galleryPanel, label, 2, y);
                    y += 25;
                };
                Action<string, VcrdbSettings, string> add = (label, design, savedName) =>
                {
                    var card = new PresetCard(label, design, savedName)
                    {
                        Location = new Point(column * (cardWidth + gap), y),
                        Size = new Size(cardWidth, cardHeight)
                    };
                    card.Activated += delegate { ApplySettingsObject(design); };
                    SetupProfileCard(card, savedName, design);
                    galleryPanel.Controls.Add(card);
                    shown++;
                    if (++column == columns) { column = 0; y += cardHeight + gap; }
                };

                bool personal = false;
                foreach (var profile in SavedDesigns())
                {
                    if (!config.Library.Matches(profile.Key, query, favoritesOnly)) continue;
                    if (!personal) { startGroup(favoritesOnly ? UiText.Get("FAVORİLERİM") : UiText.Get("PROFİLLERİM")); personal = true; }
                    string label = (config.Library.Favorites.Contains(profile.Key) ? "★ " : "") + profile.Key;
                    add(label, profile.Value, profile.Key);
                }

                if (!favoritesOnly)
                {
                    bool builtIn = false;
                    for (int i = 0; i < PresetNames.Length; i++)
                    {
                        string name = PresetNames[i];
                        if (name.IndexOf(query.Trim(), StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                        if (!builtIn) { startGroup(UiText.Get("YERLEŞİK TASARIMLAR")); builtIn = true; }
                        add(name, BuildPreset(i), null);
                    }
                }

                if (shown == 0)
                    MakeRowLabel(galleryPanel, favoritesOnly ? UiText.Get("Eşleşen favori profil yok.") : UiText.Get("Eşleşen profil bulunamadı."), 2, 4);
            }
            finally { galleryPanel.ResumeLayout(true); }
        }

        private void OnSaveProfile(object s, EventArgs e)
        {
            string name = PromptForm.ShowDialog(this, UiText.Get("Profil adı:"), UiText.Get("Yeni Profil"));
            if (name == null || name.Trim().Length == 0) return;
            name = name.Trim();
            if (!ConfigManager.IsValidProfileName(name))
            {
                ShowProfileError(UiText.Get("1–40 karakterli geçerli bir dosya adı kullan."));
                return;
            }
            if (ConfigManager.ProfileExists(name) && DarkMessage.Show(this, string.Format(UiText.Get("'{0}' güncellensin mi?"), name),
                UiText.Get("Profili güncelle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { ConfigManager.SaveProfile(name, settings); }
            catch (IOException) { ShowProfileError(UiText.Get("Profil kaydedilemedi.")); return; }
            catch (UnauthorizedAccessException) { ShowProfileError(UiText.Get("Profili kaydetme izni yok.")); return; }
            ProfilesChanged();
        }

        private void ProfilesChanged()
        {
            savedDesigns = null;
            galleryStale = true;
            RegisterFavoriteKeys();
            if (currentPage == 1) RebuildGallery();
            RebuildTrayMenu();
            RefreshGameRules();
        }

        private void EditGameRules(object sender, EventArgs e)
        {
            using (var dialog = new GameRulesDialog(config.GameProfiles, ConfigManager.ListProfiles()))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                config.GameProfiles.Clear();
                foreach (var item in dialog.Rules) config.GameProfiles.Add(item.Key, item.Value);
            }
            RefreshGameRules();
            ScheduleSave();
        }

        private void RefreshGameRules()
        {
            if (!gameProfiles.RefreshRules()) SetNotice(UiText.Get("Windows, oyun profili algılamasını başlatamadı."));
        }

        private void ApplyGameProfile(VcrdbSettings selected, string name)
        {
            automaticProfile = selected;
            temporaryFavorite = null;
            if (!calibrating) overlay.UpdateSettings(EffectiveDesign);
            if (activeGameMenu != null)
            {
                activeGameMenu.Available = name != null;
                activeGameMenu.Text = name == null ? "" : UiText.Get("Oyun profili: ") + name;
            }
        }

        private const string ProfilePackFilter = "Crosshair profil paketi (*.vcpak)|*.vcpak";

        private void ImportProfilePack(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = UiText.Get(ProfilePackFilter), CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var pack = ProfilePack.Read(dialog.FileName);
                    ProfilePack.ImportResult result;
                    using (var previewPack = new PackPreviewDialog(pack, Screen.FromControl(this).WorkingArea.Size))
                    {
                        if (previewPack.ShowDialog(this) != DialogResult.OK) return;
                        result = pack.Import(previewPack.Selected);
                    }
                    if (result.Imported > 0) ProfilesChanged();
                    DarkMessage.Show(this, String.Format(UiText.Get("{0} eklendi · {1} aynı ad korundu · {2} yazılamadı"),
                        result.Imported, result.Skipped, result.Failed), UiText.Get("Profil paketi"),
                        MessageBoxButtons.OK, result.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                catch (FormatException) { ShowProfileError(UiText.Get("Paket biçimi geçersiz veya dosya sınırları aşıldı.")); }
                catch (DecoderFallbackException) { ShowProfileError(UiText.Get("Paket metni okunamadı.")); }
                catch (IOException) { ShowProfileError(UiText.Get("Profil paketi okunamadı.")); }
                catch (UnauthorizedAccessException) { ShowProfileError(UiText.Get("Profil paketini okumak için izin yok.")); }
            }
        }

        private void ExportProfilePack(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog
            {
                Filter = UiText.Get(ProfilePackFilter), DefaultExt = "vcpak", FileName = UiText.Get("Crosshair-profilleri")
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int count = ProfilePack.Export(dialog.FileName);
                    SetNotice(count == 0 ? UiText.Get("Paket için önce bir profil kaydet.")
                        : string.Format(UiText.Get("{0} profil pakete kaydedildi."), count));
                }
                catch (IOException) { ShowProfileError(UiText.Get("Paket kaydedilemedi. Dosya yolunu ve profil sayısını kontrol et.")); }
                catch (UnauthorizedAccessException) { ShowProfileError(UiText.Get("Bu dosyaya yazmak için izin yok.")); }
            }
        }

        private void ShowProfileError(string message)
        {
            DarkMessage.Show(this, message, UiText.Get("Profil"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private VcrdbSettings BuildPreset(int index)
        {
            if (index >= 9) return ContrastPreset(index);
            VcrdbSettings s = new VcrdbSettings();
            if (index == 0) { s.Color = Color.Cyan; s.OutlineShow = false; s.DotShow = false; s.InShow = true; s.InLen = 5; s.InThick = 2; s.InOff = 3; s.OutShow = false; }
            else if (index == 1) { s.Color = Color.Lime; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = false; s.InShow = true; s.InLen = 9; s.InThick = 3; s.InOff = 6; s.OutShow = false; }
            else if (index == 2) { s.Color = Color.Cyan; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = true; s.DotThick = 3; s.InShow = false; s.OutShow = false; }
            else if (index == 3) { s.Color = Color.White; s.OutlineShow = false; s.DotShow = false; s.InShow = true; s.InLen = 3; s.InThick = 2; s.InOff = 0; s.OutShow = false; }
            else if (index == 4) { s.Color = Color.FromArgb(0, 255, 100); s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = false; s.InShow = true; s.InLen = 6; s.InThick = 2; s.InOff = 4; s.OutShow = false; }
            else if (index == 5) { s.Color = Color.Red; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = true; s.DotThick = 4; s.InShow = false; s.OutShow = false; }
            else if (index == 6) { s.Color = Color.White; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = false; s.InShow = true; s.InLen = 8; s.InThick = 3; s.InOff = 4; s.TShape = true; s.OutShow = false; }
            else if (index == 7) { s.Color = Color.Lime; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = true; s.DotThick = 2; s.InShow = true; s.InLen = 7; s.InThick = 2; s.InOff = 5; s.OutShow = false; }
            else if (index == 8) { s.Color = Color.Yellow; s.OutlineShow = true; s.OutlineThick = 1; s.DotShow = true; s.DotThick = 2; s.InShow = true; s.InLen = 14; s.InThick = 1; s.InOff = 10; s.OutShow = true; s.OutLen = 4; s.OutThick = 1; s.OutOff = 20; }
            return s;
        }

        // ----- Sayfa 2: Pozisyon -----
        private Panel BuildPositionPage()
        {
            Panel page = NewPage();
            int px = 28;
            MakeHeader(page, UiText.Get("Pozisyon ve Boyut"), px, 16);
            MakeSection(page, UiText.Get("MERKEZDEN KAYDIRMA"), px, 70);

            slOffX = AddSlider(page, UiText.Get("Yatay (X)"), px, 101, -300, 300, "px");
            slOffX.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OffsetX = slOffX.Value; Pushed(); } };
            slOffY = AddSlider(page, UiText.Get("Dikey (Y)"), px, 143, -300, 300, "px");
            slOffY.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.OffsetY = slOffY.Value; Pushed(); } };

            MakeSection(page, UiText.Get("BOYUT"), px, 194);
            ModernSlider scale2 = AddSlider(page, UiText.Get("Global Boyut"), px, 222, 50, 300, "%");
            scale2.ValueChanged += delegate (object s, EventArgs e) { if (!isUpdatingUI) { settings.Scale = scale2.Value; if (slScale != null) slScale.SetValueSilent(scale2.Value); Pushed(); } };
            // slScale değişince bu da güncellensin
            this.positionScaleSlider = scale2;

            ModernButton reset = new ModernButton();
            reset.Text = UiText.Get("Ortala / Sıfırla");
            reset.Ghost = true;
            reset.Location = new Point(px, 276);
            reset.Size = new Size(180, 38);
            reset.Click += delegate (object s, EventArgs e)
            {
                settings.OffsetX = 0; settings.OffsetY = 0; settings.Scale = 100;
                RefreshUIFromSettings(); Pushed();
            };
            page.Controls.Add(reset);

            Label note = new Label();
            note.Text = UiText.Get("Çoğu oyunda nişan noktası ekranın tam merkezindedir. Eğer crosshair\nbiraz kayık duruyorsa buradan ince ayar yapabilirsin.");
            note.ForeColor = Theme.TextDim; note.Font = uiFont; note.AutoSize = false;
            note.Size = new Size(CONTENT_W - px - 20, 50); note.Location = new Point(px, 330);
            page.Controls.Add(note);

            MakeSection(page, UiText.Get("EKRANA ÖZEL DÜZELTME"), px, 390);
            screenOffsetLabel = MakeRowLabel(page, "", px, 424);
            UpdateScreenOffsetLabel();
            OptionButton(page, UiText.Get("Piksel Kalibrasyonu"), px, 460, 176).Click += OpenCalibration;
            MakeRowLabel(page, UiText.Get("Hedef ekranı Ekran sayfasından değiştir."), px, 507);
            return page;
        }
        private ModernSlider positionScaleSlider;

        // ----- Sayfa 3: Görüntü Modu -----
        private Panel BuildDisplayPage()
        {
            Panel page = NewPage();
            int px = 28;
            MakeHeader(page, UiText.Get("Ekran ve katman"), px, 16);
            MakeSection(page, UiText.Get("HEDEF EKRAN"), px, 76);

            monitorSelect = new ComboBox
            {
                Location = new Point(px, 103), Size = new Size(320, 30),
                DropDownStyle = ComboBoxStyle.DropDownList, Font = uiFont,
                BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text, AccessibleName = UiText.Get("Hedef ekran")
            };
            Theme.DarkPicker(monitorSelect);
            monitorSelect.SelectionChangeCommitted += delegate
            {
                MonitorChoice selected = monitorSelect.SelectedItem as MonitorChoice;
                if (selected == null) return;
                config.MonitorDevice = selected.Device;
                overlay.SetMonitor(selected.Device);
                UpdateScreenOffsetLabel();
                ScheduleSave();
            };
            page.Controls.Add(monitorSelect);
            LoadMonitors();

            MakeSection(page, UiText.Get("OYUN PENCERESİ"), px, 182);
            Label info = new Label
            {
                Text = UiText.Get("Kenarlıksız veya pencereli oyun modunu kullan.\n\n"
                    + "Özel tam ekran modunda nişangâh görünmeyebilir.\n\n"
                    + "Seçili monitör ayrılırsa katman birincil ekrana geçer."),
                ForeColor = Theme.TextDim, Font = uiFont, Location = new Point(px, 211),
                Size = new Size(CONTENT_W - 55, 185)
            };
            page.Controls.Add(info);
            return page;
        }

        private sealed class MonitorChoice
        {
            internal readonly string Device;
            private readonly string label;
            internal MonitorChoice(string device, string label) { Device = device; this.label = label; }
            public override string ToString() { return label; }
        }

        private void LoadMonitors()
        {
            monitorSelect.BeginUpdate();
            try
            {
                monitorSelect.Items.Clear();
                monitorSelect.Items.Add(new MonitorChoice("", UiText.Get("Otomatik · Birincil ekran")));
                int selected = 0;
                foreach (Screen screen in Screen.AllScreens)
                {
                    string name = screen.DeviceName.Substring(screen.DeviceName.LastIndexOf('\\') + 1);
                    monitorSelect.Items.Add(new MonitorChoice(screen.DeviceName, name + " · " +
                        screen.Bounds.Width + " × " + screen.Bounds.Height + (screen.Primary ? UiText.Get(" · Birincil") : "")));
                    if (!String.IsNullOrEmpty(config.MonitorDevice) && screen.DeviceName == config.MonitorDevice)
                        selected = monitorSelect.Items.Count - 1;
                }
                if (selected == 0 && !String.IsNullOrEmpty(config.MonitorDevice))
                {
                    monitorSelect.Items.Add(new MonitorChoice(config.MonitorDevice, UiText.Get("Bağlantı yok · Birincil ekran kullanılıyor")));
                    selected = monitorSelect.Items.Count - 1;
                }
                monitorSelect.SelectedIndex = selected;
            }
            finally { monitorSelect.EndUpdate(); }
        }

        // ----- Sayfa 4: Ayarlar -----
        private Panel BuildSettingsPage()
        {
            Panel page = NewPage();
            int px = 28;
            MakeHeader(page, UiText.Get("Ayarlar"), px, 16);
            AddLanguageChoice(page, px);

            MakeSection(page, UiText.Get("CAM MALZEMESİ"), px, 69);
            var materials = new[] { GlassMaterial.Gloss, GlassMaterial.Quiet, GlassMaterial.Technical };
            var materialNames = new[] { UiText.Get("Parlak Cam"), UiText.Get("Sade Lüks"), UiText.Get("Teknik Lüks") };
            materialButtons = new ModernButton[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                var button = OptionButton(page, materialNames[i], px + i * 134, 94, 125);
                materialButtons[i] = button;
                button.Selected = config.Material == material;
                button.Click += delegate
                {
                    Glass.Material = config.Material = material;
                    foreach (var item in materialButtons) item.Selected = item == button;
                    RethemeAll(this);
                    ScheduleSave();
                };
            }

            MakeSection(page, UiText.Get("VURGU RENGİ"), px, 144);
            var accents = new[] { AccentTheme.Burgundy, AccentTheme.Orange, AccentTheme.Purple, AccentTheme.Cyan };
            var accentNames = new[] { UiText.Get("Bordo"), UiText.Get("Turuncu"), UiText.Get("Mor"), UiText.Get("Turkuaz") };
            accentButtons = new ModernButton[accents.Length];
            for (int i = 0; i < accents.Length; i++)
            {
                var accent = accents[i];
                var button = OptionButton(page, accentNames[i], px + i * 101, 169, 93);
                accentButtons[i] = button;
                button.Selected = config.Accent == accent;
                button.Click += delegate
                {
                    Theme.ApplyAccent(accent);
                    config.Accent = accent;
                    slideIndicator.BackColor = Theme.AccentInk;
                    foreach (var item in accentButtons) item.Selected = item == button;
                    RethemeAll(this);
                    ScheduleSave();
                };
            }

            MakeSection(page, UiText.Get("GÖSTER / GİZLE KISAYOLU"), px, 224);
            hotkeyBox = new HotkeyBox
            {
                Ctrl = config.HkCtrl, Alt = config.HkAlt, Shift = config.HkShift, KeyValue = config.HkKey,
                Location = new Point(px, 250), AccessibleName = UiText.Get("Göster veya gizle kısayolu")
            };
            hotkeyBox.HotkeyChanged += OnHotkeyChanged;
            page.Controls.Add(hotkeyBox);
            MakeRowLabel(page, UiText.Get("Windows ile başlat"), px + 218, 252);
            var startup = new ToggleSwitch
            {
                Location = new Point(px + 370, 256), AccessibleName = UiText.Get("Windows ile başlat")
            };
            startup.SetCheckedSilent(ReadStartup());
            startup.CheckedChanged += OnStartupToggle;
            page.Controls.Add(startup);

            hotkeyMessage = new Label
            {
                Font = sliderFont, ForeColor = Theme.TextDim,
                Text = UiText.Get("Ctrl, Alt veya Shift ile bir tuş; ya da tek başına bir F tuşu."),
                Location = new Point(px, 294), Size = new Size(CONTENT_W - px * 2, 31)
            };
            page.Controls.Add(hotkeyMessage);

            MakeSection(page, UiText.Get("PAYLAŞIM KODU"), px, 345);
            shareOutBox = MakeTextBox(page, UiText.Get("Mevcut paylaşım kodu"), px, 373, 303);
            shareOutBox.ReadOnly = true;
            var copy = OptionButton(page, UiText.Get("Kopyala"), px + 316, 369, 107);
            copy.Click += delegate
            {
                try
                {
                    Clipboard.SetText(settings.ToShareCode());
                    shareMessage.Text = UiText.Get("Kod panoya kopyalandı.");
                    shareMessage.ForeColor = Glass.Success;
                }
                catch (ExternalException)
                {
                    shareMessage.Text = UiText.Get("Pano kullanılıyor. Biraz sonra yeniden dene.");
                    shareMessage.ForeColor = Glass.Warning;
                }
            };
            shareInBox = MakeTextBox(page, UiText.Get("İçe aktarılacak paylaşım kodu"), px, 418, 303);
            shareInBox.MaxLength = 2049;
            var import = OptionButton(page, UiText.Get("İçe Aktar"), px + 316, 414, 107);
            import.Ghost = false;
            import.Click += OnImportCode;
            shareInBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                OnImportCode(sender, e);
            };

            shareMessage = new Label
            {
                Font = sliderFont, ForeColor = Theme.TextDim, Location = new Point(px, 452),
                Size = new Size(CONTENT_W - px * 2, 30)
            };
            page.Controls.Add(shareMessage);
            var reset = OptionButton(page, UiText.Get("Nişangâhı Sıfırla"), px, 493, 164);
            reset.Click += delegate
            {
                if (DarkMessage.Show(this, UiText.Get("Nişangâh varsayılan tasarıma dönsün mü?"), UiText.Get("Sıfırla"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ApplySettingsObject(new VcrdbSettings());
            };
            MakeSection(page, UiText.Get("ARKA PLAN"), px, 550);
            var backgroundPulse = AddToggle(page, UiText.Get("Nabzı sürdür"), px, 575);
            backgroundPulse.Name = "BackgroundPulseToggle";
            backgroundPulse.AccessibleName = UiText.Get("Nabız efektini arka planda sürdür");
            backgroundPulse.SetCheckedSilent(config.BackgroundPulse);
            backgroundPulse.CheckedChanged += delegate
            {
                config.BackgroundPulse = backgroundPulse.Checked;
                OnEditorActivityChanged(this, EventArgs.Empty);
                ScheduleSave();
            };
            MakeRowLabel(page, UiText.Get("Yalnızca nabız efekti açıkken geçerli."), px, 609);

            MakeRowLabel(page, UiText.Get("Basılı tutarak göster"), px + 225, 545);
            var holdToggle = new ToggleSwitch
            {
                Name = "HoldVisibilityToggle", Location = new Point(px + 390, 545),
                AccessibleName = UiText.Get("Gösterme kısayoluna basılı tutarak göster")
            };
            holdToggle.SetCheckedSilent(config.HoldVisibility);
            holdToggle.CheckedChanged += delegate { SetHoldVisibility(holdToggle.Checked); ScheduleSave(); };
            page.Controls.Add(holdToggle);
            MakeRowLabel(page, UiText.Get("Gösterme kısayolunu kullanır."), px + 225, 575);
            return page;
        }

        private void AddLanguageChoice(Panel page, int inset)
        {
            string caption = UiText.Get("Dil / Language");
            int x = CONTENT_W - inset - 116;
            MakeRowLabel(page, caption, x - 103, 25);
            var language = new ComboBox
            {
                Location = new Point(x, 22), Size = new Size(116, 30),
                DropDownStyle = ComboBoxStyle.DropDownList, Font = uiFont,
                BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text,
                AccessibleName = caption
            };
            Theme.DarkPicker(language);
            language.Items.AddRange(new object[] { "Türkçe", "English" });
            language.SelectedIndex = config.EnglishUi ? 1 : 0;
            language.SelectionChangeCommitted += delegate
            {
                bool english = language.SelectedIndex == 1;
                if (english == config.EnglishUi) return;
                config.EnglishUi = english;
                if (!ConfigManager.Save(config))
                {
                    config.EnglishUi = !english;
                    language.SelectedIndex = config.EnglishUi ? 1 : 0;
                    DarkMessage.Show(this, UiText.Get("Dil tercihi kaydedilemedi."), caption,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DarkMessage.Show(this, UiText.Get("Dil bir sonraki açılışta değişecek."), caption);
            };
            page.Controls.Add(language);
        }

        private TextBox MakeTextBox(Panel page, string name, int x, int y, int width)
        {
            var box = new TextBox
            {
                Location = new Point(x, y), Width = width, AccessibleName = name,
                BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = uiFont
            };
            page.Controls.Add(box);
            return box;
        }

        private ModernButton OptionButton(Panel page, string title, int x, int y, int width)
        {
            var button = new ModernButton
            {
                Text = title, Ghost = true, CornerRadius = 9,
                Location = new Point(x, y), Size = new Size(width, 32)
            };
            page.Controls.Add(button);
            return button;
        }

        private void RethemeAll(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var button = c as ModernButton;
                if (button != null) button.ResetTheme();
                if (Equals(c.Tag, "accent-text")) c.ForeColor = Theme.AccentInk;
                c.Invalidate();
                if (c.HasChildren) RethemeAll(c);
            }
        }

        private void OnHotkeyChanged(object s, EventArgs e)
        {
            hold.Stop();
            if (hotkeyRegistered && config.HkKey == hotkeyBox.KeyValue && config.HkCtrl == hotkeyBox.Ctrl &&
                config.HkAlt == hotkeyBox.Alt && config.HkShift == hotkeyBox.Shift) return;

            Keys previousKey = config.HkKey;
            bool previousCtrl = config.HkCtrl, previousAlt = config.HkAlt, previousShift = config.HkShift;
            bool previouslyRegistered = hotkeyRegistered;
            if (previouslyRegistered) UnregisterHotKey(Handle, HOTKEY_ID);
            config.HkCtrl = hotkeyBox.Ctrl;
            config.HkAlt = hotkeyBox.Alt;
            config.HkShift = hotkeyBox.Shift;
            config.HkKey = hotkeyBox.KeyValue;
            hotkeyRegistered = RegisterCurrentHotkey();
            if (hotkeyRegistered)
            {
                hotkeyMessage.Text = UiText.Get("Kısayol etkin.");
                hotkeyMessage.ForeColor = Glass.Success;
                SetNotice("");
                ScheduleSave();
                return;
            }
            hotkeyBox.KeyValue = config.HkKey = previousKey;
            hotkeyBox.Ctrl = config.HkCtrl = previousCtrl;
            hotkeyBox.Alt = config.HkAlt = previousAlt;
            hotkeyBox.Shift = config.HkShift = previousShift;
            hotkeyBox.Invalidate();
            hotkeyRegistered = previouslyRegistered && RegisterCurrentHotkey();
            ShowHotkeyFailure(UiText.Get(hotkeyRegistered
                ? "Kısayol kullanılamıyor. Önceki kısayol korundu."
                : "Kısayol kullanılamıyor. Başka bir kombinasyon dene."));
        }

        private const string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private static bool ReadStartup()
        {
            try
            {
                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(StartupRegistryPath))
                    return run != null && String.Equals(run.GetValue("VCRDBCrosshair") as string,
                        "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase);
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
            catch (IOException) { return false; }
        }

        private void OnStartupToggle(object s, EventArgs e)
        {
            ToggleSwitch toggle = (ToggleSwitch)s;
            try
            {
                using (RegistryKey run = Registry.CurrentUser.CreateSubKey(StartupRegistryPath))
                {
                    if (run == null) throw new IOException("Unable to open startup settings.");
                    if (toggle.Checked) run.SetValue("VCRDBCrosshair", "\"" + Application.ExecutablePath + "\"");
                    else if (ReadStartup()) run.DeleteValue("VCRDBCrosshair", false);
                }
                return;
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
            catch (IOException) { }
            toggle.SetCheckedSilent(ReadStartup());
            SetNotice(UiText.Get("Windows başlangıç ayarı değiştirilemedi."));
        }

        private void OnImportCode(object s, EventArgs e)
        {
            VcrdbSettings imported;
            if (!VcrdbSettings.TryFromShareCode(shareInBox.Text, out imported))
            {
                shareMessage.Text = UiText.Get("Kod geçersiz veya eksik. Paylaşım kodunun tamamını yapıştır.");
                shareMessage.ForeColor = Glass.Warning;
                shareInBox.Focus();
                shareInBox.SelectAll();
                return;
            }
            ApplySettingsObject(imported);
            shareInBox.Clear();
            shareMessage.Text = UiText.Get("Paylaşım kodu uygulandı.");
            shareMessage.ForeColor = Glass.Success;
        }

        // ----- Sayfa 5: Yardım -----
        private Panel BuildHelpPage()
        {
            Panel page = NewPage();
            int px = 28;
            MakeHeader(page, UiText.Get("Yardım"), px, 16);
            Label help = new Label();
            help.Text =
                UiText.Get("01   TASARLA\n") +
                UiText.Get("Nişangâh sayfasında renk, merkez noktası ve çizgileri ayarla.\n") +
                UiText.Get("Sağdaki vizörle sonucu farklı zeminlerde gör.\n\n") +
                UiText.Get("02   KAYDET VE PAYLAŞ\n") +
                UiText.Get("Profil Galerisi'nden bir tasarım seç veya kendi profilini kaydet.\n") +
                UiText.Get("Ayarlar sayfasındaki kodu paylaşarak tasarımı taşıyabilirsin.\n\n") +
                UiText.Get("03   OYUNA DÖN\n") +
                UiText.Get("Tepsiye Gizle, editörü kapatmadan arka plana alır.\n") +
                UiText.Get("Bildirim alanındaki simgeye çift tıklayarak geri dön.\n\n") +
                UiText.Get("Nişangâh görünmüyorsa Ekran ve Katman sayfasını kontrol et.");
            help.ForeColor = Theme.Text; help.Font = new Font("Segoe UI", 9.75F);
            help.AutoSize = false; help.Size = new Size(CONTENT_W - px - 20, BODY_H - 70);
            help.Location = new Point(px, 78);
            page.Controls.Add(help);
            return page;
        }

        // ---------- SAYFA GEÇİŞİ ----------
        private void SelectPage(int index, bool animate)
        {
            currentPage = index;
            if (index == 1 && galleryStale) RebuildGallery();
            for (int i = 0; i < navItems.Length; i++) navItems[i].Selected = (i == index);
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = (i == index);

            // paylaşım kodu güncelle
            if (index == 4 && shareOutBox != null) shareOutBox.Text = settings.ToShareCode();

            navTargetY = navItems[index].Top + (navItems[index].Height - slideIndicator.Height) / 2;
            animate = animate && SystemInformation.IsMenuAnimationEnabled;
            if (animate) navAnimTimer.Start();
            else slideIndicator.Top = navTargetY;

            if (animate)
            {
                pages[index].Left = 22;
                pageAnimTimer.Start();
            }
            else pages[index].Left = 0;

            pages[index].BringToFront();
        }

        private void OnPageAnim(object s, EventArgs e)
        {
            Panel p = pages[currentPage];
            int d = 0 - p.Left;
            if (Math.Abs(d) <= 2) { p.Left = 0; pageAnimTimer.Stop(); }
            else p.Left = p.Left + (int)Math.Round(d * 0.35, MidpointRounding.AwayFromZero);
        }

        // ---------- ORTAK GÜNCELLEME ----------
        private void RefreshUIFromSettings()
        {
            isUpdatingUI = true;
            if (slOpacity != null) slOpacity.SetValueSilent(settings.Opacity);
            if (slOutThick != null) slOutThick.SetValueSilent(settings.OutlineThick);
            if (slDotThick != null) slDotThick.SetValueSilent(settings.DotThick);
            if (slInLen != null) slInLen.SetValueSilent(settings.InLen);
            if (slInThick != null) slInThick.SetValueSilent(settings.InThick);
            if (slInOff != null) slInOff.SetValueSilent(settings.InOff);
            if (slOutLen != null) slOutLen.SetValueSilent(settings.OutLen);
            if (slOutThick2 != null) slOutThick2.SetValueSilent(settings.OutThick);
            if (slOutOff != null) slOutOff.SetValueSilent(settings.OutOff);
            if (slScale != null) slScale.SetValueSilent(settings.Scale);
            if (positionScaleSlider != null) positionScaleSlider.SetValueSilent(settings.Scale);
            if (slOffX != null) slOffX.SetValueSilent(settings.OffsetX);
            if (slOffY != null) slOffY.SetValueSilent(settings.OffsetY);
            UpdateMarkerButtons();
            if (roundedArms != null) roundedArms.SetCheckedSilent(settings.RoundedArms);
            if (doubleOutline != null) doubleOutline.SetCheckedSilent(settings.DoubleOutline);
            if (topLength != null) topLength.SetValueSilent(settings.TopLength);
            if (rightLength != null) rightLength.SetValueSilent(settings.RightLength);
            if (bottomLength != null) bottomLength.SetValueSilent(settings.BottomLength);
            if (leftLength != null) leftLength.SetValueSilent(settings.LeftLength);

            if (tgOutline != null) tgOutline.SetCheckedSilent(settings.OutlineShow);
            if (tgDot != null) tgDot.SetCheckedSilent(settings.DotShow);
            if (tgIn != null) tgIn.SetCheckedSilent(settings.InShow);
            if (tgOut != null) tgOut.SetCheckedSilent(settings.OutShow);
            if (tgTShape != null) tgTShape.SetCheckedSilent(settings.TShape);
            if (tgPulse != null) tgPulse.SetCheckedSilent(settings.Pulse);

            if (colorPreviewBar != null) colorPreviewBar.BackColor = settings.Color;
            if (colorMessage != null) colorMessage.Text = "";
            if (hexBox != null) hexBox.Text = "#" + (settings.Color.R.ToString("X2") + settings.Color.G.ToString("X2") + settings.Color.B.ToString("X2"));
            isUpdatingUI = false;

            if (overlay != null && !calibrating) overlay.UpdateSettings(EffectiveDesign);
            if (preview != null) preview.SetSettings(settings);
            UpdateReadout();
        }

        private void ApplySettingsObject(VcrdbSettings src)
        {
            if (settings.ToConfigString() == src.ToConfigString()) return;
            CommitHistory();
            settings.CopyFrom(src);
            MarkHistory();
            RefreshUIFromSettings();
            ScheduleSave();
        }

        // ayar değişince çağrılır
        private void Pushed()
        {
            MarkHistory();
            if (overlay != null && !calibrating) overlay.UpdateSettings(EffectiveDesign);
            if (preview != null) preview.SetSettings(settings);
            UpdateReadout();
            if (shareOutBox != null && currentPage == 4) shareOutBox.Text = settings.ToShareCode();
            ScheduleSave();
        }

        private void ScheduleSave()
        {
            dirty = true;
            if (saveTimer == null) return;
            saveTimer.Stop();
            if (!Visible || WindowState == FormWindowState.Minimized) PersistNow();
            else saveTimer.Start();
        }

        private void OnSaveTick(object s, EventArgs e)
        {
            saveTimer.Stop();
            PersistNow();
        }

        private void PersistNow()
        {
            CommitHistory();
            config.Settings = settings;
            if (!dirty) return;
            if (ConfigManager.Save(config)) dirty = false;
            else SetNotice(UiText.Get("Ayarlar kaydedilemedi. Uygulama veri klasörüne yazma iznini kontrol et."));
        }

        // ---------- HOTKEY ----------
        private bool RegisterCurrentHotkey()
        {
            uint mods = MOD_NOREPEAT;
            if (config.HkCtrl) mods |= MOD_CONTROL;
            if (config.HkAlt) mods |= MOD_ALT;
            if (config.HkShift) mods |= MOD_SHIFT;
            return RegisterHotKey(Handle, HOTKEY_ID, mods, (uint)config.HkKey);
        }

        private void ShowHotkeyFailure(string message)
        {
            hotkeyMessage.Text = message;
            hotkeyMessage.ForeColor = Glass.Warning;
            SetNotice(message);
        }

        private void SetNotice(string message)
        {
            if (notice == null) return;
            notice.Text = message;
            notice.ForeColor = String.IsNullOrEmpty(message) ? Theme.TextDim : Glass.Warning;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = (int)m.WParam;
                if (id == HOTKEY_ID) { ApplyVisibilityShortcut(); return; }
                if (HandleFavoriteShortcut(id)) return;
            }
            base.WndProc(ref m);
        }

        private void ToggleCrosshair()
        {
            if (config.HoldVisibility)
            {
                SetNotice(UiText.Get("Basılı tutma açık. Gösterme kısayoluna basılı tut."));
                return;
            }
            SetCrosshairVisible(!crosshairVisible);
        }

        private void OnStatusToggle(object s, EventArgs e) { ToggleCrosshair(); }

        // ---------- TEPSİ ----------
        private void BuildTray()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Text = "VCRDB Crosshair";
            trayIcon.Icon = appIcon ?? SystemIcons.Application;
            trayIcon.DoubleClick += delegate (object s, EventArgs e) { ShowFromTray(); };
            RebuildTrayMenu();
        }

        private void RebuildTrayMenu()
        {
            var menu = new DarkMenu();
            menu.Add(UiText.Get("Ayarları Aç"), delegate { ShowFromTray(); });
            overlayMenuItem = menu.Add("", OnStatusToggle);
            UpdateOverlayMenuText();
            string activeTitle = activeGameMenu == null || !activeGameMenu.Available ? null : activeGameMenu.Text;
            activeGameMenu = menu.Add(activeTitle ?? "", null);
            activeGameMenu.Enabled = false;
            activeGameMenu.Available = activeTitle != null;

            var profilesItem = new ToolStripMenuItem(UiText.Get("Profiller"));
            profilesItem.DropDown = new DarkMenu();
            List<string> profiles = ConfigManager.ListProfiles();
            if (profiles.Count == 0) profilesItem.DropDownItems.Add(new ToolStripMenuItem(UiText.Get("(boş)")) { Enabled = false });
            else foreach (string pn in profiles)
            {
                string nameCopy = pn;
                profilesItem.DropDownItems.Add(pn, null, delegate
                {
                    VcrdbSettings ps = ConfigManager.LoadProfile(nameCopy);
                    if (ps != null) ApplySettingsObject(ps);
                });
            }
            menu.Items.Add(profilesItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Add(UiText.Get("Çıkış"), delegate { Application.Exit(); });
            ContextMenuStrip previous = trayIcon.ContextMenuStrip;
            trayIcon.ContextMenuStrip = menu;
            if (previous != null) previous.Dispose();
        }

        private void UpdateOverlayMenuText()
        {
            if (overlayMenuItem == null) return;
            overlayMenuItem.Enabled = !config.HoldVisibility;
            overlayMenuItem.Text = config.HoldVisibility ? UiText.Get("Nişangâh: Basılı Tut") :
                crosshairVisible ? UiText.Get("Nişangâhı Gizle") : UiText.Get("Nişangâhı Göster");
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }
    }

}
