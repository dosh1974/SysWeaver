# SysWeaver.WebBrowser.WebView2

[⬆ SysWeaver overview](../../README.md)

> Headless Microsoft Edge WebView2 implementation of the browser service.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Plug-in (`IBrowserService`) |
| **Platform** | Windows x64 only |

## Purpose

An alternative engine to CEF that uses a fixed-version Edge WebView2 runtime shipped with the application, so no installed Edge runtime is needed.

## How it fits into SysWeaver

Implements `IBrowserService` from [SysWeaver.WebBrowser](../../SysWeaver.WebBrowser/README.md); list it in the manifest before consumers such as [SysWeaver.MicroService.Thumbnail.Web](../../SysWeaver.MicroService.Thumbnail.Web/README.md). It is interchangeable with [SysWeaver.WebBrowser.Cef](../SysWeaver.WebBrowser.Cef/README.md).

## Key features

- Off-screen WebView2 windows with JavaScript execution, .NET objects exposed to JavaScript and PNG/JPEG capture
- Pool of reusable windows (`MaxFreeWindows`)
- WebView2 calls run on a dedicated UI thread with its own synchronization context
- Options to disable the GPU (`DisableGPU`, process wide) and to ignore certificate errors (`IgnoreCertErrors`, dangerous)
- `IWebViewBrowserWindow` gives direct access to the WebView2 core objects

## Key components

- [`WebViewBrowser`](WebViewBrowser.cs) — the browser service
- [`WebViewBrowserParams`](WebViewBrowserParams.cs) — service parameters
- [`WebViewProxy`](WebViewProxy.cs) — environment creation and window implementation on the UI thread

## Using it

```json
{ "Type": "SysWeaver.WebBrowser.WebViewBrowser, SysWeaver.WebBrowser.WebView2" }
```

## Limitations and considerations

- Windows x64 only (`net10.0-windows`, the `WebView2.Runtime.X64` package); the runtime is loaded from the `WebView2` folder next to the executable
- The user data folder is `WebView2User` in the all-users application data folder (`Folders.AllAppFolders`)
- The browser language is fixed to English ("en")

## Relationships

- **Project:** [`SysWeaver.WebBrowser.WebView2.csproj`](SysWeaver.WebBrowser.WebView2.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.WebBrowser](../../SysWeaver.WebBrowser/README.md)
- **Third-party:** WebView2 runtime (`WebView2.Runtime.X64`), Nito.AsyncEx.Context
