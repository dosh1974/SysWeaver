# _docs

[⬆ SysWeaver overview](../README.md)

> Additional documents that are not tied to a project folder: currently the reverse proxy schematic.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Documents |

## Purpose

Holds editable source documents for the documentation. The only document is a Word drawing of the reverse proxy set-up of [SysWeaver.ReverseProxy](../SysWeaver.ReverseProxy/README.md). It was committed together with `SysWeaver.ReverseProxy/ReverseProxy.svg`, the schematic shown in that project's README, and appears to be its editable source. Nothing in the build uses this folder.

## Contents

| Item | Description |
|---|---|
| [`ReverseProxy.docx`](ReverseProxy.docx) | Schematic of the reverse proxy: HTTP clients reach a **reverse proxy server** (machine with public access, static IP and domain name; client services reached by sub path, the default, or by domain name) over HTTPS, which forwards to one or more **reverse proxy client services** (machines without public access) whose end points are either internal (the same SysWeaver service process) or any HTTP server reachable from that machine. |

## Relationships

- **Part of:** [SysWeaver](../README.md)
- **Documents:** [SysWeaver.ReverseProxy](../SysWeaver.ReverseProxy/README.md); the hosts in SysWeaver.Services are [ReverseProxyServer](https://github.com/dosh1974/SysWeaver.Services/blob/main/ReverseProxyServer/README.md) and [ReverseProxyClient](https://github.com/dosh1974/SysWeaver.Services/blob/main/ReverseProxyClient/README.md)
