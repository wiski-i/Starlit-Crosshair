using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NativeVcrdbCrosshair
{
    internal sealed class OverlayForm : Form
    {
        private const int Layered = 0x80000, Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
        private static readonly IntPtr TopmostWindow = new IntPtr(-1);
        private const uint ShowWithoutMoving = 0x0001 | 0x0002 | 0x0010 | 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);

        private readonly Timer pulseTimer;
        private VcrdbSettings settings;
        private string monitorDevice;
        private bool started, drawEnabled = true, failureReported, disposing;
        private double phase;
        private LayeredSurface surface;
        private Rectangle? screenBounds;
        private string screenDevice;
        internal Func<string, Point> PositionOffset;
        private bool editorActive = true, backgroundPulse;

        private bool ShouldPulse { get { return settings.Pulse && (editorActive || backgroundPulse); } }

        internal event EventHandler RenderFailed;
        internal event EventHandler ScreensChanged;

        internal OverlayForm(VcrdbSettings initial, string device)
        {
            settings = initial.Clone();
            monitorDevice = device;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;

            pulseTimer = new Timer { Interval = 33 };
            pulseTimer.Tick += delegate
            {
                phase = (phase + 0.18) % (Math.PI * 2);
                RefreshLayer();
            };
        }

        internal bool IsLayerVisible { get { return IsHandleCreated && IsWindowVisible(Handle); } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Layered | Transparent | ToolWindow | NoActivate;
                return cp;
            }
        }

        internal void Start()
        {
            if (started) return;
            started = true;
            // Keep a handle for marshaling even while the layer is hidden.
            IntPtr handle = Handle;
            SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
            RefreshLayer();
        }

        private void OnDisplaysChanged(object sender, EventArgs e)
        {
            if (disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (disposing) return;
                    screenBounds = null;
                    RefreshLayer();
                    if (ScreensChanged != null) ScreensChanged(this, EventArgs.Empty);
                });
            }
            catch (InvalidOperationException) { /* Window was closed while the notification was queued. */ }
        }

        internal static Screen ResolveScreen(string device)
        {
            foreach (Screen screen in Screen.AllScreens)
                if (String.Equals(screen.DeviceName, device, StringComparison.OrdinalIgnoreCase)) return screen;
            return Screen.PrimaryScreen;
        }

        internal void SetMonitor(string device)
        {
            monitorDevice = device;
            screenBounds = null;
            RefreshLayer();
        }

        internal void SetEditorActivity(bool active, bool animateInBackground)
        {
            bool wasPulsing = ShouldPulse;
            editorActive = active;
            backgroundPulse = animateInBackground;
            if (wasPulsing != ShouldPulse)
            {
                phase = 0;
                RefreshLayer();
            }
            else if (!active && !ShouldPulse) ReleaseSurface();
        }

        internal void SetDrawEnabled(bool enabled)
        {
            drawEnabled = enabled;
            RefreshLayer();
        }

        internal void UpdateSettings(VcrdbSettings next)
        {
            settings = next.Clone();
            if (!settings.Pulse) phase = 0;
            RefreshLayer();
        }

        internal void RefreshLayer()
        {
            if (!started || disposing) return;
            bool visible = drawEnabled && settings.Opacity > 0 &&
                (settings.DotShow || settings.HasVisibleArms);
            if (!visible)
            {
                HideLayer();
                return;
            }

            if (!screenBounds.HasValue)
            {
                var display = ResolveScreen(monitorDevice);
                screenBounds = display.Bounds;
                screenDevice = display.DeviceName;
            }
            Rectangle screen = screenBounds.Value;
            Point displayOffset = PositionOffset == null ? Point.Empty : PositionOffset(screenDevice);
            Rectangle area = GetLayerBounds(screen, settings, displayOffset);
            if (area.IsEmpty)
            {
                HideLayer();
                return;
            }

            try
            {
                if (surface == null || surface.Size != area.Size)
                {
                    ReleaseSurface();
                    surface = new LayeredSurface(area.Size);
                }

                double x = screen.Left + screen.Width / 2.0 + settings.OffsetX + displayOffset.X - area.Left;
                double y = screen.Top + screen.Height / 2.0 + settings.OffsetY + displayOffset.Y - area.Top;
                bool animate = ShouldPulse;
                float scale = animate ? (float)(1 + 0.07 * Math.Sin(phase)) : 1f;
                surface.Paint(settings, x, y, scale);
                surface.Present(Handle, area.Location, (byte)settings.Opacity);
                if (!IsLayerVisible && !SetWindowPos(Handle, TopmostWindow, 0, 0, 0, 0, ShowWithoutMoving))
                    throw new Win32Exception();
                failureReported = false;
                pulseTimer.Enabled = animate;
                if (!editorActive && !animate) ReleaseSurface();
            }
            catch (Win32Exception)
            {
                HideLayer();
                if (!failureReported && RenderFailed != null) RenderFailed(this, EventArgs.Empty);
                failureReported = true;
            }
        }

        private void HideLayer()
        {
            pulseTimer.Stop();
            if (IsLayerVisible) ShowWindow(Handle, 0);
            ReleaseSurface();
        }

        private void ReleaseSurface()
        {
            if (surface == null) return;
            surface.Dispose();
            surface = null;
        }

        internal static Rectangle GetLayerBounds(Rectangle screen, VcrdbSettings s, Point displayOffset = default(Point))
        {
            // Allow for the largest pulse frame plus outline and pixel rounding.
            int extent = s.DotShow ? Math.Max(5, s.DotThick) : 0;
            int armScale = Math.Max(Math.Max(s.TopLength, s.BottomLength), Math.Max(s.LeftLength, s.RightLength));
            if (s.InShow) extent = Math.Max(extent, (s.InLen * armScale + 99) / 100 + s.InOff + s.InThick);
            if (s.OutShow) extent = Math.Max(extent, (s.OutLen * armScale + 99) / 100 + s.OutOff + s.OutThick);
            if (s.OutlineShow) extent += s.OutlineThick + (s.DoubleOutline ? 1 : 0);
            int radius = Math.Max(2, (int)Math.Ceiling(extent * s.Scale / 100.0 * 1.07) + 3);
            int x = screen.Left + screen.Width / 2 + s.OffsetX + displayOffset.X;
            int y = screen.Top + screen.Height / 2 + s.OffsetY + displayOffset.Y;
            Rectangle area = Rectangle.Intersect(screen, new Rectangle(x - radius, y - radius, radius * 2 + 1, radius * 2 + 1));
            return area.Width > 0 && area.Height > 0 ? area : Rectangle.Empty;
        }

        protected override void Dispose(bool releaseManaged)
        {
            if (releaseManaged && !disposing)
            {
                disposing = true;
                if (started) SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
                pulseTimer.Dispose();
                ReleaseSurface();
            }
            base.Dispose(releaseManaged);
        }
    }

    internal sealed class LayeredSurface : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            internal uint HeaderSize;
            internal int Width, Height;
            internal ushort Planes, BitCount;
            internal uint Compression, ImageSize;
            internal int XResolution, YResolution;
            internal uint ColorsUsed, ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Blend
        {
            internal byte Operation, Flags, Opacity, AlphaFormat;
        }

        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr handle);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr destinationDc, ref Point destination,
            ref Size size, IntPtr sourceDc, ref Point source, uint key, ref Blend blend, uint flags);

        internal readonly Size Size;
        private IntPtr dc, dib, oldBitmap;
        private Bitmap pixels;

        internal LayeredSurface(Size size)
        {
            Size = size;
            try
            {
                dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero) throw new Win32Exception();
                BitmapInfo info = new BitmapInfo
                {
                    HeaderSize = (uint)Marshal.SizeOf(typeof(BitmapInfo)),
                    Width = size.Width, Height = -size.Height, Planes = 1, BitCount = 32
                };
                IntPtr bits;
                dib = CreateDIBSection(dc, ref info, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero) throw new Win32Exception();
                oldBitmap = SelectObject(dc, dib);
                if (oldBitmap == IntPtr.Zero || oldBitmap == new IntPtr(-1)) throw new Win32Exception();
                pixels = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, bits);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Paint(VcrdbSettings settings, double x, double y, float scale)
        {
            using (Graphics graphics = Graphics.FromImage(pixels))
            {
                graphics.Clear(Color.Transparent);
                CrosshairRenderer.Render(graphics, x, y, settings, scale, 255);
            }
        }

        internal void Present(IntPtr handle, Point destination, byte opacity)
        {
            Point origin = Point.Empty;
            Size size = Size;
            Blend blend = new Blend { Opacity = opacity, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(handle, IntPtr.Zero, ref destination, ref size, dc, ref origin, 0, ref blend, 2))
                throw new Win32Exception();
        }

        public void Dispose()
        {
            if (pixels != null) { pixels.Dispose(); pixels = null; }
            if (oldBitmap != IntPtr.Zero && oldBitmap != new IntPtr(-1)) SelectObject(dc, oldBitmap);
            if (dib != IntPtr.Zero) DeleteObject(dib);
            if (dc != IntPtr.Zero) DeleteDC(dc);
            oldBitmap = dib = dc = IntPtr.Zero;
        }
    }
}
