# SysWeaver.MicroService.QR

[⬆ SysWeaver overview](../README.md)

> QR code generation as SVG, provided as a service contract for other features.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Micro service (`IQrCodeService`) |

## Purpose

Any feature that needs a QR code (e.g. linking another device, sharing a URL) asks for `IQrCodeService` instead of embedding a QR library.

## Limitations and considerations

- The direct QR endpoint is a debug-role API; end-user QR codes are produced by the features that use the contract.

## Relationships

- **Project:** [`SysWeaver.MicroService.QR.csproj`](SysWeaver.MicroService.QR.csproj)
- **Builds on:** [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md)
