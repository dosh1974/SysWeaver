# srcWeb

[⬆ SysWeaver.MicroService.LanCertificateManager](../README.md)

> Source of the card template that the domain table of the LAN certificate manager uses.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.LanCertificateManager](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

This folder only contains the `lancert/` sub folder. The `EmbedLocal` target in [`SysWeaver.MicroService.LanCertificateManager.csproj`](../SysWeaver.MicroService.LanCertificateManager.csproj) runs `Compress.exe -Methods br .\srcWeb\lancert\* .\web\lancert` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `lancert/...` relative to the site root.

The row type of the *Domains* table (`LanCertificateManagerService.Data`) names the template with `[TableDataCard("domain@lancert/cards")]`, so the table shows its rows as cards by default. See [Card templates](../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md#card-templates-card) for the template syntax.

## Contents

| Item | Description |
|---|---|
| [`lancert/cards.html`](lancert/cards.html) | Template `domain`: one card per domain with the days left (warning at 30 days, error when expired), the expiration time, the last check, the certificate fingerprint and the manual update action. Not a page (no framework is loaded) |

## Relationships

- **Part of:** [SysWeaver.MicroService.LanCertificateManager](../README.md)
- **Uses:** the table view and card styles of [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/README.md) and the `sw-card` components of [SysWeaver.HttpServer](../../SysWeaver.HttpServer/README.md)
