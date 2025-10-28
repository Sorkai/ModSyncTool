# ModSyncTool

[中文文档](./README.md)

A lightweight mod sync and launcher tool built with .NET 8 WPF. It fetches update info from a remote manifest, performs verification, downloading, cleanup, and launching with one click. The UI follows Windows light/dark theme and system accent color, and provides handy tools like local file management and a manifest generator. Can synchronize mods for various games.

## Features

- One-click "Update and Launch": download changed/missing files by manifest, remove obsolete files, then launch the target app
- Follow system theme: light/dark and accent color out of the box, hot-switch without restart
- Modern, cohesive UI: primary/secondary buttons, rounded controls, progress and notices
- Local files management: browse local tree, add to ignore list via context menu
- Settings: launch executable, remote config URL, download concurrency/multi-thread, SSL ignore
- Publisher (Manifest generator): generate an online manifest from a selected folder for distribution and updates
- Logging: Serilog outputs to `logs/modsync.log` for diagnostics

> Platform: Windows (WPF, `net8.0-windows`).

## Screenshots

![](/docs/232237.webp)

## Quick Start

1) After downloading the distribution and extracting it, place the extracted files in the root directory of the application or game. Then run `ModSyncTool.exe` directly.
2) On first launch, you'll see a welcome dialog:
   - Choose "Enter link to start" and paste the remote config URL (typically an online `online_manifest.json`).
   - Or skip this once and configure the URL later in Settings.
3) Click "Update and Launch" on the main window to sync and start your app.

## Remote Config & Settings

The app persists settings locally (e.g., `local_config.json`, produced by the Settings UI). Common fields example:

```json
{
  "LaunchExecutable": "C:/Games/MyGame/Game.exe",
  "UpdateUrl": "https://example.com/online_manifest.json",
  "IsCustomDownloadSettings": true,
  "EnableMultiFileDownload": true,
  "MaxConcurrentFiles": 10,
  "EnableMultiThreadDownload": true,
  "ThreadsPerFile": 8,
  "IgnoreSslErrors": false
}
```

Notes:
- If `UpdateUrl` is empty, the app is considered "not configured" and will guide you to fill it.
- Concurrency and multi-thread can be toggled under Settings → Download.

## Publisher (Manifest Generator)

Use the built-in Publisher to create the manifest:

1) Pick the root folder that contains files to distribute
2) Fill in metadata (info/version/base download URL/launch executable)
3) Select files to include
4) Click "Generate Manifest" to produce an online manifest, then upload it to your static hosting/CDN

Then, on the client side, set the manifest URL in Settings → Remote config URL.

## Build & Run

Requirements:
- Windows 10/11
- .NET 8 SDK
- Optional: Visual Studio 2022 (or use CLI)

PowerShell (examples):

```powershell
# Restore and build
dotnet build .\ModSyncTool.sln -c Release

# Run (debug)
dotnet run --project .\ModSyncTool\ModSyncTool.csproj -c Debug

# Publish (example: x64 portable)
dotnet publish .\ModSyncTool\ModSyncTool.csproj -c Release -r win-x64 --self-contained false -o .\publish\win-x64
```

Artifacts are under `ModSyncTool\bin\<Configuration>\net8.0-windows\`.

## Logs & Troubleshooting

- Log location: `logs/modsync.log`
- Theme: the app logs messages like `ThemeService: applying Dark/Light theme` and registry values to confirm detection
- On crashes, global exception handlers write full details to the log; please attach logs when filing issues

## Project layout (partial)

```
ModSyncTool/
├─ ModSyncTool.sln
└─ ModSyncTool/
   ├─ App.xaml / App.xaml.cs           # App entry, theme/exception/main window
   ├─ Services/                        # Config, Sync, Hash, FileScanner, Dialog, Theme, Log, etc.
   ├─ ViewModels/                      # Main/Settings
   ├─ Views/                           # MainWindow, Settings, Welcome, ManifestGenerator, etc.
   └─ Themes/                          # Colors/Controls/Light/Dark resource dictionaries
```

## Contributing

Issues and PRs are welcome:
- Follow existing style and MVVM boundaries
- Prefer adding/updating UI theme styles and tests where applicable
- Ensure it builds, runs, and basic features regress cleanly

## License

Licensed under the `LICENSE.md` MIT in the repository root.

## Acknowledgements

- [Serilog](https://serilog.net/) for structured logging
- The WPF community for design and theming practices
