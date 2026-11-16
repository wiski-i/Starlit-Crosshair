# Crosshair Studio

A native Windows crosshair editor and desktop overlay. Design a reticle in the live optical preview, then use it in windowed or borderless games.

## Build and run

On Windows 10 or 11 with .NET Framework 4.x, run:

```bat
Build.bat
```

Open the resulting `OzelCrosshair.exe`. The build uses the C# compiler bundled with Windows .NET Framework. There are no packages to install.

## Use

- **Editor:** change color, opacity, outline, center marker, and line geometry. The **Şekil** tab adds a square, ring, or diamond marker, rounded ends, and separate lengths for all four arms. Use 0% to hide an arm. The preview shows the same drawing at 1×, 2×, or 3× on optical, checkerboard, and light backgrounds. **Görünürlük Testi** adds simulated bright sky, dim room, and moving backgrounds.
- **Profiles:** search by name or tags, filter favorites, and open a custom card's context menu to copy, edit metadata, or delete it. **Etiket ve Kısayol** assigns saved favorites to Ctrl+Alt+1, 2, or 3. While a game profile is active, a favorite shortcut overrides it until a different foreground process takes over.
- **Contrast and comparison:** choose a white, gold, or aqua contrast preset with black and white outlines. Set **Çift renk dış hat** on other designs in the Shape tab. Use **Yan Yana Kıyasla** to select two designs with a shared background and zoom.
- **Design history:** use **Geri Al** and **Yinele** or Ctrl+Z and Ctrl+Y. Nearby continuous adjustments are grouped. History is kept for the current session. **Tasarımı Kopyala** saves the current design under a new name.
- **Oyuna Özel:** in the profile gallery, bind a saved design to the game's actual executable name. When that process has the foreground window, the overlay uses its profile. Otherwise the editor design is restored. For games launched by another app, select the game executable rather than the launcher.
- **Profile packs:** use **Paket İçe Aktar** and **Paketi Dışa Aktar** in the profile gallery. A .vcpak file contains custom reticle profiles. Preview reticles in the import dialog and check only the entries you want. Imports keep existing profiles with the same name. App settings, shortcuts, and game bindings are stored separately.
- **Display and calibration:** select an output monitor. If it disconnects, the overlay falls back to the primary display. Under **Konum → Piksel Kalibrasyonu**, use the arrow keys for 1 px or Shift+arrows for 10 px. Apply saves the correction for the resolved monitor. The correction adds to the active profile offset.
- **Settings:** change the glass and accent, copy or import a VCRDB share code, and set the overlay shortcut. The default is Ctrl+Alt+C. Use the top status button to toggle the overlay. Enable **Basılı tutarak göster** to show it only while the shortcut is held.
- **Hide to tray:** keep the overlay running while you play. The overlay stays static in the background by default. Turn on **Nabzı sürdür** in Settings if you want an animated overlay in the background. Double-click the notification icon to reopen the editor.

Settings and profile files stay in `%APPDATA%\VCRDBCrosshair`. The overlay uses a click-through Windows desktop window. Exclusive fullscreen can hide desktop overlays; use borderless or windowed mode if available. This project does not inject code into games or require administrator access.

## Verify

```bat
tests\Run.bat
```

Run the suite on an interactive Windows desktop. It builds from source and uses a temporary data directory. It briefly shows native test windows and uses uncommon test shortcuts to check registration conflicts. See [the audit](AUDIT.md) for the verification scope and remaining checks.
