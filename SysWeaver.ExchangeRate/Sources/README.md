# Sources

[⬆ SysWeaver.ExchangeRate](../README.md)

> The exchange rate source plug-ins (`IRateSource` implementations) and their parameter classes.

| | |
|---|---|
| **Project** | [SysWeaver.ExchangeRate](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.ExchangeRate.Sources`) |

## Purpose

Each source is registered in the manifest before the `ExchangeRateService` and fetches a complete set of rates on its own schedule (`RefreshMinutes`). The commercial sources call the provider's `latest` endpoint with an API key (read through `ApiKeyParams`, e.g. from a credential file) and a reference currency; the SysWeaver source calls `Api/ExchangeRates/SyncRates` on another server that runs the service, so one server with API keys can feed many.

## Contents

| Item | Description |
|---|---|
| [`ErsExchangeRatesApiDotIo.cs`](ErsExchangeRatesApiDotIo.cs) | Source using [exchangeratesapi.io](https://exchangeratesapi.io/) |
| [`ErsExchangeRatesApiDotIoParams.cs`](ErsExchangeRatesApiDotIoParams.cs) | Its parameters: API key, refresh interval (default 8 hours), reference currency |
| [`ErsFixerDotIo.cs`](ErsFixerDotIo.cs) | Source using [fixer.io](https://fixer.io/) |
| [`ErsFixerDotIoParams.cs`](ErsFixerDotIoParams.cs) | Its parameters: API key, refresh interval (default 8 hours), reference currency |
| [`ErsSysWeaverService.cs`](ErsSysWeaverService.cs) | Source using another SysWeaver server (remote `IRemote.SyncRates` call) |
| [`ErsSysWeaverServiceParams.cs`](ErsSysWeaverServiceParams.cs) | Its parameters: the remote connection (`BaseUrl`, credentials) and refresh interval (default 5 minutes) |

## Relationships

- **Part of:** [SysWeaver.ExchangeRate](../README.md)
- **Implements:** `IRateSource` ([`IRateSource.cs`](../IRateSource.cs))
- **Uses:** [SysWeaver.Remote.Connection](../../SysWeaver.Remote.Connection/README.md) (`ErsSysWeaverService`)
