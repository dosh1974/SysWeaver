# SysWeaver.Knowledge

[⬆ SysWeaver overview](../README.md)

> An offline knowledge base — people, places, cities, countries, artists, art styles, movies, series, cars, sports, historical events and more — shipped as embedded databases with a search facility.

| | |
|---|---|
| **Layer** | Foundation (data) |
| **Kind** | Library with embedded data files |

## Purpose

Provides general world knowledge that services can query without an internet connection, for example to enrich content, generate quizzes or suggestions, or give AI features grounded reference data.

## How it fits into SysWeaver

```mermaid
flowchart LR
  DB["embedded .db files"] --> Load["typed collections<br/>Persons, Places, Movies, ..."]
  Load --> Search["InfoSearcher"]
  Search --> Consumer["your service"]
```

At the time of analysis no other SysWeaver project references it; it is a standalone data library for applications.

## Key features

- Many categories of entities loaded into typed collections.
- A searcher over the combined information.

## Limitations and considerations

- The data is a snapshot baked into the build and will age; there is no update mechanism.
- Loading all categories costs memory; load only what you need.

## Relationships

- **Project:** [`SysWeaver.Knowledge.csproj`](SysWeaver.Knowledge.csproj)
- **Builds on:** [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md), [SysWeaver.Serialization](../SysWeaver.Serialization/README.md)
