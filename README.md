# ReSerialSync

[中文](README.zh-CN.md) · [Migration write-up](docs/migration/README.md) · [Documentation](docs/README.md)

A serial debugging workbench built with Avalonia 12 and .NET 10, rewritten from [SerialSync](https://github.com/0use-TE/SerialSync).
It combines independent serial sessions, a dockable UI and an AI assistant, with real ESP32 communication tested on Windows and Arch Linux.

![AI connecting a port, sending data and receiving a reply](docs/media/ai-connect-send-hardware.png)

## Project and migration links

- Project: [github.com/0use-TE/ReSerialSync](https://github.com/0use-TE/ReSerialSync)
- English write-up: [docs/migration/README.md](https://github.com/0use-TE/ReSerialSync/blob/main/docs/migration/README.md)
- Chinese write-up: [docs/migration/README.zh-CN.md](https://github.com/0use-TE/ReSerialSync/blob/main/docs/migration/README.zh-CN.md)

The write-up covers the UI rewrite, Native AOT, published size, memory, startup measurements, AI-assisted development and hardware demonstrations.
The English edition was translated from Chinese with AI assistance; some wording may sound unusual.

## Features

- Concurrent serial sessions with independent settings, send drafts, counters and receive buffers.
- Text/HEX, UTF-8/ASCII/Latin-1, configurable line endings and timed sending.
- Dockable panels, automatic layout saving and a compact mode.
- Light/dark themes, English/Chinese localisation and Material Icons.
- Serilog file logs with keyword and severity filters.
- AI context and tool calls through DotKernel. Explicit send requests transmit the editor contents once; preparation alone does not send.

Configure an OpenAI-compatible endpoint, model and API key in settings. The key is stored as plaintext locally. AI is optional.

## Run and build

Requires .NET 10 SDK:

```powershell
dotnet run --project ReSerialSync/ReSerialSync.Desktop
dotnet run --project ReSerialSync/ReSerialSync.Tests
```

For Native AOT, build Windows on Windows and Linux on Linux using `scripts/publish.ps1` or `scripts/publish-linux.sh`.
See [runtime instructions](docs/en/releases.md) for dependencies and the [v1.0.0](https://github.com/0use-TE/ReSerialSync/releases/tag/v1.0.0) downloads. That release includes the October 9 AI sending fix and the virtualized receive view.
The Windows and Linux AOT builds have been produced. macOS has not been tested because I do not have a Mac; browser/mobile serial backends remain unfinished.

The test suite passed **127 checks** using simulated serial ports, simulated AI and Avalonia Headless.
An [ESP32 auto-reply example](SerialSyncLowerComputer/README.md) is included for device testing.

## Libraries and author

[Crystal.Avalonia](https://github.com/0use-TE/Crystal.Avalonia), [GOZA.Dock](https://github.com/0use-TE/GOZA.Dock) and [DotKernel](https://github.com/0use-TE/DotKernel) are libraries I develop.
Community components include Semi.Avalonia, Material Icons, LiveMarkdown.Avalonia and Serilog; see [third-party notices](docs/en/third-party-notices.md).

Ouse / 0use-TE · [Website](https://0use.net) · [GitHub](https://github.com/0use-TE)

[MIT licence](LICENSE). Thank you to the Avalonia team and community, and to the authors of the open-source components!
