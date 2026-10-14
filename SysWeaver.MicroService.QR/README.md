# SysWeaver.MicroService.QR

[⬆ SysWeaver overview](../README.md)

> QR code generation as SVG or PNG, provided through the `IQrCodeService` contract and configurable HTTP end points.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service (`IQrCodeService`) |

## Purpose

Any feature that needs a QR code (e.g. adding a passkey from another device, sharing a URL from an AI chat) asks the service manager for `IQrCodeService` (declared in [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md)) instead of embedding a QR library.

## Key features

- `IQrCodeService` implementation: QR codes as SVG text or as a ready-to-serve HTTP response, with configurable colours and quiet zone
- HTTP end points that encode their query string, by default `qr/Code.svg` and `qr/Code.png` (debug role); URL, auth, size, colours and compression are configurable per end point
- `CreateQrCode.svg` web API (debug role)
- Compact SVG output (a single path)

## Key components

- [`QrCodeService`](QrCodeService.cs) — the service, API and HTTP module
- [`QrCodeParams`](QrCodeParams.cs) — end points and response settings
- [`QrCodeTools`](QrCodeTools.cs) — QR matrix to SVG path

## Using it

```json
{ "Type": "SysWeaver.MicroService.QrCodeService, SysWeaver.MicroService.QR" }
```

## Limitations and considerations

- The default end points and the API require the debug role; end-user QR codes are produced by the features that use the contract

## Relationships

- **Project:** [`SysWeaver.MicroService.QR.csproj`](SysWeaver.MicroService.QR.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
- **Third-party:** QRCoder
