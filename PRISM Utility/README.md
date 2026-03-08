# PRISM Utility App Project

This project contains the WinUI 3 desktop application for PRISM Utility.

## What It Includes

- shell, navigation, and startup flow
- USB debugging UI for bulk IN / bulk OUT workflows
- scanner debugging UI for device connection, parameter tuning, preview, calibration, and buffer export
- application resources, localization strings, packaging files, and local settings integration

## Main Areas

- `Views/` - XAML pages and page code-behind
- `ViewModels/` - UI state and command logic
- `Services/` - navigation, theme, scan, and application services
- `Strings/` - localized UI resources
- `Properties/PublishProfiles/` - local publish profile templates

## Local Development

Build the full solution from the repository root:

```bash
dotnet build "PRISM Utility.sln"
```

If you use Visual Studio, open `PRISM Utility.sln` and run one of the launch profiles defined in `Properties/launchsettings.json`.

## Packaging

The project keeps MSIX packaging files in source control. Before creating public releases, review:

- `Package.appxmanifest`
- `Package.appinstaller`
- `Properties/PublishProfiles/`

These files often need project-specific publisher, version, and distribution settings.
