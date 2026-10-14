# SysWeaver.Docs

[⬆ SysWeaver overview](../README.md)

> Reads the compiler-generated XML documentation of assemblies at runtime, so that `///` comments become user-facing text: help screens, config file comments, API documentation and table column descriptions.

| | |
|---|---|
| **Layer** | Foundation |
| **Kind** | Library (no dependencies) |

## Purpose

SysWeaver follows a "document once" philosophy: the XML comment you write on a parameter field or a web API method is also what the operator sees in a generated config file, in command line help, or next to a column in the web UI. This project provides the lookup from reflection objects (types, members, methods, parameters, enum values) to that documentation.

## How it fits into SysWeaver

```mermaid
flowchart LR
  Src["/// comments in source"] --> Xml["Assembly.xml<br/>generated at build"]
  Xml --> Docs["SysWeaver.Docs<br/>XmlDoc extensions"]
  Docs --> Help["Command line help"]
  Docs --> Cfg["Config file comments"]
  Docs --> Api["API and type info in web UI"]
  Docs --> Tables["Table column descriptions"]
```

All SysWeaver projects enable XML documentation generation, which is what makes this work framework-wide. Some consumers (e.g. the config file writer in [SysWeaver.Common](../SysWeaver.Common/README.md)) load `SysWeaver.Docs.XmlDocExt` by name at runtime instead of referencing it, so it is an optional enhancement rather than a hard requirement.

## Key features

- `XmlDoc()` extension methods on `Type`, `MemberInfo`, `FieldInfo`, `PropertyInfo`, `MethodInfo`, `ConstructorInfo`, `MethodBase` and `ParameterInfo`, returning summary and remarks (`IXmlDocInfo`), plus returns and per-parameter docs for methods (`IXmlDocMethodInfo`)
- `XmlSummary()` shortcuts for fields and properties, `XmlDocEnum()` for enum values, and `ToTitle()` combining summary and remarks for tool tips
- Members looked up through derived types, generic instantiations or undocumented overrides fall back to the documentation of the related declaration, also across assemblies
- Each documentation file is parsed once (lazily) and all lookups are cached; thread safe, and collectible assemblies can still be unloaded

## Key components

- [`XmlDocExt`](XmlDocExt.cs) — the extension methods and the documentation cache

## Using it

Enable `GenerateDocumentationFile` in your own projects so your services get the same treatment, then:

```csharp
using SysWeaver.Docs;
string text = typeof(MyServiceParams).GetField("Greeting").XmlSummary();
```

## Limitations and considerations

- Only as good as the comments: undocumented members produce `null`
- Requires the `.xml` documentation files next to the assemblies; publishing pipelines that drop them (or single-file publishing, where assemblies have no location) silently remove the documentation
- Texts are the plain inner text: `<see cref="..."/>` and similar self-closing tags vanish from the sentence, and `<inheritdoc/>` is not resolved
- Some unusual signatures (e.g. types nested more than one level deep, conversion operators, multi-dimensional array parameters) are not matched and return `null`

## Relationships

- **Project:** [`SysWeaver.Docs.csproj`](SysWeaver.Docs.csproj)
- **Builds on:** —
- **Used by:** [SysWeaver.CommandLine](../SysWeaver.CommandLine/README.md), [SysWeaver.MicroService.Edit](../SysWeaver.MicroService.Edit/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md)
