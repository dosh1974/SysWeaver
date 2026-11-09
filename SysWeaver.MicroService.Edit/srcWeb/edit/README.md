# edit

[⬆ srcWeb](../README.md)

> The object editor (`Sw.Edit`) that renders a form for any type described by `TypeService`, the type documentation page and the Ace based text / code editor. Built on the Sw core and UI library (`sw/core.js`, `sw/ui.js`).

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `edit/`. Three parts:

- **Object editor** - `Edit.js` is included by other pages (`explore/api.html`, `ServerManager/services.html`, `mediaView`, the shell's `PopUpEdit` compatibility function) and by the test / demo page `index.html`. It fetches type descriptions from `TypeService` (`edit/GetTypeInfo`, `edit/GetDefaultInstance`), picks a type handler per member and lays the form out for desktop or mobile. Every change is undoable, emits an `EditChange` event and can be copied, pasted, dragged and dropped.
- **Type documentation page** - `type.html?q=<type name>`: the type name (generic arguments linked), summary, remarks, assembly, members or enum values. The title shows `q` as text from the link (`Sw.Page.SetLinkTitle`: limited, captioned by the app shell) until the server knows the type (then "TYPE: <its name>"). Table cells of type `System.Type` link here.
- **Text / code editor** - `text.html?r=<read url>[&u=<save url>][&d=<delete url>][&n=<name>][&t=<ace mode>][&b=true][&download=false][&scrolltoend=true][&wrap=true|false][&follow=true]`. `TypeService` registers it as the extension editor (`edit/text.html?r={0}&u={1}&d={2}`, raw parameters; `r` may contain `?` and quotes). `r`, `u` and `d` must be urls of this site (relative or the same origin, `Sw.Url.SafeSameOrigin`); otherwise the page shows "This file can not be shown" and requests nothing.

Processing (minification, Brotli compression, embedding) is described in [`srcWeb/`](../README.md). The folder is embedded flat (no sub folders except `external/`).

## Contents

| Item | Description |
|---|---|
| [`external/`](external/README.md) | Vendored Ace 1.44 (modes, workers, extensions, snippets) and Prettier 3.9 (unchanged) |
| [`Edit.js`](Edit.js) | `Sw.Edit` (legacy `Edit`): engine, handler registry, undo integration, menus, clipboard, drag / drop, validation, input helpers, `Sw.Edit.Handler` and `Sw.Edit.NumberHandler` base classes, `editTestMain` |
| [`EditOptions.js`](EditOptions.js) | Compatibility stub (no longer loaded): `Sw.Edit.Options` (legacy `EditOptions`) and its presets `Clean`, `CleanReadOnly`, `CleanMenu`, `CleanReadOnlyMenu`, `CleanHints`, `CleanHintsReadOnly` are defined by Edit.js (see "Options and presets") |
| [`UndoStack.js`](UndoStack.js) | `Sw.Edit.UndoStack`, `Sw.Edit.UndoEntry` (legacy `UndoStack`, `UndoEntry`) |
| [`EditDesktop.js`](EditDesktop.js) / [`EditDesktop.css`](EditDesktop.css) | Desktop layout (rows, three column grid) |
| [`EditMobile.js`](EditMobile.js) / [`EditMobile.css`](EditMobile.css) | Mobile layout (title above value, touch sizes) |
| [`Edit.css`](Edit.css) | Shared editor styles (box, title row, rows, inputs, nested entries, drag / drop, language value, legacy icon classes) |
| [`EditCollectionArray.js`](EditCollectionArray.js), [`EditCollectionMap.js`](EditCollectionMap.js) | Collection strategies: arrays / lists and dictionaries |
| `EditType*.js` | Type handlers: `Boolean`, `Collection`, `DateTime`, `Default`, `Enum`, `Integer`, `Number`, `Object`, `String`, `TimeSpan` + `Guid` (EditTypeTimeSpan.js), `Color` (spec, EditTypeColor.js), `Email` / `Url` / `Phone` (specs, EditTypeSpecs.js), `Unit` (spec, EditTypeUnit.js: numbers with a unit), `Language` (plug-in) |
| [`EditLanguage.css`](EditLanguage.css) | Rows of the language selection (Language plug-in) |
| [`type.js`](type.js) / [`type.html`](type.html) / [`type.css`](type.css) | `Sw.TypeInfo` (legacy `TypeInfoCache`), `Sw.TypeFlags` (`TypeMemberFlags`), `Sw.TypeDoc` (`AddTypeName`, `AddTextElement`, `BuildLink`, `BuildTypeNameRec`, `typeAdd`, `typeAddSection`, `typeMain`) and the type page |
| [`text.js`](text.js) / [`text.html`](text.html) / [`text.css`](text.css) | `Sw.TextPage` (legacy `textMain`): the text editor page (text.css: the page and the log mode colors) |
| [`AceEditor.js`](AceEditor.js) / [`ace.css`](ace.css) | `Sw.AceEditor`: the shared Ace setup. `Sw.AceEditor.Theme(editor, host)` applies the `ace-sysweaver` theme (ace.css: editor, gutter, tokens, search box, popups; follows the light / dark Sw theme, also on the popups Ace appends to body), used by text.js. `await Sw.AceEditor.Create({ Mode, ReadOnly, TabSize, Wrap, MinLines, MaxLines, Resizable, SizeKey, Placeholder, Label, Class, OnChange })` loads Ace and the theme on demand (from any page: `Sw.Net.IncludeJs(null, "../edit/AceEditor.js")`) and returns an editor with the interface of `Sw.CodeEditor` (`E`, `Input`, `Get`, `Set`, `Focus`, `SetMarkers`) plus `Editor`, `SetReadOnly`, `Resize`; with MaxLines it grows with its text. `Resizable` (with MaxLines) adds a size grip in the bottom right corner (`.sw-ace-grip`, a separator): drag up / down (or arrow keys, Shift = 10 lines) sets the most lines shown (a shorter text keeps its height), double click / End = the whole text, Enter = MaxLines again; `SizeKey` remembers it in local storage ("all" or a line count). Used by the JSON tabs of explore/api.html |
| [`index.html`](index.html) / [`index.js`](index.js) / [`index.css`](index.css) | Object editor test / demo page (`Sw.EditDemo`) |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.Edit](../../README.md)
- **Talks to:** [`TypeService`](../../TypeService.cs) (`GetTypeInfo`, `GetDefaultInstance`), `iso_data/languages.js` and `iso_data/language/*.svg` (Language plug-in)
- **Depends on:** `sw/core.js` + `sw/ui.js` (or the legacy `common/common.js`, which contains both)
- **Used by:** ExploreModule (`explore/api.html`: `Sw.Edit`, `Sw.Edit.Options`, `Sw.TypeDoc`), ServerManager (`services.html`: `Edit`, `EditOptions`), Media (`mediaView/media.js`: `Edit`, `EditOptions`, `UndoStack`, `Edit.AddType`), HttpServer (`shell/compat_application.js` `PopUpEdit`: `Edit`, `EditOptions`, `TypeMemberFlags`; `ui/cells.js` links to `type.html`), Log / ServerManager / FolderSync / TextFile tables (links to `text.html`)

## Behavior

### Loading

`Edit.js` starts loading every other editor script and the three style sheets relative to its own url while it is evaluated (`Sw.Edit.Init()`, the promise is `Sw.Edit.Ready`). The dynamically inserted scripts delay the window load event, so `onload` code can create editors. All async entry points (`SetObject`, `FromServer`, `CreateDefault`, `Validate`, `GetType`) await `Ready` themselves. Plug-ins of this folder (`EditTypeLanguage.js`) are loaded on demand the first time a type uses their spec.

**Page code (`Sw.Page.Main` runs at DOM ready, before the editor files are loaded).** Usable at once: `new Sw.Edit(type | typeName, ...)`, `new Sw.Edit.Options()` and its presets (defined by Edit.js itself), `Sw.Edit.AddType`, `Sw.Edit.Flags`, `Sw.Edit.FormFooter`, then `await editor.SetObject(value)`. Everything else defined by the loaded files (`Sw.Edit.UndoStack`, `UndoEntry`, `TypeInfo`, `LayoutDesktop`, `LayoutMobile`, `Handlers.*`, `Handler` subclasses in plug-ins) needs `await Sw.Edit.Ready`; reading one of the first five earlier logs a console error that says so (instead of "... is not a constructor").

```js
Sw.Page.Main(async () => {
    Sw.Edit.AddType(DeviceType);                         // a client TypeDesc, see "Client types"
    const o = new Sw.Edit.Options();
    o.ReadOnly = !canEdit;
    const ed = new Sw.Edit("Acme.Device", null, o);      // a registered (or server) type name, or the TypeDesc itself
    await ed.SetObject(device);                          // waits for Sw.Edit.Ready
    const foot = new Sw.Edit.FormFooter(ed, { Actions: [saveButton] });
    host.append(ed.Element, foot.E);
});
```

A type name that is neither registered nor known by the server (`edit/GetTypeInfo` returns null) renders an error state in the editor (`div.sw-edit-typeerror`, "Unknown type ...") and logs a console error naming the fix; it never renders an empty "No members" editor. A `type` that is neither an object nor a string throws a `TypeError`.

### Editor structure

```
div.sw-edit.sw-edit-root[.sw-edit-desktop|.sw-edit-mobile][.sw-edit-noborder][.sw-edit-collapsed]
  div.sw-edit-head          title row (Options.Title): expand button, menu button, title (copy on click), header extras
  div.sw-edit-body          member rows
    div.sw-edit-row[data-member]   .sw-edit-label > .sw-edit-name | .sw-edit-icon | .sw-edit-value
    div.sw-edit-data        nested content (objects, collection items: div.sw-edit-entry > .sw-edit-entryvalue > child editor)
```

Root editors without a forced style switch between the desktop and the mobile layout by their own width (`Sw.Edit.MobileWidth`, 520 px; a ResizeObserver, css only, no re-render). Child editors use their root's layout.

### Options and presets

`new Sw.Edit.Options()` gives the defaults (a framed editor with a title row, Dev tool tips and type hints). The presets are shared instances: always `Clone()` them before changing a field.

| Preset | Title row | Frame | Hint lines | Dev tool tips | Use |
|---|---|---|---|---|---|
| (defaults) | yes (expand, menu, title) | yes | yes | yes | A stand alone editor (a tab page, a demo / debug page) |
| `Clean` / `CleanReadOnly` | no | no | no | no | Embedded values (a dialog, a list cell) |
| `CleanHints` / `CleanHintsReadOnly` | no | no | yes, for people (`PlainHints`) | no | A settings form in a card or dialog (the hint line says "1 to 10", "up to 20 characters", "7 to 365 days"; no type names) |
| `CleanMenu` / `CleanReadOnlyMenu` | no (a menu button per member) | no | no | no | Embedded values with member menus |

**`ExpandObjects` (default `false`, opt in, forms for people).** Nested objects that exist (a page's Title / Text / Media, a quiz's Layout / Options) start expanded as normal grouped fields instead of a collapsed row with a one line summary (the summary is hidden while open; the expand button still collapses it; a `null` object stays "Set a value"). It reaches every nested editor and the items of collections, and an item of a list of plain values reads "Item 1", "Item 2" instead of "[0]", "[1]". Combine with `PlainHints: true`, `Title: false`, `CanExpand: false`.

**In a card.** An editor placed directly in a card body (`Sw.Card`: `section.sw-card > div.sw-card-body > editor.Element`) uses the card as its frame, with any options (Design v2, no card in a card): it gets `sw-edit-incard` (no own frame), and when the card has a title its title row (the editor menu: copy / paste / JSON / set default of the whole value, and its drag / drop) moves into the card's tools (`.sw-edit-head-incard`: the menu button only; the card title names the value). Nothing else changes (rows, hints, Dev tool tips). The class `sw-edit-framed` on `editor.Element` before it is shown keeps the old framed look; an editor with `CanExpand` and `IsExpanded: false` keeps its own title row (the expand button). `card.SetTools(...)` after the editor was shown replaces the tools, so add card tools first.

```js
const card = Sw.Card({ Title: "Desk settings", Span: 6 });
const ed = new Sw.Edit("Globex.DeskSettings");               // default options: the menu joins the card head, no inner frame
card.BodyE.append(ed.Element);
await ed.SetObject(settings);
card.E.append(new Sw.Edit.FormFooter(ed, { Actions: [saveButton] }).E);

const plain = new Sw.Edit("Globex.DeskSettings", null, Sw.Edit.Options.CleanHints.Clone()); // no menu at all, hints kept
```

### Widths, hints, validation messages

- **Control widths by type** (desktop layout; the mobile layout uses the full width): number boxes start at `--sw-edit-number-w` (9rem) and grow with their value (`Sw.Edit.FitNumberBox(input)`, `--sw-edit-number-len`) up to `--sw-edit-number-max` (20rem; wider values show the whole value as tool tip); short values (enum selects, date / time, colors, durations, e-mail / phone) up to `--sw-edit-field-w` (22rem); one line texts and urls up to `--sw-edit-text-w` (40rem); multi line texts and nested data use the full width. Override the tokens on `.sw-edit` or a container (`.my-form .sw-edit { --sw-edit-text-w: 30rem }`).
- **Rows** are calm like the data grid: no tinted blocks, a hairline between rows (`--sw-edit-rule`), the focused row gets the hover tint.
- **Hint line** (`Sw.Edit.HintElement(Sw.Edit.MemberHint(member, options))`): every " · " part is a `span.sw-edit-hintpart` that never breaks inside ("2020-01-01 to 2030-12-31"); the line wraps between parts, a part wider than the label column ends with an ellipsis, the tool tip has the whole hint. The default (developer) hint starts with the short type ("Int32 · 1 to 500", "String · max 64 chars"). With `PlainHints: true` (the `CleanHints` presets) the hint is for people (`Sw.Edit.PlainHint`): no type names or raw specs, only the format and the limits in words: "up to 20 characters", "7 to 365 days", "1 s to 1 h", "up to 6 items", "e-mail address", "web address", "no transparency", "one or more", "optional", or nothing. A handler's `Hint(member, options)` (plug-ins) replaces both.
- **Validation**: `await ed.Validate(true)` returns `[path, message]` and **shows the error once itself**: it focuses the field and shows the message under it (`div.sw-edit-fielderror`, role alert, linked by `aria-describedby`, `aria-invalid` on the input; the member name is not repeated there: "Text too long: ...") until the value changes or the next `Validate(true)`; only when that field can't be seen (a hidden or collapsed member, an editor that is not shown) it shows a toast instead. The result has `Shown: true`: **do not toast it again** (`SwWeb check --design` reports `Sw.Fail(err[1])` after `Validate(true)` as `design-error-twice`). `Validate()` / `Validate(false)` only returns the error; its message starts with the member's display name ("Desk name: Text too long: ..."), so show `err[1]` (never `err[0] + ": " + err[1]`: the path is internal, "Edit.Text"). Nested members read "Box › Serial: ...".

### Type handlers

`Sw.Edit.GetTypeHandler(typeName, member)` resolves (and caches):

1. a member spec `X[:args]` (`member.Type`, except `Hide:` / `Allow.`): `Sw.Edit.RegisterSpec(X, factory)` or the global `EditTypeXFactory(typeName, specParts)`,
2. the type map: `System.SByte/Int16/Int32/Int64/Byte/UInt16/UInt32/UInt64` -> Integer (64 bit limited to the safe integer range), `Single/Double/Decimal` -> Number, `Boolean`, `DateTime/DateOnly/TimeOnly` -> DateTime, all also as `System.Nullable`1[[...]]`, `String`, `Object` (read only String), plus `Sw.Edit.Register(typeName, handler)`,
3. by member flags: `Collection` -> Collection, `IsObject` -> Object, `IsEnum` -> Enum,
4. Default (read only text).

Unknown specs (`Country`, `Currency`, `Inline`) fall back to the base type's handler. A spec factory may return null for member types it does not handle (ex: `Color` on a `String[]`, `Email` on a non string), then the normal lookup applies.

Handler contract (duck typed; `Sw.Edit.Handler` is an optional base class):

| Member | Description |
|---|---|
| `CreateDefault(member, options)` | Value for a new object / item |
| `GetDefault(member, options)` | The declared default |
| `IsOfType(obj, member, options)` | A pasted / dropped value is usable |
| `Condition(obj, member, options)` | Convert / clamp an accepted value |
| `async Validate(obj, member, options, path)` | `null` or `[path, message]` |
| `ToString(obj, member, options)` | Display text |
| `Hint(member, options)` | Optional: the hint line under the member name (a string, `""` = none; null / undefined = the automatic hint). Plug-ins use it to say what the value is in words ("0 to 5 stars"); never a type name |
| `async AddEditor(obj, editor, editContext, member, title, options)` | Build the editor in `editContext.Element` (`editContext`: `Row`, `PrevRow`, `Element`, `Icon`, `ReadOnly`, `CreateData()`), write the default into `obj[member.Name]` when undefined, return the item |
| `IgnoreMembers`, `ForceReadOnly` | Optional flags |

The item: `SetValue(v)` (store + EditChange), `UpdateValue(isFirst)` (refresh after undo / redo, focuses unless first), `Focus` (an element: the control that gets the focus), `Title` (constraint lines of the tooltip), `DefValue`, `DefValueText`, optional `DataRow`, `AddMenuItems(menu, close)` (push Sw.Menu items, or old `WebMenuItem` shaped items, to `menu.Items`), `FindItem(path)`, `GetValue()`. The layout adds `Hide()`, `Show()`, `Icon`, `Row`; the engine adds `TypeHandler`, `TitleElement`, `ReadOnly`, and for every handler names the `Focus` control after the member (`aria-labelledby` the member name) when it has no name of its own (`aria-label`, `aria-labelledby`, a `label`).

Handlers in short:

| Handler | Editor | Notes |
|---|---|---|
| String | `input.sw-input` / textarea (Multiline) / password | `Max` = maxlength; paste truncates and removes line breaks (single line); "\t" default = "" |
| Integer / Number | filtered text box (digits, leading "-" when negative values are allowed, one "." for floats) + `Sw.Slider` for Slider members with both limits; UI font, tabular figures, one width (`--sw-edit-number-w`, 9rem); read only: the number as text, no slider | step from `EditParams` or a power of ten (<= 100 steps, floats 10..100); `Max: null` = type range |
| Boolean | `Sw.Toggle`; read only: "Yes" / "No" text | paste accepts true / false, "true" / "false", 0 / 1 |
| Enum | `Sw.Select` (searchable for long lists); flags: toggle chips (bits merged); read only: the value name as text (flags: the exact name, ex: "All", else the set names "Read, Execute") | values from `Default` = `def>value<name<summary<remarks|...`; `Allow.<Member>` disables values whose bit is off in `<Member>`; paste accepts numbers, names, display names, `A + B` |
| DateTime | native date + time inputs (DateOnly / TimeOnly only one) | local display, ISO storage, DateUnspecified without conversion, Min / Max day range, nullable: empty = null |
| Object | expand button + summary, child editor in the data row | expanding null creates the default object (undoable) |
| Collection | expand button + count + "+ Add", one child editor per item, "+ Add item" row | see Collections below; dictionaries (`KeyTypeName`): add asks for the key; fixed when read only or Min >= Max |
| Language (spec) | flag + name button, searchable paged selection; read only: flag + name as text; without the IsoDataService (Api/HaveService probe): a two letter code text box, nothing under `iso_data/` is requested (no 404) | recent picks in `localStorage["SysWeaver.PopUpRecent.Language"]` |
| Default | read only text | |

### Client types (TypeDesc)

The editor renders a **TypeDesc**: the json of the C# `TypeDesc` / `TypeMemberDesc` (TypeDesc.cs, TypeMemberDesc.cs of SysWeaver.MicroService.Edit) that `edit/GetTypeInfo` returns. A page can describe its own types with the same shape and register them with `Sw.Edit.AddType(type)` (client types win over the server; the first registration of a name wins). Every value that the server sends as text (`Min`, `Max`, `Default`) is a **string** in a client type too.

```js
const F = Sw.Edit.Flags;
Sw.Edit.AddType({
    TypeName: "Acme.Device", DisplayName: "Device", Flags: F.IsObject, Summary: "A device", Asm: "fleet.js",
    Members: [
        { Name: "Name", DisplayName: "Name", TypeName: "System.String", Flags: F.IsPrimitive, Max: "32", Default: "Pump" },
        { Name: "Retries", DisplayName: "Retries", TypeName: "System.Int32", Flags: F.IsPrimitive | F.Slider, Min: "0", Max: "10", Default: "3" },
        { Name: "Mode", DisplayName: "Mode", TypeName: "Acme.Mode", Flags: F.IsPrimitive | F.IsEnum, Default: "1>0<Off<Turned off|1<On" },
        { Name: "Serial", DisplayName: "Serial", TypeName: "System.String", Flags: F.IsPrimitive | F.ReadOnly },
        { Name: "Poll", DisplayName: "Poll interval", TypeName: "System.Int32", Flags: F.IsPrimitive, Type: "Unit:Seconds", Min: "1", Max: "3600", Default: "30" },
        { Name: "Tags", DisplayName: "Tags", TypeName: "System.String[]", ElementTypeName: "System.String", Flags: F.Collection | F.Indexed, Max: "5", ElementInst: { Flags: F.IsPrimitive, Max: "16" } },
        { Name: "Limits", DisplayName: "Limits", TypeName: "System.Collections.Generic.Dictionary`2[[System.String],[System.Int32]]", KeyTypeName: "System.String", ElementTypeName: "System.Int32", Flags: F.Collection, ElementInst: { Flags: F.IsPrimitive, Min: "0", Max: "100" } },
        { Name: "Location", DisplayName: "Location", TypeName: "Acme.Point", Flags: F.IsObject },   // another registered (or server) type
    ],
});
```

| Field | On | Description |
|---|---|---|
| `TypeName` | type, member | The .NET type name: selects the handler (`System.Int32`, `System.String`, `System.Nullable`1[[System.Double]]`, `System.TimeSpan` ...) and, for objects / collection elements, the TypeDesc of the value (`GetType`) |
| `DisplayName`, `Summary`, `Remarks`, `Asm`, `Ext` | type, member | Texts: the label / title, tool tips, the assembly (Dev tooltips), an external info link |
| `Flags` | type, member | `Sw.Edit.Flags` bits: `IsPrimitive` (a value), `IsObject` (members), `Collection` (+ `Indexed` for lists / arrays; without it a dictionary), `IsEnum` (+ `IsFlags`), `ReadOnly`, `AcceptNull`, `Hide`, `Multiline`, `Password`, `Slider`, `DateUnspecified` |
| `Members` | type | The members (`TypeMemberDesc`) in display order |
| `Name` | member | The property name in the value object |
| `Min`, `Max` | member | Strings: number range, text length (`Max`), item count (collections), date range (`"2020-01-01"`), TimeSpan range (`"00:00:01"`); null / missing = no limit |
| `Default` | member | String: the default as text (`"3"`, `"true"`, `"00:01:30"`), objects / collections as json (`'{"X":1}'`, `'["a"]'`), `"\t"` = the type's empty value. **Enums** pack their values here: `default>value<name<summary<remarks|value<name|...` (ex: `"2>0<Off<Turned off|1<On|2<Auto"`, default 2; flags enums use bit values and may list combinations, ex: `7<All`) |
| `Type` | member | The editor spec `X[:args]`: `Color:Css[:Alpha]`, `Color:HdrRgb::8`, `Unit:Seconds`, `Language`, `Email`, `Url`, `Phone`, `Hide:<expression>` (ex: `Hide:!this.ShowSecret`), `Allow.<Member>` (enums) or a plug-in name (below) |
| `EditParams` | member | Handler parameters (ex: the slider step) |
| `ElementTypeName`, `KeyTypeName` | collection member | The item (value) and key type names |
| `ElementInst`, `KeyInst` | collection member | Per item / key overrides with the member fields (`Flags`, `Min`, `Max`, `Default`, `Type`, `EditParams`), ex: `{ Flags: F.IsPrimitive \| F.ReadOnly }` = read only items |
| `Group` | member | Client types: a heading (`div.sw-edit-group`) before the first member of a group; a heading whose members are all hidden is hidden (`UpdateGroups()`) |
| `Unit` | member | Client types: a plain unit text after a number box ("px", "%", "×"; no conversion, unlike the `Unit:` spec), in a slot of one width on every number row of the editor (`--sw-edit-suffix-w`, class `sw-edit-units` on the editor), so the boxes and sliders line up with and without a unit |
| `Hint` | member | Client types: the hint line under the name (`""` = none), instead of the automatic one |
| `HideIf` | member | Client types: `function(obj)`, true hides the member (re-evaluated on every change, like `Hide:`, but keeps `Type` free for the editor spec) |
| `MenuItems` | member | Client types: `function(obj)` returning (a promise of) Sw.Menu items: the member's own actions in its menu (ex: "Use the play position") |

### Plug-ins (custom editors)

A plug-in edits members whose `Type` spec is its name (C# `[EditType("Name")]` or a client TypeDesc `Type: "Name:arg1:arg2"`):

```js
await Sw.Edit.Ready;                                       // Sw.Edit.Handler is defined by the loaded files
class EditTypeRating extends Sw.Edit.Handler {
    GetDefault(member) { return member.Default ? +member.Default : 0; }   // required: new values and "Set default" use it
    IsOfType(obj) { return typeof obj === "number"; }      // a pasted / dropped value is usable
    Condition(obj) { return Math.max(0, Math.min(5, Math.round(+obj || 0))); }
    async Validate(obj, member, options, path) { return obj >= 0 && obj <= 5 ? null : [path, member.DisplayName + ": 0 to 5"]; }
    ToString(obj) { return (obj || 0) + " of 5"; }          // read only text, the "Set default" hint
    Hint() { return "0 to 5"; }                             // the hint line under the name (optional; "" = none)
    async AddEditor(obj, editor, ctx, member, title, options) {
        const mn = member.Name;
        if (obj[mn] === undefined) obj[mn] = this.GetDefault(member);
        if (this.IsReadOnly(member, options)) {               // read only: text, no control
            const text = Sw.Edit.ReadOnlyText(() => obj[mn], v => this.ToString(v));
            ctx.Element.append(text);
            return Sw.Edit.Handler.Item(obj, editor, member, { Focus: text });   // the framework refreshes it (text.UpdateValue)
        }
        const box = new Sw.NumberBox({ Value: obj[mn], Min: 0, Max: 5, Step: 1,
            OnChange: v => editor.SetMemberValue(member, v, obj[mn], "Set rating") });
        ctx.Element.append(box.E);
        //  Set: called with the current value after every change (undo, redo, Set default, paste, drop, Reset; also its own)
        return Sw.Edit.Handler.Item(obj, editor, member, { Focus: box, Set: v => { box.Set(v, true); box.Render(); } });
    }
}
// app/app.js (app wide): Sw.Extend.EditSpec waits for Sw.Edit, so it is safe on pages without the editor (a pop-up editor that loads edit/Edit.js later gets it too)
Sw.Extend.EditSpec("Rating", (typeName, parts) => typeName === "System.Int32" ? new EditTypeRating() : null);
```

What the framework does for a plug-in (a handler that is not one of the editor's own classes):

- **The shown value is never stale.** `Handler.Item` calls `Set(value)` with the current value after every change that did not come from the control (undo / redo, Set default, Paste, drop, the form footer Reset, a JSON tab). Without `Set` and `Update` it calls `Focus.UpdateValue()` when the element has one (`ReadOnlyText`, `CreateTextInput`), else it renders the member again (`AddEditor` once more).
- **"Set default" works.** When the item has no `DefValue`, the default is `GetDefault(member)` and its text `ToString(default)`. The menu says "This field has no default value." only for handlers that really have none.
- **The control is named.** `Focus` may be an element or a Sw component (`box`: its input is used; a wrapper resolves to its first control). When it has no accessible name of its own it gets `aria-labelledby` = the member name, like the built-in editors. Validation messages are linked to it (`aria-describedby`, `aria-invalid`).
- **The hint line** is `Hint(member, options)` when the handler has it; otherwise the automatic hint, which for a plug-in has no type name in the CleanHints presets and shows the spec name ("Rating") only in the default (developer) hints.

- `RegisterSpec(name, factory)`: `factory(typeName, specParts)` returns a handler, or null for member types it does not handle (the normal lookup applies). `Sw.Extend.EditSpec(name, factory)` = `Sw.Ready("Edit", E => E.RegisterSpec(name, factory))` (core/README.md `Sw.Ready`): use it in `app/app.js` and shared scripts; `Sw.Edit.RegisterSpec` directly only in a page that loads edit/Edit.js. The old global hook `window.EditType<Name>Factory` still works.
- `Register(typeName, handler)` replaces the handler of a .NET type everywhere.
- The handler contract is the table under "Type handlers"; `Sw.Edit.Handler` gives `IsReadOnly(member, options)`, `CreateDefault` (calls your `GetDefault`), `Condition`, `ToString`, `static Item(obj, editor, member, o)` and `static NullCheck`. Read only members must not render controls that look usable: use `Sw.Edit.ReadOnlyText` (or `CreateTextInput` / `CreateSelectionInput` with `readOnly`, which return text).
- Plug-ins that ship in this folder load on demand (`Sw.Edit.BuiltInPlugins`, `EditType<Name>.js`); a plug-in that needs data sets `Sw.Edit.PluginReady[name]` to a promise. Optional services must be probed before their files are requested (EditTypeLanguage.js: `Api/HaveService`), so an app without the service gets no 404.

### Menus, clipboard, drag and drop

- Member menu: click / tap the title, right click, long press (touch), the context menu key or Shift+F10 anywhere in the row, or the optional menu button (`Options.MenuIcon`). Items: Set default (with the default as hint), Copy (disabled for passwords), Paste (the clipboard is read when the menu opens; the hint previews the value, the tooltip explains why it is disabled), handler items, Copy name (Dev: Copy member name / Copy type name).
- Object menu (title row menu button, long press / right click on the title row): Set default, Copy (JSON with `$type`), Paste, editor supplied items (collection items).
- Copied / dragged values: strings raw, everything else JSON, objects with `$type`. Pasted / dropped values are accepted when the `$type` matches, null is allowed, or the handler's `IsOfType` accepts them; they are then conditioned. Every member title and the title row are drag sources (`application/json` with the JSON value, a string as a JSON string; `text/plain` with the copy text, a string raw) and, unless read only, drop targets (undoable "Dropped" steps; a string member reads a dropped JSON string as the string).


### Read only, null

- **Read only** (the `ReadOnly` option, a `ReadOnly` member / type, an API result): text values are shown by `Sw.Edit.ReadOnlyText` (a focusable, selectable `span.sw-edit-rotext`, no `<input>`), so no caret, typing, paste or drop can change them; an empty text shows a quiet "empty" token (`sw-edit-rotext-blank`, the shape of the null token; `Sw.Edit.EmptyText()`), never a blank. **Read only members render display text, not disabled controls** (Design v2, PORTING.md B14): enums and flags as their names, booleans as "Yes" / "No", numbers without a slider, colors as the swatch (true color, no button look) and the text without the alpha slider / intensity box, the language as flag + name. A form footer (`Sw.Edit.FormFooter`) of an editor where nothing is editable (`FormFooter.NothingEditable`: the ReadOnly option, a read only type, or every visible member read only) is hidden; `ShowReadOnly: true` keeps it with the actions only (Reset and the count hidden; explore/api.html keeps Send this way). Read only state is inherited by nested objects, collection items (also a read only element type) and dictionary values; collections lose every structural control (Add, item buttons, drag handles, item menu entries are disabled). Member and title row menus, Ctrl+V and drop refuse with "Value is read only". **A writable value never overwrites its read only parts**: every path that replaces a whole value or a member value (the title row Paste / Ctrl+V / drop, Set default, the form footer Reset, a member Paste / drop, "Paste as new item", explore/api.html's JSON tab) goes through `Sw.Edit.KeepReadOnly` / `KeepReadOnlyMember`: read only members keep their current values at every depth (nested objects, collection items, an object member of a read only or unknown type, the Default handler); a collection whose element type is read only accepts only re-ordered / removed old items and new default items; new pasted items get the defaults of their read only members and lose `$type`. One warning toast names what was kept ("2 read only values kept their values: Fixed text, Box.Serial."). Undo / redo replay the merged value. Read only labels are not italic: a small lock follows the name (a whole read only editor shows the lock on its title row only).
- **Null**: null objects and collections show a dashed mono `null` token; editable ones get "Set a value" (objects: the default object; fixed collections: an empty collection; editable collections have "+ Add", which also creates them). A read only editor whose whole value is null (`SetObject(null)`, ex: an API result) shows `null` and "No value (the object is null)." instead of an empty default object.

### Collections

- Head: expand button, count (or the null token), **"+ Add"** (also when collapsed). Expanded: the items and a dashed **"+ Add item"** row at the end (dropping an item on it adds it).
- Items (indexed, not fixed): a **drag handle** (pointer drag with mouse, touch or pen re-orders, a focus colored line shows the target; the handle's arrow keys Up / Down / Home / End move the item), remove and insert-after buttons; **Alt+Up / Alt+Down (Alt+Home / Alt+End)** anywhere in an item moves it. Item menu: Remove, Insert before / after, **Duplicate**, Move to top / up / down / bottom. Every move is one undo step and keeps the focus on the moved item.
- Copy / paste between lists: copy an item (its title row menu / Ctrl+C on its title row buttons, primitive items: the member menu / Ctrl+C) and paste it on another list with the member menu **"Paste as new item"** (also a json array of items), Ctrl+V on the list row, or drop it on the list title / "+ Add item". Objects without `$type` (copied from a json text, ex: the JSON tab of explore/api.html) are accepted when every property is a member of the element type. Dictionaries ask for the key of each pasted item.

### Keyboard

Ctrl+Z / Ctrl+Y undo / redo; in a member row outside text fields (toggles, selects, buttons, the title) **Ctrl+C / Ctrl+V** copy / paste the member value (text fields keep the native keys), on the title row buttons the whole value; the context menu key / Shift+F10 opens the member (title row: object) menu; Alt+Up / Alt+Down move collection items.

### Colors (EditTypeColor.js)

Selected by the member spec `Color[:<Format>[:Alpha][:<MaxIntensity>]]`, written by the C# attribute `[EditColor(EditColorFormat format = Auto, bool alpha = false, float maxIntensity = 16)]` (SysWeaver.Common, `EditColorAttribute.cs`; constant `EditTypes.Color`). The value cell: a swatch (`Sw.ColorPicker`; a checkerboard shows through transparent colors), a mono text box with the stored form (any css color, `0x` hex or 3 to 4 numbers can be typed or pasted; invalid text is marked and reverted), an **Alpha** slider (formats with alpha) and an **Intensity** box (HDR formats: the stored rgb is color × intensity). Picker drags and slider drags are one undo step each.

| Format | Stored as | Example (C#) |
|---|---|---|
| `Auto` | String = Css, integers = UInt32Argb, `byte[]` = Rgb8 / Rgba8, float / double arrays = Rgb / Rgba (by length), objects with R, G, B(, A) = a float struct | `[EditColor] public String Accent;` |
| `Css` | `"#rrggbb"` (`"#rrggbbaa"` with `alpha: true`); a typed css color (named, `rgb()`, `hsl()`) is kept as typed | `[EditColor(EditColorFormat.Css, true)]` |
| `Hex` | like Css, but always stored as `"#rrggbb"` (`"#rrggbbaa"` with alpha): typed css colors are converted (for servers that read only hex) | `[EditColor(EditColorFormat.Hex)]` |
| `Rgb`, `Rgba` | `float[3]` / `float[4]` in 0..1 | `[EditColor(EditColorFormat.Rgba)] public float[] Tint;` |
| `HdrRgb`, `HdrRgba` | float arrays, rgb above 1 up to `maxIntensity` | `[EditColor(EditColorFormat.HdrRgb, maxIntensity: 8)]` |
| `Rgb8`, `Rgba8` | `byte[3]` / `byte[4]` in 0..255 | `[EditColor(EditColorFormat.Rgba8)] public byte[] Pixel;` |
| `Struct8` | an object with integer `R`, `G`, `B` in 0..255 and optionally `A` (0..255) or `Opacity` (0..100 percent) | `[EditColor(EditColorFormat.Struct8, true)] public CssColor Text;` or `Sw.Edit.Register("My.CssColor", new Sw.Edit.Handlers.Color("My.CssColor", ["Color", "Struct8", "Alpha"]))` for every member of a type |
| `UInt32Rgb` / `UInt32Rgba` / `UInt32Argb` / `UInt32Abgr` | one integer `0xRRGGBB` / `0xRRGGBBAA` / `0xAARRGGBB` (System.Drawing `ToArgb`; an `int` member stores the signed bit pattern) / `0xAABBGGRR` (RGBA bytes, little endian) | `[EditColor(EditColorFormat.UInt32Rgba)] public uint Color;` |

A `String[]` of colors is a collection: put the attribute on the element (`EditElementAttributes`); numeric arrays are one color.

Typed text: `0x` hex is read in the member's own integer layout (the form the text box shows), else as `0xRRGGBBAA` (the order of css `#rrggbbaa`; up to 6 digits are `0xRRGGBB`, opaque). A list of 3 or 4 numbers is read as 0..255 values when every number is whole and one is above 1 (above the maximum intensity for HDR) or the alpha is above 1, as 0..1 values when one has a fraction, else in the format's own scale; values outside the scale are refused. A note after the box says how the text was read when that differs from the format ("Read as 0 to 255 values", "Read as 0xRRGGBBAA"). A member without alpha (Css without `alpha`, Rgb, HdrRgb, Rgb8, UInt32Rgb, Hex without alpha) says "no alpha" in its hint line and warns when a transparent color was typed ("No alpha in this value: stored opaque (50 % opacity dropped)") instead of dropping the alpha silently.

### Other editors

- **TimeSpan** (`System.TimeSpan`, also nullable; EditTypeTimeSpan.js): a text box that takes the .NET "c" format (`00:01:30`, `1.02:03:04.5`) or units (`90s`, `5m`, `1h 30m`, `2d`, `250ms`, a plain number = seconds), stores the "c" format (as SwJson writes it) and shows the readable duration after the box ("= 1 min 30 s"); Min / Max ("c" strings) are enforced: a value outside them is set to the limit and the hint says so in the warning colour ("Set to the maximum 1 d (2 d is too long)") until the value changes. Before, TimeSpan members were read only.
- **Guid** (`System.Guid`): a mono text box (validated, braces / no dashes accepted, stored lower case "D") and a **New** button (+ "New id" in the member menu).
- **Numbers with a unit** (`Unit:<Unit>` spec, C# `[EditUnit(EditUnits.Seconds)]`, EditTypeUnit.js): any number member (int, long, double ...) keeps a plain number in its unit; the box takes typed units (durations `90s`, `1h 30m`, `2d`, `250ms`; byte sizes `2 MB`, `512k`, `1.5 GiB` (1024 based); percent `25%`), the unit follows the box ("s", "MB", "%"), the hint shows the readable form ("= 1 h 30 min", "= 32 MB", Ratio "= 25 %"), Min / Max apply (set to the limit with a warning hint), the Slider flag adds a slider. Units: Milliseconds, Seconds, Minutes, Hours, Days, Bytes, KiloBytes, MegaBytes, GigaBytes, Percent (0 to 100), Ratio (0 to 1). The hint line under the name: "Int32 · 1 to 86400 s".
- **Email / Url / Phone** specs (`EditTypes.Email`, `EditTypes.Url`, `EditTypes.Phone`, EditTypeSpecs.js): the string editor with the matching phone keyboard (inputmode), a format check while typing and in validation (empty is valid; relative urls are allowed).
### Undo / redo

One `UndoStack` per editor tree. Changes are recorded with dotted paths (`Edit.Member[2].Sub`) and re-found from the root on undo / redo (expanding collapsed parents). Continuous typing in one input (one focus session) and one slider drag are one step. Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z (Cmd on Apple) in an editor, and outside any text field of the page for the last used editor. `UndoStack.OnChange(fn)`, `UndoTitle()`, `RedoTitle()` serve undo buttons.

### Events

`EditChange` (CustomEvent, not bubbling) on the editor element with `detail {Next, Member, Value, Key, KeyName}`; parent editors re-dispatch child events chained by `Next`. Password values are reported as `"***"`.

### Text editor page

**Layout (Design v2 fill page):** `Sw.PageLayout` "fill" (`main.sw-page-fill.text-main`): the shared compact page header (in the app shell, ex: Monitoring > Log file: the menu name as h1 and the file name as description; stand alone: the file name as h1 and the file type as description; the modified marker follows the name, its tool tip is the read url) with the file actions Reload, Beautify (json / json5 / css / html / markdown / javascript), Download, Save (primary while modified) and Delete (always in the "More actions" kebab menu: `sw-ph-in-menu`; actions that do not fit move there too), then one card (`section.sw-card.sw-fill.text-card`) = a tool bar of ghost icon tools (find, problem tools of logs, the status, Wrap lines, Follow, Scroll to the end) above a full height Ace editor (line numbers, folding, indent guides, auto indent, live autocompletion with snippets, search / replace, multi cursor; the syntax worker only runs when editable). Ctrl+S saves (when `u` is given); leaving with unsaved changes asks first. Save posts `{Url, Name, Content}` to `u`, Delete (confirmed with the shared `Sw.Cells.ConfirmDanger`: "Delete the file?", "File: name", "This cannot be undone.", Cancel / Delete; the button spinner only runs after the confirmation, also for Reload with unsaved changes) posts `{Url, Name}` to `d`; InterOp `FileSaved` / `FileDeleted`. **The server log** (`d` = `.../logFile/DeleteLogFile`): Delete first asks the log service for a one time delete token (`Api/logFile/GetDeleteLogFileToken` -> `{Token, Name, Size, LastWrite, Warning}`) and confirms with "Delete the log file?", the facts Size / Lines / Last written, the server's warning (the history of the server is lost) and a type to confirm field ("delete", SAF-2 / SAF-4); the delete posts `{Url, Name, Token}`. A server without the token api gets the plain confirmation. After a delete the result is the "deleted" state in the card (announced, no toast) and the focus goes to Reload. The mode comes from `t` or the file extension (c / c++ / cpp / h / hpp -> c_cpp, cs -> csharp, css, csv, glsl, htm / html -> html, js -> javascript, json, md -> markdown, err / txt / cfg -> plain_text, log -> sw_log, bat / cmd -> batchfile, sql -> mysql, svg, xml / config / csproj -> xml); without `t` a plain text whose lines are SysWeaver log lines (`LooksLikeLog`: at least one and at least half of the first 200 unindented lines are "HH:mm:ss Level" header lines, ex: `Api/logFile/LogFile.txt` of Debug > Log file) also gets `sw_log` (the text is fetched once, before the page is built), and `t=plain_text` shows a log as plain text. The tool bar status shows the line count of read only logs ("1138 lines"), else the friendly file type name and the cursor ("JavaScript · 12:4"; tool tip "File type: <name> (<ace mode>)", the line count and the cursor line and column). **Scroll to the end** jumps to the last line. A "Wrap lines" toggle (soft wrap; default from `wrap`, else the remembered choice in localStorage `SysWeaver.TextWrap`, else on for narrow screens and logs). A visible **find field** (all texts, at the start of the tool bar) searches the editor (case insensitive, literal; Ace find with all matches marked, the match count "3 / 12" or "No matches" inside the field), with Previous / Next match buttons; Enter / F3 next, Shift+Enter / Shift+F3 previous, Escape back to the text. Ctrl+F (in the editor or anywhere on the page) focuses it with the selected text; Ctrl+F in the field opens Ace's full search box (match case, whole word, regular expression). **Follow** (toggle, read only logs and `scrolltoend` texts; off by default, `follow=true` turns it on): reloads every 5 s while the page is visible (the log apis are cached 4-5 s on the server), adds only the new lines when the file grew (`Append`; view, selection and folds stay) or shows the file anew when it was replaced, and keeps the end in view; when the user scrolls away from the end it pauses (pause icon, dashed border, tool tip says so) and resumes at the end; failures show one message per streak. **Logs** also get Previous problem / Next problem (Shift+F8 / F8: the previous / next Warning, Error, Fatal or Critical entry from the cursor, wrapping; disabled with "This log has no warnings or errors." as tool tip, else the count) and a "Problems only" toggle that folds every line outside the problem entries ("··· 120 lines ···"; a click on a fold opens it); both follow reloads and Follow. On phones (portrait) the header keeps only its action row (the top bar names the page; ui/page.css), the card is edge to edge and the tool bar has two rows (the find field, then the tools); on low screens (phones in landscape, <= 500 px high) the header actions show their icons and lie over the right end of the tool bar row (`--text-ph-w`), so the text keeps the height. **States:** a read only text without content shows an empty state in the card ("The log is empty" / "The file is empty"), a failed load an error state (what failed, Try again, Details with the technical text; no toast), a link without `r` or with a foreign url an error state, a deleted file a "deleted" state in the card. **While the text loads** the card already has its final size: the editor area shows a skeleton (`Sw.StateView` loading, `.text-loadstate`) and the editor is laid out hidden (`.text-loading`), then shown at its final place (the end of a log already in view), so nothing moves (LAY-1 / LAY-2: CLS 0 with every api delayed); meanwhile the file actions and tools are disabled with the reason "Wait until the log is loaded" / "Wait until the file is loaded" (a role guard keeps its own reason). Log date lines and "== File log started ==" lines are separator bands (Ace full line markers, `.text-log-sep`). Read only `sw_log` logs are shown compact (`CompactLog`): header lines lose the dot leader (". . . .[Source]     message" -> "[Source] message"), continuation lines (message lines, stack frames) get a fixed indent of one tab instead of their 17-52 leading spaces and lose the repeated source; the line numbers stay the same, and a copy (Ace "copy" event, `RawText`) and Download give the raw file text. Wrapped log rows are indented one tab more than their line (Ace "code" wrap with indented soft wrap; editable logs keep their text and use `HangingIndent`), and long paths and urls wrap after "/" and "\" instead of before "://" or inside names (`LogBreakPoints`; a token longer than the row is still split). Logs show no fold widgets, and on phones the gutter is narrow. With `scrolltoend` the end stays in view while the wrapped rows settle (up to 2 s, until the user scrolls or types). Logs use the normal text size (`--sw-size`), code the code size (`--sw-mono-size`). The `sw_log` mode (defined by text.js) colors SysWeaver log lines by level (Debug muted, Info normal, Warning amber, Error red, contrast >= 4.5:1 in all themes) with the level word as a tinted badge (Debug neutral, Info the info hue, Warning / Error their soft state colour); continuation lines (stack traces) keep the level of their header line. The `ace-sysweaver` theme replaces Ace's default theme and follows the Sw tokens (also the search box, tooltips and the autocomplete popup, which Ace appends to `body`).

### Server interaction

| Call | Used by |
|---|---|
| POST `edit/GetTypeInfo` (type name) -> TypeDesc / null | `Sw.TypeInfo.GetType` (cached per page, also null) |
| POST `edit/GetDefaultInstance` (type name) -> json / null | `Sw.TypeInfo.GetInstance` (cached as json, a fresh copy per call) |
| GET `r`, POST `u`, POST `d` | text page |

## JavaScript reference

### `Sw.Edit` (Edit.js; legacy global `Edit`)

| Member | Description |
|---|---|
| `new Sw.Edit(type, style, options, parent, member, evListener)` | Create an editor (`type`: a TypeDesc, or a type name resolved by SetObject from `AddType` / the server, unknown = error state + console error; `style`: "Desktop", "Mobile" or falsy = automatic). Usable before `Ready` |
| `Element`, `Type`, `Options`, `Parent`, `Member`, `Object`, `MemberMap`, `UndoStack`, `Style`, `Key`, `KeyName`, `ObjName`, `AddMenuItems` | State |
| `await SetObject(obj)` | Set the value (in place; undefined = default) and render |
| `GetObject()`, `GetCleanCopy(src)` | The value (primitives unwrapped), a copy with declared members only |
| `await Validate(focusOnError)` | `null` or `[path, message]`; focuses and flashes the row |
| `IsObjectReadOnly()`, `ReplaceObject(value, title)`, `PasteObject()`, `PasteObjectDialog([text])`, `CopyMember(member)`, `PasteMember(item, member)` | Whole value read only test; undoable replace of the whole value; clipboard of the whole value / a member (toasts say why not); when the page can't read the clipboard (no permission, not a secure context) or the clipboard has no valid value, the title row menu's "Paste..." (and Ctrl+V on the title row) opens a paste dialog (`Sw.Dialog.Prompt`, json for objects, checked before it closes), so the whole value can always be replaced |
| `Invoke(do, undo, title, name, tracking)`, `SetMemberValue(member, new, old, title, tracking)` | Undoable changes |
| `FindItem(path)`, `GetObjectName()`, `ChangeTitle(name)`, `UpdateValue()`, `NotifyChange()`, `Changed(member, value)` | Internals used by handlers |
| `ShowMemberMenu(item, member, ev)`, `ShowObjectMenu(ev, anchor)`, `GetCopyObject(json)`, `GetCopyMember(member, json)`, `CanConsumeCopy(obj)`, `ParseObject(text)`, `SetTypeInfo(...)` | Menus and clipboard |
| `Sw.Edit.Init()`, `Ready` | Load the editor files |
| `new Sw.Edit.FormFooter(editor, { Actions, ResetText, OnReset, ShowReset, ShowReadOnly, Class, Badge })`, `SetBaseline()`, `Changes()`, `ResetAll()`, `ReadOnly`, `static NothingEditable(editor)` | The sticky form footer: Reset, the change count, the actions (hidden when nothing is editable, see Read only) |
| `FromServer(typeName, style, options, props, obj)`, `Boolean(name, value, title, onChange, readOnly, showMenu)` | Shortcuts |
| `AddType(type)`, `GetType(name, props, jsOnly)`, `Types` | Type descriptions (client registered first) |
| `CreateDefault(type, options, setTypeName)`, `CleanUp(type, obj)`, `IsPrimitive(t)`, `IsReadOnly(member, options)`, `IsValid(v)`, `IsTrue(v)`, `GetIntParam(v, def)`, `IsDifferent(a, b)`, `CloneType(src, props)`, `CloneValue(v)` | Helpers |
| `MemberHint(member)`, `HintElement(hint)`, `FitNumberBox(input)`, instance `AdoptCard()` | The hint line text / element (parts that never break inside), a number box sized to its value, the in-card frame rule (runs by itself when the editor is shown) |
| `ValidateType(type, obj, options, path)` | Validation |
| `await CheckJson(type, value, options)`, `JsonLines(text)`, `JsonKind(v)` | Type check of a parsed json value at every depth (numbers and integer ranges, booleans, strings, enums / flags names or numbers, dates, TimeSpan "c", Guid, collections, nested objects with unknown members, null only where allowed; editor specs by their handler): `null` or `{Path, Text}`; `JsonLines` maps json paths ("Filters[0].Op") to 1 based lines for markers (explore/api.html's JSON tab) |
| `await KeepReadOnly(type, newVal, oldVal, options, path, kept)`, `KeepReadOnlyMember(member, value, old, ...)`, `KeepReadOnlyItem(type, item, ...)`, `IsMemberReadOnly(member)`, `ReportKept(kept)`, `KeepMemberReadOnly(member, value)` (instance), `StripType(v, type)` | Keep read only parts in a replacing value (see Read only); `kept` gets the display paths, `ReportKept` shows the one toast |
| `CreateChangeEvent(member, value, next, key, keyName)` | The event |
| `ClipboardKey(ev)`, `IsObjectOf(obj, type)`, `ReadOnlyText(getValue, toString, multi)`, `GetTypedTextOrJson(obj, json, typeName)`, `ParseValue(text, member, handler, options)`, `DragSource(el, getJson)`, `DropTarget(el, onDrop)`, `ShowMenu(ev, anchor, items, label)`, `Short(text)` | Clipboard, drag / drop, menus |
| `CreateTextInput(...)`, `CreateGenericInput(...)`, `CreateSliderInput(...)`, `CreateSelectionInput(...)`, `BindInput(...)` | Input helpers (old signatures) |
| `SetToString(el, obj)`, `SetCollectionCount(el, collection, strategy)` | Summaries |
| `Register(typeName, handler)`, `RegisterSpec(spec, factory)`, `GetTypeHandler(typeName, member)`, `GetTypeMap()`, `Shared(name, create)` | Handler registry |
| `Handlers`, `Collections`, `Layouts`, `Handler`, `NumberHandler`, `Flags`, `OptionDefaults`, `MobileWidth`, `BuiltInPlugins`, `LastEdit`, `Util` | Registries and constants |

### Other files

| File | API |
|---|---|
| Edit.js (EditOptions.js is a compatibility stub) | `Sw.Edit.Options` (`EditOptions`, usable before `Ready`): `Title`, `Dev`, `ReadOnly`, `CanExpand`, `IsExpanded`, `KeyName`, `Border`, `ExpandAllCollections`, `ExpandObjects`, `MenuIcon`, `MenuIconClass`, `MenuColorClass`, `AddToHeader`, `MarkChanged` (a member that is not at its default gets `sw-edit-changed`: an accent name and a dot), plus `Name`, `UndoStack`; `Clone()`; presets |
| UndoStack.js | `Invoke`, `Undo`, `Redo`, `CanUndo`, `CanRedo`, `UndoTitle`, `RedoTitle`, `OnChange(fn)`, `Clear`, `Pos`, `Ops` |
| EditDesktop.js / EditMobile.js | `Sw.Edit.Layouts.Desktop/Mobile`: `AddTypeInfo(editor, head, type, title, options)`, `AddMember(obj, editor, body, member, title, options, isLast)`; classes `EditDesktop`, `EditMobile` |
| EditCollectionArray.js / EditCollectionMap.js | `Sw.Edit.Collections.Array/Map` (`EditCollectionArray.Inst`, `EditCollectionMap.Inst`): `Indexed`, `createVal`, `countVal`, `getVal`, `setVal`, `getKeyName`, `keys`, `iterate` (old name `íterate`), `onKeys`, `addVal`, `removeVal`, `insertAt` (+ arrays: `insertAfter`, `move`; maps: `hasKey`) |
| EditType*.js | `Sw.Edit.Handlers.X` and the legacy globals `EditTypeX`; `EditTypeLanguageFactory` |
| type.js | `Sw.TypeInfo` {`GetType`, `GetInstance`, `TypeLink`, `Req`, `ReqNew`}, `Sw.TypeFlags`, `Sw.TypeDoc` {`AddTextElement`, `BuildLink`, `BuildTypeNameRec`, `AddTypeName`, `AddSection`, `Add`, `Main`} and the legacy globals |
| text.js | `Sw.TextPage` {`Main`, `Save`, `Delete`, `SetText`, `Append`, `Find`, `FocusFind`, `GotoProblem`, `ApplyProblemsOnly`, `SetFollow`, `Poll`, `IsDirty`, `Editor`, `SanitizeHtml`, `ModeName`}, `textMain()` |
| index.js | `Sw.EditDemo` (test page) |

## CSS reference

| Class | Description |
|---|---|
| `.sw-edit`, `.sw-edit-root`, `.sw-edit-child`, `.sw-edit-noborder` (`.NoBorder`), `.sw-edit-readonly`, `.sw-edit-collapsed` (`.EditorCollapsed`) | Editor boxes |
| `.sw-edit-desktop`, `.sw-edit-mobile` | Layout (set on the root editor) |
| `.sw-edit-head`, `.sw-edit-typetitle`, `.sw-edit-headextra`, `.sw-edit-expand`, `.sw-edit-menubtn` | Title row |
| `.sw-edit-body`, `.sw-edit-row`, `.sw-edit-label`, `.sw-edit-name`, `.sw-edit-icon`, `.sw-edit-value`, `.sw-edit-data`, `.sw-edit-entry`, `.sw-edit-entryvalue` | Rows |
| `.sw-edit-ro` (`.EditReadOnly`), `.sw-hide`, `.sw-edit-error`, `.sw-edit-expanded`, `.sw-edit-draggable`, `.sw-edit-drop` | States |
| `.sw-edit-input`, `.sw-edit-multiline`, `.sw-edit-number` (`--sw-edit-number-w` 9rem minimum, `--sw-edit-number-len` set by `FitNumberBox`, `--sw-edit-number-max` 20rem; `--sw-edit-chars` is no longer used; other widths: `--sw-edit-field-w`, `--sw-edit-text-w`, see "Widths"), `.sw-edit-roselect`, `.sw-edit-robool`, `.sw-edit-language-ro`, `.sw-edit-langcode-input`, `.sw-edit-typeerror`, `.sw-edit-formfoot-readonly`, `.sw-edit-hasslider`, `.sw-edit-datetime`, `.sw-edit-select`, `.sw-edit-flags`, `.sw-edit-info`, `.sw-edit-language`, `.sw-edit-langsel` | Editors |
| `.sw-edit-typehint` > `.sw-edit-hintpart`, `.sw-edit-fielderror`, `.sw-edit-haserror`, `--sw-edit-rule` | Hint line parts, the validation message under a field, the row hairline |
| `.sw-edit-incard`, `.sw-edit-cardtitled`, `.sw-edit-head-incard`, `.sw-edit-framed` (opt out) | An editor directly in a card body (see "Options and presets") |
| `.EditBoolean`, `.EditTrue`, `.EditObject`, `.EditContainer`, `.EditSlider`, `.DataRow` | Old hook classes kept on value cells |
| `.SysWeaverEditIcon*` | Legacy mask icon classes |
| `--SysWeaverEditDesktopRad` (used by app/tags.css), `--SysWeaverEditFadeDuration`, `--SysWeaverEditDesktopRadInp(2)` | Legacy variables (mapped to Sw tokens) |
| text.css: `.text-page`, `.text-bar`, `.text-name`, `.text-dirty`, `.text-info`, `.text-find` (`.text-find-field`, `.text-find-input`, `.text-find-count`, `.text-find-none`, `.text-find-hit`), `.text-problems`, `.text-follow-paused`, `.text-break`, `.text-editor`, `.text-message` | Text page (the Ace theme `.ace-sysweaver` and `.sw-ace`, the frame of an `Sw.AceEditor`: ace.css) |
| type.css: `.sw-type-page`, `.sw-type-section` (`.TypeCode`, `.TypeCol`, `.TypeItalic`), `.sw-type-title`, `.sw-type-name`, `.sw-type-table` (`.TypeMembers`, `.TypeEnumValues`), `SysWeaver-Text` | Type page |

## Not allowed (403)

- text.html (text.js): a refused read (403) shows the shared "not allowed" state (`Sw.Page.ShowNotAllowed`: who, the role needed, Home) instead of the raw "403 - Forbidden" text plus a toast; no tool bar.
- Save (`u`) and Delete (`d`) are enabled like in the original viewer (no role lookup before; no `CanCall` request): a refused Save / Delete (403) names the role and stays disabled (`Sw.Access.ActionDenied`); Ctrl+S does nothing then, and a Save never re-enables a refused Delete.
