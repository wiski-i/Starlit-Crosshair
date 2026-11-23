using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NativeVcrdbCrosshair
{
    internal sealed class GameRulesDialog : Form
    {
        private readonly TextBox executable;
        private readonly ComboBox profile;
        private readonly ListBox rules;
        private readonly Label feedback;

        internal readonly Dictionary<string, string> Rules;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDialogFrame(Handle);
        }

        internal GameRulesDialog(Dictionary<string, string> current, List<string> profiles)
        {
            Rules = new Dictionary<string, string>(current, StringComparer.OrdinalIgnoreCase);
            Text = UiText.Get("Oyuna özel profiller");
            ClientSize = new Size(510, 435);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            BackColor = Theme.BgPanel;
            ForeColor = Theme.Text;

            AddLabel(UiText.Get("Oyun ön plandayken bağlı profil kullanılır. Oyundan çıkınca editördeki tasarım geri gelir."), 22, 15, 466, 42);
            AddLabel(UiText.Get("Bağlı oyunlar"), 22, 65, 466, 19);
            rules = new ListBox
            {
                Bounds = new Rectangle(22, 86, 466, 125), BackColor = Theme.BgPanelAlt,
                ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = UiText.Get("Bağlı oyunlar")
            };
            rules.SelectedIndexChanged += delegate
            {
                var entry = rules.SelectedItem as RuleEntry;
                if (entry == null) return;
                executable.Text = entry.Executable;
                profile.SelectedIndex = profile.FindStringExact(entry.Profile);
            };
            Controls.Add(rules);

            AddLabel(UiText.Get("Oyun dosyası (.exe)"), 22, 221, 190, 20);
            executable = new TextBox
            {
                Bounds = new Rectangle(22, 245, 244, 28), BackColor = Theme.BgPanelAlt,
                ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = UiText.Get("Oyun dosyası adı"), MaxLength = 220
            };
            Controls.Add(executable);
            ButtonAt(UiText.Get("Dosya seç"), 273, 243, 91).Click += ChooseExecutable;
            ButtonAt(UiText.Get("Bağı kaldır"), 370, 243, 118).Click += RemoveRule;

            AddLabel(UiText.Get("Kayıtlı profil"), 22, 284, 230, 18);
            profile = new ComboBox
            {
                Bounds = new Rectangle(22, 306, 244, 28), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                DrawMode = DrawMode.OwnerDrawFixed,
                BackColor = Theme.BgPanelAlt, ForeColor = Theme.Text, AccessibleName = UiText.Get("Kayıtlı profil")
            };
            profile.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                Color fill = (e.State & DrawItemState.Selected) != 0 ? Theme.Accent : Theme.BgPanelAlt;
                using (var background = new SolidBrush(fill)) e.Graphics.FillRectangle(background, e.Bounds);
                if (e.Index >= 0)
                    TextRenderer.DrawText(e.Graphics, (string)profile.Items[e.Index], profile.Font, e.Bounds,
                        Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            foreach (var name in profiles) profile.Items.Add(name);
            if (profile.Items.Count > 0) profile.SelectedIndex = 0;
            Controls.Add(profile);
            var bind = ButtonAt(UiText.Get("Bağla / Güncelle"), 273, 304, 215);
            bind.Click += BindRule;
            bind.Enabled = profile.Items.Count > 0;
            feedback = AddLabel(profile.Items.Count == 0 ? UiText.Get("Önce Profil Galerisi’nden bir tasarım kaydet.") : "",
                22, 345, 466, 33);

            var save = ButtonAt(UiText.Get("Kaydet"), 391, 387, 97);
            save.Ghost = false;
            save.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            var cancel = new Button { DialogResult = DialogResult.Cancel, TabStop = false };
            CancelButton = cancel;
            cancel.Visible = false;
            Controls.Add(cancel);
            ButtonAt(UiText.Get("Vazgeç"), 285, 387, 96).Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            ReloadRules(null);
        }

        private Label AddLabel(string text, int x, int y, int width, int height)
        {
            var label = new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, height),
                ForeColor = Theme.TextDim
            };
            Controls.Add(label);
            return label;
        }

        private ModernButton ButtonAt(string text, int x, int y, int width)
        {
            var button = new ModernButton
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, 31), Ghost = true
            };
            Controls.Add(button);
            return button;
        }

        private void ChooseExecutable(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = UiText.Get("Windows uygulaması (*.exe)|*.exe"), CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) executable.Text = Path.GetFileName(dialog.FileName);
        }

        private void BindRule(object sender, EventArgs e)
        {
            string name = executable.Text.Trim();
            if (!GameProfileWatcher.IsExecutableName(name) || profile.SelectedItem == null)
            {
                feedback.Text = UiText.Get("Geçerli bir .exe adı ve kayıtlı bir profil seç.");
                feedback.ForeColor = Glass.Warning;
                return;
            }
            if (!Rules.ContainsKey(name) && Rules.Count >= 250)
            {
                feedback.Text = UiText.Get("En çok 250 oyun bağı kullanılabilir.");
                feedback.ForeColor = Glass.Warning;
                return;
            }
            Rules[name] = (string)profile.SelectedItem;
            ReloadRules(name);
            feedback.Text = UiText.Get("Bağ hazır. Uygulamak için Kaydet’e bas.");
            feedback.ForeColor = Glass.Success;
        }

        private void RemoveRule(object sender, EventArgs e)
        {
            var entry = rules.SelectedItem as RuleEntry;
            if (entry == null) return;
            Rules.Remove(entry.Executable);
            ReloadRules(null);
            executable.Clear();
            feedback.Text = UiText.Get("Bağ kaldırıldı. Uygulamak için Kaydet’e bas.");
            feedback.ForeColor = Glass.Success;
        }

        private void ReloadRules(string selection)
        {
            rules.Items.Clear();
            var keys = new List<string>(Rules.Keys);
            keys.Sort(StringComparer.CurrentCultureIgnoreCase);
            foreach (var key in keys)
            {
                rules.Items.Add(new RuleEntry(key, Rules[key]));
                if (String.Equals(key, selection, StringComparison.OrdinalIgnoreCase)) rules.SelectedIndex = rules.Items.Count - 1;
            }
        }

        private sealed class RuleEntry
        {
            internal readonly string Executable, Profile;
            internal RuleEntry(string executable, string profile) { Executable = executable; Profile = profile; }
            public override string ToString() { return Executable + "  →  " + Profile; }
        }
    }
}
