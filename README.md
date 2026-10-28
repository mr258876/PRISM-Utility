# PRISM Utility

PRISM Utility is a WinUI 3 desktop application for [PRISM-Film-Scanner](https://github.com/mr258876/PRISM-Film-Scanner/tree/main) on Windows.

The repository currently contains two interactive tools:

- `USB Debugging`: inspect devices, configurations, interfaces, and endpoints, then send or receive bulk transfers.
- `Scan Debugging`: connect to supported scanner hardware, start capture sessions, adjust scan parameters, preview decoded image data, and export captured buffers.

## Project Layout

- `PRISM Utility/` - WinUI 3 desktop application, pages, view models, packaging files, and app resources
- `PRISM Utility.Core/` - shared services, models, file helpers, and USB abstractions
- `docs/architecture/` - [Architecture documentation](docs/architecture/README.md)
- `docs/` - [Documentation index](docs/README.md) and [current backlog and verification status](docs/TODO.md)

## Requirements

- Windows 10 1809 or later
- .NET 10 SDK (10.0.4xx; selected by `global.json`) for the C# features used by the current source
- .NET 8 runtime (the application still targets .NET 8)
- Visual Studio 2026 with WinUI / Windows App SDK tooling, or a compatible command-line MSBuild environment
- C++ desktop development tools with MSVC v143 x64/x86 (also available as an optional component in Visual Studio 2026)
- Windows SDK 10.0.19041 or later
- [Adobe DNG SDK](https://helpx.adobe.com/camera-raw/desktop/dng-and-file-formats/digital-negative.html), extracted into `DngSdkWarpper/Adobe_DNG_SDK/` so that `dng_sdk/source`, `libjxl`, and `xmp` are directly under that directory
- [PRISM-Film-Scanner](https://github.com/mr258876/PRISM-Film-Scanner/tree/main) hardware

## Build

From PowerShell in the repository root, build the x64 native bridge and then the full solution:

```powershell
.\tools\build.ps1
```

To build and run the existing tests:

```powershell
.\tools\build.ps1 -Test
```

Use `-Configuration Release` for a release build. The script locates Visual Studio's MSBuild automatically; the full solution includes a C++ project that requires this MSBuild environment.

Current test results, known failures, and remaining validation are tracked in the [development backlog](docs/TODO.md).

## Run

In Visual Studio, use **File > Open > Project/Solution** to open `PRISM Utility.sln`. Opening the repository as a folder does not load the solution's startup profiles. Select **Debug / x64**, set **PrismUtility** as the startup project, and start the **PRISM Utility (Unpackaged)** profile from `PRISM Utility/Properties/launchSettings.json`.

For Visual Studio Code, open the `Host Software` folder and install the recommended Microsoft C# extension. In **Run and Debug**, select **PRISM Utility (x64 / Unpackaged)** and press **F5**. The launch configuration builds the native bridge and solution before starting the application. **Ctrl+F5** starts it without debugging, and **Ctrl+Shift+B** builds it.

## Notes

- The application includes low-level USB and scanner debugging functionality. Use it carefully on production hardware.

## License

See `LICENSE`.
