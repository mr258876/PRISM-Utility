# PRISM Utility Core Project

This project contains shared models and services used by the WinUI application.

## Responsibilities

- USB device access and bulk transfer helpers
- file and JSON helpers for persisted settings and exported data
- shared DTOs and service contracts used across the solution

## Main Areas

- `Contracts/Models/` - data transfer objects for USB and scan workflows
- `Contracts/Services/` - shared service interfaces
- `Services/` - concrete implementations such as USB and file services
- `Helpers/` - serialization helpers and utility code

## Notes

The application project references this library directly. In normal development you will build it through the solution root:

```bash
dotnet build "PRISM Utility.sln"
```
