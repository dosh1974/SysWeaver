# srcWeb

[⬆ SysWeaver.ExchangeRate](../README.md)

> Source of the embedded web assets of the exchange rate service: the card templates of its rate and source tables.

| | |
|---|---|
| **Project** | [SysWeaver.ExchangeRate](../README.md) |
| **Kind** | Embedded web assets |

## Purpose

The `EmbedLocal` target in [`SysWeaver.ExchangeRate.csproj`](../SysWeaver.ExchangeRate.csproj) runs `Compress.exe -Methods br .\srcWeb\exchange\* .\web\exchange` before each build. The generated `web/**/*.br` files (not in git) are embedded as resources and served as `exchange/...` relative to the site root.

The row types of the tables point at these templates with `[TableDataCard]` (see [`ExchangeRateService.RateRow.cs`](../ExchangeRateService.RateRow.cs) and [`ExchangeRateService.SourceRow.cs`](../ExchangeRateService.SourceRow.cs)), so the tables can show each row as a card (the "Card templates" section of the [explore README](../../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md#card-templates-card) describes the format).

## Contents

| Item | Description |
|---|---|
| [`exchange/cards.html`](exchange/cards.html) | Card templates, not a page: `source` (`SourceRow`, `SourcesTable`, cards by default; badges for the source in use and for failures: 1 or more is a warning, 10 or more an error) and `rate` (`RateRow`, `RatesTable`, table by default; the rate, the amounts compared with US dollars, the country and the update time) |

## Relationships

- **Part of:** [SysWeaver.ExchangeRate](../README.md)
- **Used by:** `explore/table.js` of [SysWeaver.HttpServer.ExploreModule](../../SysWeaver.HttpServer.ExploreModule/README.md) (loads the templates)
