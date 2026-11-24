[English](README.md) · [Türkçe](README.tr.md)

# Starlit Crosshair

A native Windows crosshair editor with a click-through desktop overlay. Build a reticle in a live preview, save designs, and use them in windowed or borderless games. The application window is titled **Crosshair Studio**; the file you run is **OzelCrosshair.exe**.

## Download and run

1. Open the [latest release](https://github.com/wiski-i/Starlit-Crosshair/releases/latest).
2. Download `Starlit-Crosshair-<version>-windows.zip` and extract the entire ZIP to a folder you can write to.
3. Open `OzelCrosshair.exe`. No installer or administrator access is required.

Requires **Windows 10 or Windows 11** and **.NET Framework 4.8 or later** (included in Windows 11 and recent Windows 10 installations). A modern .NET SDK alone does not replace .NET Framework. The program is not digitally signed; Windows may show a publisher warning. Verify the download against the release's `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Starlit-Crosshair-1.0.0-windows.zip -Algorithm SHA256
```

Compare the hash with the line for that exact filename. The portable ZIP contains the executable, both READMEs, license and changelog. Your settings are stored separately in your Windows account.

## Design a crosshair

- Change color, opacity, outline, center marker, line lengths, thickness, gap, scale and position. The Shape tab offers square, ring and diamond markers, rounded ends and independent arm lengths; 0% hides an arm.
- The live preview uses the same renderer as the overlay. Compare at 1×, 2× or 3× over light, checkerboard and optical backgrounds. Visibility Test adds simulated sky, dim-room and moving backgrounds.
- Contrast presets and a black/white double outline help a design stand out. Use side-by-side comparison to inspect two profiles at the same zoom and background.
- Undo and redo with **Ctrl+Z / Ctrl+Y** or the toolbar. Nearby continuous adjustments are grouped; history lasts for the current session. Share-code output follows imports, reset, profile selection, undo and redo.

## Profiles and game bindings

Save the current design with a descriptive name. Search custom profiles by name or tags, filter favorites, and use a card's context menu to copy, edit metadata or delete a profile. Assign favorites to **Ctrl+Alt+1 / 2 / 3** through Tags and Shortcut. A favorite overrides a game's automatic design until a different foreground process takes over.

In **Game Profiles**, bind a saved profile to the game's **actual `.exe` filename**, such as `example-game.exe`. Choose the game process, not its launcher. While that process is in the foreground, the overlay uses its bound design; otherwise it restores your editor design. This does not replace the design saved in the editor.

## Monitors and calibration

Select the output monitor on the display/layer page. If it disconnects, the overlay falls back to the primary monitor. Under **Position → Pixel Calibration**, nudge by 1 px with the arrow keys or 10 px with Shift+arrows. Apply stores a correction for that monitor, added to the current profile's position offset. Physical monitor reconnection and mixed-DPI configurations still need hardware-specific checks.

## Share and back up

- Copy or import a **VCRDB share code** on the Settings page to transfer one reticle design.
- Import or export **`.vcpak` profile packs** from the profile gallery. Preview and select entries before importing. Existing profiles with the same name are kept.
- Packs carry **custom designs only**. They do not carry application settings, favorite assignments, tags, monitor calibration or game bindings. A share code also carries a design only.
- For a **full backup**, exit the application from its tray menu and copy the entire `%APPDATA%\VCRDBCrosshair` folder, including `config.ini` and `profiles`. To restore, keep the app closed and restore that folder. Back up an existing folder before replacing it.

## Shortcuts, tray and language

| Action | Default |
| --- | --- |
| Toggle the overlay | Ctrl+Alt+C (configurable) |
| Load an assigned favorite | Ctrl+Alt+1, Ctrl+Alt+2, Ctrl+Alt+3 |
| Undo / redo a design edit | Ctrl+Z / Ctrl+Y in the editor |

Use the top status control to toggle visibility. **Hold to show** displays the overlay only while your configured chord is held. **Hide to tray** keeps it running while you play; double-click the notification icon to reopen the editor. The overlay stays static when the editor is hidden or minimized by default; enable background pulse in Settings to keep its pulse animation running.

Change the accent, glass material or language in Settings. **Language changes apply after you exit and reopen the application.** A second launch shows a warning using your saved language and theme; use the existing tray icon to reopen the first instance.

## Build, test and package

From a Windows checkout with the .NET Framework C# compiler available:

```bat
Build.bat
tests\Run.bat test-results
```

The scripts use the Windows .NET Framework compiler, enforce **C# 5**, and treat compiler warnings as errors. There are no NuGet packages to restore. `Build.bat` produces `OzelCrosshair.exe` in the repository root.

Run the complete regression suite on an **interactive Windows desktop dedicated to testing**. It briefly shows native windows, tests global shortcut registration, activates a fixture window, and samples the real desktop compositor. Screenshots are written to the optional directory argument (`test-results` above). Tests use their own unique temporary data folder and remove it afterward. If Windows refuses foreground activation or hotkeys, the suite prints a `SKIP`; read those messages before interpreting a passing exit code.

On a non-active test desktop, `tests\Run.bat --offscreen test-results` runs the remaining checks and explicitly skips compositor capture. It is **not equivalent to the full desktop suite**. `tests\Run.bat --ui-regression` runs just the two release bug regressions.

To build, run the complete tests, create a portable ZIP and calculate its SHA-256 locally:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Prepare-Release.ps1
```

Files are written to `dist/`. The script stops on build/test errors, removes its own temporary packaging folder, and never pushes or publishes anything. [Windows CI](.github/workflows/windows.yml) runs the same process on pushes, pull requests and manual dispatch. Successful `v*` tag builds verify the tag against the EXE product version and publish the ZIP and checksum. An existing release keeps its description when its assets are refreshed. See [CHANGELOG.md](CHANGELOG.md) and [RELEASE_NOTES.md](RELEASE_NOTES.md).

## Troubleshooting and limits

- **Overlay missing:** enable it, check the selected monitor, and try windowed or borderless mode. Exclusive fullscreen can hide desktop overlays.
- **Shortcut rejected:** another program may own it. Choose another chord; a failed registration keeps the previous working setting.
- **Game profile not switching:** check the actual foreground game `.exe` name and confirm the saved profile still exists.
- **App already open:** reopen it from the Windows notification area, including hidden tray icons.
- **Compiler missing / app will not start:** enable or repair .NET Framework 4.8 or later. Build/test scripts report a missing compiler and return a nonzero exit code.
- **Language unchanged:** exit from the tray and restart; hiding the editor does not restart the application.

The overlay uses a Windows desktop window and **does not inject code into games**. Compatibility with every game or anti-cheat system is not guaranteed. Check the rules for your game. Automated Windows regression checks do not establish live-game, anti-cheat, physical multi-monitor or mixed-DPI compatibility. [AUDIT.md](AUDIT.md) records older checks with their original scope; it is not a certification of this release.

## License

[GNU General Public License v3.0](LICENSE).
