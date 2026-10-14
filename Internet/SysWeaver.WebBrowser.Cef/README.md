# SysWeaver.WebBrowser.Cef

[⬆ SysWeaver overview](../../README.md)

> Headless Chromium (CEF, off-screen) implementation of the browser service.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Plug-in (`IBrowserService`) |
| **Platform** | Windows only |

## Purpose

Render and script web pages without a visible window — screenshots, thumbnails, automation — using an embedded Chromium (CefSharp off-screen).

## How it fits into SysWeaver

Implements `IBrowserService` from [SysWeaver.WebBrowser](../../SysWeaver.WebBrowser/README.md); list it in the manifest before consumers such as [SysWeaver.MicroService.Thumbnail.Web](../../SysWeaver.MicroService.Thumbnail.Web/README.md). It is interchangeable with [SysWeaver.WebBrowser.WebView2](../SysWeaver.WebBrowser.WebView2/README.md).

## Key features

- Off-screen Chromium windows with JavaScript execution, .NET objects exposed to JavaScript and PNG/JPEG capture
- Pool of reusable windows (`MaxFreeWindows`)
- All CEF calls are marshalled to a dedicated thread
- Optional disc cache (`CachePath`, empty for the default location, null for memory only) and CEF log file `<executable>_cef.log`
- Initialization at construction or on first use (`Init`)

## Key components

- [`CefWebBrowser`](CefWebBrowser.cs) — the browser service
- [`CefWebBrowserParams`](CefWebBrowserParams.cs) — service parameters

## Using it

```json
{ "Type": "SysWeaver.WebBrowser.CefWebBrowser, SysWeaver.WebBrowser.Cef" }
```

## Limitations and considerations

- Windows only (`net10.0-windows`, Windows Chromium runtimes for x86, x64 and arm64)
- CEF is large and memory hungry
- CEF logs verbosely unless `SmallLog` is set
- The project references SysWeaver.MicroService.MySqlAudit, pulling database dependencies into consumers, although the code does not use it

## Relationships

- **Project:** [`SysWeaver.WebBrowser.Cef.csproj`](SysWeaver.WebBrowser.Cef.csproj)
- **Builds on:** [SysWeaver.Common](../../SysWeaver.Common/README.md), [SysWeaver.MicroService.MySqlAudit](../../SysWeaver.MicroService.MySqlAudit/README.md), [SysWeaver.WebBrowser](../../SysWeaver.WebBrowser/README.md)
- **Third-party:** CefSharp (`CefSharp.OffScreen.NETCore`)
