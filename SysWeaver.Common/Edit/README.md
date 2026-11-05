# Edit

[⬆ SysWeaver.Common](../README.md)

> Attributes that describe how members of an object are presented and validated in a generated editor.

| | |
|---|---|
| **Project** | [SysWeaver.Common](../README.md) |
| **Kind** | Source code (attributes, namespace `SysWeaver`) |

## Purpose

Put these attributes on fields and properties of a type to control the editor that is generated for it: display names, order, defaults, ranges, sliders, password masking, multi-line text, visibility and specialised editor types. They only describe; the object editor of [SysWeaver.MicroService.Edit](../../SysWeaver.MicroService.Edit/README.md) reads them. Many services put them on their parameter and request types.

## Contents

| Item | Description |
|---|---|
| [`EditAllowNullAttribute.cs`](EditAllowNullAttribute.cs) | Allows a member to be null |
| [`EditDefaultAttribute.cs`](EditDefaultAttribute.cs) | The default value used when editing |
| [`EditDisplayNameAttribute.cs`](EditDisplayNameAttribute.cs) | Display name (default is the member name with camel casing removed) |
| [`EditElementAttributesAttribute.cs`](EditElementAttributesAttribute.cs) | Names a type whose member (with the same name) carries the edit attributes for collection elements |
| [`EditKeyAttributesAttribute.cs`](EditKeyAttributesAttribute.cs) | The same for dictionary keys |
| [`EditMaxAttribute.cs`](EditMaxAttribute.cs) | Maximum value (or maximum number of chars for strings) |
| [`EditMinAttribute.cs`](EditMinAttribute.cs) | Minimum value (or minimum number of chars), and `EditDateUnspecifiedAttribute` for dates edited as unspecified |
| [`EditRangeAttribute.cs`](EditRangeAttribute.cs) | Minimum and maximum in one attribute |
| [`EditSliderAttribute.cs`](EditSliderAttribute.cs) | Use a slider (requires a range), optionally with a step size |
| [`EditMultilineAttribute.cs`](EditMultilineAttribute.cs) | Allows multiple lines of text |
| [`EditOrderAttribute.cs`](EditOrderAttribute.cs) | Member order in the editor (low to high) |
| [`EditPasswordAttribute.cs`](EditPasswordAttribute.cs) | Masked input, plus `EditHideAttribute` and `EditReadOnlyAttribute` |
| [`EditTypeAttribute.cs`](EditTypeAttribute.cs) | Selects an editor type, plus `EditHideIfAttribute` / `EditHideOps` to hide a member based on another member or an expression |
| [`EditColorAttribute.cs`](EditColorAttribute.cs) | Edit a member as a color and say how it is stored (`EditColorFormat`: Css, Hex (css strings always stored as #rrggbb), Rgb, Rgba, HdrRgb, HdrRgba, Rgb8, Rgba8, UInt32Rgb / Rgba / Argb / Abgr, Auto) |
| [`EditUnitAttribute.cs`](EditUnitAttribute.cs) | The unit of a number member (`EditUnits`: Milliseconds, Seconds, Minutes, Hours, Days, Bytes, KiloBytes, MegaBytes, GigaBytes, Percent, Ratio): the editor shows the unit and a readable form ("= 2 h"), and converts typed values with a unit ("90s", "2 MB", "25%") |
| [`EditTypes.cs`](EditTypes.cs) | Predefined editor types (language, country, currency, email, url, phone, color, unit, inline objects, ...) |

## Relationships

- **Part of:** [SysWeaver.Common](../README.md)
- **Used by:** [SysWeaver.MicroService.Edit](../../SysWeaver.MicroService.Edit/README.md) (the editor); annotated types in e.g. [SysWeaver.TableData](../../SysWeaver.TableData/README.md), [SysWeaver.AI](../../SysWeaver.AI/README.md), [SysWeaver.MicroService.UserManager](../../SysWeaver.MicroService.UserManager/README.md)
