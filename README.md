# Shiri · Desktop Calendar

A local Windows desktop calendar, inspired by DesktopCal's desktop-pinning approach, implemented independently. No login, no VIP tier, no network requests.

[简体中文](README.zh-CN.md)

## Running

Double-click `dist\DesktopTodo.exe`. To pin it to your desktop, right-click the file and choose **Send to → Desktop (create shortcut)**. The published build ships with the .NET runtime — no .NET, Python, or Node installation required.

The calendar sits on the Windows desktop icon layer, above desktop icons but below other windows. Clicking **Show Desktop** in the taskbar keeps the calendar visible. The main calendar, the edit dialog, and the settings window all run as tool windows without taskbar buttons. Double-clicking the tray icon or choosing **Show Calendar** brings the calendar to the foreground; clicking **Back to Desktop** in the calendar returns it to the desktop layer.

## Usage

- **Add**: Double-click a date cell, or click **+ New** / **+ Add Todo**. You can set date, time, notes, a category colour, and a reminder.
- **View**: Click a date to see that day's tasks in the right-hand panel. Switch between month, week, and list views at the top. The list view supports cross-date search on titles and notes.
- **Complete**: Tick the checkbox in the right panel, or right-click a task in the calendar.
- **Edit**: Click a task title in the right panel, or double-click a task in the calendar.
- **Reschedule**: Drag a task from one calendar cell to another, or change the date in the edit dialog.
- **Delete**: Right-click a task or click the **⋯** button on its right side. Accidentally deleted items can be restored with **Undo** (bottom-right).
- **Position & Size**: Drag the "Shiri" title bar to move the window; drag the right edge, bottom edge, or the bottom-right corner to resize. Minimum size is 560 × 420; below 820 px wide the right-hand detail panel is hidden automatically. Placement can be locked in Settings.
- **Appearance**: Switch between light and dark themes and adjust opacity in Settings.
- **Dates**: Use the arrows to change month/week; **Today** jumps back to the current date; click the year-month label to jump to a specific date.
- **Shortcuts**: When the calendar has focus: `Ctrl+N` to create, `Ctrl+Z` to undo; in the edit dialog, `Ctrl+Enter` saves.
- **Exit**: Right-click the tray icon → **Quit Shiri**, or use **Quit Shiri** in Settings.

Start-on-boot is off by default and can be enabled in Settings. After moving the calendar's position, disable and re-enable the shortcut to refresh it.

## Data & Backup

Data folder: `%LOCALAPPDATA%\ShiriCalendar` (typically `C:\Users\<you>\AppData\Local\ShiriCalendar`).

- `calendar.json` — tasks and preferences.
- `calendar.json.bak` — the last successfully saved copy.
- **Settings → Export Backup** saves an extra JSON file; **Import Backup** merges new tasks by ID without overwriting existing ones.
- **Undo** covers task changes made during the current run; undo history is not persisted across restarts.
- If the active file is corrupt and the backup is valid, the corrupted file is preserved and the backup is loaded. Without a valid backup the app stops rather than overwriting data with an empty calendar.

Existing DesktopCal data is not automatically read or migrated. The import feature accepts Shiri's own backup format.

## Scope

Month / week / list views, lunar calendar, category colours, task completion, notes, scheduled reminders, and local backup are included. Account system, cloud sync, recurring tasks, statutory-holiday tables, and two-way external-calendar sync are **not** included.

Date range: 1901 – 2100. The lunar calendar follows the system's calendar-library support range.

Reminders require the app to be running and the machine to be on. Actual notification display is subject to Windows notification settings. Missed reminders are surfaced the next time the app starts. The app does not wake the computer on a schedule.

The app pins a tool window (no taskbar button) above the Explorer desktop window and periodically repairs z-order to avoid intermittent transparency glitches with WPF embedded in Explorer. The window also recovers automatically if Explorer restarts or the window is minimised by mistake. Hot-plugging multiple monitors still needs to be verified on the target hardware.

## Development

Requires the .NET 10 SDK, targeting Windows x64. WPF for the UI; WinForms only for tray and monitor APIs. No third-party runtime dependencies.

```powershell
dotnet build -c Release
.\bin\Release\net10.0-windows\DesktopTodo.exe --self-test --report test-results.txt
.\bin\Release\net10.0-windows\DesktopTodo.exe --window-contract-self-test --report window-contract-test-results.txt
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

The icon source-generation script `scripts\make-icon.py` requires Pillow; the generated icon is already included, so the script is not needed for a normal build.

Dev-only test options: `--data-dir <folder>` to use an isolated data directory, `--windowed` to launch as a regular window, `--diagnostics` to dump window-ownership checks, `--capture <PNG path>` to export the current screen.

## License

[MIT](LICENSE)
