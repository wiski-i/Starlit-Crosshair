using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NativeVcrdbCrosshair
{
    public partial class MainForm
    {
        private const int FavoriteHotkeyBase = 0xB011;
        private static readonly string[] PresetNames = { "TenZ", "Shroud", "ScreaM", "Yay", "Valorant",
            "Nokta", "T-Şekli", "Klasik +", "Sniper", "Kontrast Beyaz", "Kontrast Sarı", "Kontrast Turkuaz" };

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        private HoldToShow hold;
        private bool visibilityBeforeHold = true, calibrating;
        private string calibrationDevice;
        private Point calibrationOffset;
        private readonly bool[] favoriteKeysRegistered = new bool[3];
        private VcrdbSettings temporaryFavorite;
        private DesignHistory history;
        private bool historyPending;
        private ModernButton undoButton, redoButton, favoritesFilter;
        private ToggleSwitch doubleOutline;
        private TextBox profileSearch;
        private Label screenOffsetLabel;
        private List<KeyValuePair<string, VcrdbSettings>> savedDesigns;
        private bool galleryStale = true;

        private void InitializeWorkflow()
        {
            history = new DesignHistory(settings);
            hold = new HoldToShow(IsVisibilityShortcutDown, SetCrosshairVisible);
            overlay.PositionOffset = device =>
                calibrating && String.Equals(calibrationDevice, device, StringComparison.OrdinalIgnoreCase)
                    ? calibrationOffset : config.Library.PositionFor(device);
        }

        private void DisposeWorkflow()
        {
            if (hold != null) hold.Dispose();
            if (!IsHandleCreated) return;
            for (int i = 0; i < favoriteKeysRegistered.Length; i++)
                if (favoriteKeysRegistered[i]) UnregisterHotKey(Handle, FavoriteHotkeyBase + i);
        }

        private static bool IsDown(Keys key) { return GetAsyncKeyState((int)key) < 0; }

        private bool IsVisibilityShortcutDown()
        {
            return config.HoldVisibility && IsDown(config.HkKey) &&
                (!config.HkCtrl || IsDown(Keys.ControlKey)) &&
                (!config.HkAlt || IsDown(Keys.Menu)) &&
                (!config.HkShift || IsDown(Keys.ShiftKey));
        }

        private void SetHoldVisibility(bool enabled)
        {
            if (enabled && !config.HoldVisibility) visibilityBeforeHold = crosshairVisible;
            config.HoldVisibility = enabled;
            hold.Stop();
            SetCrosshairVisible(!enabled && visibilityBeforeHold);
        }

        private void ApplyVisibilityShortcut()
        {
            if (calibrating) return;
            if (config.HoldVisibility) hold.Begin();
            else ToggleCrosshair();
        }

        private void SetCrosshairVisible(bool visible)
        {
            crosshairVisible = visible;
            if (overlay != null) overlay.SetDrawEnabled(calibrating || visible);
            if (statusPill != null) statusPill.On = visible;
            UpdateOverlayMenuText();
        }

        private void RegisterFavoriteKeys()
        {
            bool unavailable = false;
            for (int i = 0; i < favoriteKeysRegistered.Length; i++)
            {
                int id = FavoriteHotkeyBase + i;
                if (favoriteKeysRegistered[i]) UnregisterHotKey(Handle, id);
                string name = config.Library.Shortcuts[i];
                bool assigned = name != null && ConfigManager.ProfileExists(name);
                favoriteKeysRegistered[i] = assigned && RegisterHotKey(Handle, id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)(Keys.D1 + i));
                if (assigned && !favoriteKeysRegistered[i]) unavailable = true;
            }
            if (unavailable)
                SetNotice("Favori kısayollarından biri kullanılıyor. Yeni profil ayarından başka bir yuva seç.");
        }

        private bool HandleFavoriteShortcut(int id)
        {
            int index = id - FavoriteHotkeyBase;
            if (index < 0 || index >= favoriteKeysRegistered.Length || !favoriteKeysRegistered[index]) return false;
            var design = ConfigManager.LoadProfile(config.Library.Shortcuts[index]);
            if (design == null || calibrating) return true;
            if (automaticProfile != null)
            {
                temporaryFavorite = design;
                overlay.UpdateSettings(EffectiveDesign);
            }
            else ApplySettingsObject(design);
            return true;
        }

        private VcrdbSettings EffectiveDesign { get { return temporaryFavorite ?? automaticProfile ?? settings; } }

        private void MarkHistory()
        {
            if (history == null) return;
            historyPending = true;
            UpdateHistoryButtons();
        }

        private void CommitHistory()
        {
            if (!historyPending) return;
            historyPending = false;
            history.Record(settings);
            UpdateHistoryButtons();
        }

        private void ChangeHistory(bool redo)
        {
            CommitHistory();
            var original = redo ? history.Redo() : history.Undo();
            if (original == null) return;
            settings.CopyFrom(original);
            RefreshUIFromSettings();
            if (shareOutBox != null && currentPage == 4) shareOutBox.Text = settings.ToShareCode();
            UpdateHistoryButtons();
            ScheduleSave();
        }

        private void UpdateHistoryButtons()
        {
            if (undoButton != null) undoButton.Enabled = historyPending || history.CanUndo;
            if (redoButton != null) redoButton.Enabled = !historyPending && history.CanRedo;
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (history != null && !(ActiveControl is TextBoxBase) && !(ActiveControl is ComboBox) &&
                (keyData == (Keys.Control | Keys.Z) || keyData == (Keys.Control | Keys.Y)))
            {
                ChangeHistory((keyData & Keys.KeyCode) == Keys.Y);
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void AddEditorActions(Panel page, int x)
        {
            undoButton = OptionButton(page, "Geri Al", x, 626, 77);
            undoButton.AccessibleName = "Geri al (Ctrl+Z)";
            undoButton.Click += delegate { ChangeHistory(false); };
            redoButton = OptionButton(page, "Yinele", x + 85, 626, 77);
            redoButton.AccessibleName = "Yinele (Ctrl+Y)";
            redoButton.Click += delegate { ChangeHistory(true); };
            OptionButton(page, "Tasarımı Kopyala", x + 171, 626, 145).Click += delegate { SaveDesignCopy(settings, "Tasarım kopyası"); };
            UpdateHistoryButtons();
        }

        private void SaveDesignCopy(VcrdbSettings design, string proposed)
        {
            const string suffix = " kopya";
            if (proposed.Length > 40) proposed = proposed.Substring(0, 40 - suffix.Length).TrimEnd() + suffix;
            string name = PromptForm.ShowDialog(this, "Yeni profil adı:", proposed);
            if (name == null || name.Trim().Length == 0) return;
            name = name.Trim();
            if (!ConfigManager.IsValidProfileName(name))
            {
                ShowProfileError("Yeni kopya için 1–40 karakterli geçerli bir ad kullan.");
                return;
            }
            try
            {
                if (!ConfigManager.TryAddProfile(name, design))
                {
                    ShowProfileError("Bu ad zaten kullanılıyor. Kopya için farklı bir ad seç.");
                    return;
                }
                ProfilesChanged();
                SetNotice("Tasarım kopyalandı: " + name);
            }
            catch (IOException) { ShowProfileError("Tasarım kopyalanamadı."); }
            catch (UnauthorizedAccessException) { ShowProfileError("Profil klasörüne yazma izni yok."); }
        }

        private void AddLibraryTools(Panel page)
        {
            profileSearch = MakeTextBox(page, "Profil adı veya etiketle ara", 28, 137, 229);
            profileSearch.MaxLength = 160;
            profileSearch.TextChanged += delegate { RebuildGallery(); };
            profileSearch.Name = "ProfileSearch";
            favoritesFilter = OptionButton(page, "Favoriler", 265, 133, 98);
            favoritesFilter.Click += delegate { favoritesFilter.Selected = !favoritesFilter.Selected; RebuildGallery(); };
            OptionButton(page, "Yan Yana Kıyasla", 372, 133, 145).Click += CompareProfiles;
            OptionButton(page, "Etiket ve Kısayol", 28, 174, 156).Click += delegate { EditProfileDetails(null); };
            MakeRowLabel(page, "Üstte profil adı veya etiketle ara.", 198, 178);
        }

        private List<KeyValuePair<string, VcrdbSettings>> SavedDesigns()
        {
            if (savedDesigns != null) return savedDesigns;
            savedDesigns = new List<KeyValuePair<string, VcrdbSettings>>();
            foreach (string name in ConfigManager.ListProfiles())
            {
                VcrdbSettings design = ConfigManager.LoadProfile(name);
                if (design != null) savedDesigns.Add(new KeyValuePair<string, VcrdbSettings>(name, design));
            }
            return savedDesigns;
        }

        private void CompareProfiles(object sender, EventArgs e)
        {
            var designs = new List<KeyValuePair<string, VcrdbSettings>>
            {
                new KeyValuePair<string, VcrdbSettings>("Editördeki tasarım", settings.Clone())
            };
            foreach (var item in SavedDesigns())
                designs.Add(new KeyValuePair<string, VcrdbSettings>("Profil · " + item.Key, item.Value));
            for (int i = 0; i < PresetNames.Length; i++)
                designs.Add(new KeyValuePair<string, VcrdbSettings>("Yerleşik · " + PresetNames[i], BuildPreset(i)));

            using (var dialog = new CompareProfilesDialog(designs, Screen.FromControl(this).WorkingArea.Size))
                dialog.ShowDialog(this);
        }

        private void EditProfileDetails(string preferred)
        {
            var profiles = ConfigManager.ListProfiles();
            if (profiles.Count == 0)
            {
                SetNotice("Etiket veya kısayol eklemek için önce bir profil kaydet.");
                return;
            }
            using (var dialog = new ProfileDetailsDialog(profiles, config.Library, preferred))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                config.Library.UpdateProfile(dialog.ProfileName, dialog.Tags, dialog.Favorite, dialog.Slot);
            }
            RegisterFavoriteKeys();
            RebuildGallery();
            ScheduleSave();
        }

        private void SetupProfileCard(PresetCard card, string name, VcrdbSettings design)
        {
            if (name == null) return;
            string tags;
            if (config.Library.Tags.TryGetValue(name, out tags)) card.SecondaryText = tags;
            var menu = new ContextMenu();
            menu.MenuItems.Add("Etiketler ve favori kısayolu", delegate { EditProfileDetails(name); });
            menu.MenuItems.Add("Kopyasını Kaydet", delegate { SaveDesignCopy(design, name + " kopya"); });
            menu.MenuItems.Add("Sil", delegate { DeleteSavedProfile(name); });
            card.ContextMenu = menu;
            card.Disposed += delegate { menu.Dispose(); };
            card.DeleteRequested += delegate { DeleteSavedProfile(name); };
        }

        private void DeleteSavedProfile(string name)
        {
            if (MessageBox.Show(this, "“" + name + "” profili silinsin mi?", "Profili sil",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                ConfigManager.DeleteProfile(name);
                config.Library.RemoveProfile(name);
                ProfilesChanged();
                ScheduleSave();
            }
            catch (IOException) { ShowProfileError("Profil silinemedi."); }
            catch (UnauthorizedAccessException) { ShowProfileError("Profili silme izni yok."); }
        }

        private void OpenCalibration(object sender, EventArgs e)
        {
            var screen = OverlayForm.ResolveScreen(config.MonitorDevice);
            string device = screen.DeviceName;
            Point previous = config.Library.PositionFor(device);
            bool saved = false;
            calibrationDevice = device;
            calibrationOffset = previous;
            hold.Stop();
            calibrating = true;
            // Give even a fully transparent design a visible reference during calibration.
            var guide = new VcrdbSettings { Color = Color.White, DoubleOutline = true, InLen = 10, InOff = 4 };
            overlay.UpdateSettings(guide);
            overlay.SetDrawEnabled(true);
            try
            {
                using (var dialog = new CalibrationDialog("Hedef ekran · " + DisplayName(device), previous, offset =>
                {
                    calibrationOffset = offset;
                    overlay.RefreshLayer();
                }))
                    saved = dialog.ShowDialog(this) == DialogResult.OK;
            }
            finally
            {
                if (saved) config.Library.SetPosition(device, calibrationOffset);
                calibrating = false;
                overlay.UpdateSettings(EffectiveDesign);
                overlay.SetDrawEnabled(crosshairVisible);
                UpdateScreenOffsetLabel();
                if (saved) ScheduleSave();
            }
        }

        private static string DisplayName(string device)
        {
            return device.Substring(device.LastIndexOf('\\') + 1);
        }

        private void UpdateScreenOffsetLabel()
        {
            if (screenOffsetLabel == null) return;
            string device = OverlayForm.ResolveScreen(config.MonitorDevice).DeviceName;
            Point offset = config.Library.PositionFor(device);
            screenOffsetLabel.Text = String.Format("{0} · X {1:+0;-0;0}px  Y {2:+0;-0;0}px", DisplayName(device), offset.X, offset.Y);
        }

        private static VcrdbSettings ContrastPreset(int index)
        {
            return new VcrdbSettings
            {
                Color = index == 9 ? Color.White : index == 10 ? Color.Gold : Color.Aqua,
                OutlineShow = true, DoubleOutline = true, OutlineThick = 2,
                InLen = 9, InThick = 4, InOff = 5, DotShow = true, DotThick = 3
            };
        }
    }
}
