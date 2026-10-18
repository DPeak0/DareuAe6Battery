# DAREU AE6 V2 Battery Tray

A small Windows 11 system-tray monitor for the DAREU AE6 V2 connected through its 2.4G receiver.

The application is designed for low background use:

- one read at startup, then one read every five minutes;
- no busy loop and no persistent HID handle;
- device enumeration happens only when a read is due or the user chooses Refresh;
- the tray icon is redrawn only after a successful level change;
- only the battery feature-report read path is exposed;
- no DPI, lighting, pairing, profile, or firmware writes.

## Current hardware contract

The verified receiver is:

- VID `0x260D`
- PID `0x1261`
- HID usage page `0xFF02`
- HID usage `0x0002`
- feature report length `64`

The protocol notes are in [docs/protocol.md](docs/protocol.md).

## Build prerequisite

This repository targets .NET 10. The current development machine has the .NET 10 runtime but no SDK. Install the .NET 10 SDK before building, or use the Windows CI workflow.

```powershell
dotnet restore .\DareuAe6Battery.sln
dotnet build .\DareuAe6Battery.sln -c Release
dotnet test .\DareuAe6Battery.sln -c Release
```

## Publish

```powershell
dotnet publish .\src\DareuAe6.Tray\DareuAe6.Tray.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:PublishTrimmed=false
```

Keep the published file in a permanent folder. A future installer can add an HKCU startup entry without administrator rights.

## Usage

Start `DareuAe6.Tray.exe`. The program has no main window; it appears in the notification area. Right-click the icon for Refresh or Exit. If the receiver is unplugged or the mouse is unavailable, the icon reports that state and the next scheduled read will retry.

Only one instance is allowed through a named local mutex.

## Project layout

- `src/DareuAe6.Core`: protocol parser and low-frequency poll coordinator.
- `src/DareuAe6.Windows`: Windows SetupAPI/HID read transport.
- `src/DareuAe6.Tray`: hidden WinForms tray application.
- `tests/DareuAe6.Core.Tests`: protocol tests.
