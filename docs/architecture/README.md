# Host Software architecture

This index points to the architecture documents for PRISM Utility. Start with the system overview, then use the grouped topic documents for module details, source paths, risks, and test coverage.

## System map

- [System overview](system-overview.md)

## C1. Application startup and shell

- [Application lifecycle and dependency injection](app-lifecycle-and-di.md)
- [Navigation and shell](navigation-and-shell.md)

## C2. UI and ViewModel structure

- [UI and MVVM structure](ui-mvvm.md)
- [ViewModel lifecycle](viewmodel-lifecycle.md)

## C3. Settings, persistence, logging, localization, and theme

- [Settings persistence](settings-persistence.md)
- [Logging, localization, and theme](logging-localization-theme.md)

## C4. USB transport and scanner access

- [USB transport](usb-transport.md)
- [Scanner session and access coordination](scanner-session-and-access.md)

## C5. Scan, calibration, and image pipeline

- [Scan protocol and execution](scan-protocol-and-execution.md)
- [Scan workflow and device control](scan-workflow-and-device-control.md)
- [Image decoding and preview](image-decoding-preview.md)
- [Alignment, color processing, and composite preview](alignment-color-processing.md)
- [Calibration, autofocus, and film profiles](calibration-autofocus-film-profiles.md)

## C6. Export, native bridge, testing, and quality

- [DNG export and native bridge](dng-export-native-bridge.md)
- [Testing and build](testing-and-build.md)
- [Issues and remediation](issues-and-remediation.md)
