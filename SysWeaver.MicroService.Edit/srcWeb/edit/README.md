# edit

[⬆ srcWeb](../README.md)

> The browser object editor that renders a form for any type described by `TypeService`, plus the type information page and the Ace-based text / code editor.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Edit](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

Served under `edit/`. There are three parts:

- **Object editor** — `Edit.js` is included by other pages (e.g. `explore/api.html` of the [API explorer](../../../SysWeaver.HttpServer.ExploreModule/README.md) and `ServerManager/services.html` of [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md)). It loads the type information from `TypeService`, picks an editor per member (`EditType*.js`, loaded on demand) and lays the form out in a desktop or mobile style. A member with a special type specification (`Type` in the member description) is handled by a global `EditType<Spec>Factory` function if one is present, which is how `EditTypeLanguage.js` plugs in.
- **Type information page** — `type.html?q=<type name>` shows the documentation of a type and its members; common table formatting links type names to it.
- **Text / code editor** — `text.html?r=<read url>` shows a text file with Ace, optionally with save (`u=`), delete (`d=`), download, reload and a Prettier based *Beautify* button. `TypeService` registers it as the editor for common text file extensions, and log or manifest tables link to it.

Processing (minification, Brotli compression, embedding) is described in [`srcWeb/`](../README.md).

## Contents

| Item | Description |
|---|---|
| [`external/`](external/README.md) | Vendored Ace editor (modes, themes, workers, extensions), Prettier and Ace snippets |
| [`Edit.js`](Edit.js) | `Edit` class: the object editor, type handler lookup, undo / redo, `editTestMain` for the test page |
| [`EditOptions.js`](EditOptions.js) | `EditOptions`: title, read-only, expansion, menu and preset option sets (`Clean`, `CleanReadOnly`, ...) |
| [`EditDesktop.js`](EditDesktop.js) / [`EditDesktop.css`](EditDesktop.css) | Desktop (table) layout of the form |
| [`EditMobile.js`](EditMobile.js) / [`EditMobile.css`](EditMobile.css) | Mobile layout of the form |
| [`Edit.css`](Edit.css) | Shared editor styles |
| [`UndoStack.js`](UndoStack.js) | Undo / redo stack used by the editor |
| [`EditTypeBoolean.js`](EditTypeBoolean.js) | Check box editor for booleans |
| [`EditTypeInteger.js`](EditTypeInteger.js) | Integer editor (ranges, sliders) |
| [`EditTypeNumber.js`](EditTypeNumber.js) | Floating point editor (ranges, sliders) |
| [`EditTypeString.js`](EditTypeString.js) | String editor (single / multi-line, password) |
| [`EditTypeEnum.js`](EditTypeEnum.js) | Enum and flags editor |
| [`EditTypeDateTime.js`](EditTypeDateTime.js) | Date and time editor |
| [`EditTypeObject.js`](EditTypeObject.js) | Nested object editor |
| [`EditTypeCollection.js`](EditTypeCollection.js) | Collection editor; loads `EditCollectionArray.js` / `EditCollectionMap.js` |
| [`EditCollectionArray.js`](EditCollectionArray.js) | Array / list operations for the collection editor |
| [`EditCollectionMap.js`](EditCollectionMap.js) | Dictionary / map operations for the collection editor |
| [`EditTypeLanguage.js`](EditTypeLanguage.js) / [`EditLanguage.css`](EditLanguage.css) | Language selector (`EditTypeLanguageFactory`), uses `iso_data/languages.js` |
| [`EditTypeDefault.js`](EditTypeDefault.js) | Fallback editor for other types |
| [`type.html`](type.html) / [`type.js`](type.js) / [`type.css`](type.css) | Type information page; `type.js` also defines `TypeMemberFlags` and the type info cache used by `Edit.js` |
| [`text.html`](text.html) / [`text.js`](text.js) / [`text.css`](text.css) | Text / code editor page |
| [`index.html`](index.html) | "Edit test" page that runs `editTestMain()` |

## Relationships

- **Part of:** [srcWeb](../README.md), [SysWeaver.MicroService.Edit](../../README.md)
- **Talks to:** [`TypeService`](../../TypeService.cs) (`GetTypeInfo`, `GetDefaultInstance`), ISO data from [`IsoDataService`](../../IsoDataService.cs)
- **Used by:** [SysWeaver.HttpServer.ExploreModule](../../../SysWeaver.HttpServer.ExploreModule/README.md), [SysWeaver.MicroService.ServerManager](../../../SysWeaver.MicroService.ServerManager/README.md), [SysWeaver.MicroService.FolderSync](../../../SysWeaver.MicroService.FolderSync/README.md) (manifest links to `text.html`)

## Behavior (web analysis)

### Loading and initialization

There are three independent entry points. They share no runtime state except `type.js`, which both the object editor and the type page load.

**Object editor (a library; no page of its own in normal use).** A host page includes `../edit/Edit.js` with a plain `<script defer>`, plus `Edit.css`, `EditDesktop.css` and `EditMobile.css`, after `common/common.js` and `app/application.js`. The last line of `Edit.js` calls `Edit.Init()` synchronously while the script is evaluated. It captures `document.currentScript.src` and calls `includeJs(current, ...)` from common.js in parallel for `type.js`, `UndoStack.js`, `EditOptions.js`, `EditDesktop.js`, `EditMobile.js`, `EditTypeBoolean.js`, `EditTypeDefault.js`, `EditTypeEnum.js`, `EditTypeInteger.js`, `EditTypeNumber.js`, `EditTypeString.js`, `EditTypeCollection.js`, `EditTypeObject.js` and `EditTypeDateTime.js`. `EditTypeCollection.js` then runs `EditTypeCollection.Init()`, which includes `EditCollectionArray.js` and `EditCollectionMap.js`. The returned promise is never awaited or exposed. Consumers assume the dynamic scripts have loaded by the time they create an `Edit`, usually in a body `onload` handler. `includeJs` loads each absolute URL only once, tracked in `window.IncMap`.

`EditTypeLanguage.js` is **not** part of `Edit.Init()`. A page must include it itself (no page in the repository does). Once loaded, it pulls in `../iso_data/languages.js` and `EditLanguage.css`.

**Type page** `type.html?q=<type name>` loads `common.js`, `common/tab.js` and `type.js`, plus `theme.css`, `tab.css` and `type.css`, and calls `typeMain()` on body load. It does not load `app/application.js`.

**Text page** `text.html?r=...` loads `common.js`, `app/application.js`, `external/ace.js`, `external/ext-language_tools.js`, the Prettier standalone build and six parser plugins (`estree`, `typescript`, `babel`, `postcss`, `markdown`, `html`), then `text.js`. The body has `onload="textMain()"`.

**Edit test page** `index.html` only links stylesheets (`theme.css`, `application.css` and the three editor CSS files) and calls `editTestMain()` on load. It has no `<script>` tags, so `editTestMain` (defined at the end of `Edit.js`) can only run if the scripts are provided some other way.

No file in this folder uses `${Variable}` server templating. The one `${...}` in `text.js` is a JavaScript template literal.

### Files

#### `Edit.js`: class `Edit` (the form engine) and `editTestMain()`

An `Edit` instance wraps one type description (`TypeDesc` JSON from `GetTypeInfo`) and one object value. It owns a root custom element, `SysWeaver-EditDesktop` or `SysWeaver-EditMobile`, that contains a `<table>`.

- `constructor(type, style, options, parentEditor, member, evListener)`
  - `options` defaults to `new EditOptions()`. The undo stack is `options.UndoStack`, else the parent's stack, else a new `UndoStack`, so a whole nested editor tree shares one stack.
  - `ObjName` is `options.Name`, else the last dot segment of `type.TypeName`, else `"Edit"`. It is used to build the undo path names.
  - Style selection when `style` is falsy: `"Desktop"` by default, switched to `"Mobile"` when the window is portrait and either `window.Mobile` is set or the width is at most 800 px (400 px for mobile in portrait). On mobile (`window.Mobile`), the root element gets a CSS `zoom` that scales the 200–400 px or 400–800 px design range to the window width.
  - The layout strategy object is created with `eval("new Edit" + style + "()")`, so the class name comes from the style string.
  - `TypeHandler` is resolved with `Edit.GetTypeHandler(type.TypeName, member ?? first member for collection/primitive types)`. If the handler declares `IgnoreMembers`, the type is cloned and turned into a synthetic primitive with a single member.
  - The DOM element gets a back-reference, `e.Editor = this`, and the class `NoBorder` when `!options.Border`.
  - If `options.Title` is set, it builds a tooltip (summary and remarks, plus type name and assembly when `options.Dev`) and calls `layout.AddTypeInfo(...)`, which calls `SetTypeInfo`.
  - A child editor forwards every `EditChange` event to its parent's element as a new `EditChange` event with the child event in `detail.Next`. The result is a linked chain from the outermost editor to the innermost.
  - Keyboard: Ctrl+Z / Ctrl+Y on the element call `UndoStack.Undo/Redo`. `Edit.LastEdit` tracks the last focused editor. A single `document.body` keydown listener (`Edit.AddGlobalListener`, installed once and guarded by `Edit.HaveGlobalListener`) sends Ctrl+Z / Ctrl+Y to `Edit.LastEdit.UndoStack`.
- `SetObject(obj)`: an `undefined` value (or a falsy value for non-primitives) is replaced by `Edit.CreateDefault`. A primitive value is wrapped as `{ [member0.Name]: value }`, so every handler edits `obj[memberName]`. It deletes every table row except the title row, removes the previous "Hide:" listeners, then calls `layout.AddMember(...)` for each member without the `TypeMemberFlags.Hide` flag. For each member it:
  - evaluates a `member.Type` that starts with `"Hide:"` as `new Function("return " + expr).call(obj)`. It re-evaluates on every `EditChange` on the editor element and toggles the item's `Hide()/Show()` and a `Hide` class on its `DataRow`. The C# `EditHideIfAttribute` emits these expressions, for example `Hide:this.Member===value`.
  - stores the item in `MemberMap` (an instance field `new Map()` that is never cleared between `SetObject` calls) and adds the class `EditButton` to the icon cell.
  - wires a popup member menu, opened by clicking the title cell, by its context menu, or by an optional `ColorIcon` when `options.MenuIcon` is set. Menu items:
    - *Set default* uses `item.DefValue`; disabled when the value already equals the default or is read only.
    - *Copy* is blocked for `Password` members.
    - *Paste* reads the clipboard with `ValueFormat.readFromClipboard` and JSON-parses it for non-string members. It accepts the value when `$type` matches `member.TypeName` or `IsOfType` passes, then applies `Condition`.
    - Items supplied by the handler (`item.AddMenuItems(menu, close)`) and by the editor (`this.AddMenuItems(menu.Items, close)`, which collections use for primitive elements).
    - *Copy name*, plus *Copy member name* and *Copy type name* in Dev mode.

    Menus are built with `WebMenu`, `WebMenuItem.From`, `MainMenu` and `MainMenuStyle`, shown by `PopUpElementMenu` (application.js) and positioned with `TrackElement` (common.js). An item with `Flags: 1` is disabled.
- `SetTypeInfo(c, expandElement, type, title, options)` builds the title cell:
  - an optional expand/collapse `ColorIcon`, which toggles the class `EditorCollapsed` on the table.
  - a `SysWeaver-PopupMenuIcon` with an object-level menu: *Set default* (calls `SetObject()`), *Copy*, *Paste* and `edit.AddMenuItems`.
  - the `options.AddToHeader(c)` hook, which collections use to inject their remove and insert icons.
  - the `SysWeaver-TypeTitle` text (`options.KeyName`, else `type.DisplayName`) with a copy-on-click tooltip.
  - drag and drop of the whole object: the payload is `application/json` with `$type`, and a drop is accepted when `CanConsumeCopy` passes.
- `static SetMember(obj, editor, editContext, member, title, options, c)`: resolves the handler and calls `handler.AddEditor(...)`. It adds `TypeHandler` and `TitleElement` to the returned item. It makes the title cell draggable (`application/json` from `GetCopyMember(member, true)`) and a drop target for JSON or `text/plain`, routing drops through `Invoke` so they can be undone.
- `Invoke(doChange, doReverse, title, name, tracking)`: computes a dotted path. `GetObjectName()` gives the parent path + `ObjName` + `KeyName`, and `.member` is appended. It then registers redo and undo closures on the shared `UndoStack`. At run time the closures find their target **by path** from the root (`root.FindItem(path)`), call `doChange(item)` or `doReverse(item)`, then `item.UpdateValue(isFirst)`. Because of this, undo still works after sub-editors are re-rendered.
- `FindItem(path)`: splits the path on `.`. A single segment returns the root. Otherwise it looks up `MemberMap` by member name (with `[key]` removed) and passes deeper paths to the item's own `FindItem`, which collections and objects implement.
- Value access and validation:
  - `GetObject()` unwraps a primitive value.
  - `GetCleanCopy(src)` returns a JSON copy limited to the declared members.
  - `static CleanUp(type, obj)` deletes undeclared properties recursively and keeps `$type`.
  - `Validate(focusOnError)` calls `static ValidateType(type, obj, options, name)`, which returns `[path, message]` or `null`. On error it can focus the failing item's `Focus` element.
- `NotifyChange()` dispatches `EditChange`. `ChangeTitle(name)` updates `SysWeaver-TypeTitle` / `SysWeaver-MemberTitle`; it is used when collection items are renumbered.
- Static helpers used by the handlers. Each returned element gets an `UpdateValue()` method and an `isInternal` guard against re-entrant change events.
  - `CreateGenericInput(inputType, get, set, parse, toString, validate, readOnly)`.
  - `CreateTextInput(get, set, parse, toString, placeholder, validate, keyFilterOrCharset, maxLength, def, readOnly, lines)` creates a textarea when `lines > 1` and filters characters in `beforeinput`. An `input` event is a temporary change. A `change` event is the final change and re-formats the text. Escape restores the original value.
  - `CreateSliderInput(getStep, setStep, stepCount, readOnly)` creates an `<input type=range>`.
  - `CreateSelectionInput(get, set, values, def, readOnly, flags, onOpenFn)` creates a `<select>`, `multiple` for flag enums, where option changes are merged bitwise.
- Other static helpers: `IsDifferent`, `IsValid` (`!== undefined`), `IsTrue`, `GetIntParam`, `GetTypedTextOrJson` (adds `$type` to objects), `SetToString` (summary text of an object, truncated to 24 characters) and `SetCollectionCount`.
- Type metadata:
  - The `static Types` map plus `AddType(type)` register client-side type descriptions, used with `GetType(name, props, isJsType=true)`.
  - `GetType(typeName, props, isJsType)` looks in `Edit.Types`, then `TypeInfoCache.GetType`, and returns a deep JSON clone with `props` (for example `ElementInst`) merged in.
  - `FromServer(serverTypeName, style, options, props, obj)` fetches the type and creates an editor for it.
  - `CreateDefault(type, options, setTypeName)` returns the handler default for a primitive. For an object it uses the server default instance from `TypeInfoCache.GetInstance`, falling back to per-member handler defaults.
- Handler registry, `GetTypeHandler(typename, member, options)`: there are two lazily built maps, `Edit.TypeMap` for calls without a member and `Edit.TypeMapMember` for calls with one, both filled by `GetTypeMap()`:
  - `System.SByte/Int16/Int32/Int64/Byte/UInt16/UInt32/UInt64` map to `EditTypeInteger`, and `Single/Double/Decimal` to `EditTypeNumber`.
  - `Boolean` maps to `EditTypeBoolean`, and `DateTime/DateOnly/TimeOnly` to `EditTypeDateTime`.
  - All of these also have `System.Nullable`1[[...]]` variants.
  - `System.String` maps to `EditTypeString`, and `System.Object` to a read-only `EditTypeString`.

  If the member has a `member.Type` (the C# `[EditType]` spec), the key becomes `typename|spec`. If a global function `window["EditType" + spec.split(':')[0] + "Factory"](typeName, specParts)` exists, it is used. This is the plug-in point. `EditTypeLanguageFactory` is the only implementation; `EditTypes.Country`, `Currency`, `Email` and `Inline` have no client implementation and fall back to the base type. Otherwise the choice depends on the member flags: `Collection` gives a shared `EditTypeCollection`, `IsObject` a shared `EditTypeObject`, `IsEnum` a shared `EditTypeEnum`, and anything else a shared `EditTypeDefault`. Each result is cached under its key.
- `static async Boolean(name, value, title, onChange, readOnly, showMenu)`: a shortcut that builds a one-member primitive boolean editor with the `EditOptions.Clean*` presets and passes each `EditChange` on to `onChange(value)`.
- `editTestMain()` calls `Edit.FromServer("SysWeaver.MicroService.EditTest")` (a C# test class in `TypeService.cs`) and logs each change path (`Member.Name` + the `KeyName` chain from `detail.Next`) to the console.

#### `EditOptions.js`: class `EditOptions`

- Public fields, with defaults: `Title` (true), `Dev` (true), `ReadOnly`, `CanExpand` (true), `IsExpanded` (true), `KeyName`, `Border` (true), `ExpandAllCollections`, `MenuIcon`, `MenuIconClass`, `MenuColorClass` and `AddToHeader`. `Clone()` copies only these fields.
- The engine also uses two properties that are not declared or cloned: `Name` and `UndoStack`.
- Static presets built by IIFEs: `CleanMenu` (no title, no expand, no border, no dev), `CleanReadOnlyMenu`, `Clean` and `CleanReadOnly`. The "Menu" and non-menu presets currently differ only in `ReadOnly`, because `MenuIcon` is false in all of them.

#### `UndoStack.js`: `UndoEntry` and `UndoStack`

A linear stack made of `Ops` and `Pos`.

- `Invoke(doChange, doReverse, title, name, tracking)` drops the redo history. If `tracking` is truthy and the top entry has the same `name` and `tracking` object, it **coalesces**: it runs `doChange()` and replaces the entry's `DoChange` and `Title` but keeps the original `DoReverse`. This makes continuous typing in one input, or dragging a slider, a single undo step.
- `Undo()` / `Redo()` call `DoReverse()` / `DoChange(false)` and focus `op.Focus` if it is set.
- Errors are caught and logged with `console.warn`.
- Handlers pass a sixth argument (the element to focus) to `Edit.Invoke`, which takes only five, so `UndoEntry.Focus` is never set.

#### `EditDesktop.js` and `EditMobile.js`: layout strategies

Both expose the same duck-typed interface: `AddTypeInfo(editor, table, type, title, options)` and `async AddMember(obj, editor, table, member, title, options, isLast)`.

- Desktop: one `tr.ValueRow` per member with three cells: the title (`SysWeaver-MemberTitle`), an icon cell and the editor cell. The title row has colSpan 3, and a `tr.SeparatorRow` separates members.
- Mobile: a `tr.HeaderRow` (title and icon) followed by a `tr.ValueRow` (the editor cell, colSpan 2).
- Both pass an `editContext` `{ PrevRow, Element, CreateData() }` to the handler. `CreateData()` appends a full-width row, which the collection and object handlers use for nested content.
- Both attach `Hide()` / `Show()` (toggling the class `Hide` on the rows), set `Icon`, add `EditReadOnly` to the title cell, and put the handler's `Title` (constraints text) at the start of the tooltip.

#### Type handlers (`EditType*.js`)

All handlers implement the same duck-typed contract:

| Method | Purpose |
|---|---|
| `CreateDefault(member, options)`, `GetDefault(...)` | Default value |
| `IsOfType(obj, member, options)` | Whether a pasted or dropped value fits |
| `Condition(obj, member, options)` | Coerce or clamp a pasted or dropped value |
| `async Validate(obj, member, options, name)` | Returns `[name, message]` or `null` |
| `ToString(obj, member, options)` | Display text |
| `async AddEditor(obj, editor, editContext, member, title, options)` | Builds the inputs and returns an *item* |

The *item* has the shape `{ Focus, Title, DefValue, DefValueText, SetValue(v), UpdateValue(isFirst), [DataRow], [AddMenuItems(menu, close)], [FindItem(path)], [GetValue()] }`.

`AddEditor` writes the default into `obj[member.Name]` when it is undefined, builds the inputs with the `Edit.Create*Input` helpers, and sends user changes through `editor.Invoke(...)`. `SetValue` writes `obj[mn]` and dispatches `EditChange` on `editor.Element`.

- **`EditTypeBoolean.js`**: a two-step range slider (`CreateSliderInput`, steps 0..1) styled as a toggle, with the cell classes `EditBoolean` and `EditTrue`. It accepts `true`, `false`, `"true"`, `"false"`, 0 and 1.
- **`EditTypeInteger.js`** (`constructor(signed, byteSize, nullable, literalMin, literalMax)`):
  - The type range comes from the byte size; Int64 and UInt64 use `Number.MIN/MAX_SAFE_INTEGER`.
  - `GetMinMaxDefault` merges `member.Min/Max/Default` with the type range and returns `[min, max, def, typeTitle, haveMin, haveMax, parsedDef]`.
  - The editor is a text input that only accepts digits and a leading `-`, with a max length from the digit count.
  - A range slider is added when the `Slider` flag is set and both min and max are given (cell class `EditSlider`). The step comes from `member.EditParams`, or is a power of ten chosen automatically to give at most 100 steps.
- **`EditTypeNumber.js`** (`constructor(typeMin, typeMax, maxLen)`): the same pattern for floating point values. It accepts `,` as the decimal separator in metadata and allows one `.` in the input. When a slider exists, it computes a fixed number of decimals for display.
- **`EditTypeString.js`** (`constructor(forceReadOnly)`):
  - A text input, or a 3-line textarea when the `Multiline` flag is set (not for passwords). Passwords get `type=password`.
  - `maxlength` comes from `Max`. A default of `"\t"` means an empty string.
  - The text element is passed as the undo *tracking* object, so typing coalesces into one undo step.
- **`EditTypeEnum.js`**:
  - Decodes the enum metadata that `TypeService` packs into `member.Default` as `<default>><value><name><summary><remarks>|...`: `>` separates the default from the list, `|` separates values and `<` separates fields.
  - `GetValueMap` caches, per type name, a `Map` keyed by the number, the numeric string, the raw name and the de-camel-cased name. It also holds the lists `___single___`, `___edit___` and `___editro___`.
  - The editor is a `<select>`, multi-select for `IsFlags`, where multi-bit flag values are shown as combinations.
  - A member `Type` of `Allow.<OtherMember>` disables, when the select gains focus, each option whose bit `1 << value` is not set in `obj[OtherMember]`.
- **`EditTypeDateTime.js`** (`constructor(forceReadOnly, onlyDate, onlyTime)`):
  - Uses `<input type=date>` and/or `<input type=time step=1>` (`HalfWidth` when both are shown).
  - Converts between local date/time strings and either ISO strings (`toISOString`) or `Date` objects.
  - With the `DateUnspecified` flag, a date-only value is kept as `yyyy-mm-ddThh:mm:ss` without timezone conversion.
  - Min and max come from metadata; the default is now, rounded to the hour.
- **`EditTypeObject.js`**: nested object editor.
  - Shows a 36 px expand `ColorIcon` and a `SysWeaver-EditInfo` summary (`Edit.SetToString`).
  - Expanding creates a child `Edit` (same style, `Title=false`, `Name=member`) inside `SysWeaver-EditEntry > SysWeaver-EditEntryValue` in the data row. Expanding a `null` value first creates a default object through an undoable "Create object" step.
  - Default: the `member.Default` JSON, `{}` for `"\t"`, otherwise the server default instance (`TypeInfoCache.GetInstance`), otherwise a default built by a temporary child `Edit`.
  - `UpdateValue` collapses and re-expands to refresh. `FindItem` expands on demand.
- **`EditTypeCollection.js`**: list and dictionary editor.
  - Picks a container strategy with `GetContainer(member)`: `EditCollectionMap.Inst` when `member.Key && member.Element`, otherwise `EditCollectionArray.Inst`. The server's `TypeMemberDesc` exposes `KeyTypeName`, `KeyInst` and `ElementInst` but not `Key` or `Element`, so in practice the array strategy is always used.
  - Shows an expand icon and a count text (`Edit.SetCollectionCount`). Expanding creates one child `Edit` per element (a `SysWeaver-EditEntry` with header and value), stored in a local `keyMap` under `"" + key`, with `KeyName` `"[i]"`.
  - The element type comes from `Edit.GetType(member.ElementTypeName, member.ElementInst)`. For primitive element types, min, max, default and flags are copied from the element type onto the synthetic member.
  - Item operations, all undoable through `editor.Invoke`: remove, insert before/after, move top/up/down/bottom (these four need the `Indexed` flag), clear or make empty, and add.
  - Header icons (`../icons/clear.svg`, `../icons/plus.svg`) are injected through `EditOptions.AddToHeader`.
  - The collection is fixed (`isFixed`) when `Min >= Max` or it is read only. The `ExpandAllCollections` option expands it automatically.
  - After a structural change, `reOrder` renumbers the keys and titles. Changes are reported as one `EditChange` for the whole collection (`colChanged`).
- **`EditCollectionArray.js` and `EditCollectionMap.js`**: stateless strategy singletons (`static Inst`) with `createVal`, `countVal`, `getVal`, `setVal`, `getKeyName`, `íterate`, `onKeys`, `addVal` and `removeVal` (which returns whether the keys need renumbering). Only the array strategy has `insertAt`, `insertAfter` and `move`. The `íterate` method name uses a non-ASCII `í`, and its callback returns truthy to continue.
- **`EditTypeLanguage.js`**: a global `EditTypeLanguageFactory(typename)` and the class `EditTypeLanguage`, which throws for anything but `System.String`.
  - Renders `SysWeaver-EditLanguage`: a flag `img` from `../iso_data/language/<iso>.svg` and a `SysWeaver-EditLangText`.
  - A click opens a static `SearchablePoupSelection("Language", ...)` (application.js). It lists `IsoLanguage.Languages`, ranked by name or code match and by recent use, as `SysWeaver-EditLangSel` rows.
  - The value is the ISO 639-1 code; the default is `"en"`.
- **`EditTypeDefault.js`**: the fallback for unknown types. It shows a read-only text input with `"" + value`, and sets `member.Flags |= 16` (ReadOnly) on the shared member description.

#### `type.js`: shared metadata module and the type page

- `const TypeMemberFlags` (frozen) mirrors the C# `TypeMemberFlags.cs`: `Multiline 1, Password 2, AcceptNull 4, Slider 8, ReadOnly 16, Collection 32, Indexed 64, IsEnum 128, IsPrimitive 256, IsObject 512, Hide 1024, IsFlags 2048, DateUnspecified 2048`. The last two share a bit, as they do on the server. Several files still use the literals `16` and `2` instead of the names.
- `class TypeInfoCache`:
  - Holds static URLs resolved relative to the script: `TypeLink` (`edit/type.html`), `Req` (`edit/GetTypeInfo`) and `ReqNew` (`edit/GetDefaultInstance`).
  - `GetType(typeName)` caches responses in `Types`, including `null` results.
  - `GetInstance(typeName)` caches the JSON string and returns a fresh parse on each call.
- Rendering helpers. These are global functions, also used by `explore/api.js`:
  - `AddTextElement`.
  - `BuildLink` creates a link to `type.html?q=`.
  - `BuildTypeNameRec` parses .NET reflection names with arrays and `` `n[[...]] `` generics into text and link parts, rendered as `Base<Arg,...>`.
  - `AddTypeName(target, name, link, ext)` and `typeAddSection`.
  - `typeAdd(target, data|typeName)` renders sections for the type name (with an optional external documentation link `data.Ext`, opened with `Open`), summary, remarks and assembly. It then adds either an enum values table (`table.TypeEnumValues`) or a members table (`table.TypeMembers`) whose type cells link to `type.html`.
- `typeMain()` shows `AddLoading()`, reads `q` with `getUrlParams()`, sets `document.title`, fetches the type with `TypeInfoCache.GetType`, renders it into `SysWeaver-Page.Wide`, and calls `Fail(...)` on errors.

#### `text.js`: the Ace-based viewer and editor page

- `SanitizeHtml(html)` quotes unquoted attribute values that contain `/` or `:` before Prettier formats HTML.
- `textMain()`:
  - Reads the URL parameters (lower-cased by `getUrlParams`).
  - Picks the Ace mode from `t=` or from a file extension map. A comment says the map must stay in sync with `TextExtensions` in `TypeService.cs`.
  - Builds a `Button.CreateRow()` toolbar:
    - *Reload* fetches the file again, beautifies when `b=true`, and optionally scrolls to the end.
    - *Beautify* is shown when Prettier has a formatter for the mode.
    - *Download* (`downloadText`) is shown unless `download=false`.
    - *Save* is shown only when `u=` is given.
    - *Delete* is shown only when `d=` is given, and asks for `Confirm` first.
  - Creates the editor with `ace.edit(SysWeaver-AceEditor, options)`: `readOnly = !u`, autocompletion enabled, `useWorker` only when editable, `setMode("ace/mode/<type>")`, no print margin.
  - A `MutationObserver` on `body` adds the `ace-sysweaver` class to Ace popups and tooltips appended there.
  - Ends with `PageLoaded()`.
- `beautifyButton` is assigned without a declaration, so it becomes an implicit global. The "No read url" path calls `TF(...)`, which neither common.js nor application.js defines; only `_TF` exists.

#### CSS

- **`Edit.css`**: shared variables (`--SysWeaverEditFadeDuration`), `SysWeaver-MemberTitle`, `SysWeaver-TypeTitle`, `SysWeaver-EditInfo`, `EditReadOnly`, drag cursors (`td.EditDraggable`), `EditFloatRight` and `tr.Hide`. It also defines the icon mask classes `SysWeaverEditIcon*` (`Menu`, `Default`, `Copy`, `Paste`, `Expand`, `Collapse`, `AddItem`, `ClearCollection`, `AddItemBefore/After`, `MoveItemTop/Up/Down/Bottom`), which point at `../icons/*.svg` and are used by `ColorIcon` from common.js.
- **`EditDesktop.css` and `EditMobile.css`**: nearly parallel rule sets, scoped by the root element name.
  - They key on row classes (`TitleRow`, `ValueRow`, `HeaderRow` on mobile, `SeparatorRow`, `DataRow` + `EditorExpanded`, `table.EditorCollapsed`) and cell classes (`EditButton`, `EditContainer`, `EditBoolean`/`EditTrue`, `EditSlider`).
  - They style inputs, selects, textareas and `HalfWidth`, and override the look inside `SysWeaver-TabContent > ...` and `SysWeaver-EditEntryValue > ...`.
  - Only the desktop sheet defines `.NoBorder`.
  - Both use theme variables from `common/theme.css` (`--ThemeMain`, `--ThemeAcc1/2`, `--ThemeBackground`, `--ThemeRad*`, `--Theme*RGB` and others).
- **`EditLanguage.css`**: `SysWeaver-EditLanguage`, `SysWeaver-EditLangSel` and `SysWeaver-EditLangText`. It uses `--SysWeaverEditDesktopRadInp` from the desktop sheet.
- **`type.css`**: the `SysWeaver-TypeSection` variants (`TypeCode`, `TypeCol`, `TypeItalic`), the two tables and `.SysWeaverTypeIconExternal`.
- **`text.css`**: the page grid (a toolbar row and an editor row, with an `html.Embedded` variant), plus a complete Ace theme under `.ace-sysweaver` (tokens, gutter, search box, autocomplete popup) mapped onto the theme variables.

### Vendored libraries

- **Ace 1.44.0**: `external/ace.js` (an `ace.define` no-conflict build), with `ext-*.js`, `keybinding-*.js`, 199 `mode-*.js`, 11 `worker-*.js` and `external/snippets/*.js`.
  - Only `ace.js` and `ext-language_tools.js` are loaded statically, by `text.html`.
  - Ace loads `mode-<type>.js`, the matching worker and the snippets on demand. It finds them from the `ace.js` script URL; first-party code never calls `ace.config.set`.
  - No `theme-*.js` file is used. The look comes from the default theme plus the `.ace-sysweaver` overrides in `text.css`.
- **Prettier 3.9.6**: `external/prettier/standalone.js` plus plugins. `text.js` uses the globals `prettier.format` and `prettierPlugins` with this parser map:

  | Ace mode | Prettier parser |
  |---|---|
  | `json`, `json5` | `json5` (tabs, `quoteProps: preserve`, trailing commas) |
  | `css` | `css` |
  | `html` | `html` (after `SanitizeHtml`) |
  | `markdown` | `markdown` |
  | `javascript` | `typescript` |
  | `xml` | `xml` (no XML plugin is vendored, so this formatter throws and the error is shown or logged) |

  The plugins `acorn`, `angular`, `flow`, `glimmer`, `graphql`, `meriyah` and `yaml` are vendored but not loaded.
- The object editor and the type page use no third-party code.

### Server interaction

| Caller | Endpoint | How it is called |
|---|---|---|
| `type.js` `TypeInfoCache.GetType` | `edit/GetTypeInfo`: `TypeService.GetTypeInfo(String typeName)`, under `[WebApiUrl("../edit")]`, with static client and request caching. Sessions with the debug role get a richer description. | `sendRequest(url, typeName, false)` |
| `type.js` `TypeInfoCache.GetInstance` | `edit/GetDefaultInstance`: `TypeService.GetDefaultInstance`. Returns a new instance of a public, non-abstract, non-`IDisposable` type that has a parameterless constructor, otherwise `null`. | `sendRequest(url, typeName, false)` |
| `text.js` on load and *Reload* | The `r=` read URL | `getRequest(url, false, false, null, r => r.text())` |
| `text.js` *Save* | The `u=` URL | `sendRequest(u, { Url, Name, Content })` |
| `text.js` *Delete* | The `d=` URL | `sendRequest(d, { Url, Name })` |
| `EditTypeLanguage.js` | The static `../iso_data/languages.js` (served by `IsoDataHttpServerModule`) and the flag images `../iso_data/language/<iso>.svg` and `../iso_data/icons/unknown.svg` | `includeJs` / `img.src` |

URL parameters:

- `type.html`: `q`, the type name.
- `text.html`:
  - `r`: the read URL (required).
  - `u`: the save URL; enables editing.
  - `d`: the delete URL.
  - `n`: the display and download file name; defaults to the last path segment of `r`.
  - `t`: overrides the Ace mode.
  - `b=true`: beautify automatically on load and reload.
  - `download=false`: hides the download button.
  - `scrolltoend=true`: matching is case-insensitive because `getUrlParams` lower-cases keys; callers write `scrollToEnd=true`.

Page registration:

- `TypeService` registers `edit/text.html?r={0}&u={1}&d={2}` as the extension editor (`ServiceManager.TryAddExtensionEditor`) for `bat, cmd, c, c++, cpp, h, hpp, cs, css, csv, glsl, htm, html, js, json, md, txt, sql, svg, xml, config, csproj, log, err, cfg`, and removes the registrations on dispose.
- Table formatting in `common.js` links type names to `../edit/type.html?q=...`.
- Other projects link to `text.html` directly: `TextFile.cs` (`TableDataUrl`), the manifest links in `FolderSyncService.Data.cs`, `ServerManager/serviceInfo.js` and `logFile/logfile.js`.

### State and persistence

- **In-memory caches** (per page): `TypeInfoCache.Types` and `InstanceTypes` (null results are cached too), `Edit.Types` (server types and client-registered types), the handler maps `Edit.TypeMap` / `Edit.TypeMapMember` and the shared handler instances, and the `EditTypeEnum.TypeMap` value maps.
- **Edit state**: the edited object itself holds the state; `obj[member.Name]` is changed in place. The undo history is the `UndoStack` shared by an editor tree, and `Edit.LastEdit` is the global keyboard target.
- **Change notification** uses the DOM: each `SysWeaver-Edit*` element receives a `CustomEvent("EditChange", { detail: { Next, Member, Value, Key, KeyName } })`, and each editor re-dispatches it to its parent. Values of `Password` members are replaced by `"***"` in the events.
- **Clipboard and drag and drop**: the clipboard is accessed through `navigator.clipboard` via `ValueFormat.readFromClipboard` / `copyToClipboard`. Drag and drop uses the MIME type `application/json` (with `$type`) or `text/plain`.
- **Messaging**: `text.js` broadcasts `InterOp.Post("FileSaved", { Url, Name, Content })` and `InterOp.Post("FileDeleted", { Url, Name })` over the BroadcastChannel `"SysWeaver"`, or a window-based fallback. Nothing in the repository listens for these message types.
- **Storage**: `SearchablePoupSelection("Language")` (application.js) keeps recent selections under the key `SysWeaver.PopUpRecent.Language`. Nothing else in this folder uses `localStorage` or `sessionStorage`.

### Dependencies

What this folder needs:

- From `common/common.js`:
  - Loading and requests: `includeJs`, `includeCss`, `sendRequest`, `getRequest`, `getUrlParams`.
  - Text: `_T`, `_TF`, and `ValueFormat` (`AddNonNullLine`, `copyOnClick`, `copyToClipboard`, `readFromClipboard`, `createLink`, `removeCamelCase`, `countSuffix`).
  - UI: `ColorIcon`, `Button` (`CreateRow`, `StartWorking`, ...), `badClick`, `isPureClick`, `keyboardClick`, `createImg`, `makeHtmlSafe`, `Open`, `TrackElement`.
  - Page and messaging: `Fail`, `Info`, `AddLoading`, `PageLoaded`, `downloadText`, `InterOp`, `window.Mobile`.
- From `app/application.js`: `WebMenu`, `WebMenuItem`, `MainMenu`, `MainMenuStyle`, `PopUpElementMenu`, `Confirm`, `SearchablePoupSelection`.
- From `iso_data/languages.js`: `IsoLanguage`.
- CSS: the variables in `common/theme.css`, `app/application.css`, and `common/tab.css` (type page).

Who uses this folder:

- `explore/api.html` and `api.js` (ExploreModule) create `new Edit(data.Arg)` for request input and `new Edit(data.Return, null, opt)` for output, and use `AddTypeName` from `type.js`.
- `ServerManager/services.html`.
- `app/application.js`: `PopUpEdit` and a similar helper fetch `../edit/GetTypeInfo` themselves, create `new Edit(type, undefined, opt)` and use `TypeMemberFlags`.
- `mediaView/media.js` creates `new Edit(typeInfo, null, options)`.
- The pages that only link to `type.html` / `text.html`, listed under *Server interaction*.

### Structural observations

- **Module style**: classic scripts where everything is global:
  - classes `Edit`, `EditOptions`, `UndoStack`, `UndoEntry`, `EditDesktop`, `EditMobile`, `EditType*`, `EditCollection*` and `TypeInfoCache`.
  - the const `TypeMemberFlags`.
  - the functions in `type.js` and `text.js`.

  `includeJs` controls the load order at run time; there are no imports. `Edit.Init()` is fire-and-forget, so readiness depends on an implicit timing contract with the host page.
- **Dispatch by name**: the layout class is chosen with `eval("new Edit" + style + "()")`, custom editors are found by the global name `EditType<Spec>Factory`, and the container strategy is duck-typed. Moving to modules would need an explicit registry for all three.
- **Duck-typed handler interface**: the handler methods (`CreateDefault/GetDefault/IsOfType/Condition/Validate/ToString/AddEditor`) and the returned item shape are not documented, and their signatures vary:
  - `GetDefault` takes `mmd` arrays in Integer and Number, returns `[def, title]` in Boolean, and returns a plain value elsewhere.
  - `CreateDefault` in Boolean and Collection takes element `[0]` of the `GetDefault` result.
- **Duplication**:
  - `EditTypeInteger` and `EditTypeNumber` share most of their code (the min/max/default merge and the slider step logic).
  - The member menu in `Edit.SetObject` and the object menu in `Edit.SetTypeInfo` duplicate the copy, paste and default logic.
  - The drag and drop handlers are duplicated in `SetMember` and `SetTypeInfo`.
  - `CreateTextInput` and `CreateGenericInput` duplicate the change handling.
  - Ctrl+Z/Y is handled both on each element and globally.
  - `EditDesktop.css` and `EditMobile.css` are largely parallel copies.
  - The enum metadata parsing exists in both `EditTypeEnum.js` and `type.js`.
- **Server contracts encoded as strings**:
  - Enum values are packed into `Default` (`def>v<name<summary<remarks|...`).
  - `"\t"` means "empty default".
  - `member.Type` can hold `Hide:<js expression>` or `Allow.<member>`.
  - Handler map keys use `Nullable`1[[...]]` type names.
  - The text editor's extension list exists in both `TypeService.cs` and `text.js`, each with a "keep in sync" comment.
  - `TypeMemberFlags` is copied from C# by hand.
- **Mutation of shared metadata**: handlers write into member descriptions. `EditTypeDefault` sets the ReadOnly bit, and the collection editor overwrites `Min/Max/Default/Flags/Summary/DisplayName` on the first member of the element type. `Edit.GetType` returns deep clones to limit this, but every editor on a page shares the handler maps.
- **Undo by path**: undo entries hold no DOM references. They find their targets again with `FindItem(dotted path)`, which expands collapsed collections and objects as a side effect. The paths depend on `ObjName`, `KeyName` (`[i]`) and member names.
- **Full rebuild on render**: `SetObject` deletes and recreates the rows, and `UpdateValue` on objects and collections collapses and re-expands them. `MemberMap` is keyed by name and overwritten on each rebuild.
- **User-visible strings**: most strings go through `_T` / `_TF` with translator context. Several handlers (Boolean, Integer, Number, String, Enum, DateTime, Object, Default, Language) still hard-code English for titles, validation messages and undo titles.
- **Mixed concerns per file**: `type.js` combines a shared library (flags, the cache and the type-name rendering that other projects use) with the `type.html` page entry. `text.js` is a self-contained page that shares only `common` and `app`.
