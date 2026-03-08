# PRISM Utility

PRISM Utility is a WinUI 3 desktop application for [PRISM-Film-Scanner](https://github.com/mr258876/PRISM-Film-Scanner/tree/main) on Windows.

The repository currently contains two interactive tools:

- `USB Debugging`: inspect devices, configurations, interfaces, and endpoints, then send or receive bulk transfers.
- `Scan Debugging`: connect to supported scanner hardware, start capture sessions, adjust scan parameters, preview decoded image data, and export captured buffers.

## Project Layout

- `PRISM Utility/` - WinUI 3 desktop application, pages, view models, packaging files, and app resources
- `PRISM Utility.Core/` - shared services, models, file helpers, and USB abstractions

## Requirements

- Windows 10 1809 or later
- .NET 8 SDK
- Visual Studio 2022 with WinUI / Windows App SDK tooling, or a compatible command-line MSBuild environment
- [PRISM-Film-Scanner](https://github.com/mr258876/PRISM-Film-Scanner/tree/main) hardware

## Build

```bash
dotnet build "PRISM Utility.sln"
```

## Run

Open `PRISM Utility.sln` in Visual Studio and start either the packaged or unpackaged profile from `PRISM Utility/Properties/launchsettings.json`.

## Notes

- The application includes low-level USB and scanner debugging functionality. Use it carefully on production hardware.

## License

See `LICENSE`.
