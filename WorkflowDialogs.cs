using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NativeVcrdbCrosshair
{
    internal abstract class WorkflowDialog : Form
    {
        protected WorkflowDialog(string title, Size size)
        {
            Text = title;
            ClientSize = size;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            BackColor = Theme.BgPanel;
            ForeColor = Theme.Text;
            var cancel = new Button { DialogResult = DialogResult.Cancel, Visible = false, TabStop = false };
            Controls.Add(cancel);
            CancelButton = cancel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDialogFrame(Handle);
        }

        protected Label Caption(string text, int x, int y, int width, int height)
        {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height), ForeColor = Theme.TextDim };
            Controls.Add(label);
            return label;
        }

        protected ModernButton ActionButton(string text, int x, int y, int width, Action action, bool primary = false)
        {
            var button = new ModernButton { Text = text, Bounds = new Rectangle(x, y, width, 34), Ghost = !primary };
            button.Click += delegate { action(); };
            Controls.Add(button);
            return button;
        }

        protected ComboBox Picker(string name, int x, int y, int width)
        {
            var box = new ComboBox
            {
                AccessibleName = name, Bounds = new Rectangle(x, y, width, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            Theme.DarkPicker(box);
            Controls.Add(box);
            return box;
        }
    }

    internal sealed class ProfileDetailsDialog : WorkflowDialog
    {
        private readonly ProfileLibrary library;
        private readonly ComboBox profile, slot;
        private readonly TextBox tags;
        private readonly ToggleSwitch favorite;
        internal string ProfileName { get { return (string)profile.SelectedItem; } }
        internal string Tags { get { return tags.Text; } }
        internal bool Favorite { get { return favorite.Checked; } }
        internal int Slot { get { return slot.SelectedIndex - 1; } }

        internal ProfileDetailsDialog(List<string> names, ProfileLibrary library, string selected)
            : base(UiText.Get("Etiketler ve favoriler"), new Size(490, 310))
        {
            this.library = library;
            Caption(UiText.Get("Kayıtlı profil"), 22, 17, 390, 19);
            profile = Picker(UiText.Get("Kayıtlı profil"), 22, 40, 446);
            foreach (string name in names) profile.Items.Add(name);
            Caption(UiText.Get("Etiketler · virgülle ayır"), 22, 78, 390, 19);
            tags = new TextBox
            {
                Bounds = new Rectangle(22, 100, 446, 28), MaxLength = 120,
                BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text,
                AccessibleName = UiText.Get("Profil etiketleri")
            };
            Controls.Add(tags);
            Caption(UiText.Get("Favori olarak işaretle"), 22, 143, 180, 22);
            favorite = new ToggleSwitch { Location = new Point(183, 138), AccessibleName = UiText.Get("Favori") };
            Controls.Add(favorite);
            Caption(UiText.Get("Favori kısayolu"), 22, 181, 190, 20);
            slot = Picker(UiText.Get("Favori kısayolu"), 22, 202, 246);
            slot.Items.AddRange(new object[] { UiText.Get("Kısayol yok"), "Ctrl + Alt + 1", "Ctrl + Alt + 2", "Ctrl + Alt + 3" });
            profile.SelectedIndexChanged += delegate { LoadSelected(); };
            profile.SelectedIndex = Math.Max(0, names.FindIndex(name => String.Equals(name, selected, StringComparison.OrdinalIgnoreCase)));
            ActionButton(UiText.Get("Vazgeç"), 246, 260, 102, Close);
            ActionButton(UiText.Get("Kaydet"), 356, 260, 112, Save, true);
        }

        private void LoadSelected()
        {
            string existing;
            tags.Text = library.Tags.TryGetValue(ProfileName, out existing) ? existing : "";
            favorite.SetCheckedSilent(library.Favorites.Contains(ProfileName));
            slot.SelectedIndex = Array.FindIndex(library.Shortcuts,
                name => String.Equals(name, ProfileName, StringComparison.OrdinalIgnoreCase)) + 1;
        }

        private void Save()
        {
            if (Slot >= 0)
            {
                string previous = library.Shortcuts[Slot];
                if (previous != null && !String.Equals(previous, ProfileName, StringComparison.OrdinalIgnoreCase) &&
                    MessageBox.Show(this, string.Format(UiText.Get("Bu kısayol “{0}” profiline atanmış. Yeni profile taşınsın mı?"), previous),
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }
            DialogResult = DialogResult.OK;
        }
    }

    internal sealed class CalibrationDialog : WorkflowDialog
    {
        private readonly Label readout;
        private readonly Action<Point> show;
        internal Point Offset { get; private set; }

        internal CalibrationDialog(string display, Point initial, Action<Point> show)
            : base(UiText.Get("Piksel kalibrasyonu"), new Size(440, 282))
        {
            this.show = show;
            Offset = initial;
            Caption(display, 22, 18, 396, 22);
            Caption(UiText.Get("Yön tuşları 1 piksel, Shift ile 10 piksel. Düzeltme bu ekrana kaydedilir."), 22, 44, 396, 44);
            readout = Caption("", 164, 107, 236, 29);
            ActionButton("↑", 66, 96, 37, () => Nudge(0, -1));
            ActionButton("←", 22, 136, 37, () => Nudge(-1, 0));
            ActionButton("↓", 66, 136, 37, () => Nudge(0, 1));
            ActionButton("→", 110, 136, 37, () => Nudge(1, 0));
            ActionButton(UiText.Get("Ortala"), 164, 151, 101, () => SetOffset(Point.Empty));
            ActionButton(UiText.Get("Vazgeç"), 199, 226, 102, Close);
            ActionButton(UiText.Get("Uygula"), 310, 226, 108, () => DialogResult = DialogResult.OK, true);
            RefreshReadout();
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if ((keyData & (Keys.Control | Keys.Alt)) != 0) return base.ProcessCmdKey(ref message, keyData);
            int step = (keyData & Keys.Shift) != 0 ? 10 : 1;
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: Nudge(-step, 0); return true;
                case Keys.Right: Nudge(step, 0); return true;
                case Keys.Up: Nudge(0, -step); return true;
                case Keys.Down: Nudge(0, step); return true;
                default: return base.ProcessCmdKey(ref message, keyData);
            }
        }

        private void Nudge(int x, int y)
        {
            SetOffset(ProfileLibrary.BoundPosition(new Point(Offset.X + x, Offset.Y + y)));
        }

        private void SetOffset(Point offset)
        {
            if (Offset == offset) return;
            Offset = offset;
            RefreshReadout();
            show(Offset);
        }

        private void RefreshReadout()
        {
            readout.Text = String.Format("X {0:+0;-0;0} px     Y {1:+0;-0;0} px", Offset.X, Offset.Y);
        }
    }

    internal sealed class CompareProfilesDialog : WorkflowDialog
    {
        private readonly PreviewPanel left, right;
        private readonly ComboBox leftChoice, rightChoice;

        internal CompareProfilesDialog(List<KeyValuePair<string, VcrdbSettings>> designs, Size available)
            : base(UiText.Get("Yan yana profil kıyaslama"), new Size(Math.Max(460, Math.Min(930, available.Width - 28)),
                Math.Max(350, Math.Min(650, available.Height - 48))))
        {
            int width = (ClientSize.Width - 60) / 2;
            int second = width + 40;
            Caption(UiText.Get("Sol profil"), 20, 16, width, 20);
            Caption(UiText.Get("Sağ profil"), second, 16, width, 20);
            leftChoice = Picker(UiText.Get("Sol profil"), 20, 38, width);
            rightChoice = Picker(UiText.Get("Sağ profil"), second, 38, width);
            foreach (var design in designs)
            {
                leftChoice.Items.Add(design.Key);
                rightChoice.Items.Add(design.Key);
            }

            int previewHeight = ClientSize.Height - 156;
            left = new PreviewPanel(designs[0].Value) { Bounds = new Rectangle(20, 80, width, previewHeight) };
            right = new PreviewPanel(designs[1].Value) { Bounds = new Rectangle(second, 80, width, previewHeight) };
            Controls.Add(left);
            Controls.Add(right);
            leftChoice.SelectedIndexChanged += delegate { left.SetSettings(designs[leftChoice.SelectedIndex].Value); };
            rightChoice.SelectedIndexChanged += delegate { right.SetSettings(designs[rightChoice.SelectedIndex].Value); };
            leftChoice.SelectedIndex = 0;
            rightChoice.SelectedIndex = 1;

            var scene = Picker(UiText.Get("Ortak zemin"), 20, ClientSize.Height - 48, 143);
            scene.Items.AddRange(new object[] { UiText.Get("Optik vizör"), UiText.Get("Gökyüzü"), UiText.Get("Loş ortam") });
            scene.SelectedIndexChanged += delegate
            {
                int mode = scene.SelectedIndex == 0 ? 0 : VisibilityScenes.Sky + scene.SelectedIndex - 1;
                left.SetBackgroundMode(mode);
                right.SetBackgroundMode(mode);
            };
            scene.SelectedIndex = 0;
            var zoom = Picker(UiText.Get("Ortak yakınlaştırma"), 175, ClientSize.Height - 48, 86);
            zoom.Items.AddRange(new object[] { "1×", "2×", "3×" });
            zoom.SelectedIndexChanged += delegate { left.Zoom = right.Zoom = zoom.SelectedIndex + 1; left.Invalidate(); right.Invalidate(); };
            zoom.SelectedIndex = 1;
            ActionButton(UiText.Get("Kapat"), ClientSize.Width - 122, ClientSize.Height - 53, 102, Close);
        }
    }

    internal sealed class PackPreviewDialog : WorkflowDialog
    {
        private readonly CheckedListBox entries;
        private readonly Label feedback;
        internal readonly List<string> Selected = new List<string>();

        internal PackPreviewDialog(ProfilePack pack, Size available)
            : base(UiText.Get("Profil paketi önizleme"), new Size(Math.Max(485, Math.Min(720, available.Width - 24)), 440))
        {
            Caption(UiText.Get("Kutuları işaretleyerek alınacak profilleri seç. Aynı adlı yerel profiller korunur."), 20, 18, ClientSize.Width - 40, 36);
            int leftWidth = ClientSize.Width * 2 / 5;
            entries = new CheckedListBox
            {
                Bounds = new Rectangle(20, 64, leftWidth, 277), CheckOnClick = true,
                BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = UiText.Get("Paketteki profiller")
            };
            foreach (var item in pack.Entries)
            {
                bool exists = ConfigManager.ProfileExists(item.Key);
                entries.Items.Add(new PackRow(item.Key, item.Value, exists), !exists);
            }
            Controls.Add(entries);
            var preview = new PreviewPanel(new VcrdbSettings())
            {
                Bounds = new Rectangle(leftWidth + 36, 64, ClientSize.Width - leftWidth - 56, 273)
            };
            Controls.Add(preview);
            entries.SelectedIndexChanged += delegate
            {
                var item = entries.SelectedItem as PackRow;
                if (item != null) preview.SetSettings(item.Design);
            };
            entries.SelectedIndex = 0;
            ActionButton(UiText.Get("Yeni Olanları Seç"), 20, 350, 141, () =>
            {
                for (int i = 0; i < entries.Items.Count; i++) entries.SetItemChecked(i, !((PackRow)entries.Items[i]).Exists);
            });
            ActionButton(UiText.Get("Seçimi Kaldır"), 169, 350, 116, () =>
            {
                for (int i = 0; i < entries.Items.Count; i++) entries.SetItemChecked(i, false);
            });
            feedback = Caption("", 20, 398, ClientSize.Width - 224, 25);
            ActionButton(UiText.Get("Seçilenleri Al"), ClientSize.Width - 176, 391, 156, ImportSelection, true);
        }

        private void ImportSelection()
        {
            Selected.Clear();
            foreach (PackRow row in entries.CheckedItems) Selected.Add(row.Name);
            if (Selected.Count > 0) DialogResult = DialogResult.OK;
            else feedback.Text = UiText.Get("İçe aktarmak için en az bir profil seç.");
        }

        private sealed class PackRow
        {
            internal readonly string Name;
            internal readonly VcrdbSettings Design;
            internal readonly bool Exists;
            internal PackRow(string name, VcrdbSettings design, bool exists) { Name = name; Design = design; Exists = exists; }
            public override string ToString() { return Exists ? Name + UiText.Get(" · Yerelde var") : Name; }
        }
    }
}
