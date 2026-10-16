# WebMenu

[⬆ SysWeaver.MicroServices](../README.md)

> Attributes that place entries in the web UI's menu tree, and the menu data types sent to the browser.

| | |
|---|---|
| **Project** | [SysWeaver.MicroServices](../README.md) |
| **Kind** | Source code (attributes and data types) |

## Purpose

A service declares its menu entries with `[WebMenu*]` attributes. Each attribute has a menu name, an id/path (e.g. `"Debug/Media/ImageEffects"`), a display name, a title, an icon, an order, the required auth and kind-specific data. An optional "dynamic" member name lets the service decide at run time whether the entry is shown, e.g. `PassKeyService.CanSignInWithPassKey`. The `WebMenuService` in [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md) collects the attributes and returns the resulting `WebMenu` / `WebMenuItem` tree to the browser.

## Contents

| Item | Description |
|---|---|
| [`WebMenuAttribute.cs`](WebMenuAttribute.cs) | Base attribute with all common properties (on methods or classes) |
| [`WebMenuPathAttribute.cs`](WebMenuPathAttribute.cs) | A menu folder / path entry (on classes) |
| [`WebMenuEmbeddedAttribute.cs`](WebMenuEmbeddedAttribute.cs) | An embedded page shown inside the application (on classes) |
| [`WebMenuLinkAttribute.cs`](WebMenuLinkAttribute.cs) | A link to a page (on classes) |
| [`WebMenuLinkExternalAttribute.cs`](WebMenuLinkExternalAttribute.cs) | A link opened outside the application (on classes) |
| [`WebMenuJsAttribute.cs`](WebMenuJsAttribute.cs) | An entry that runs JavaScript (on classes) |
| [`WebMenuTableAttribute.cs`](WebMenuTableAttribute.cs) | A table view of a table data API (on methods or classes) |
| [`WebMenuChartAttribute.cs`](WebMenuChartAttribute.cs) | A chart view of a chart API (on methods) |
| [`WebMenu.cs`](WebMenu.cs) | A menu: root URI and items |
| [`WebMenuItem.cs`](WebMenuItem.cs) | One menu item: id, name, type, title, icon class, data and children |
| [`WebMenuItemTypes.cs`](WebMenuItemTypes.cs) | Item kinds: `Path`, `Table`, `Embedded`, `Link`, `LinkExternal`, `Js`, `Chart` |

## Limitations and considerations

- The XML doc summary of `WebMenuPathAttribute` ("Put this attribute on a method to make it available online") looks copied from `WebApiAttribute`. The attribute can only be placed on classes.

## Relationships

- **Part of:** [SysWeaver.MicroServices](../README.md)
- **Interpreted by:** `WebMenuService` in [SysWeaver.MicroService.HttpServer](../../SysWeaver.MicroService.HttpServer/README.md)
- **Related:** [`WebApi/`](../WebApi/README.md)
