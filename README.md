# Task Status Assistant

A lightweight Windows tray app. It shows Caps Lock / Num Lock states and a keyboard-toggled "Fan" state on the tray icon, and can send notifications when states change.

## Install (Portable)

1. Copy everything from `TrayStatusHelper\\bin\\Release\\net8.0-windows\\win-x64\\publish\\` to any folder.
1. Run `TaskStatusAssistant.exe`.
1. (Optional) Right-click the tray icon -> `Start with Windows` to auto-start on login (uses HKCU Run; no admin required).

Note: The app runs in the system tray and does not open a main window. To exit, right-click the tray icon -> `Exit`.

## Usage

- The tray icon has 3 dots:
  - Top: Caps (green = on, red = off)
  - Middle: Num (green = on, red = off)
  - Bottom: Fan (green = on, red = off)
- Right-click menu:
  - `Show Status`: Minimal status panel
  - `Notifications`: Warn when Caps turns on, and when Num turns off
  - `Fan Toggle (Fn+1)`: Toggle the fan state manually
  - `Fan Hotkey (Fn+1)`:
    - `Set / Learn`: Learn the key that toggles Fan (press Fn+1; captured key will be saved)
    - `Clear`: Reset the saved hotkey
  - `Start with Windows`: Auto-start on login
  - `Exit`: Close the app

### About Fan (Fn+1)

This project does not read the real hardware fan status. The Fan dot represents the state you toggle via a key (Fn+1 or whichever key you learned).

On some laptops, Fn combinations do not generate a Windows key event (they are handled by the OEM firmware/utility). In that case, `Set / Learn` may not detect Fn+1. For real fan telemetry, a vendor API/WMI integration is required.

## Build From Source

Requirements:
- Windows 10/11
- .NET SDK (project targets `net8.0-windows`) or Visual Studio 2022

Build:
```powershell
cd .\\TrayStatusHelper
dotnet build -c Release
```

Run:
```powershell
.\bin\Release\net8.0-windows\TaskStatusAssistant.exe
```

## Publish (Self-Contained)

Publish for Windows (works even if .NET is not installed on the target machine):
```powershell
cd .\\TrayStatusHelper
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output:
- `TrayStatusHelper\\bin\\Release\\net8.0-windows\\win-x64\\publish\\TaskStatusAssistant.exe`
- Note: Windows Desktop publishes may include extra side files (.dll). Distribute the whole `publish` folder.

## Where Are Settings Stored?

- App settings (HKCU):
  - `HKCU\\Software\\TrayStatusHelper`
  - `NotificationsEnabled` (DWORD 0/1)
  - `FanSimEnabled` (DWORD 0/1)
  - `FanHotkeyVKey` (DWORD)
- Auto-start entry (HKCU Run):
  - `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`
  - `Task Status Assistant` (string: exe path)
  - Legacy value name (older builds): `TrayStatusHelper` (the app migrates this automatically)

## Uninstall

1. Right-click the tray icon -> disable `Start with Windows`.
1. Right-click the tray icon -> `Exit`.
1. Delete the folder where you placed the app.
1. (Optional) Remove settings: `HKCU\\Software\\TrayStatusHelper`.
