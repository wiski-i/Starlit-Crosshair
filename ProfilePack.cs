using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NativeVcrdbCrosshair
{
    internal sealed class ProfilePack
    {
        private const string Header = "VCRDB-PACK-1";
        private const int MaxBytes = 1024 * 1024;
        private const int MaxProfiles = 500;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Dictionary<string, VcrdbSettings> profiles;

        private ProfilePack(Dictionary<string, VcrdbSettings> profiles)
        {
            this.profiles = profiles;
        }

        internal int Count { get { return profiles.Count; } }
        internal IEnumerable<KeyValuePair<string, VcrdbSettings>> Entries { get { return profiles; } }

        internal static int Export(string path)
        {
            var text = new StringBuilder().AppendLine(Header);
            int exported = 0;
            foreach (string name in ConfigManager.ListProfiles())
            {
                if (!ConfigManager.IsValidProfileName(name))
                    throw new IOException("A profile has an unsupported name.");
                var profile = ConfigManager.LoadProfile(name);
                if (profile == null) throw new IOException("A profile could not be read: " + name);
                if (++exported > MaxProfiles) throw new IOException("Too many profiles for one pack.");
                text.Append(Convert.ToBase64String(Utf8.GetBytes(name))).Append('\t').AppendLine(profile.ToShareCode());
            }
            if (exported == 0) return 0;
            ConfigManager.WriteAtomically(path, text.ToString());
            return exported;
        }

        internal static ProfilePack Read(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > MaxBytes) throw new FormatException("Profile pack is too large.");
                using (var reader = new StreamReader(stream, Utf8, false))
                {
                    if (reader.ReadLine() != Header) throw new FormatException("Invalid profile pack header.");
                    var entries = new Dictionary<string, VcrdbSettings>(StringComparer.OrdinalIgnoreCase);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (entries.Count >= MaxProfiles || parts.Length != 2 || parts[0].Length > 256)
                            throw new FormatException("Invalid profile pack entry.");
                        string name = Utf8.GetString(Convert.FromBase64String(parts[0]));
                        VcrdbSettings profile;
                        if (!ConfigManager.IsValidProfileName(name) || name != name.Trim() || entries.ContainsKey(name) ||
                            !VcrdbSettings.TryFromShareCode(parts[1], out profile))
                            throw new FormatException("Invalid or duplicate profile.");
                        entries.Add(name, profile);
                    }
                    if (entries.Count == 0) throw new FormatException("The profile pack is empty.");
                    return new ProfilePack(entries);
                }
            }
        }

        internal ImportResult Import(IEnumerable<string> selection = null)
        {
            var result = new ImportResult();
            var selected = selection == null ? null : new HashSet<string>(selection, StringComparer.OrdinalIgnoreCase);
            foreach (var item in profiles)
            {
                if (selected != null && !selected.Contains(item.Key)) continue;
                try
                {
                    if (ConfigManager.TryAddProfile(item.Key, item.Value)) result.Imported++;
                    else result.Skipped++;
                }
                catch (IOException) { result.Failed++; }
                catch (UnauthorizedAccessException) { result.Failed++; }
            }
            return result;
        }

        internal struct ImportResult
        {
            internal int Imported, Skipped, Failed;
        }
    }
}
