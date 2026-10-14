# SysWeaver.IsoData

[⬆ SysWeaver overview](../README.md)

> Built-in reference data for countries, currencies, languages and international phone prefixes, with currency-aware amount formatting.

| | |
|---|---|
| **Layer** | Foundation |
| **Kind** | Library (built-in data) |

## Purpose

Gives every service the same authoritative lists of ISO countries, currencies, languages and phone prefixes without a database or web lookup. Many features rely on it: language validation and the default target-language list in the HTTP server, phone number validation during login and sign-up, currency handling for exchange rates, country data for maps and certificates, and editor drop-downs.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Iso["SysWeaver.IsoData"] --> Http["HTTP server<br/>languages"]
  Iso --> Auth["Auth services<br/>phone numbers"]
  Iso --> Fx["Exchange rates<br/>currencies"]
  Iso --> Map["Maps<br/>countries"]
  Iso --> Edit["Editors and debug tables<br/>via IsoDataModule"]
```

## Key features

- `IsoCountry`: ISO 3166 Alpha 2 code, official/common name, main currency, population, land area and spoken languages. Lookup by code only (`TryGet`, "Sweden" gives null) or by code and fuzzy name/nick name/abbreviation (`TryGetName`, `Aliases`).
- `IsoCurrency`: ISO 4217 code and number, symbol, under units, separators and the "biggest" user country. Lookup by code or number (`TryGet`, `Validate`), formatting via `ToString(amount, CurrencyFormatOptions, ...)`, plus `CurrencyGlyphs`/`IsCurrencyGlyph` (all chars needed to render amounts, e.g. for font subsets).
- `CurrencyFormatOptions`: flags selecting ISO prefix/suffix or symbol style, thousands separator, forced rounding and automatic removal of zero under units.
- `IsoLanguage`: ISO 639-1/639-2 codes and names. Lookup by code (`TryGet`, also accepts regional codes like `en-GB`), by name (`TryGetName`), normalization (`Validate`), countries using a language (`GetCountries`) and `Common` (languages used in countries with 10M+ people in total, kept identical to the set computed before the 2026-10-07 population refresh: `sr` kept, `he`, `ho` and `tg` excluded; used as the default target languages).
- `PhonePrefix`: international calling codes (with region prefixes, dialing prefixes and valid local digit counts). `Identify` finds the code(s) of a partial number; `GetValidatedPhoneNumber` validates a full number and splits it into prefix and local number (throwing with a human readable message when invalid).
- `AmountExt`: `Amount.CurrencyInfo()` and `Amount.ToAmountString()` for displaying money correctly per currency.
- Data classes are annotated with table-data attributes so they render nicely (flags, links) in debug tables.

## Key components

- [`IsoCountry`](IsoCountry.cs) — countries with names, aliases, currency, population, area and languages
- [`IsoCurrency`](IsoCurrency.cs) — currencies and currency-aware amount formatting
- [`IsoLanguage`](IsoLanguage.cs) — languages, regional codes and validation
- [`PhonePrefix`](PhonePrefix.cs) — international calling codes and phone number validation

## Using it

Reference it and use the static lookups (e.g. `IsoCountry.TryGet("SE")`, `IsoCurrency.TryGet("USD").ToString(12.5M, CurrencyFormatOptions.Symbol)`, `PhonePrefix.GetValidatedPhoneNumber(...)`); to browse the data in the web UI add [SysWeaver.HttpServer.IsoDataModule](../SysWeaver.HttpServer.IsoDataModule/README.md).

## Limitations and considerations

- Static data compiled into the assembly; updates (new currencies, renamed countries) require a new build.
- Data is curated rather than authoritative. `IsoCountry` population (number of people) and land area (km²) figures come from the World Bank Open Data API (fetched 2026-10-07; `SP.POP.TOTL` 2025 values and `AG.LND.TOTL.K2` 2023 values); territories the World Bank doesn't cover (ex: AX, FK, GF, RE, TW, VA) keep older, approximate (around 2020) figures, zero when unknown.
- Validation methods differ in style: `IsoCurrency.Validate` returns null for unknown input, while `IsoLanguage.Validate` and `PhonePrefix.GetValidatedPhoneNumber` throw a `FormatException` with a human readable message (`ArgumentNullException` for a null phone number).
- Code lookups (`TryGet`) are case insensitive but don't trim; the name lookups (`IsoCountry.TryGetName`) normalize case, diacritics, periods and hyphens and accept abbreviations ("St", "Rep", "Is", "N" / "S" / "E" / "W" for directions).
- `IsoCurrency.ToString` rounds to the nearest under unit with banker's rounding; without `ApplyThousandsSeparator` the integer part is formatted with the current culture, and negative values use the negative sign of the current culture (not always '-'; `CurrencyGlyphs` contains both '-' and U+2212, the only visible negative signs of the .NET cultures).
- `IsoLanguage.Common` is a shared mutable array and `GetCountries` returns shared arrays: don't modify them.
- `PhonePrefix.Identify` matches the longest prefix (country code + region prefix), so a number of a shared country code (ex: +1) whose digits start with a region prefix is attributed to that region.

## Relationships

- **Project:** [`SysWeaver.IsoData.csproj`](SysWeaver.IsoData.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md)
- **Used by:** [SysWeaver.AI](../SysWeaver.AI/README.md), [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.ExchangeRate](../SysWeaver.ExchangeRate/README.md), [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md), [SysWeaver.HttpServer.IsoDataModule](../SysWeaver.HttpServer.IsoDataModule/README.md), [SysWeaver.Knowledge](../SysWeaver.Knowledge/README.md), [SysWeaver.LanguageIdentifier.FastText](../SysWeaver.LanguageIdentifier.FastText/README.md), [SysWeaver.Map](../SysWeaver.Map/README.md), [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md), [SysWeaver.MicroService.Translation](../SysWeaver.MicroService.Translation/README.md), [SysWeaver.MicroService.TranslationDbCache](../SysWeaver.MicroService.TranslationDbCache/README.md), [SysWeaver.Security](../SysWeaver.Security/README.md), [SysWeaver.TextMessage.AmazonEndUserMessaging](../Communication/SysWeaver.TextMessage.AmazonEndUserMessaging/README.md), [SysWeaver.TextMessage.Fake](../Communication/SysWeaver.TextMessage.Fake/README.md), [SysWeaver.TextMessage.Router](../Communication/SysWeaver.TextMessage.Router/README.md), [SysWeaver.TextMessage.Twilio](../Communication/SysWeaver.TextMessage.Twilio/README.md), [SysWeaver.TextMessage.WhatsApp](../Communication/SysWeaver.TextMessage.WhatsApp/README.md), [SysWeaver.Translation.Google](../SysWeaver.Translation.Google/README.md); in [SysWeaver.Services](https://github.com/dosh1974/SysWeaver.Services/blob/main/README.md): ExchangeRateService, FakeEmailService, FakeTextService, WebServerService
