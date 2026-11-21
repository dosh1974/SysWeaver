# Internet

[⬆ SysWeaver overview](../README.md)

> Groups the web browser implementations of the browser service defined in SysWeaver.WebBrowser.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

[SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md) defines the browser service; the projects in this folder implement it with a real browser engine: Chromium through CEF, Microsoft Edge WebView2, or Chromium / Firefox / WebKit through Playwright. The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.WebBrowser.Cef/`](SysWeaver.WebBrowser.Cef/README.md) | Headless Chromium (CEF, off-screen) implementation of the browser service. |
| [`SysWeaver.WebBrowser.WebView2/`](SysWeaver.WebBrowser.WebView2/README.md) | Headless Microsoft Edge WebView2 implementation of the browser service. |
| [`SysWeaver.Browser.Playwright/`](SysWeaver.Browser.Playwright/README.md) | Playwright implementation of the browser service (project `SysWeaver.WebBrowser.Playwright`): Chromium (an installed Chrome or Edge, or the one downloaded by Playwright), Firefox or WebKit, headless or windowed; cross-platform. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Implements:** [SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md)
