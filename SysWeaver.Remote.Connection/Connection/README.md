# Connection

[⬆ SysWeaver.Remote.Connection](../README.md)

> The internal machinery behind remote API proxies: IL generation of the interface implementation, URL parameter and form encoding, and per request time-outs.

| | |
|---|---|
| **Project** | [SysWeaver.Remote.Connection](../README.md) |
| **Kind** | Source code |

## Purpose

[`RemoteConnection.Create<T>()`](../RemoteConnection.cs) uses the code in this folder to turn an `IRemoteApi`-style interface into a working class. `InterfaceTypeCache<T>` validates the interface and emits a class derived from [`RemoteConnectionBase`](../RemoteConnectionBase.cs). The class is built once per interface, so a validation error surfaces as a `TypeInitializationException`. Each end point gets an `ApiMeta<T>` field with its own settings. GET/DELETE parameters are written into the URL by compiled encoders (`UriParamsEncoder<T>`, built from `EncoderHelper` expression blocks). The `formUrl` serializers use `FormUrlWriter`. Per end point time-outs travel with the request and are applied by `HttpClientTimeoutHandler`.

## Contents

| Item | Description |
|---|---|
| [`InterfaceTypeCache.cs`](InterfaceTypeCache.cs) | Interface validation and IL emission of the implementation, `ApiMeta` / `ApiMeta<T>` per end point meta data |
| [`UriParamsEncoder.cs`](UriParamsEncoder.cs) | `UriParamsEncoder<T>`: formats parameters into a URL template such as `Get?id={id}` (invariant culture, URL escaped) |
| [`EncoderHelper.cs`](EncoderHelper.cs) | Expression tree building blocks used by `UriParamsEncoder<T>` |
| [`FormUrlWriter.cs`](FormUrlWriter.cs) | Growable buffer that writes the public fields and properties of an object as `application/x-www-form-urlencoded` (UTF-8), with compiled per-type writers |
| [`HttpClientTimeoutHandler.cs`](HttpClientTimeoutHandler.cs) | `DelegatingHandler` that applies a per request time-out and reports it as a `TimeoutException` |
| [`HttpRequestExtensions.cs`](HttpRequestExtensions.cs) | Stores the per request time-out in `HttpRequestMessage.Options` for the handler |

## Relationships

- **Part of:** [SysWeaver.Remote.Connection](../README.md)
- **Used by:** [`RemoteConnection.cs`](../RemoteConnection.cs), [`RemoteConnectionBase.cs`](../RemoteConnectionBase.cs), [`FormUrlSerializer.cs`](../FormUrlSerializer.cs), [`IgnoreDefaultsFormUrlSerializer.cs`](../IgnoreDefaultsFormUrlSerializer.cs)
