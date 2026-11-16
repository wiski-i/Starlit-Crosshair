using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace NativeVcrdbCrosshair
{
    internal sealed class ProfileLibrary
    {
        internal readonly string[] Shortcuts = new string[3];
        internal readonly Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, Point> ScreenPositions = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);

        internal bool Matches(string name, string query, bool favoritesOnly)
        {
            if (favoritesOnly && !Favorites.Contains(name)) return false;
            string tags;
            Tags.TryGetValue(name, out tags);
            var compare = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
            var options = System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
            foreach (string word in query.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (compare.IndexOf(name, word, options) < 0 && compare.IndexOf(tags ?? "", word, options) < 0) return false;
            return true;
        }

        internal void UpdateProfile(string name, string tags, bool favorite, int slot)
        {
            tags = tags.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (tags.Length == 0) Tags.Remove(name);
            else Tags[name] = tags;
            if (favorite || slot >= 0) Favorites.Add(name);
            else Favorites.Remove(name);
            for (int i = 0; i < Shortcuts.Length; i++)
                if (String.Equals(Shortcuts[i], name, StringComparison.OrdinalIgnoreCase)) Shortcuts[i] = null;
            if (slot >= 0 && slot < Shortcuts.Length) Shortcuts[slot] = name;
        }

        internal void RemoveProfile(string name) { UpdateProfile(name, "", false, -1); }

        internal Point PositionFor(string device)
        {
            Point offset;
            return device != null && ScreenPositions.TryGetValue(device, out offset) ? offset : Point.Empty;
        }

        internal static Point BoundPosition(Point offset)
        {
            return new Point(Math.Max(-300, Math.Min(300, offset.X)), Math.Max(-300, Math.Min(300, offset.Y)));
        }

        internal void SetPosition(string device, Point position)
        {
            position = BoundPosition(position);
            if (position.IsEmpty) ScreenPositions.Remove(device);
            else ScreenPositions[device] = position;
        }

        internal void Load(Dictionary<string, string> data)
        {
            string value;
            for (int i = 0; i < Shortcuts.Length; i++)
                if (data.TryGetValue("FavoriteKey" + i, out value) && ConfigManager.IsValidProfileName(value))
                {
                    Shortcuts[i] = value;
                    Favorites.Add(value);
                }

            foreach (var item in data)
            {
                int separator = item.Key.IndexOf('.');
                if (separator < 0) continue;
                string type = item.Key.Substring(0, separator);
                if (type != "ScreenOffset" && type != "ProfileTag" && type != "ProfileStar") continue;
                string name = Decode(item.Key.Substring(separator + 1));
                if (String.IsNullOrWhiteSpace(name)) continue;
                if (type == "ScreenOffset")
                {
                    string[] parts = item.Value.Split(',');
                    int x, y;
                    if (name.Length <= 128 && parts.Length == 2 && Int32.TryParse(parts[0], out x) && Int32.TryParse(parts[1], out y))
                        SetPosition(name, new Point(x, y));
                }
                else if (ConfigManager.IsValidProfileName(name))
                {
                    if (type == "ProfileTag" && item.Value.Length <= 120) Tags[name] = item.Value;
                    if (type == "ProfileStar" && item.Value == "1") Favorites.Add(name);
                }
            }
        }

        internal void Append(StringBuilder text)
        {
            for (int i = 0; i < Shortcuts.Length; i++)
                if (Shortcuts[i] != null) text.AppendLine("FavoriteKey" + i + "=" + Shortcuts[i]);
            foreach (var item in Tags) text.AppendLine("ProfileTag." + Encode(item.Key) + "=" + item.Value);
            foreach (string name in Favorites) text.AppendLine("ProfileStar." + Encode(name) + "=1");
            foreach (var item in ScreenPositions)
                text.AppendLine("ScreenOffset." + Encode(item.Key) + "=" + item.Value.X + "," + item.Value.Y);
        }

        // No padding makes these names safe as INI keys.
        private static string Encode(string name) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(name)).TrimEnd('='); }

        private static string Decode(string name)
        {
            if (name.Length == 0 || name.Length > 512) return null;
            try
            {
                return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(name.PadRight((name.Length + 3) / 4 * 4, '=')));
            }
            catch (FormatException) { return null; }
            catch (DecoderFallbackException) { return null; }
        }
    }

    internal sealed class DesignHistory
    {
        private readonly List<VcrdbSettings> undo = new List<VcrdbSettings>();
        private readonly Stack<VcrdbSettings> redo = new Stack<VcrdbSettings>();
        private VcrdbSettings current;

        internal DesignHistory(VcrdbSettings initial) { current = initial.Clone(); }
        internal bool CanUndo { get { return undo.Count != 0; } }
        internal bool CanRedo { get { return redo.Count != 0; } }

        internal void Record(VcrdbSettings value)
        {
            if (value.ToConfigString() == current.ToConfigString()) return;
            if (undo.Count == 40) undo.RemoveAt(0);
            undo.Add(current);
            current = value.Clone();
            redo.Clear();
        }

        internal VcrdbSettings Undo()
        {
            if (!CanUndo) return null;
            redo.Push(current);
            int index = undo.Count - 1;
            current = undo[index];
            undo.RemoveAt(index);
            return current.Clone();
        }

        internal VcrdbSettings Redo()
        {
            if (!CanRedo) return null;
            undo.Add(current);
            current = redo.Pop();
            return current.Clone();
        }
    }

    internal sealed class HoldToShow : IDisposable
    {
        private readonly System.Windows.Forms.Timer timer;
        private readonly Func<bool> down;
        private readonly Action<bool> show;

        internal HoldToShow(Func<bool> down, Action<bool> show)
        {
            this.down = down;
            this.show = show;
            timer = new System.Windows.Forms.Timer { Interval = 25 };
            timer.Tick += delegate { Poll(); };
        }

        internal bool Tracking { get { return timer.Enabled; } }

        internal void Begin()
        {
            if (Tracking || !down()) return;
            show(true);
            timer.Start();
        }

        internal void Poll()
        {
            if (Tracking && !down()) Stop();
        }

        internal void Stop()
        {
            if (!Tracking) return;
            timer.Stop();
            show(false);
        }

        public void Dispose() { timer.Dispose(); }
    }
}
