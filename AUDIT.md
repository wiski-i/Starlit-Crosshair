# Local audit

The input folder contained Program.cs, Build.bat, app.manifest, and an existing executable. The source built before changes. The old executable was not run.

| Finding | Resolution |
| --- | --- |
| Unrelated Base64 decoded as a default crosshair; invalid opacity in a local settings file could throw on paint. | Share codes must contain the expected fields and valid values. Local values are clamped before rendering. |
| Magenta disappeared because it doubled as the transparency key. Partial opacity was drawn against a fixed color. | Use a small 32-bit layered Windows surface with per-pixel alpha, then apply the selected opacity once. |
| A full-screen layered window repeatedly forced itself to the top and kept pulsing when hidden. | Keep a click-through, non-activating topmost window around the reticle. Update on edits or display changes; animate only while visible. |
| The overlay was pinned to the primary screen; the help page recommended elevation for fullscreen compatibility. | Add monitor selection and a primary-display fallback. Explain the exclusive fullscreen limitation and remove the elevation shortcut. |
| Hotkey errors were silent, and a rejected shortcut replaced the stored working setting. | Keep the previous shortcut on failure and show the error by the shortcut field. Persist only successful changes. |
| Profile name sanitization could collide with another file. Saving could silently fail or overwrite. | Validate names, confirm replacements, save atomically, and report failures. |
| Moving between pages could leave a transition timer running. Duplicated scale sliders could disagree, and custom controls were mostly mouse-only. | Stop the animation at its target, synchronize scale both ways, and add keyboard and focus support to editor controls. |
| Gallery rebuilds left undisposed controls. Startup state could disagree with Windows. | Dispose replaced controls and menus. Read startup state from the current user's registry entry. |

The solution follows Microsoft's [layered-window update](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow) and [alpha blending](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-blendfunction) APIs.

## Background rendering and title bar

Rounded controls inherit the actual title-bar paint, including its gradient. The Windows overlay is shown without activation using its native window handle. With the editor hidden or minimized, the default policy freezes pulse, stops preview animation, and releases drawing buffers while preserving the last composited static frame. Hotkey toggles reuse the tray menu.

A same-machine tray test with pulse enabled measured 149 layer timer ticks and 31.3 ms of process CPU over 7.00 seconds on the prior build. The polished build recorded zero layer ticks and 0.0 ms of process CPU at the timer measurement resolution over 7.01 seconds. The overlay remained visible in both runs. This sample measures the app process and excludes compositor CPU and GPU work.

## Verification

The regression suite covers code validation, local config recovery, settings and profile round trips, reflection symmetry on odd and even pixel dimensions, actual Windows compositing with magenta and 50% opacity, click-through, focus retention, layer visibility, scale synchronization, and tray reopen. Editor, profile gallery, settings, and compact layout were reviewed from native WinForms renders.

The protected tool session returned Win32 error 5 and verified the blocked registration path. A separate run in a normal interactive session passed live shortcut collision and restored the previous registered key. Physical monitor reconnection, mixed DPI and game-specific compatibility also require manual checks. A license file was not supplied with the original source.


## Profile and preview expansion

Legacy share codes still load. The new regression covers asymmetric arms, hollow ring and diamond geometry in rendered pixels, profile pack round trips and conflicting profile names, and the visibility test timer stopping when hidden. Game binding uses Windows [foreground notifications](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook), subscribed only when a binding exists. Automatic selection never rewrites the saved editor design.
The fixture verified matching the current Windows foreground process and restoring the editor design. Windows denied bringing the separate test process to the foreground during this run, so a live game-to-desktop focus switch remains for manual acceptance.


## Profile workflow

The Windows regression verifies library metadata and shortcut persistence, a registered favorite message loading the matching design, undo and redo updating editor controls, a selected package import preserving local names and skipping unchecked entries, two-color outline pixels, and independent display correction. A separate native Windows test registered an uncommon shortcut, sent its key-down and key-up events, confirmed the overlay visibility transitions, and checked that the release timer stopped after release. Calibration was verified with native dialog nudge controls and simulated display coordinates. A second physical monitor and an in-game focus switch remain manual checks.

The gallery loads on first use and keeps loaded designs in memory for searching. Hold mode polls only the configured chord while it is held. Its idle state has no recurring input work.
