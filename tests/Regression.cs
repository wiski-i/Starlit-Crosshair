using System;
using System.Drawing;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NativeVcrdbCrosshair;

internal static class Regression
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
    }

    private static string Encode(string text)
    {
        return "VCRDB-" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    private static void TestSecondInstanceWarning()
    {
        var config = new AppConfig { EnglishUi = true, Accent = AccentTheme.Cyan, Material = GlassMaterial.Technical };
        Check(ConfigManager.Save(config), "Save second-instance theme fixture.");
        Theme.ApplyAccent(AccentTheme.Orange);
        Glass.Material = GlassMaterial.Gloss;
        UiText.UseEnglish = false;
        using (var ready = new System.Threading.ManualResetEvent(false))
        using (var release = new System.Threading.ManualResetEvent(false))
        {
            bool acquired = false;
            var owner = new System.Threading.Thread(delegate()
            {
                using (var mutex = new System.Threading.Mutex(false, @"Local\VCRDB.Crosshair"))
                {
                    try { acquired = mutex.WaitOne(3000); }
                    catch (System.Threading.AbandonedMutexException) { acquired = true; }
                    ready.Set();
                    if (!acquired) return;
                    release.WaitOne();
                    mutex.ReleaseMutex();
                }
            });
            owner.Start();
            ready.WaitOne();
            if (!acquired)
            {
                owner.Join();
                throw new InvalidOperationException("Close the running Crosshair application before testing the second-instance warning.");
            }
            bool seen = false, themed = false;
            EventHandler inspect = delegate
            {
                if (Application.OpenForms.Count == 0) return;
                seen = true;
                themed = UiText.UseEnglish && Theme.Current == config.Accent && Glass.Material == config.Material;
                Application.OpenForms[0].Close();
            };
            Application.Idle += inspect;
            try { NativeVcrdbCrosshair.Program.Main(); }
            finally
            {
                Application.Idle -= inspect;
                release.Set();
                owner.Join();
            }
            Check(seen && themed, "Second-instance warning loads the saved language, accent and material before display.");
        }
        Check(ConfigManager.Save(new AppConfig { HkKey = Keys.F20, HkShift = true }), "Restore UI fixture.");
        Console.WriteLine("PASS: second-instance warning uses the saved theme.");
    }

    private static void CheckShareCode(MainForm form, string operation)
    {
        Check(Field<TextBox>(form, "shareOutBox").Text == Field<VcrdbSettings>(form, "settings").ToShareCode(),
            operation + " refreshes the displayed share code.");
    }

    private static void TestShareCodeSynchronization()
    {
        Check(ConfigManager.Save(new AppConfig { HkKey = Keys.F20, HkShift = true }), "Prepare share-code fixture.");
        using (var form = new MainForm(new Size(2000, 1000)))
        {
            form.Show();
            Pump();
            Call(form, "SelectPage", 4, false);
            var imported = new VcrdbSettings { Color = Color.Magenta, Scale = 137, OffsetX = 23 };
            Field<TextBox>(form, "shareInBox").Text = imported.ToShareCode();
            Call(form, "OnImportCode", form, EventArgs.Empty);
            Check(Field<VcrdbSettings>(form, "settings").ToShareCode() == imported.ToShareCode(), "Import applies the design.");
            CheckShareCode(form, "Import");
            Call(form, "ChangeHistory", false);
            CheckShareCode(form, "Undo");
            Call(form, "ChangeHistory", true);
            CheckShareCode(form, "Redo");
            Call(form, "ApplySettingsObject", new VcrdbSettings());
            CheckShareCode(form, "Reset");
            ConfigManager.SaveProfile("Share-code fixture", imported);
            Call(form, "SelectPage", 1, false);
            Call(form, "ApplySettingsObject", ConfigManager.LoadProfile("Share-code fixture"));
            CheckShareCode(form, "Saved profile applied from gallery");
            Call(form, "ChangeHistory", false);
            CheckShareCode(form, "Undo while settings page is hidden");
            ConfigManager.DeleteProfile("Share-code fixture");
            form.Close();
        }
        Console.WriteLine("PASS: import, undo, redo, reset and saved profiles keep the share code current.");
    }
    private static void TestSettings()
    {
        VcrdbSettings value;
        var settings = new VcrdbSettings { Color = Color.Magenta, Opacity = 147, OffsetY = -270, Pulse = true };
        string data = settings.ToConfigString();
        Check(VcrdbSettings.TryFromShareCode(settings.ToShareCode(), out value) && value.ToConfigString() == data, "Share code round trip.");
        Check(VcrdbSettings.TryFromShareCode(settings.ToShareCode().Substring(6), out value), "Prefixless legacy code.");
        Check(!VcrdbSettings.TryFromShareCode("aGVsbG8=", out value), "Reject unrelated Base64.");
        Check(!VcrdbSettings.TryFromShareCode(Encode(data.Replace("Opacity=147", "Opacity=999")), out value), "Reject out-of-range opacity.");
        Check(!VcrdbSettings.TryFromShareCode(Encode(data.Replace("Pulse=1", "Pulse=2")), out value), "Reject invalid flags.");
        Check(!VcrdbSettings.TryFromShareCode(new string('A', 2050), out value), "Reject oversized share code.");
        value = VcrdbSettings.FromConfigString("Opacity=999\nOffsetX=-900\nInThick=-2\nScale=9000");
        Check(value.Opacity == 255 && value.OffsetX == -300 && value.InThick == 1 && value.Scale == 300, "Recover invalid local values.");

        var app = new AppConfig { Accent = AccentTheme.Cyan, Material = GlassMaterial.Technical, MonitorDevice = @"\\.\DISPLAY2" };
        Check(ConfigManager.Save(app), "First config save.");
        app.Settings.CopyFrom(settings);
        Check(ConfigManager.Save(app), "Atomic replacement.");
        var loaded = ConfigManager.Load();
        Check(loaded.Accent == app.Accent && loaded.Material == app.Material && loaded.MonitorDevice == app.MonitorDevice &&
            loaded.Settings.ToConfigString() == data, "Reload settings and display options.");
        foreach (string invalid in new[] { "..", "NUL.txt", "COM¹", @"a\b", "test.", "test " })
            Check(!ConfigManager.IsValidProfileName(invalid), "Reject invalid profile name: " + invalid);
        ConfigManager.SaveProfile("Pembe nişangâh", settings);
        Check(ConfigManager.ProfileExists("Pembe nişangâh") && ConfigManager.LoadProfile("Pembe nişangâh").Color.ToArgb() == Color.Magenta.ToArgb(),
            "Save and read Unicode profile.");
        ConfigManager.DeleteProfile("Pembe nişangâh");
        Check(!ConfigManager.ProfileExists("Pembe nişangâh"), "Delete profile.");
        File.Delete(ConfigManager.ConfigPath);
    }

    private static void TestShapesAndPack()
    {
        var shape = new VcrdbSettings
        {
            Marker = CenterShape.Ring, DotShow = true, DotThick = 14, RoundedArms = true,
            TopLength = 0, RightLength = 180
        };
        VcrdbSettings decoded;
        Check(VcrdbSettings.TryFromShareCode(shape.ToShareCode(), out decoded) &&
            decoded.Marker == CenterShape.Ring && decoded.TopLength == 0 && decoded.RightLength == 180 && decoded.RoundedArms,
            "New shape fields round-trip through share codes.");
        string legacy = "Color=-1\nOpacity=255\nOutlineShow=1\nOutlineThick=1\nDotShow=0\nDotThick=2\nInShow=1\nInLen=6\nInThick=2\nInOff=3\nOutShow=0\nOutLen=2\nOutThick=2\nOutOff=10\nOffsetX=0\nOffsetY=0\nScale=100\nTShape=0\nPulse=0";
        Check(VcrdbSettings.TryFromShareCode(Encode(legacy), out decoded) &&
            decoded.Marker == CenterShape.Square && decoded.TopLength == 100 && !decoded.RoundedArms,
            "Legacy share code retains the old geometry.");
        Check(!VcrdbSettings.TryFromShareCode(Encode(shape.ToConfigString().Replace("RightLength=180", "RightLength=201")), out decoded),
            "Invalid arm length is rejected.");

        using (var image = new Bitmap(90, 90))
        using (var graphics = Graphics.FromImage(image))
        {
            shape.InShow = false;
            CrosshairRenderer.Render(graphics, 45, 45, shape, 1);
            Check(image.GetPixel(45, 45).A == 0 && image.GetPixel(45, 39).A > 0, "Ring remains hollow.");
            graphics.Clear(Color.Transparent);
            shape.Marker = CenterShape.Diamond;
            CrosshairRenderer.Render(graphics, 45, 45, shape, 1);
            Check(image.GetPixel(45, 45).A > 0 && image.GetPixel(39, 39).A == 0, "Diamond has a filled center and cut corners.");
            graphics.Clear(Color.Transparent);
            shape.DotShow = false;
            shape.InShow = true;
            shape.InLen = 10;
            CrosshairRenderer.Render(graphics, 45, 45, shape, 1);
            Check(image.GetPixel(45, 36).A == 0 && image.GetPixel(59, 45).A > 0 && image.GetPixel(30, 45).A == 0,
                "Disable the top arm and extend only the right arm.");
        }

        string file = Path.Combine(ConfigManager.Dir, "profiles-test.vcpak");
        ConfigManager.SaveProfile("Halka Örnek", shape);
        ConfigManager.SaveProfile("Saklanacak", new VcrdbSettings());
        try
        {
            Check(ProfilePack.Export(file) == 2, "Export both custom profiles.");
            ConfigManager.DeleteProfile("Halka Örnek");
            ConfigManager.SaveProfile("Saklanacak", new VcrdbSettings { OffsetX = 81 });
            var read = ProfilePack.Read(file);
            var result = read.Import();
            Check(read.Count == 2 && result.Imported == 1 && result.Skipped == 1 && result.Failed == 0,
                "Restore the missing entry and skip the existing profile.");
            Check(ConfigManager.LoadProfile("Saklanacak").OffsetX == 81 &&
                ConfigManager.LoadProfile("Halka Örnek").RightLength == 180, "Existing profile and new shape fields survive import.");
            File.AppendAllText(file, "invalid trailing record");
            bool rejected = false;
            try { ProfilePack.Read(file); }
            catch (FormatException) { rejected = true; }
            Check(rejected, "Malformed tail rejects the entire pack at validation.");
        }
        finally
        {
            File.Delete(file);
            ConfigManager.DeleteProfile("Halka Örnek");
            ConfigManager.DeleteProfile("Saklanacak");
        }

        using (var preview = new PreviewPanel(shape))
        {
            var timer = Field<Timer>(preview, "pulseTimer");
            preview.SetBackgroundMode(VisibilityScenes.Sky);
            Check(!timer.Enabled, "Sky uses a static backdrop.");
            preview.SetBackgroundMode(VisibilityScenes.Motion);
            Check(timer.Enabled, "Motion enables preview updates while visible.");
            preview.Hide();
            Check(!timer.Enabled, "Hidden motion test stops all preview updates.");
        }
    }

    private static void TestGameProfiles()
    {
        const string profile = "Game fixture profile";
        var config = new AppConfig { HkKey = Keys.F20, HkShift = true };
        string executable = Path.GetFileName(Application.ExecutablePath);
        config.GameProfiles.Add(executable.ToUpperInvariant(), profile);
        Check(ConfigManager.Save(config), "Save the game binding.");
        Check(ConfigManager.Load().ProfileForGame(executable) == profile &&
            ConfigManager.Load().ProfileForGame("unbound-test.exe") == null, "Match the persisted executable case-insensitively.");

        ConfigManager.SaveProfile(profile, new VcrdbSettings { Color = Color.Aqua, OffsetX = 37 });
        try
        {
            using (var form = new MainForm(new Size(2000, 1000)))
            {
                form.Show();
                Pump();
                var overlay = Field<OverlayForm>(form, "overlay");
                var watcher = Field<GameProfileWatcher>(form, "gameProfiles");
                watcher.ApplyProfile(profile, false);
                Check(Field<VcrdbSettings>(overlay, "settings").OffsetX == 37, "Apply only to the overlay.");
                Check(Field<VcrdbSettings>(form, "settings").OffsetX == 0, "Keep the editor profile untouched.");
                watcher.ApplyProfile(null, false);
                Check(Field<VcrdbSettings>(overlay, "settings").OffsetX == 0, "Restore the manual design.");
                using (var game = Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--foreground-fixture")
                    { UseShellExecute = false, CreateNoWindow = true }))
                {
                    try
                    {
                        IntPtr window = IntPtr.Zero;
                        for (int i = 0; i < 12 && window == IntPtr.Zero && !game.HasExited; i++)
                        {
                            Pump();
                            game.Refresh();
                            window = game.MainWindowHandle;
                        }
                        Check(window != IntPtr.Zero, "Open the separate game test window.");
                        if (GetForegroundWindow() != window && !SetForegroundWindow(window))
                        {
                            Console.WriteLine("SKIP: this desktop did not allow activating the separate test game.");
                            uint pid;
                            IntPtr current = GetForegroundWindow();
                            GetWindowThreadProcessId(current, out pid);
                            if (pid != 0 && pid != Process.GetCurrentProcess().Id)
                            {
                                string foregroundExe;
                                using (var foregroundProcess = Process.GetProcessById((int)pid))
                                    foregroundExe = foregroundProcess.ProcessName + ".exe";
                                var runningConfig = Field<AppConfig>(form, "config");
                                runningConfig.GameProfiles[foregroundExe] = profile;
                                Check(watcher.RefreshRules(), "Register for actual Windows foreground notifications.");
                                if (GetForegroundWindow() == current)
                                {
                                    Check(Field<VcrdbSettings>(overlay, "settings").OffsetX == 37,
                                        "The actual foreground executable is detected when refreshing bindings.");
                                    Console.WriteLine("PASS: matched the existing Windows foreground process.");
                                }
                                runningConfig.GameProfiles.Remove(foregroundExe);
                                watcher.RefreshRules();
                            }
                        }
                        else
                        {
                            Pump();
                            Check(Field<VcrdbSettings>(overlay, "settings").OffsetX == 37, "Foreground notification applies the bound game profile.");
                            Console.WriteLine("PASS: Windows foreground event applied the test game profile.");
                        }
                    }
                    finally
                    {
                        game.CloseMainWindow();
                        if (!game.WaitForExit(2000)) game.Kill();
                    }
                }
                Pump();
                Check(Field<VcrdbSettings>(overlay, "settings").OffsetX == 0, "Closing the bound window restores manual settings.");
                form.Close();
            }
        }
        finally { ConfigManager.DeleteProfile(profile); }
    }

    private static void TestWorkflowData()
    {
        var config = new AppConfig { HoldVisibility = true };
        config.Library.UpdateProfile("Mavi halka", "loş, erişilebilir", true, 0);
        config.Library.UpdateProfile("Beyaz", "gökyüzü", false, -1);
        config.Library.SetPosition("TEST-DISPLAY-1", new Point(7, -9));
        config.Library.SetPosition("TEST-DISPLAY-2", new Point(-12, 8));
        Check(ConfigManager.Save(config), "Save hold mode, tags, favorite slots, and screen calibrations.");
        config = ConfigManager.Load();
        Check(config.HoldVisibility && config.Library.Shortcuts[0] == "Mavi halka", "Reload the trigger settings.");
        Check(config.Library.Matches("Mavi halka", "halka loş", true) &&
            !config.Library.Matches("Beyaz", "gökyüzü", true) &&
            config.Library.Matches("Beyaz", "gökyüzü", false), "Search matches tags and respects the favorite filter.");
        Check(config.Library.PositionFor("test-display-1") == new Point(7, -9) &&
            config.Library.PositionFor("TEST-DISPLAY-2") == new Point(-12, 8) &&
            config.Library.PositionFor("other") == Point.Empty, "Each display keeps its own calibration.");
        Rectangle screen = new Rectangle(-1920, 0, 1920, 1080);
        var settings = new VcrdbSettings { OffsetX = 2, OffsetY = 3 };
        Rectangle before = OverlayForm.GetLayerBounds(screen, settings);
        Rectangle after = OverlayForm.GetLayerBounds(screen, settings, config.Library.PositionFor("TEST-DISPLAY-1"));
        Check(new Point(after.X - before.X, after.Y - before.Y) == new Point(7, -9), "Display correction adds to the profile offset.");
        config.Library.RemoveProfile("Mavi halka");
        Check(config.Library.Shortcuts[0] == null && !config.Library.Favorites.Contains("Mavi halka"), "Removing a profile frees its shortcut.");

        bool pressed = false, shown = false;
        using (var hold = new HoldToShow(() => pressed, value => shown = value))
        {
            hold.Begin();
            Check(!hold.Tracking && !shown, "A delayed hotkey message cannot leave a held overlay visible.");
            pressed = true;
            hold.Begin();
            Check(hold.Tracking && shown, "Holding shows the overlay.");
            pressed = false;
            hold.Poll();
            Check(!hold.Tracking && !shown, "Release hides the overlay and stops the timer.");
        }

        var history = new DesignHistory(settings);
        settings.OffsetX = 19;
        history.Record(settings);
        Check(history.Undo().OffsetX == 2 && history.Redo().OffsetX == 19, "Design changes can be undone and redone.");
        history.Undo();
        settings.OffsetX = 11;
        history.Record(settings);
        Check(!history.CanRedo, "A new edit discards the redo branch.");

        var contrast = new VcrdbSettings
        {
            DotShow = true, DotThick = 8, InShow = false, OutlineThick = 2,
            Color = Color.Aqua, DoubleOutline = true
        };
        using (var pixels = new Bitmap(64, 64))
        using (var graphics = Graphics.FromImage(pixels))
        {
            CrosshairRenderer.Render(graphics, 32, 32, contrast, 1);
            Check(pixels.GetPixel(25, 32).ToArgb() == Color.White.ToArgb() &&
                pixels.GetPixel(26, 32).ToArgb() == Color.Black.ToArgb() &&
                pixels.GetPixel(28, 32).ToArgb() == Color.Aqua.ToArgb(), "Draw white and black outlines around the selected color.");
        }
        VcrdbSettings loaded;
        Check(VcrdbSettings.TryFromShareCode(contrast.ToShareCode(), out loaded) && loaded.DoubleOutline,
            "Contrast outline survives share code.");
    }

    private static void TestWorkflowWindow(string screenshots)
    {
        var library = new AppConfig { HkKey = Keys.F20, HkShift = true };
        library.Library.UpdateProfile("Aqua test", "loş, erişilebilir", true, 0);
        Check(ConfigManager.Save(library), "Prepare workflow fixture.");
        string package = Path.Combine(ConfigManager.Dir, "workflow.vcpak");
        ConfigManager.SaveProfile("Aqua test", new VcrdbSettings { Color = Color.Aqua, OffsetX = 37 });
        ConfigManager.SaveProfile("Altın test", new VcrdbSettings { Color = Color.Gold, DoubleOutline = true });
        try
        {
            Check(ProfilePack.Export(package) == 2, "Export the workflow fixture.");
            ConfigManager.DeleteProfile("Altın test");
            var pack = ProfilePack.Read(package);
            var onlySelected = pack.Import(new[] { "Aqua test" });
            Check(onlySelected.Skipped == 1 && onlySelected.Imported == 0 && !ConfigManager.ProfileExists("Altın test"),
                "Selective import leaves unchecked entries out and preserves conflicts.");
            using (var form = new MainForm(new Size(2000, 1000)))
            {
                form.Show();
                Pump();
                var slider = Field<ModernSlider>(form, "slScale");
                slider.Value = 148;
                Call(form, "ChangeHistory", false);
                Check(slider.Value == 100, "Undo updates actual editor controls.");
                Call(form, "ChangeHistory", true);
                Check(slider.Value == 148, "Redo updates actual editor controls.");
                var registered = Field<bool[]>(form, "favoriteKeysRegistered");
                if (registered[0])
                {
                    var message = Message.Create(form.Handle, 0x0312, new IntPtr(0xB011), IntPtr.Zero);
                    typeof(MainForm).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(form, new object[] { message });
                    Check(Field<VcrdbSettings>(form, "settings").OffsetX == 37, "The registered favorite key loads its design.");
                }
                else Console.WriteLine("SKIP: the Ctrl+Alt+1 favorite was not available on this desktop.");

                var holdToggle = (ToggleSwitch)Field<Panel[]>(form, "pages")[4].Controls["HoldVisibilityToggle"];
                holdToggle.Checked = true;
                Check(!Field<OverlayForm>(form, "overlay").IsLayerVisible && !Field<HoldToShow>(form, "hold").Tracking,
                    "Hold mode without a press keeps the native layer hidden and its timer off.");
                holdToggle.Checked = false;
                Check(Field<OverlayForm>(form, "overlay").IsLayerVisible, "Toggling hold mode off restores normal visibility.");

                Call(form, "SelectPage", 1, false);
                Field<TextBox>(form, "profileSearch").Text = "loş";
                var gallery = Field<Panel>(form, "galleryPanel");
                int cards = 0;
                foreach (Control child in gallery.Controls) if (child is PresetCard) cards++;
                Check(cards == 1, "The live gallery search matches the saved tag.");
                Field<TextBox>(form, "profileSearch").Clear();
                if (screenshots != null)
                {
                    Render(form, Path.Combine(screenshots, "library.png"));
                    using (var dialog = new PackPreviewDialog(pack, new Size(1100, 900)))
                    {
                        dialog.Show(form);
                        Pump();
                        Render(dialog, Path.Combine(screenshots, "pack-selection.png"));
                    }
                    var designs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, VcrdbSettings>>(pack.Entries);
                    using (var dialog = new CompareProfilesDialog(designs, new Size(1100, 900)))
                    {
                        dialog.Show(form);
                        Pump();
                        Render(dialog, Path.Combine(screenshots, "comparison.png"));
                    }
                    using (var dialog = new CalibrationDialog("Hedef ekran · DISPLAY1", Point.Empty, delegate { }))
                    {
                        dialog.Show(form);
                        Call(dialog, "Nudge", 1, -10);
                        Check(dialog.Offset == new Point(1, -10), "Calibration updates axis readout and staged value precisely.");
                        Render(dialog, Path.Combine(screenshots, "calibration.png"));
                    }
                }
                form.Close();
            }
        }
        finally
        {
            File.Delete(package);
            ConfigManager.DeleteProfile("Aqua test");
            ConfigManager.DeleteProfile("Altın test");
        }
    }

    private static void TestGeometry()
    {
        var settings = new VcrdbSettings();
        var bounds = new Rectangle(-1920, 0, 1920, 1080);
        Rectangle layer = OverlayForm.GetLayerBounds(bounds, settings);
        Check(bounds.Contains(layer) && layer.Contains(-960, 540) && layer.Width < 64, "Clip the small layer to its target.");
        Check(OverlayForm.ResolveScreen("missing-test-display").Primary, "Missing monitor falls back to primary.");
        foreach (int size in new[] { 64, 65 })
        using (var bitmap = new Bitmap(size, size))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            CrosshairRenderer.Render(graphics, size / 2.0, size / 2.0, settings, 1, 255);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    Check(bitmap.GetPixel(x, y) == bitmap.GetPixel(size - 1 - x, y) &&
                        bitmap.GetPixel(x, y) == bitmap.GetPixel(x, size - 1 - y), "Symmetry on even and odd pixel bounds.");
        }
    }

    private static void Pump()
    {
        for (int i = 0; i < 12; i++)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(18);
        }
    }

    private static Color Sample(Point at)
    {
        using (var bitmap = new Bitmap(1, 1))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(at, Point.Empty, new Size(1, 1));
            return bitmap.GetPixel(0, 0);
        }
    }

    private static void TestDesktopLayer()
    {
        var screen = Screen.PrimaryScreen.Bounds;
        var center = new Point(screen.X + screen.Width / 2, screen.Y + screen.Height / 2);
        var settings = new VcrdbSettings
        {
            Color = Color.Magenta, DotShow = true, DotThick = 6, OutlineShow = false,
            InShow = false, OutShow = false, Opacity = 255
        };
        using (var background = new Form())
        using (var layer = new OverlayForm(settings, ""))
        {
            background.FormBorderStyle = FormBorderStyle.None;
            background.StartPosition = FormStartPosition.Manual;
            background.Bounds = new Rectangle(center.X - 36, center.Y - 36, 72, 72);
            background.BackColor = Color.Black;
            background.TopMost = true;
            background.Show();
            background.Activate();
            Pump();
            Check(WindowFromPoint(center) == background.Handle, "The test background owns the sample point.");
            layer.Start();
            Pump();
            Check(layer.IsLayerVisible && layer.Bounds.Contains(center), "Native layer opens at screen center.");
            Check(Sample(center).ToArgb() == Color.Magenta.ToArgb(), "Magenta survives the real compositor.");
            var hit = WindowFromPoint(center);
            Check(hit == background.Handle, "Layer passes pointer hits through (hit: " + hit + ", background: " + background.Handle + ", overlay: " + layer.Handle + ").");
            IntPtr activeWindow = GetForegroundWindow();
            Check(activeWindow != layer.Handle, "Layer never takes foreground focus.");
            settings.Opacity = 128;
            layer.UpdateSettings(settings);
            Pump();
            Color half = Sample(center);
            Check(Math.Abs(half.R - 128) <= 2 && half.G <= 2 && Math.Abs(half.B - 128) <= 2, "50% opacity blends over the real desktop.");
            layer.SetDrawEnabled(false);
            Pump();
            Check(!layer.IsLayerVisible && Sample(center).ToArgb() == Color.Black.ToArgb(), "Hidden layer clears its pixels.");
            layer.SetDrawEnabled(true);
            Pump();
            Check(layer.IsLayerVisible, "Layer can be reopened.");
        }
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void Call(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }

    private static void Render(Form form, string file)
    {
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(file, ImageFormat.Png);
        }
    }

    private static void TestTitleCorners(MainForm form)
    {
        var pill = Field<StatusPill>(form, "statusPill");
        Control bar = pill.Parent;
        using (var visible = new Bitmap(bar.Width, bar.Height))
        using (var bare = new Bitmap(bar.Width, bar.Height))
        {
            bar.DrawToBitmap(visible, bar.ClientRectangle);
            var buttons = new Control[] { pill, bar.Controls["WindowMinimize"], bar.Controls["WindowClose"] };
            foreach (var button in buttons) button.Hide();
            try
            {
                bar.DrawToBitmap(bare, bar.ClientRectangle);
                foreach (var button in buttons)
                {
                    Point position = button.Location;
                    Check(visible.GetPixel(position.X + 1, position.Y + 1).ToArgb() ==
                        bare.GetPixel(position.X + 1, position.Y + 1).ToArgb(),
                        "Rounded button corner must show the title bar gradient: " + button.AccessibleName);
                }
            }
            finally { foreach (var button in buttons) button.Show(); }
        }
    }

    private static void TestBackground(MainForm form)
    {
        var editorPreview = Field<PreviewPanel>(form, "preview");
        var overlay = Field<OverlayForm>(form, "overlay");
        var overlayTimer = Field<Timer>(overlay, "pulseTimer");
        var previewTimer = Field<Timer>(editorPreview, "pulseTimer");
        var backgroundPulse = (ToggleSwitch)Field<Panel[]>(form, "pages")[4].Controls["BackgroundPulseToggle"];
        var pulse = Field<ToggleSwitch>(form, "tgPulse");

        pulse.Checked = true;
        Check(overlayTimer.Enabled && previewTimer.Enabled, "Pulse animates when the editor is open.");
        Call(form, "OnHidePlay", form, EventArgs.Empty);
        Check(overlay.IsLayerVisible && !overlayTimer.Enabled && !previewTimer.Enabled, "Tray keeps the static layer with no animation timer.");
        Check(Field<LayeredSurface>(overlay, "surface") == null && Field<Bitmap>(editorPreview, "background") == null,
            "Release hidden editor and static layer drawing buffers.");
        Call(form, "ShowFromTray");
        Check(overlayTimer.Enabled && previewTimer.Enabled, "Reopening restores pulse.");

        form.WindowState = FormWindowState.Minimized;
        Pump();
        Check(overlay.IsLayerVisible && !overlayTimer.Enabled && !previewTimer.Enabled, "Minimizing also keeps a static layer.");
        Call(form, "ShowFromTray");
        backgroundPulse.Checked = true;
        Call(form, "OnHidePlay", form, EventArgs.Empty);
        Check(overlayTimer.Enabled && !previewTimer.Enabled, "Background pulse preference affects the layer only.");
        Check(ConfigManager.Load().BackgroundPulse, "Save the background pulse preference.");
        Call(form, "ShowFromTray");
        backgroundPulse.Checked = false;
        pulse.Checked = false;
    }

    private static void TestWindow(string screenshots)
    {
        // Use a test-owned shortcut to avoid changing the user's configuration.
        var config = new AppConfig { HkKey = Keys.F20, HkShift = true };
        Check(ConfigManager.Save(config), "Fixture config.");
        using (var form = new MainForm(new Size(2000, 1000)))
        using (var reserved = new Form())
        {
            form.Show();
            Pump();
            var box = Field<HotkeyBox>(form, "hotkeyBox");
            bool registered = Field<bool>(form, "hotkeyRegistered");
            bool reservedKey = RegisterHotKey(reserved.Handle, 105, 7, (uint)Keys.F21);
            int hotkeyError = Marshal.GetLastWin32Error();
            if (reservedKey)
            {
                try
                {
                    Check(registered, "Original hotkey registered.");
                    box.KeyValue = Keys.F21;
                    Call(form, "OnHotkeyChanged", box, EventArgs.Empty);
                    Check(box.KeyValue == Keys.F20 && Field<bool>(form, "hotkeyRegistered"), "Restore shortcut on registration collision.");
                }
                finally { UnregisterHotKey(reserved.Handle, 105); }
            }
            else if (hotkeyError == 5)
            {
                Check(!registered && Field<Label>(form, "hotkeyMessage").ForeColor == Glass.Warning, "Surface blocked shortcut registration.");
                box.KeyValue = Keys.F21;
                Call(form, "OnHotkeyChanged", box, EventArgs.Empty);
                Check(box.KeyValue == Keys.F20, "Do not persist a rejected shortcut.");
                Console.WriteLine("SKIP: Windows denied global hotkey registration in this session (error 5).");
            }
            else throw new System.ComponentModel.Win32Exception(hotkeyError, "Unable to reserve the test hotkey.");

            TestTitleCorners(form);
            TestBackground(form);
            Field<ModernSlider>(form, "slScale").Value = 184;
            Check(Field<ModernSlider>(form, "positionScaleSlider").Value == 184, "Editing scale updates both controls.");
            Field<ModernSlider>(form, "positionScaleSlider").Value = 126;
            Check(Field<ModernSlider>(form, "slScale").Value == 126, "Position scale updates the editor.");
            Call(form, "ToggleCrosshair");
            Check(!Field<OverlayForm>(form, "overlay").IsLayerVisible && !Field<StatusPill>(form, "statusPill").On,
                "Status and native layer toggle together.");
            Call(form, "OnHidePlay", form, EventArgs.Empty);
            Check(!form.Visible, "Editor hides to the tray.");
            Call(form, "ShowFromTray");
            Pump();
            Check(form.Visible, "Editor reopens from the tray.");

            if (screenshots != null)
            {
                Field<ModernSlider>(form, "slScale").Value = 100;
                Call(form, "ToggleCrosshair");
                Call(form, "SetNotice", "");
                Render(form, Path.Combine(screenshots, "studio.png"));
                var preview = Field<PreviewPanel>(form, "preview");
                var settings = Field<VcrdbSettings>(form, "settings").Clone();
                settings.DotShow = true;
                settings.DotThick = 13;
                settings.Marker = CenterShape.Ring;
                settings.RoundedArms = true;
                settings.TopLength = 0;
                Call(form, "ApplySettingsObject", settings);
                foreach (Control control in Field<Panel[]>(form, "pages")[0].Controls)
                {
                    var tab = control as TabStrip;
                    if (tab != null) Call(tab, "SelectTab", 4);
                }
                var visibility = preview.Parent.Controls["VisibilityTest"];
                Call(visibility, "OnClick", EventArgs.Empty);
                Render(form, Path.Combine(screenshots, "shape-and-sky.png"));
                foreach (Control group in preview.Parent.Controls)
                    if (group.Visible)
                        foreach (Control choice in group.Controls)
                            if (choice.Text == "Hareket") Call(choice, "OnClick", EventArgs.Empty);
                Render(form, Path.Combine(screenshots, "motion.png"));
                Call(visibility, "OnClick", EventArgs.Empty);
                Call(form, "SelectPage", 1, false);
                Pump();
                Render(form, Path.Combine(screenshots, "profiles.png"));
                using (var dialog = new GameRulesDialog(
                    new System.Collections.Generic.Dictionary<string, string> { { "example-game.exe", "Örnek profil" } },
                    new System.Collections.Generic.List<string> { "Örnek profil" }))
                {
                    dialog.Show(form);
                    Pump();
                    Render(dialog, Path.Combine(screenshots, "game-profiles.png"));
                }
                Call(form, "SelectPage", 4, false);
                Field<Label>(form, "hotkeyMessage").Text = "Ctrl, Alt veya Shift ile bir tuş; ya da tek başına bir F tuşu.";
                Field<Label>(form, "hotkeyMessage").ForeColor = Theme.TextDim;
                Field<HotkeyBox>(form, "hotkeyBox").KeyValue = Keys.C;
                Field<HotkeyBox>(form, "hotkeyBox").Shift = false;
                Pump();
                Render(form, Path.Combine(screenshots, "settings.png"));
            }
            form.Close();
        }

        using (var form = new MainForm(new Size(800, 560)))
        {
            form.Show();
            Pump();
            Check(!form.HorizontalScroll.Visible && form.VerticalScroll.Visible, "Small display stacks the preview and scrolls only vertically.");
            if (screenshots != null) Render(form, Path.Combine(screenshots, "compact.png"));
            form.Close();
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 1 && args[0] == "--foreground-fixture")
        {
            using (var game = new Form { Text = "Crosshair test game", Size = new Size(265, 142) })
                Application.Run(game);
            return 0;
        }
        string data = Path.Combine(Path.GetTempPath(), "CrosshairTests-" + Guid.NewGuid().ToString("N"));
        ConfigManager.Dir = data;
        ConfigManager.ConfigPath = Path.Combine(data, "config.ini");
        ConfigManager.ProfilesDir = Path.Combine(data, "profiles");
        try
        {
            TestSecondInstanceWarning();
            TestShareCodeSynchronization();
            if (args.Length == 1 && args[0] == "--ui-regression") return 0;
            TestSettings();
            TestShapesAndPack();
            TestWorkflowData();
            TestGeometry();
            if (args.Length > 0 && args[0] == "--offscreen")
                Console.WriteLine("SKIP: desktop pixel capture requires an active desktop; offscreen run requested.");
            else TestDesktopLayer();
            string screenshots = args.Length == 0 ? null : args[args.Length - 1];
            if (screenshots == "--offscreen") screenshots = null;
            if (screenshots != null) Directory.CreateDirectory(screenshots);
            TestWindow(screenshots);
            TestGameProfiles();
            TestWorkflowWindow(screenshots);
            Console.WriteLine("PASS: settings, profiles, geometry, shortcut rollback, rounded controls, background rendering, shapes, profile packages, foreground bindings, library, calibration, hold mode, history and tray. See SKIP messages for desktop limitations.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
    }
}
