# VPNDriveMapper

VPNDriveMapper is a Windows tray utility that maps configured network drives when a VPN is reachable, and disconnects those mappings when the VPN is no longer reachable.

## Features

- Tray-only application with no main window.
- VPN detection through a configured reachable IP address.
- Automatic network drive mapping and disconnection.
- Tray menu for VPN detection settings, drive mappings, autostart, and exit.
- Dynamic tray icon that reflects VPN and mapping status.

## Requirements

- Windows
- .NET Framework 4.8
- Visual Studio or Build Tools for Visual Studio

## Build

```powershell
dotnet build VPNDriveMapper.csproj -c Release
```

The release build output is generated under:

```text
bin\Release
```

## Configuration

Configuration is stored under the current user's application data folder:

```text
%AppData%\VPNDriveMapper\config.xml
```

The repository does not include personal VPN IP addresses, drive letters, or network share paths. Configure those values locally from the tray menu after launching the app.
