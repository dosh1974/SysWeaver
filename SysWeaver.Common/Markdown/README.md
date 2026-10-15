# Markdown

[⬆ SysWeaver.Common](../README.md)

> An internal GitHub flavoured markdown parser with renderers for HTML (plain or with inline styles for e-mails), Slack "mrkdwn" and plain text.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (internal types, namespace `SysWeaver.Markdown`) |

## Purpose

The types in this folder are not public; they are exposed through `WebTools.MarkdownToHtml`, `WebTools.MarkdownToHtmlDocument` (a complete, inline styled e-mail document) and `WebTools.MarkdownToSlack` in [`WebTools_Markdown.cs`](../WebTools_Markdown.cs). `MdDocument` parses CommonMark with the GFM extensions and some GitHub extras (tables, footnotes, alerts, math blocks, ...) into a block tree; inline content is parsed by `MdInline` and rendered through an `MdFormat`. Raw HTML is sanitised when rendered, and deeply nested containers are rendered as paragraphs to protect against malicious input.

## Contents

| Item | Description |
|---|---|
| [`MdDocument.cs`](MdDocument.cs) | Block parser and the block types (`MdHeading`, `MdParagraph`, `MdCode`, `MdMath`, `MdRule`, `MdQuote`, `MdAlert`, `MdHtml`, `MdList`, `MdTable`), link reference and footnote definitions |
| [`MdInline.cs`](MdInline.cs) | Inline parser (emphasis, code spans, links, images, allowed html tags and attributes), the `MdFormat` base class and the plain text format |
| [`MdHtmlFormat.cs`](MdHtmlFormat.cs) | HTML renderer, unstyled or with inline styles for e-mails; GitHub style heading ids |
| [`MdSlackFormat.cs`](MdSlackFormat.cs) | Slack "mrkdwn" renderer (flattened quotes, tables as aligned text in a code block) |
| [`MdEmoji.cs`](MdEmoji.cs) | Common GitHub emoji short codes (also valid in Slack) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** `WebTools` in the project root ([`WebTools_Markdown.cs`](../WebTools_Markdown.cs))
