# srcWeb

[⬆ SysWeaver.Email.Fake](../README.md)

> Source of the web assets embedded in SysWeaver.Email.Fake: the fake email inbox.

| | |
|---|---|
| **Project** | [SysWeaver.Email.Fake](../README.md) |
| **Kind** | Embedded web assets (source) |

## Purpose

Holds the uncompressed source of the web inbox. The `EmbedLocal` target in [`SysWeaver.Email.Fake.csproj`](../SysWeaver.Email.Fake.csproj) runs before each build:

```text
..\..\_tools\compress\Compress.exe -Methods br .\srcWeb\fakeEmail\* .\web\fakeEmail
```

[Compress](https://github.com/dosh1974/SysWeaver.Tools/blob/main/Compress/README.md) (see [_tools](../../../_tools/README.md)) writes Brotli-compressed copies to the generated `web/fakeEmail/` folder (not in git), and every `web/**/*.br` file is embedded in the assembly as a resource. The inbox is then opened at `fakeEmail/index.html` (the *Debug / Fake email* menu link declared on `FakeEmailService`). There is no SVG optimisation step for this project.

## Contents

| Item | Description |
|---|---|
| [`fakeEmail/`](fakeEmail/README.md) | The standalone web inbox (html, css and js, no external libraries) |

## Relationships

- **Part of:** [SysWeaver.Email.Fake](../README.md)
- **Uses:** [`FakeEmailService`](../FakeEmailService.cs) web API (`Api/fakeEmail/`)
- **Built with:** [_tools/compress](../../../_tools/README.md)
