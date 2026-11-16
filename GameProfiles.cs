using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace NativeVcrdbCrosshair
{
    internal sealed class GameProfileWatcher : IDisposable
    {
        private const uint ForegroundEvent = 3;
        private const uint OutOfContext = 0;
        private delegate void WinEventCallback(IntPtr hook, uint type, IntPtr window, int obj, int child, uint thread, uint time);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module,
            WinEventCallback callback, uint process, uint thread, uint flags);

        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();

        private readonly AppConfig config;
        private readonly Action<VcrdbSettings, string> apply;
        private readonly WinEventCallback callback;
        private readonly uint ownProcess = GetCurrentProcessId();
        private IntPtr hook;
        private string appliedProfile;
        private uint foregroundProcess;

        internal GameProfileWatcher(AppConfig config, Action<VcrdbSettings, string> apply)
        {
            this.config = config;
            this.apply = apply;
            callback = OnForeground;
        }

        internal bool RefreshRules()
        {
            if (config.GameProfiles.Count == 0)
            {
                Dispose();
                ApplyProfile(null, false);
                return true;
            }
            if (hook == IntPtr.Zero)
                hook = SetWinEventHook(ForegroundEvent, ForegroundEvent, IntPtr.Zero, callback, 0, 0, OutOfContext);
            if (hook == IntPtr.Zero) return false;
            UpdateForeground(true);
            return true;
        }

        private void OnForeground(IntPtr source, uint type, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            if (hook != IntPtr.Zero && source == hook) UpdateForeground(false);
        }

        private void UpdateForeground(bool refresh)
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            string executable = pid == 0 || pid == ownProcess ? null : GetProcessName(pid);
            bool processChanged = foregroundProcess != pid;
            foregroundProcess = pid;
            ApplyProfile(config.ProfileForGame(executable), refresh || processChanged);
        }

        internal void ApplyProfile(string profile, bool refresh)
        {
            if (!refresh && String.Equals(appliedProfile, profile, StringComparison.OrdinalIgnoreCase)) return;
            VcrdbSettings settings = profile == null ? null : ConfigManager.LoadProfile(profile);
            if (settings == null && appliedProfile == null) return;
            appliedProfile = settings == null ? null : profile;
            apply(settings, appliedProfile);
        }

        private static string GetProcessName(uint id)
        {
            try
            {
                using (var process = Process.GetProcessById((int)id))
                    return process.ProcessName + ".exe";
            }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (Win32Exception) { return null; }
        }

        internal static bool IsExecutableName(string name)
        {
            return !String.IsNullOrWhiteSpace(name) && name.Length <= 220 && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && name.Length > 4 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && name.IndexOf('=') < 0;
        }

        public void Dispose()
        {
            if (hook == IntPtr.Zero) return;
            UnhookWinEvent(hook);
            hook = IntPtr.Zero;
        }
    }
}
