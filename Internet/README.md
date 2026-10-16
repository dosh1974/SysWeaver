# Internet

[⬆ SysWeaver overview](../README.md)

> Groups the headless web browser implementations of the browser service defined in SysWeaver.WebBrowser.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Group of projects |

## Purpose

[SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md) defines the browser service; the projects in this folder implement it with a real, headless browser engine (Chromium through CEF, or Microsoft Edge WebView2). The folder itself only holds the project folders.

## Contents

| Item | Description |
|---|---|
| [`SysWeaver.WebBrowser.Cef/`](SysWeaver.WebBrowser.Cef/README.md) | Headless Chromium (CEF, off-screen) implementation of the browser service. |
| [`SysWeaver.WebBrowser.WebView2/`](SysWeaver.WebBrowser.WebView2/README.md) | Headless Microsoft Edge WebView2 implementation of the browser service. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Implements:** [SysWeaver.WebBrowser](../SysWeaver.WebBrowser/README.md)
