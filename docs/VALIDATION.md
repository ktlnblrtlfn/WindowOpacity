# Validation record

Validated on 7 October 2026 on this Windows 11 x64 workstation using .NET SDK 8.0.423. All tests below were executed; coverage marked untested has not been inferred from compilation.

## Automated integration coverage

The dependency-free `WindowOpacity.Tests` executable creates its own independent Win32/Windows Forms and WPF processes. It tests actual HWND styles, alpha, physical bounds, WinEvent delivery, WPF popup binding, and cleanup. A separate production-process test uses UI Automation to invoke the caption button, move the slider, and reset, then sends real keyboard input to the registered emergency shortcut and a real outside mouse click. It closes its blank target windows and requests normal utility shutdown.

Verified:

- Caption rectangle retrieval; equal-sized overlay directly left of Minimize.
- Foreground-only placement and keyboard focus retained by target.
- Physical alignment through movement, resizing, maximizing, minimizing, and restoring.
- Real-time slider values and native alpha 153 at 60%.
- Exact baseline style after Reset, disable, emergency reset, and normal production-process exit.
- Existing layered alpha 190 remains untouched; such windows are rejected.
- Opacity below 20% clamps to 20%.
- Popup stays within the monitor work area and closes on a real outside click.
- Ctrl + Alt + Shift + O works through actual RegisterHotKey/keyboard event delivery.
- Destroyed targets detach safely; the overlay can immediately follow another eligible foreground window.
- The tested WPF renderer strips external WS_EX_LAYERED. The service detects rejection and restores baseline state rather than treating it as a successful opacity change.
- Actual installed Notepad: native caption location, 60% alpha, reset, production caption click/popup, focus, outside click, emergency shortcut, movement, maximized placement, and normal-exit restoration.
- 45 native constants verified against official Microsoft SDK headers by the included script.

Final verification completed 73 assertions across two successful invocations: 37 fixture assertions and 36 production/Notepad assertions against the published self-contained EXE. UI Automation supplies deterministic input; manual perceptual assessment of animation smoothness is still needed. Popup opening is awaited for up to two seconds in the cold-start production test.

## Application compatibility matrix

| Application | Actual result in this run |
| --- | --- |
| Installed Notepad | Native coordinates and production opacity/reset/cleanup flow passed |
| Classic Win32 fixture | Caption, alpha, movement, resize, maximize/minimize/restore, lifecycle and reset passed |
| WPF fixture | Caption detected; renderer rejected external layering; original state preserved |
| Running ChatGPT desktop application | Native caption buttons marked invisible; UI Automation measured Minimise/Maximise/Close and live overlay matched exactly. Target process stayed running and extended style stayed unchanged. Opacity was not changed during its ongoing task. |
| File Explorer | Not tested |
| Calculator | Not tested |
| Brave | Measured accessible caption triplet and exact live foreground overlay placement verified; browser stayed running and extended style remained 0x200100. Opacity was not changed. |
| Edge, Chrome | Not tested |
| Visual Studio Code | Not tested |
| Windows Terminal | Not tested |
| Task Manager | Not tested |
| Windows Settings | Not tested |
| Paint | Not tested |

## Additional coverage still required

Windows 10, 100/125/150/175/200% scaling on a controlled matrix, mixed-DPI monitor transitions, actual shell snap left/right, rapid human Alt+Tab, true fullscreen applications, elevated/protected processes, theme changes, user startup/logon behavior, exclusions and remembered-opacity persistence across real logins, and extended idle performance/leak measurements. Current tests exercise the workstation's existing DPI and monitor work area only. No claim of universal custom-titlebar support or near-zero measured idle CPU is made.

Reproduce from the solution root:

```powershell
dotnet run --project WindowOpacity.Tests -c Release -- classic wpf smoke notepad
./scripts/Verify-NativeConstants.ps1
```

The Notepad test skips if any Notepad process is already running. Do not switch foreground applications while interactive tests run. To smoke-test the standalone distribution, set `WINDOWOPACITY_TEST_EXE` in the test process environment to its absolute EXE path, then run the `smoke` or `notepad` scenarios.

## ChatGPT caption adapter

Brave placement refinement: the live Tab search dropdown occupied `(3443,471)-(3479,507)`, overlapping the former transparency footprint. The adapter now measures that dropdown in the caption row and reserves it. The updated live overlay matched `(3383,471)-(3440,508)`, leaving a three-physical-pixel gap before Tab search at 120 DPI. Brave remained running and its extended style remained `0x200100`; no opacity change or dropdown invocation was performed. Both English and Turkish Tab search labels are recognized, and size/DPI changes invalidate the measured placement snapshot.

The observed main window was `Chrome_WidgetWin_1`, with invisible native caption rectangles and an empty DWM caption cluster. Read-only accessibility inspection found the British-English labels `Minimise`, `Maximise`, and `Close`. At 120 DPI the measured minimize rectangle was `(1904,1143)-(1961,1188)`. After updating only WindowOpacity, its live foreground overlay was `(1847,1143)-(1904,1188)`, exactly adjacent to Minimise. ChatGPT's PID remained 6432 and its extended style remained `0x240100` throughout. No target alpha/style mutation, window closure, restart, or keyboard/mouse input was performed during this fix.

Queries run on a background MTA worker, with at most one pending query; they cannot block the hook/UI dispatcher. A verified snapshot follows pure window translations. Size or DPI changes require new measured geometry. Failed queries retry after 30 seconds and the overlay stays hidden rather than using guessed bounds. Window destruction invalidates snapshots. This adapter reads only matching caption-button metadata and never invokes target controls. See Microsoft's [UI Automation threading guidance](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-threading-issues).

## Update on 8 October 2026

- WhatsApp and WhatsApp Beta: measured caption triplets and actual overlay placement passed; both kept their 70% opacity.
- 14 isolated identity/persistence assertions passed: package updates retain identity, publishers and WhatsApp channels remain separate, migration persists, and existing stable settings take precedence over old version paths.
- 9 isolated recovery assertions passed: marked previous-instance layering is recoverable, alpha remains controllable, baseline styles restore, and unmarked layered renderers remain untouched.
- Redesigned popup: the styled thumb updates the slider through WPF drag events; the percentage sits above the track.
- Text and icon verification at the workstation's native 120 DPI: text has no effect ancestor, the caption/popup share one vector symbol, all four invisible caption corners accept native hits, and the target keeps foreground focus.
- Background shadow renders independently of text; text uses Display formatting and layout rounding. The translucent panel does not implement desktop backdrop blur.

These checks supplement the earlier integration run. The full hardware/application matrix is still untested.

Release privacy verification: Release builds disable debug symbols and CodeView/PDB paths. The rebuilt self-contained EXE was inspected after decompressing all 448 bundle entries: no local username, Glass-Windows build path, settings.json, logs, or PDB files. Identity (14) and recovery (9) checks passed after the change.
