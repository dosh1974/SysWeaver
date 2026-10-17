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
- `beautifyButton` is assigned without a declaration, so it becomes an implicit global.

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
  | `xml` | none (no XML plugin is vendored, so XML files get no Beautify button) |

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

## JavaScript reference

Conventions used in this reference: user-visible strings are localized with `_T` / `_TF` (common.js) unless stated; "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function; `obj`/`member`/`options` are the edited object, a `TypeMemberDesc` from `GetTypeInfo` and an `EditOptions` instance unless stated otherwise.

**Type-handler contract (duck-typed).** Every `EditType*` handler (and the result of a global `EditType<Spec>Factory`) is a plain object that `Edit.GetTypeHandler` returns and caches. The engine calls:

| Member | Signature | Purpose |
|---|---|---|
| `CreateDefault` | `(member, options)` | Value written into a new object for this member (`Edit.CreateDefault`, collection "add"). |
| `GetDefault` | `(member, options)` | Default described by the metadata (shape varies per handler; see each handler). |
| `IsOfType` | `(obj, member, options)` | Whether a pasted / dropped value is acceptable. |
| `Condition` | `(obj, member, options)` | Coerces/clamps an accepted pasted or dropped value; returns the value to store. |
| `Validate` | async `(obj, member, options, name)` | Returns `null` or `[path, message]`. |
| `ToString` | `(obj, member, options)` | Display text. |
| `AddEditor` | async `(obj, editor, editContext, member, title, options)` | Builds the DOM inside `editContext.Element` (and optionally a data row via `editContext.CreateData()`), writes the default into `obj[member.Name]` if undefined and returns an *item*. |
| `IgnoreMembers` | optional field | If truthy the editor treats the type as a single-member primitive (`Edit` constructor, `EditTypeCollection`); no handler in this folder sets it. |

The *item* returned by `AddEditor` has `SetValue(v)` (store into `obj[name]` and dispatch `EditChange`), `UpdateValue(isFirst)` (refresh the UI from the object, called after undo/redo), `Focus` (element to focus on validation error), `Title` (constraint text prepended to the tooltip), `DefValue` / `DefValueText` (for *Set default*), and optionally `DataRow`, `AddMenuItems(menu, close)`, `FindItem(path)` and `GetValue()`. The layout (`EditDesktop` / `EditMobile`) adds `Hide()`, `Show()` and `Icon`; `Edit.SetMember` adds `TypeHandler` and `TitleElement`.

### `Edit.js`

The object-editor engine. Loaded by host pages with a static `<script defer src="../edit/Edit.js">`; on evaluation it runs `Edit.Init()`, which loads the rest of the editor scripts with `includeJs` relative to `document.currentScript.src`. Defines the global class `Edit` and the global function `editTestMain`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `Edit` | class | The form engine (below). |
| `Edit.Init()` | immediately executed call | Last statement before `editTestMain`: starts loading `type.js`, `UndoStack.js`, `EditOptions.js`, `EditDesktop.js`, `EditMobile.js` and the `EditType*.js` handlers; the promise is not awaited. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `editTestMain` | async `()` | Entry point of the "Edit test" page (`index.html`): creates an editor with `Edit.FromServer("SysWeaver.MicroService.EditTest")`, appends it to `document.body` and logs every `EditChange` as `path = value` to the console (path built from `detail.Member.Name` and the `KeyName` chain following `detail.Next`; objects JSON-stringified). |

#### class `Edit`

One instance edits one object (or one primitive value) of one type description. Created with `new Edit(type, style, options, parent, member, evListener)` (or `Edit.FromServer` / `Edit.Boolean`), then filled with `SetObject(value)`; the host appends `edit.Element`. Child editors (objects, collection items) are created by the handlers with the parent editor as `parentEditor` and share its undo stack.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(type, style, options, parentEditor, member, evListener)` | `type`: `TypeDesc`; `style`: `"Desktop"`/`"Mobile"` or falsy for auto (Mobile when the window is portrait and either `window.Mobile` is set or the width is ≤ 800, else Desktop); `options` defaults to `new EditOptions()`; `parentEditor`: enclosing `Edit` or null; `member`: member desc used for handler lookup; `evListener`: optional `EditChange` listener. Picks the undo stack (`options.UndoStack` → parent's → new), computes `ObjName`, creates `SysWeaver-Edit<style>` (CSS `zoom` scaling on `window.Mobile`, class `NoBorder` when `!options.Border`) with a `<table>`, instantiates the layout with `eval("new Edit"+style+"()")`, resolves `TypeHandler`, converts the type to a one-member primitive when the handler has `IgnoreMembers`, sets `Element.Editor = this`, adds the title row through `layout.AddTypeInfo` when `options.Title` (tooltip with type name/assembly in Dev mode, summary, remarks). A child re-dispatches each `EditChange` to the parent element via `Edit.CreateChangeEvent(member, GetObject(), ev, parent.Key, parent.KeyName)`. Sets `e.onkeydown` (Ctrl+Z → `UndoStack.Undo`, Ctrl+Y → `UndoStack.Redo`, records `Edit.LastEdit`) and calls `Edit.AddGlobalListener()`. |
| `Element`, `Table`, `Edit`, `Type`, `Member`, `Parent`, `Options`, `Style`, `ObjName`, `UndoStack`, `TypeHandler` | field | | Root custom element, its `<table>`, the layout strategy object, the (possibly converted) type, the member desc, the parent editor (or null), the options, the style string, the path name segment (`options.Name` → last dot segment of `type.TypeName` → `"Edit"`), the shared `UndoStack` and the resolved type handler. |
| `Object` | field | | The edited object (set by `SetObject`; primitives are wrapped as `{ [member0.Name]: value }`). |
| `Key`, `KeyName` | field | | Collection key and its display form (`"[i]"`), set by `EditTypeCollection` for item editors; null otherwise. |
| `ArrayEntry` | field | | The `SysWeaver-EditEntry` element of a collection item editor (set by `EditTypeCollection`). |
| `AddMenuItems` | field | `(menuItems, close)` or null | Optional hook adding items to the object menu (and to the member menu of a primitive editor); collections set it on item editors. |
| `RemoveListerners` | field | array | `EditChange` listeners added for `Hide:` expressions; removed on each `SetObject`. |
| `MemberMap` | field | `Map` | Member name → item returned by the layout/handler; filled by `SetObject`. |
| `ChangeTitle` | method | `(name)` | Replaces the text of the first `SysWeaver-TypeTitle` (and, for a primitive editor without title, `SysWeaver-MemberTitle`) when different, refreshing the copy-on-click tooltip when `options.MenuIcon`. Used when collection items are renumbered. |
| `FindItem` | async method | `(name)` → item / `Edit` / null | Resolves a dotted undo path: one segment returns `this` (or the single item of a primitive editor); otherwise looks up `MemberMap` by the second segment (without `[key]`) and recurses into `item.FindItem(rest)` for deeper paths or keyed segments. |
| `GetObjectName` | method | `()` → string | Path of this editor: parent's `GetObjectName()` + `"."` + `ObjName` + `KeyName`. |
| `Invoke` | async method | `(doChange, doReverse, title, name, tracking)` | Registers an undoable change on `UndoStack`. Path = `GetObjectName()` (+ `"." + name` for non-primitives). Wraps the callbacks in inner `onRedo(isFirst)` / `onUndo()` which look up the target with `root.FindItem(path)`, call `doChange(item)` / `doReverse(item)` and then `item.UpdateValue(isFirst)`. `tracking` enables coalescing (see `UndoStack.Invoke`). |
| `UpdateValue` | async method | `()` | Re-renders: `SetObject(GetObject())` (used when an `Edit` is itself the target of an undo path). |
| `GetObject` | method | `()` | Returns `Object`, or the unwrapped value for primitive types. |
| `GetCleanCopy` | method | `(src)` | Returns `src` for primitives, else a JSON deep copy containing only the declared member names. |
| `SetObject` | async method | `(obj)` | Sets the value and rebuilds the rows. `undefined` (or falsy for non-primitives) is replaced by `Edit.CreateDefault`; primitives are wrapped. Deletes all rows except the title row, removes old `Hide:` listeners, then for each member without `TypeMemberFlags.Hide` builds a tooltip (`Dev`: member name, type name, summary, remarks; else summary) and calls `layout.AddMember(...)`. Per item it: evaluates a `member.Type` of `Hide:<expr>` with `new Function("return "+expr).call(obj)` now and on every `EditChange` (inner listener `ls`, toggles `item.Hide()/Show()` and class `Hide` on `DataRow`); stores the item in `MemberMap`; adds class `EditButton` to `item.Icon`; wires the member menu (inner `popupMenu`) to the title cell's `te.onclick` / `te.oncontextmenu` and, with `options.MenuIcon`, to a `ColorIcon(options.MenuIconClass, options.MenuColorClass)` in the icon cell. |
| `Validate` | async method | `(focusOnError)` → `[path, message]` / null | Runs `Edit.ValidateType(Type, Object, Options, primitive ? null : "Edit")`; with `focusOnError` focuses `FindItem(path).Focus`. |
| `GetCopyObject` | method | `(forceJson)` → string | `Edit.GetTypedTextOrJson(GetObject(), forceJson, Type.TypeName)`; clipboard / drag text of the whole value. |
| `GetCopyMember` | method | `(member, forceJson)` → string | Same for one member's value (`obj[member.Name]`, or the primitive value). |
| `CanConsumeCopy` | method | `(obj)` → bool | Primitive editor: `obj` must be a non-object accepted by the handler's `IsOfType`. Object editor: `obj.$type` (before the first comma) must equal `Type.TypeName`. |
| `NotifyChange` | method | `()` | Dispatches an `EditChange` (no member, value `GetObject()`, the editor's `Key` / `KeyName`) on `Element`. |
| `SetTypeInfo` | method | `(c, expandElement, type, title, options)` | Called by the layout's `AddTypeInfo` to fill title cell `c`: optional expand/collapse `ColorIcon` (when `options.CanExpand` and not primitive; inner `setExpand` / `toggleExpand` toggle class `EditorCollapsed` on `expandElement`), a `SysWeaver-PopupMenuIcon` with the object menu (inner `menuPop`: *Set default* → `SetObject()`, *Copy* `GetCopyObject()`, *Paste* from clipboard after `$type`/`IsOfType` check and `Condition`, then `this.AddMenuItems`), also opened by `c.oncontextmenu`; calls `options.AddToHeader(c)`; appends `SysWeaver-TypeTitle` (`options.KeyName` or `type.DisplayName`, `BaseTitle` = tooltip) and a copy-on-click tooltip. Makes `c` draggable (class `EditDraggable`, `application/json` from `GetCopyObject(true)`) and, unless read only, a drop target (`c.ondragover` / `c.ondrop`): JSON accepted via `CanConsumeCopy` is applied undoably with `SetObject` + `NotifyChange`; `text/plain` is applied directly with an `EditChange`. |
| `LastEdit` | static field | | Editor that most recently was created or received a keydown; target of the global Ctrl+Z / Ctrl+Y handler. |
| `HaveGlobalListener` | static field | bool | Guard so `AddGlobalListener` installs only once. |
| `Types` | static field | `Map` | Type name → `TypeDesc` (client-registered via `AddType` or fetched by `GetType`; `null` cached for unknown server types). |
| `CollectionTypeHandler`, `DefaultTypeHandler`, `TypeMap` | static field | | Shared `EditTypeCollection` / `EditTypeDefault` instances and the handler map for lookups without a member. `TypeMapMember`, `ObjectTypeHandler` and `EnumTypeHandler` are created on demand as static properties the same way. |
| `Boolean` | static async method | `(name, value, title, onChange, readOnly, showMenu)` → `Edit` | Builds a one-member primitive `System.Boolean` editor (Desktop) using `EditOptions.Clean`, `CleanReadOnly`, `CleanMenu` or `CleanReadOnlyMenu`, sets `value` and forwards each `EditChange` `detail.Value` to `onChange`. |
| `Init` | static async method | `()` | Loads the dependent scripts in parallel with `includeJs(currentScriptSrc, ...)` (list under Globals). |
| `IsPrimitive` | static method | `(type)` → bool | `TypeMemberFlags.IsPrimitive` set on `type.Flags`. |
| `CreateDefault` | static async method | `(type, options, setTypeName)` | Primitive: `handler.CreateDefault(member0)`. Object: the server default instance (`TypeInfoCache.GetInstance`), else `{}` with each member set to its handler's `CreateDefault`; `setTypeName` adds `$type`. |
| `AddGlobalListener` | static method | `()` | Adds one `keydown` listener to `document.body` sending Ctrl+Z / Ctrl+Y (no Shift/Alt/Meta) to `Edit.LastEdit.UndoStack`. |
| `CreateChangeEvent` | static method | `(member, value, next, key, keyName)` → `CustomEvent` | Creates `EditChange` with `detail { Next, Member, Value, Key, KeyName }`; a truthy value of a Password member (`Flags & 2`) becomes `"***"`. |
| `IsDifferent` | static method | `(a, b)` → bool | Different `typeof`, `!==` for non-objects, JSON string comparison for objects. |
| `CleanUp` | static async method | `(type, obj)` | Deletes own properties of `obj` not declared in `type.Members` (keeps `$type`), recursing into object members and array elements using `Edit.GetType(...)`. |
| `AddType` | static method | `(type)` | Registers a client-side `TypeDesc` in `Types` unless its `TypeName` is already present. |
| `CloneType` | static method | `(src, props)` | JSON deep clone of `src` with `props` assigned; null for falsy `src`. |
| `GetType` | static async method | `(typeName, props, isJsType)` | Returns a clone from `Types`; if missing and not `isJsType`, fetches via `TypeInfoCache.GetType` and caches (including null). |
| `FromServer` | static async method | `(serverTypeName, style, options, props, obj)` → `Edit` / null | `GetType` + `new Edit(t, style, options)` + `SetObject(obj)`. |
| `IsValid` | static method | `(value)` → bool | `typeof value !== "undefined"`. |
| `SetMember` | static async method | `(obj, editor, editContext, member, title, options, c)` → item | Called by the layouts: resolves the handler, calls `handler.AddEditor`, sets `item.TypeHandler` / `item.TitleElement = c`, makes title cell `c` draggable (`EditDraggable`, `application/json` from `GetCopyMember(member, true)`) and, unless read only, a drop target (`c.ondragover`, `c.ondrop`): JSON with matching `$type`/`IsOfType` or `text/plain` passing `IsOfType` is `Condition`ed and applied via `editor.Invoke` (`SetValue`). |
| `IsTrue` | static method | `(val)` → bool | True for `"1"`, `"true"`, `"True"` (after string conversion). |
| `CreateGenericInput` | static method | `(inputType, getValue, setValue, parseValue, toString, validateValue, readOnly)` → `<input>` | Input of the given type; inner `onChange(ev, isTemp)` parses, validates, reformats on final `change` (not on `input`) and calls `setValue(newValue, ev)`, guarded by `isInternal`. `i.onkeyup`: Escape restores the original value. Adds `i.UpdateValue()` (reload from `getValue`). Read only → `readOnly` + class `EditReadOnly`. |
| `CreateTextInput` | static method | `(getValue, setValue, parseValue, toString, placeHolder, validateValue, validateKeyOrPattern, maxLength, def, readOnly, numberOfLines)` → `<input>`/`<textarea>` | Like `CreateGenericInput` but text: `<textarea>` when `numberOfLines > 1`, `maxlength`, placeholder (first line). `validateKeyOrPattern` (string of allowed chars or `fn(ch, pos, value)`) filters `beforeinput` data, rewriting the value and dispatching `change`. `def` is unused. |
| `CreateSliderInput` | static method | `(getStep, setStep, stepCount, readOnly)` → `<input type=range>` | Range 0..`stepCount`; inner `cf` calls `setStep(i.value)` on `change`/`input`; disabled when read only; adds `i.UpdateValue()`. |
| `CreateSelectionInput` | static method | `(getValue, setValue, values, def, readOnly, flags, onOpenFn)` → `<select>` | `values` = `[value, text, title]` arrays. With `flags`, a `multiple` select (size ≤ 4) whose changes are merged bitwise into the current value; otherwise single selection. `onOpenFn(options)` runs on `focusin`. Adds `i.UpdateValue()`. |
| `ValidateType` | static async method | `(type, obj, options, name)` → `[path, message]` / null | Validates each non-hidden member with its handler's `Validate`; an `undefined` value yields "No value found!". Path is `name.member` (or `name` for primitives). |
| `GetIntParam` | static method | `(val, def)` | `parseInt(val)` when `val` is defined, else `def`. |
| `GetTypedTextOrJson` | static method | `(obj, forceJson, typeName)` → string | `"null"`, the string itself (unless `forceJson`), JSON for non-objects/arrays, or JSON with `$type: typeName` added to objects. |
| `SetToString` | static method | `(element, obj)` | Writes a summary of `obj` into `element.innerText` / `title`: `"null"`, the value itself for non-objects, else `toString()` / `ToString()` / JSON, truncated to 21 chars + `...` (full text in title) when longer than 24. |
| `SetCollectionCount` | static method | `(element, collection, container)` | Writes `null`, `empty`, `1 item` or `{n} items` (+ tooltip) using `container.countVal`. |
| `GetTypeMap` | static method | `()` → `Map` | Builds the built-in handler map: signed/unsigned integers → `EditTypeInteger`, `Single/Double/Decimal` → `EditTypeNumber`, `Boolean` → `EditTypeBoolean`, `DateTime/DateOnly/TimeOnly` → `EditTypeDateTime`, the same for `System.Nullable`1[[...]]` names (inner helper `N(n)` builds them), `String` → `EditTypeString`, `Object` → read-only `EditTypeString`. |
| `GetObjectTypeHandler` | static method | `()` | Lazily creates the shared `EditTypeObject` (`Edit.ObjectTypeHandler`). |
| `GetTypeHandler` | static method | `(typename, member, options)` → handler | Uses `TypeMapMember` (with member) or `TypeMap` (without), built by `GetTypeMap` on first use. Key is `typename` or `typename|member.Type`. Unknown keys: global `window["EditType"+spec.split(':')[0]+"Factory"](typename, specParts)` if defined, else the base type's handler, else by flags `Collection` → `EditTypeCollection`, `IsObject` → `EditTypeObject`, `IsEnum` → `EditTypeEnum`, otherwise `EditTypeDefault`; result cached under the key. |

#### Inner functions of `Edit.prototype.SetObject` / `SetTypeInfo` / `Invoke`

| Function | Signature | Description |
|---|---|---|
| `ls` (SetObject) | `()` | `EditChange` listener re-evaluating a `Hide:` expression and calling `item.Hide()` / `item.Show()`. |
| `popupMenu` (SetObject) | async `(ev)` | Ignores bad clicks, reads the clipboard and opens a `PopUpElementMenu` (class `DefaultMenu`, `WebMenu` named `EditObject`) with: *Set default* (`item.DefValue`, disabled when equal/undefined/read only), *Copy* (disabled for Password), *Paste* (JSON-parsed for non-string types, checked with `$type`/`AcceptNull`/`IsOfType`, `Condition`ed), handler `item.AddMenuItems`, editor `AddMenuItems` (primitive editors), *Copy name* and, in Dev mode, *Copy member name* / *Copy type name*. Changes go through `editor.Invoke`. Positioned with `TrackElement`. |
| `notifyChange` (SetObject, inside the menu callback) | `()` | Dispatches `EditChange` for the member's current value (declared, not called). |
| `menuPop` (SetTypeInfo) | async `()` | Opens the object menu (`WebMenu` named `EditProp`) next to the menu icon. |
| `setExpand`, `toggleExpand` (SetTypeInfo) | `()` | Apply / flip the expanded state: class `EditorCollapsed` and icon `SysWeaverEditIconCollapse` / `SysWeaverEditIconExpand`. |
| `onRedo`, `onUndo` (Invoke) | async `(isFirst)` / async `()` | Path-based redo/undo closures registered on the `UndoStack`. |
| `onChange` (CreateGenericInput, CreateTextInput) | async `(ev, isTemp)` | Shared input → value handler (see above). |
| `cf` (CreateSliderInput) | async `(ev)` | Slider change handler. |
| `N` (GetTypeMap) | `(n)` → string | `"System.Nullable`1[[" + n + "]]"`. |

### `EditDesktop.js`

Desktop layout strategy, loaded by `Edit.Init()`. Defines the global class `EditDesktop`; instantiated by the `Edit` constructor through `eval("new EditDesktop()")` when the style is `"Desktop"`.

#### class `EditDesktop`

Stateless; one instance per `Edit`. Lays a form out as a three-column table (title, icon, editor).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `()` | Empty. |
| `AddTypeInfo` | method | `(editor, table, type, title, options)` | Inserts `tr.TitleRow` with one cell (colSpan 3) filled by `editor.SetTypeInfo(c, table, ...)`, followed by a `tr.SeparatorRow`. |
| `AddMember` | async method | `(obj, editor, table, member, title, options, isLast)` → item / null | Inserts `tr.ValueRow` with a title cell holding `SysWeaver-MemberTitle` (`member.DisplayName`), an icon cell and an editor cell; builds `editContext { PrevRow, Element, CreateData() }` (`CreateData` appends a row with one colSpan-3 cell and returns the cell) and calls `Edit.SetMember`. Prepends `item.Title` to the tooltip (`BaseTitle`), adds a `SeparatorRow` unless `isLast`, attaches `item.Hide()` / `item.Show()` (`ret.Hide` / `ret.Show`; toggle class `Hide` on the value row and separator), sets `item.Icon`, and adds `EditReadOnly` to the title cell when read only. |

### `EditMobile.js`

Mobile layout strategy, loaded by `Edit.Init()`. Defines the global class `EditMobile`; instantiated through `eval("new EditMobile()")` when the style is `"Mobile"`.

#### class `EditMobile`

Stateless; lays a form out as a two-column table with the editor below its title.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `()` | Empty. |
| `AddTypeInfo` | method | `(editor, table, type, title, options)` | Same as desktop with colSpan 2. |
| `AddMember` | async method | `(obj, editor, table, member, title, options, isLast)` → item | Inserts `tr.HeaderRow` (title cell with `SysWeaver-MemberTitle`, icon cell) and `tr.ValueRow` (editor cell, colSpan 2); `editContext.PrevRow` is the value row and `CreateData()` appends a colSpan-2 cell. Calls `Edit.SetMember`, prepends `item.Title` to the tooltip, sets `item.Icon`, adds a `SeparatorRow` unless `isLast`, attaches `item.Hide()` / `item.Show()` (`ret.Hide` / `ret.Show`; class `Hide` on header, value and separator rows) and adds `EditReadOnly` when read only. |

### `EditOptions.js`

Option bag for `Edit`, loaded by `Edit.Init()`. Defines the global class `EditOptions`.

#### class `EditOptions`

Created with `new EditOptions()` (all defaults) or from a preset via `Clone()`; passed to `new Edit(...)` and copied/modified by the object and collection handlers for child editors.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Title` | field | `true` | Show the title row (type name, menu, expand icon). |
| `Dev` | field | `true` | Developer tooltips and *Copy member/type name* menu items. |
| `ReadOnly` | field | `false` | Make every member read only. |
| `CanExpand` | field | `true` | Show the expand/collapse icon in the title row. |
| `IsExpanded` | field | `true` | Initial expanded state of the title-row toggle. |
| `KeyName` | field | `null` | Title text override (collection item key such as `[3]`). |
| `Border` | field | `true` | When false the root element gets class `NoBorder`. |
| `ExpandAllCollections` | field | `false` | Collections expand automatically on creation. |
| `MenuIcon` | field | `false` | Add a menu `ColorIcon` in each member's icon cell. |
| `MenuIconClass`, `MenuColorClass` | field | `"SysWeaverEditIconMenu"`, `"IconColorThemeAcc2"` | Icon and colour classes for that menu icon. |
| `AddToHeader` | field | `null` or `(cell)` | Hook called by `Edit.SetTypeInfo` to add elements to the title cell. |
| `Name`, `UndoStack` | field (undeclared) | | Read by the `Edit` constructor (path name / shared undo stack); set by handlers, not copied by `Clone`. |
| `Clone` | method | `()` → `EditOptions` | Copies the declared fields above into a new instance. |
| `CleanMenu` | static field | | Preset built by an IIFE: `Title`, `CanExpand`, `IsExpanded`, `Border`, `Dev` all false. |
| `CleanReadOnlyMenu` | static field | | `CleanMenu` clone with `ReadOnly = true`. |
| `Clean` | static field | | `CleanMenu` clone with `MenuIcon = false`. |
| `CleanReadOnly` | static field | | `CleanMenu` clone with `ReadOnly = true`, `MenuIcon = false`. |

### `UndoStack.js`

Linear undo/redo history, loaded by `Edit.Init()`. Defines the global classes `UndoEntry` and `UndoStack`.

#### class `UndoEntry`

One recorded operation; created by `UndoStack.Invoke`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `(doChange, doReverse, title, name, tracking)` | Stores the arguments. |
| `DoChange`, `DoReverse`, `Title`, `Name`, `Tracking` | field | | Redo callback (`(isFirst)`), undo callback, display title, path name and coalescing token. `Focus` (optional element) is read by `Undo`/`Redo` but never assigned. |
| `toString` | method | `()` | `"<Name>: <Title>"`. |

#### class `UndoStack`

One instance per editor tree (created by the root `Edit` unless `options.UndoStack` is supplied).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Pos` | field | `0` | Number of applied operations (index of the next redo). |
| `Ops` | field | `[]` | Recorded `UndoEntry` list. |
| `toString` | method | `()` | `"Pos/Ops.length"`. |
| `Invoke` | async method | `(doChange, doReverse, title, name, tracking)` | Truncates redo history; if `tracking` is truthy and the top entry has the same `name` and `tracking`, runs `doChange()` and replaces that entry's `DoChange` and `Title` (coalescing). Otherwise runs `doChange(true)` and pushes a new entry. Errors are logged with `console.warn` and the entry is not pushed. |
| `CanUndo` | method | `()` → bool | `Pos > 0`. |
| `CanRedo` | method | `()` → bool | `Pos < Ops.length`. |
| `Undo` | async method | `()` → bool | Runs `DoReverse()` of the entry below `Pos`, decrements `Pos`, focuses `op.Focus` if set; false when nothing to undo or on error. |
| `Redo` | async method | `()` → bool | Runs `DoChange(false)` of the entry at `Pos`, increments `Pos`, focuses `op.Focus` if set; false when nothing to redo or on error. |

### `EditCollectionArray.js`

Array container strategy for `EditTypeCollection`, loaded by `EditTypeCollection.Init()`. Defines the global class `EditCollectionArray`.

#### class `EditCollectionArray`

Stateless; used through the singleton `EditCollectionArray.Inst` (returned by `EditTypeCollection.GetContainer` when the member has no `Key`). `c` is the JS array, `key` an index.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Inst` | static field | | Shared instance. |
| `createVal` | method | `()` → `[]` | New empty collection. |
| `countVal` | method | `(c)` → number | Length, 0 for falsy `c`. |
| `getVal` | method | `(c, key)` | `c[key]`. |
| `setVal` | method | `(c, key, newValue)` | `c[key] = newValue`. |
| `getKeyName` | method | `(key)` → string | `"[" + key + "]"`. |
| `íterate` | async method | `(c, onKeyValue)` → bool | (Name uses a non-ASCII `í`.) Calls `await onKeyValue(i, c[i])` in order; stops and returns false when the callback returns falsy, true when all ran (undefined for falsy `c`). |
| `onKeys` | async method | `(c, onKey)` | Awaits `onKey(i)` for each index. |
| `addVal` | method | `(c, newValue, key)` → key | Inserts at `key` when `key` is defined and `< c.length`, else appends; returns the new index. |
| `removeVal` | method | `(c, key)` → `true` | Splices out the element; returns true (keys must be renumbered). |
| `insertAt` | method | `(c, newValue, key)` → key | Inserts at `key` (indexed collections only). |
| `insertAfter` | method | `(c, newValue, key)` → key | Inserts at `key + 1` and returns that index. |
| `move` | method | `(c, from, to)` | Moves one element from index `from` to index `to`. |

### `EditCollectionMap.js`

`Map` container strategy for `EditTypeCollection`, loaded by `EditTypeCollection.Init()`. Defines the global class `EditCollectionMap`.

#### class `EditCollectionMap`

Stateless; used through `EditCollectionMap.Inst`, returned by `EditTypeCollection.GetContainer` when `member.Key && member.Element`. Same interface as `EditCollectionArray` minus the indexed operations.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Inst` | static field | | Shared instance. |
| `createVal` | method | `()` → `Map` | New empty map. |
| `countVal` | method | `(c)` → number | `c.size`, 0 for falsy `c`. |
| `getVal` | method | `(c, key)` | `c.get(key)`. |
| `setVal` | method | `(c, key, newValue)` | `c.set(key, newValue)`. |
| `getKeyName` | method | `(key)` → string | `"[" + key + "]"`. |
| `íterate` | async method | `(c, onKeyValue)` → bool | Iterates `c.entries()`, passing each key / value to `onKeyValue`; stops and returns false when the callback returns falsy, true when all ran (undefined for falsy `c`). |
| `onKeys` | async method | `(c, onKey)` | Awaits `onKey(key)` for each key of `c.keys()`. |
| `addVal` | method | `(c, newValue, key)` → key | `c.set(key, newValue)`. |
| `removeVal` | method | `(c, key)` → `false` | `c.delete(key)`; returns false (no renumbering needed). |

### `EditTypeObject.js`

Type handler for nested objects (members with `TypeMemberFlags.IsObject`), loaded by `Edit.Init()`; a single shared instance is created by `Edit.GetObjectTypeHandler()`. Defines the global class `EditTypeObject`. Implements the handler contract described above.

#### class `EditTypeObject`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateDefault` | method | `(member, options)` | Returns `GetDefault(member, options)`. |
| `IsOfType` | method | `(obj, member, options)` → bool | True when `member` is not primitive, `obj` is an object and `obj.$type === member.TypeName`. |
| `Condition` | method | `(obj)` | Returns `obj` unchanged. |
| `Validate` | async method | `(obj, member, options, name)` | Null value: OK with `AcceptNull`, else `[name, "<DisplayName>: Value is null and null is not allowed!"]`. Otherwise runs `Edit.ValidateType` on the member's type (`Edit.GetType(member.TypeName, member.ElementInst)`) and prefixes the error as `[name + "." + path, DisplayName + "/" + message]`. |
| `ToString` | method | `(obj, member, options)` | `JSON.stringify(obj)`, or null. |
| `GetDefault` | method | `(member, options)` | `{}` for a `"\t"` default, `JSON.parse(member.Default)` when defined, `null` with `AcceptNull`, else `{}`. |
| `CloneDef` | static method | `(def)` | JSON deep clone, null for falsy. |
| `AddEditor` | async method | `(obj, editor, editContext, member, title, options)` → item | Resolves the member type, clones options for the child (`Title=false`, `ReadOnly`, `Name = member.Name`). If the metadata default is an empty object, uses the server default instance (`TypeInfoCache.GetInstance`) or else builds one with a temporary child `Edit` (`SetObject({})`), adding a "Default value" tooltip line. Writes a clone of the default into `obj[mn]` when undefined. Adds class `EditObject` to the cell, creates a `DataRow` via `editContext.CreateData()`, a 36 px expand `ColorIcon` (disabled when read only and null) and a `SysWeaver-EditInfo` summary (`Edit.SetToString`). Returns `{ Title, DefValue, DefValueText (JSON), DataRow, SetValue, UpdateValue, AddMenuItems, FindItem }`: `SetValue` stores and dispatches `EditChange`; `UpdateValue` refreshes the summary and collapses / re-expands (only re-expands when non-null); `AddMenuItems` adds nothing; `FindItem(name)` expands if needed and delegates to the child editor. |

#### Inner functions of `EditTypeObject.AddEditor`

| Function | Signature | Description |
|---|---|---|
| `toggleExpand` | async `(ev)` | Ignores non-pure clicks. Expanding clears the data cell, creates `SysWeaver-EditEntry` (`SysWeaver-EditEntryHeader` + `SysWeaver-EditEntryValue`) with a child `Edit(objectType, editor.Style, newOpt, editor, member)` set to `obj[mn]`; if the value was null, records an undoable "Create object" step that stores the child's default object. Keeps the summary in sync on child `EditChange`, sets the collapse icon and class `EditorExpanded` on the data row and the previous row. Collapsing clears the cell and reverses these. |

### `EditTypeCollection.js`

Type handler for collections (members with `TypeMemberFlags.Collection`), loaded by `Edit.Init()`; the shared instance is `Edit.CollectionTypeHandler`. On evaluation runs `EditTypeCollection.Init()`, which loads `EditCollectionArray.js` and `EditCollectionMap.js`. Defines the global class `EditTypeCollection`. Implements the handler contract described above; each element is edited by its own child `Edit`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `EditTypeCollection` | class | The collection handler (below). |
| `EditTypeCollection.Init()` | immediately executed call | Starts loading the two container strategy scripts (not awaited). |

#### class `EditTypeCollection`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Init` | static async method | `()` | `includeJs(currentScriptSrc, "EditCollectionArray.js")` and `"EditCollectionMap.js"` in parallel. |
| `CreateDefault` | method | `(member, options)` | Returns `GetDefault(member, options)[0]` (element 0 of the default collection). |
| `IsOfType` | method | `(obj, member)` → bool | False unless `obj` is an array; false when the count exceeds `member.Max` (default 1000000000); otherwise true. |
| `Condition` | method | `(obj)` | Returns `obj` unchanged. |
| `Validate` | async method | `(obj, member, options, name)` | Null: OK with `AcceptNull`, else "Value may not be null!". Checks count against `Max` ("To many items!") and `Min` ("To few items!") using `ValueFormat.countSuffix`. Then validates each element with `Edit.ValidateType(elementType, value, options, name + "[key]")`, temporarily setting the element type's `DisplayName` to the key name; returns the first error as `[path, DisplayName + message]`. |
| `ToString` | method | `(obj, member, options)` | `"" + obj`. |
| `GetDefault` | method | `(member, options)` | Empty container for a `"\t"` default; `null` with `AcceptNull`; otherwise an empty container (`GetContainer(member).createVal()`). |
| `GetContainer` | method | `(member)` → strategy | `EditCollectionMap.Inst` when `member.Key` and `member.Element`, `null` when only `member.Key` (set, not implemented), else `EditCollectionArray.Inst`. |
| `AddEditor` | async method | `(obj, editor, editContext, member, title, options)` → item | Reads `Min`/`Max` (tooltip lines "Minimum/Maximum allowed count"), writes the default into `obj[mn]` when undefined, computes `isReadOnly`, `isFixed` (`Min >= Max` or read only) and `isIndexed` (`TypeMemberFlags.Indexed`). Adds class `EditContainer` to the cell, a `DataRow` via `CreateData()`, a 36 px expand `ColorIcon` and a `SysWeaver-EditInfo` count (`Edit.SetCollectionCount`). Resolves the element type (`Edit.GetType(member.ElementTypeName, member.ElementInst)`) and handler. Expands immediately with `options.ExpandAllCollections`. Returns `{ Title, DefValue, DefValueText ("empty"), DataRow, Focus (expand icon), UpdateValue, FindItem, GetValue, SetValue, AddMenuItems }`: `UpdateValue` refreshes the count; `FindItem(name)` parses `[key]` from the first segment, expands if needed and delegates to that item editor; `GetValue` returns `obj[mn]`; `SetValue` stores, re-renders when expanded and calls `colChanged`; `AddMenuItems(menu, close)` (not when read only) adds *Clear collection* / *Make empty* (`Id: "Clear"`) and *Add item* (`Id: "Add"`, appends `CreateNewDefault()` via `container.addVal`, creating the container if null, expanding afterwards), both undoable via `editor.Invoke`. |

#### Inner functions of `EditTypeCollection.AddEditor`

| Function | Signature | Description |
|---|---|---|
| `toggleExpand` | async `(ev, onEditor)` | Ignores non-pure clicks, stops propagation. Expanding clears the data cell and calls `addItemEditor` for each key (`onEditor(key, edit)` callback optional), switches the icon and adds `EditorExpanded` to the data and previous row when non-empty. Collapsing clears `keyMap` and the cell. |
| `reOrder` | async `(values)` | Walks the `SysWeaver-EditEntry` children in key order and, where the key changed, updates `keyMap`, the item editor's `Key`, `ArrayEntry`, `KeyName` and title (`ChangeTitle`). |
| `CreateNewDefault` | async `()` | Default for a new element: `null` when the element type accepts null and the handler's `GetDefault` is falsy, else `Edit.CreateDefault(elementType, options)`. |
| `focus` | `(key)` | Focuses the item editor element for `key`. |
| `remove` | `(key)` | Removes the item's `SysWeaver-EditEntry` from the DOM. |
| `colChanged` | `()` | Dispatches `EditChange` for the whole collection (`member`, `obj[mn]`) on `editor.Element`. |
| `addItemEditor` | async `(values, key, relativeTo, relation)` → `Edit` | Creates `SysWeaver-EditEntry` (header + value) with a child `Edit(elementType, editor.Style, newOpt, editor, member.ElementInst, evListener)`; `newOpt` is a new `EditOptions` with `KeyName = "[key]"`, `Name = mn` and a `newOpt.AddToHeader` hook (inner, adds a remove `ColorIcon("../icons/clear.svg")` with class `EditFloatRight` and, for indexed collections, an insert-after `ColorIcon("../icons/plus.svg")` unless fixed/read only). For primitive elements copies `Min/Max/Default/Flags/Summary/Remark` from the element type onto its first member and sets `DisplayName` to the key name. The `evListener` writes the child's top-level value back with `container.setVal`. Registers the editor in `keyMap`, sets `Key`, `KeyName`, `ArrayEntry`, and `AddMenuItems` (inner, see below), then inserts the entry at `relativeTo`/`relation` (`insertAdjacentElement`) or appends it. |
| `Remove` | async `()` (inside `addItemEditor`) | Undoable removal of the item (`container.removeVal`, DOM removal, `reOrder` when needed); undo re-inserts with `container.insertAt` and a new item editor at the original position. |
| `InsertAfter` | async `()` (inside `addItemEditor`) | Undoable insertion of `CreateNewDefault()` after the item (`container.insertAfter`), via `itemEdit.Invoke`. |
| `itemEdit.AddMenuItems` | `(menuItems, close)` (inside `addItemEditor`) | Adds *Remove item* (`Id: "Remove"`, disabled when fixed) and, for indexed collections, *Insert before* / *Insert after* (disabled when fixed or at `Max`), *Move to top* / *Move up* / *Move down* / *Move to bottom* (`Id`s `MoveTop`, `MoveUp`, `MoveDown`, `MoveBottom`; disabled when fixed or already at the end), each with the `SysWeaverEditIcon*` icon classes. |
| `Move` | async `(from, to)` (inside `itemEdit.AddMenuItems`) | Undoable move: `container.move`, moves the DOM entry (or expands), `reOrder`, `colChanged`, count update; undo moves back. |

### `EditTypeBoolean.js`

Type handler for `System.Boolean` and `System.Nullable<Boolean>`. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeBoolean`. `Edit.GetTypeMap()` creates one instance per map (it passes `true` for the nullable variant, but the class has no constructor, so the argument is ignored). It implements the type-handler contract and renders the value as a two-position slider.

#### class `EditTypeBoolean`

Stateless handler; one instance is shared by all boolean members.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateDefault` | method | `CreateDefault(member, options)` → value | Returns element `[0]` of `GetDefault` (the member default converted to a boolean, or `undefined`/the raw invalid value when no default is set). |
| `IsOfType` | method | `IsOfType(obj)` → bool | True for a `boolean`, the strings `"true"`/`"false"`, and the numbers `0`/`1`. Everything else is false. |
| `Condition` | method | `Condition(obj)` → bool | Converts to a boolean: string → `obj == "true"`, boolean → `!!obj`, anything else → `obj == 1`. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | Accepts booleans and the strings `"true"`/`"false"`. Any other value, including the numbers 0 and 1, gives an error that names the expected values `"true"`, `"false"`, 1 or 0. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"true"` if `obj` is truthy, otherwise `"false"`. |
| `GetDefault` | method | `GetDefault(member, options)` → `[def, typeTitle]` | When `member.Default` is valid (`Edit.IsValid`), converts it with `Edit.IsTrue` and adds a "Default value" line to the tooltip text `typeTitle` (`ValueFormat.AddNonNullLine`). Returns `[def, typeTitle]`; `typeTitle` is `null` when there is no default. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Sets `obj[member.Name]` to the default (or `false`) when it is `undefined` or an object (null). Creates a slider with `Edit.CreateSliderInput` (range 0..1, read-only when `options.ReadOnly` or the member has the `ReadOnly` flag). The slider getter toggles the class `EditTrue` on the cell. The setter records an undoable change through `editor.Invoke` (description "Set value: …", undo restores the old value). Adds `EditBoolean` to the cell and appends the slider. Returns `{Focus, Title, DefValue, DefValueText, SetValue, UpdateValue}`: `SetValue(newVal)` writes `obj[mn]` and dispatches an `EditChange` event (`Edit.CreateChangeEvent`) on `editor.Element`; `UpdateValue(isFirst)` refreshes the slider and the `EditTrue` class and focuses the slider unless `isFirst`. |

### `EditTypeDateTime.js`

Type handler for `System.DateTime`, `System.DateOnly` and `System.TimeOnly` and their nullable versions. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeDateTime`. `Edit.GetTypeMap()` creates instances with `(false)`, `(false, true)` and `(false, false, true)`. The nullable entries get a fourth argument `true`, which the constructor ignores. Values are kept as ISO strings, `Date` objects, or (date-only / time-only) plain `yyyy-mm-dd` / `hh:mm:ss` strings. The editor uses native `<input type="date">` and `<input type="time">` controls.

#### class `EditTypeDateTime`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `MinDate` | static field | `Date` | `new Date(0)` (1970-01-01T00:00:00Z), the smallest allowed value. Also used by `Condition` as the replacement for unparsable input. |
| `MaxDate` | static field | `Date` | `new Date(8640000000000000)`, the largest JavaScript date. |
| `ToDateTime` | static method | `ToDateTime(obj, onInvalid, onDefault)` → `Date` or fallback | Returns `obj` if it already is a `Date`. Returns `onDefault` for `"\t"` or `""`. Otherwise parses `"" + obj` with `Date.parse` and returns a new `Date`, or `onInvalid` when parsing fails (or gives 0). |
| `constructor` | constructor | `new EditTypeDateTime(forceReadOnly, onlyDate, onlyTime)` | Stores the three booleans as `ForceReadOnly`, `OnlyDate` and `OnlyTime`. |
| `ForceReadOnly`, `OnlyDate`, `OnlyTime` | field | bool | Set by the constructor. `ForceReadOnly` makes the editor read-only. `OnlyDate` / `OnlyTime` select the DateOnly / TimeOnly behaviour (only one input, string values). |
| `GetTimeString` | static method | `GetTimeString(h, m, s)` → string | Formats `hh:mm:ss`, each part zero-padded to 2 digits. |
| `GetDateString` | static method | `GetDateString(y, m, d)` → string | Formats `yyyy-mm-dd` (year padded to 4, month and day to 2). `m` is 1-based. |
| `GetLocalTimeString` | method | `GetLocalTimeString(v, unspecifiedDate)` → string or `null` | `null` for date-only handlers. Returns `v` unchanged for time-only handlers. Otherwise converts `v` to a `Date` (if needed) and returns its local `hh:mm:ss`. `unspecifiedDate` is not used. |
| `GetLocalDateString` | method | `GetLocalDateString(v, unspecifiedDate)` → string or `null` | `null` for time-only handlers. For date-only handlers a non-`Date` value is returned unchanged. Otherwise returns the local `yyyy-mm-dd` of `v`. `unspecifiedDate` is not used. |
| `SetLocalTimeString` | method | `SetLocalTimeString(org, v, unspecifiedDate)` → `Date`, string or `null` | Merges the time text `v` (`h[:m[:s]]`) into the original value `org`. `null` for date-only, `v` itself for time-only. Otherwise builds a local `Date` from the date of `org` and the parsed time. Returns a `Date` if `org` was one, else `toISOString()`. |
| `SetLocalDateString` | method | `SetLocalDateString(org, v, unspecifiedDate)` → `Date`, string or `null` | Merges the date text `v` (`y-m-d`) into `org`. `null` for time-only. Returns `v` unchanged for date-only when `org` is not a `Date`. With `unspecifiedDate` it returns the string `yyyy-mm-ddThh:mm:ss` (local time of `org`, no zone). Otherwise builds a local `Date` and returns it as a `Date` or ISO string, like `SetLocalTimeString`. |
| `IsValidDate` | static method | `IsValidDate(v)` → bool | True for a `Date`, or a `y-m-d` string with three parts, a numeric year, a month 1–12 and a day 1–31. Not referenced elsewhere in this folder. |
| `IsValidTime` | static method | `IsValidTime(v)` → bool | True for a `Date`, or a `h[:m[:s]]` string with hour 0–23 and minute/second 0–60. Not referenced elsewhere in this folder. |
| `CreateDefault` | method | `CreateDefault(member, options)` → value | Returns the default (`[2]`) from `GetMinMax`. |
| `IsOfType` | method | `IsOfType(obj)` → bool | True for a `Date` or a string that `Date.parse` accepts. |
| `Condition` | method | `Condition(obj, member, options)` → value | Clamps the value into `[min, max]` from `GetMinMax`. A `Date` input is clamped and returned as a `Date` (unclamped for time-only). For a time-only handler a non-`Date` value is returned unchanged. Otherwise the string is parsed (unparsable → `MinDate`), clamped, and returned as a local date string (date-only) or `toISOString()`. |
| `GetDate` | static method | `GetDate(d, hour)` → `Date` | Local date of `d` at `hour`:00:00, or 12:00 when `hour` is missing. |
| `GetMinDate` | static method | `GetMinDate(d)` → `Date` | Local date of `d` at 00:00:00. |
| `GetMaxDate` | static method | `GetMaxDate(d)` → `Date` | Local date of `d` at 23:59:59.999. |
| `GetMinMax` | method | `GetMinMax(member, options)` → `[min, max, def, typeTitle, haveMin, haveMax, parsedDef, defText]` | Parses `member.Min` (start of day) and `member.Max` (end of day) with `ToDateTime`, clamps both to `MinDate`/`MaxDate` and makes `max >= min`. The default is `member.Default` (parsed), else the current local date at the current hour (minutes and seconds zeroed), clamped to `[min, max]`. `haveMin`/`haveMax` are true when the member value was valid and survived clamping. Tooltip lines "Minimum value", "Maximum value" and "Default value" are added when the value was given, or always when `options.Dev` is set. If `member.Default` is a string, the returned `def` is converted to an ISO string. `defText` is `ToString(def)`. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | A non-`Date` must be a string ("Value must be a string"). Time-only strings are accepted without further checks. Other strings must parse ("Value must be a valid date / time string"). The value is then compared with `min`/`max` from `GetMinMax`. Below `min` is an error only when the member lacks `AcceptNull`. The date-only messages show the limit with `GetLocalDateString`. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"<date> <time>"` for full DateTime, else only the date or the time part, using `GetLocalDateString` / `GetLocalTimeString`. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Initialises `obj[mn]` to the default when `undefined`. Unless time-only, creates a date input (`Edit.CreateGenericInput("date", …)`) whose parser merges the picked date into the current value with `SetLocalDateString`, and whose formatter is `GetLocalDateString`; `min`/`max` attributes are set when `haveMin`/`haveMax`. Unless date-only, creates a time input with `step="1"` using `SetLocalTimeString` / `GetLocalTimeString`. Both inputs get the class `HalfWidth` when both are shown (the time input always gets it). Read-only from `options.ReadOnly`, the `ReadOnly` member flag or `ForceReadOnly`. Returns `{Focus, Title, DefValue, DefValueText, SetValue, UpdateValue}` with the same `SetValue`/`UpdateValue` behaviour as the other primitive handlers (`EditChange` event; refresh both inputs and focus the first unless `isFirst`). |

#### Inner functions of `EditTypeDateTime.AddEditor`

| Function | Signature | Description |
|---|---|---|
| `setValue` | async `setValue(v)` → bool | Shared setter of both inputs. Returns `false` if `v` equals the current value. Otherwise records an undoable change with `editor.Invoke` (description `Set value: "<ToString>" [<raw>]`) and returns `true`. |

### `EditTypeDefault.js`

Fallback type handler used by `Edit.GetTypeHandler()` when no other handler matches (unknown non-primitive types, members without special flags). Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeDefault`. One shared instance lives in `Edit.DefaultTypeHandler`. The editor is a read-only text box showing `"" + value`.

#### class `EditTypeDefault`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `CreateDefault` | method | `CreateDefault(member, options)` → value | Returns `GetDefault(member, options)`. |
| `IsOfType` | method | `IsOfType(obj, member, options)` → bool | For a non-primitive member: true if `obj` is an object whose `$type` equals `member.TypeName`. Always false for primitives. |
| `Condition` | method | `Condition(obj)` → `obj` | Returns `obj` unchanged (removing `$type` is commented out). |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | Error "Value is null and null is not allowed!" when `obj == null` and the member lacks `AcceptNull`. Otherwise `null`. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"" + obj`. |
| `GetDefault` | method | `GetDefault(member, options)` → value | `member.Default` when valid; `null` when the member has `AcceptNull`; otherwise `{}` (also when `member` is missing). |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Uses `member.Default` (if valid) as the default and adds it to the tooltip. Sets `obj[mn]` to the default or `null` when `undefined`. Creates a read-only `Edit.CreateTextInput` with the formatter `v => "" + v` and the member summary as placeholder, and appends it. Sets bit 16 (`ReadOnly`) on `member.Flags` (this changes the member description object). Returns `{Title, DefValue, DefValueText, UpdateValue}` with no `Focus` and no `SetValue`. |

### `EditTypeEnum.js`

Type handler for enum members (`IsEnum` flag), including `[Flags]` enums. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeEnum`. `Edit.GetTypeHandler()` keeps one shared instance in `Edit.EnumTypeHandler`. The enum values are encoded by the server in `member.Default` as `<default>;…>value<Name<Summary<Remarks|value<Name…` (the part after `>` is a `|`-separated list of `<`-separated fields). The editor is a selection input (`Edit.CreateSelectionInput`); flag enums use a multi-select.

#### class `EditTypeEnum`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `NumSetBits` | static method | `NumSetBits(num)` → number | Counts the `1` digits in `num.toString(2)`. |
| `constructor` | constructor | `new EditTypeEnum(forceReadOnly)` | Sets `ForceReadOnly` and creates an empty `TypeMap`. |
| `ForceReadOnly` | field | bool | Forces the editor to read-only. |
| `TypeMap` | field | `Map<typeName, Map>` | Cache of value maps per `member.TypeName`, filled by `GetValueMap`. |
| `GetValueMap` | method | `GetValueMap(member, options)` → `Map` | Builds (once per type name) a map from every number, number string, raw name and de-camel-cased name (`ValueFormat.removeCamelCase`) to `[value, displayName, rawName]`. Special keys: `"___single___"` (flag enums: list of `[bit, displayName]` for single-bit values; empty for normal enums), `"___edit___"` (options for the editable selector, `[value, displayName, tooltip]`; multi-bit flag values get their decomposed text prepended to the tooltip) and `"___editro___"` (options for the read-only selector: all values for normal enums, single-bit values for flag enums). For flag enums `0` and `"0"` map to `[0, "0", "0"]`. The tooltip is `"<value>: <name>"` plus summary and remarks lines. |
| `CreateDefault` | method | `CreateDefault(member, options)` → number | Returns `GetDefault(member, options)`. |
| `IsOfType` | method | `IsOfType(obj)` → bool | True for any string and for numbers where `parseInt` is not `NaN`. |
| `Decode` | method | `Decode(obj, member, options)` → number or `null` | A number is split into its set bits; a string is split on `+`. Each part is looked up in the value map (a numeric part only matches if it is a known value) and the results are OR-ed. Returns `null` if any part is unknown. |
| `Condition` | method | `Condition(obj, member, options)` → value | Decodes `obj`; if the result is not a number, uses `GetDefault`. Returns that number. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | Error for `null`, and for values that `Decode` cannot turn into a number ("not a valid enum value, expecting a number or enum text"). |
| `ToString` | method | `ToString(obj, member, options)` → string | Normal enum: the display name of `obj`, or `obj` itself if unknown. Flag enum: the display names of all set single-bit values joined with `" + "`, or `"0"` when none is set. |
| `GetDefault` | method | `GetDefault(member, options)` → number | `parseInt` of the text before the first `;` in `member.Default`, or `0` when there is no valid default. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Initialises `obj[mn]` to the default when `undefined` and adds a "Default value" tooltip line (as text). If `member.Type` starts with `Allow.`, the rest names a sibling member that holds a bit mask of allowed values: an open callback disables every option whose bit (`1 << value`) is not set in that mask (a warning is logged if the sibling is `undefined`). Creates `Edit.CreateSelectionInput(getter, setter, options, def, readOnly, isFlags, openFn)`; the option list is `"___editro___"` when read-only, else `"___edit___"`. The setter `parseInt`s the selection and records an undoable change via `editor.Invoke`. Returns `{Focus, Title, DefValue, DefValueText, SetValue, UpdateValue}` as for the other primitive handlers. |

### `EditTypeInteger.js`

Type handler for the integer types `SByte`, `Int16`, `Int32`, `Int64`, `Byte`, `UInt16`, `UInt32`, `UInt64` and their nullable versions. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeInteger`. `Edit.GetTypeMap()` creates the instances; the 64-bit types pass `Number.MIN_SAFE_INTEGER` / `MAX_SAFE_INTEGER` (or 0) as literal limits. The editor is a filtered text box, plus a slider when the member has the `Slider` flag and both limits.

#### class `EditTypeInteger`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new EditTypeInteger(signed, byteSize, nullable, literalMin, literalMax)` | Stores `Signed`, `ByteSize` and `Nullable`. If `literalMax` is given, `TypeMin`/`TypeMax` are the literal limits; otherwise they are computed from the size: `0 … 2^(8·byteSize)` unsigned, `-2^(8·byteSize-1) … 2^(8·byteSize-1)-1` signed. |
| `Signed`, `ByteSize`, `Nullable`, `TypeMin`, `TypeMax` | field | | Constructor values and the type range. `Nullable` is stored but not read by this class. |
| `IsOfType` | method | `IsOfType(obj)` → bool | Accepts numbers and strings that `parseInt` (or `parseFloat`, rounded) can parse, if the value is within `[TypeMin, TypeMax]`. |
| `Condition` | method | `Condition(obj, member, options)` → number | Parses strings (`parseInt`, falling back to `parseFloat`), rounds and truncates to 32 bits (`Math.round(obj) | 0`), then clamps to the member min/max from `GetMinMaxDefault`. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | Error "Value to small!" / "Value to large!" when `obj` is outside the member min/max. |
| `GetDefault` | method | `GetDefault(mmd)` → number | Takes the array returned by `GetMinMaxDefault`. Returns `mmd[2]` if truthy, else `0` clamped to `[mmd[0], mmd[1]]`. |
| `CreateDefault` | method | `CreateDefault(member, options)` → number | `GetDefault(GetMinMaxDefault(member, options))`. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"" + obj`. |
| `GetMinMaxDefault` | method | `GetMinMaxDefault(member, options)` → `[min, max, def, typeTitle, haveMin, haveMax, parsedDef]` | Parses `member.Min`/`member.Max` as integers and clamps them to the type range (`max >= min`). Parses `member.Default` (`parseFloat`, truncated), clamped to `[min, max]`, `0` when missing. `haveMin`/`haveMax` are true when the member value was valid and survived clamping. Adds "Minimum value", "Maximum value", "Default value" tooltip lines when given, or always with `options.Dev`. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Initialises `obj[mn]` with `GetDefault` when it is `undefined` or an object (null). Creates `Edit.CreateTextInput` with the parser `fromText`, a key filter that accepts digits and a leading `-` (only when the type is signed and `min < 0`), a maximum length equal to the longer of the min/max texts, and `style.maxWidth = (maxLen + 1)em`. If the member has the `Slider` flag and both `haveMin` and `haveMax`, also creates `Edit.CreateSliderInput`: the step is `member.EditParams` as an integer (a warning is logged when invalid) or the smallest power of ten that gives at most 100 steps; the slider position maps to `(pos + floor(min/step)) * step`, clamped. The cell then gets the class `EditSlider`. Both setters record undoable changes via `editor.Invoke`. `DefValue`/`DefValueText` are the parsed member default (`mmd[6]`). `UpdateValue(isFirst)` refreshes the text and slider and focuses the text unless `isFirst` or the slider has focus. |

#### Inner functions of `EditTypeInteger.AddEditor`

| Function | Signature | Description |
|---|---|---|
| `fromText` | `fromText(v)` → number | Parses the text with `parseFloat` and `Math.floor`, uses 0 for `NaN`/0, and clamps to `[min, max]`. |

### `EditTypeLanguage.js`

Plug-in type handler for string members with the C# `[EditType(EditTypes.Language)]` spec (`member.Type == "Language"`). Not loaded by `Edit.Init()`; a page must include it with a `<script>` or `includeJs` after `Edit.js`. `Edit.GetTypeHandler()` finds it through the global factory `EditTypeLanguageFactory`. Defines the globals `EditTypeLanguageFactory` and `EditTypeLanguage`, and runs `EditTypeLanguage.Init()` at load. Depends on `SearchablePoupSelection`, `badClick`, `keyboardClick` and `createImg` (`app/application.js` / `common.js`) and on `IsoLanguage` from `../iso_data/languages.js`. The value is a two-letter ISO 639-1 code; the editor shows a flag and the language name and opens a searchable pop-up list on click.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `EditTypeLanguage.Init()` | immediately executed code | Called at the end of the file; loads `../iso_data/languages.js` and `EditLanguage.css`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `EditTypeLanguageFactory` | `EditTypeLanguageFactory(typename)` → `EditTypeLanguage` | Factory looked up by `Edit.GetTypeHandler()` as `window["EditType" + spec + "Factory"]`. Returns `new EditTypeLanguage(typename)`. |

#### class `EditTypeLanguage`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Init` | static async method | `Init()` → Promise | Loads `../iso_data/languages.js` (`includeJs`) and `EditLanguage.css` (`includeCss`) relative to the current script, in parallel. |
| `LanguageSel` | static field | `SearchablePoupSelection` | Shared language picker, created with the name `"Language"` (recent selections are stored under that name by `SearchablePoupSelection`), the key `data => data.Iso639_1`, an item renderer, a search function and a page size of 10. The renderer creates a `SysWeaver-EditLangSel` element (focusable, class `Recent` for recently used items, tooltip with name and code, keyboard-clickable) holding a flag `img` from `../iso_data/language/<code>.svg` and a `SysWeaver-EditLangText` with the name. Its `onclick` ignores `badClick` events and calls the selection callback. The search function ranks every `IsoLanguage.Languages` entry with the inner function `rank`, adds the recent-use rank (missing → 9999999), sorts ascending, then applies `offset` and `maxCount`. It writes the rank to `data.Rank`. |
| `constructor` | constructor | `new EditTypeLanguage(typename)` | Throws a string error unless `typename` is `"System.String"`. |
| `CreateDefault` | method | `CreateDefault(member, options)` → string or `null` | `GetDefault(member, options)`. |
| `IsOfType` | method | `IsOfType(obj)` → bool | True for anything that is not `undefined` or an object. |
| `Condition` | method | `Condition(obj, member, options)` → string | Looks up `"" + obj` with `IsoLanguage.Get` and returns its `Iso639_1` code, or `"en"` when unknown. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | `null` is allowed only with `AcceptNull`. Otherwise the value must be found by `IsoLanguage.Get` and equal its `Iso639_1` code exactly. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"" + obj`. |
| `GetDefault` | method | `GetDefault(member, options)` → string or `null` | `member.Default` when valid (`"\t"` → `"en"`); `null` with `AcceptNull`; otherwise `"en"`. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Initialises `obj[mn]` to the default. Creates a `SysWeaver-EditLanguage` element containing an `img` and a `SysWeaver-EditLangText`. Unless read-only (`options.ReadOnly`, the `ReadOnly` flag or `this.ForceReadOnly`, which is never set by the constructor) it gets `tabIndex 0`, the class `Enabled`, keyboard click support and an `onclick` that opens `LanguageSel.Show("Select language", callback, 25)`. The callback records the chosen code as an undoable change via `editor.Invoke`. Returns `{Focus, Title, DefValue, DefValueText, SetValue, UpdateValue}`; `UpdateValue` calls the inner `updateValue` and focuses the element unless `isFirst`. |

#### Inner functions of `EditTypeLanguage`

| Function | Signature | Description |
|---|---|---|
| `ls.onclick` (in `LanguageSel` renderer) | async `(ev)` | Ignores the event when `badClick(ev)`; otherwise awaits `onSelectFn(data)`. |
| `rank` (in `LanguageSel` search) | `rank(val)` → number | 0 when there is no search text. Otherwise combines the position of the text in the lower-case name and in the lower-case code (`nameIndex + codeIndex * 4`, missing name match → 4, missing code match → 1000). Returns 1000000 (excluded) when neither matches. |
| `e.onclick` (in `AddEditor`) | async `(ev)` | Opens the language picker and applies the selection as described above. |
| `updateValue` (in `AddEditor`) | `updateValue()` | Shows the current value: empty → no image, text "None"; unknown code → `../iso_data/icons/unknown.svg` and "Invalid!"; known code → its flag and name. Sets the same tooltip on the image and the text, with "Click to select another language." appended when editable. |

### `EditTypeNumber.js`

Type handler for the floating point types `Single`, `Double`, `Decimal` and their nullable versions. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeNumber`. `Edit.GetTypeMap()` passes the type range and a maximum text length (11 or 19); the fourth argument (`true` for nullable) is ignored. Works like `EditTypeInteger`, but keeps fractions and computes a decimal count for slider values.

#### class `EditTypeNumber`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new EditTypeNumber(typeMin, typeMax, maxLen)` | Stores `TypeMin`, `TypeMax` and `MaxLen`. |
| `TypeMin`, `TypeMax`, `MaxLen` | field | number | Type range and maximum input length. |
| `IsOfType` | method | `IsOfType(obj)` → bool | Accepts numbers and strings that `parseFloat` can parse, if within `[TypeMin, TypeMax]`. |
| `Condition` | method | `Condition(obj, member, options)` → number | `parseFloat`s strings and clamps to the member min/max from `GetMinMaxDefault`. |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | Error "Value to small!" / "Value to large!" when outside the member min/max. |
| `GetDefault` | method | `GetDefault(mmd)` → number | `mmd[2]` if truthy, else `0` clamped to `[mmd[0], mmd[1]]`. |
| `CreateDefault` | method | `CreateDefault(member, options)` → number | `GetDefault(GetMinMaxDefault(member, options))`. |
| `ToString` | method | `ToString(obj, member, options)` → string | `"" + obj`. |
| `GetMinMaxDefault` | method | `GetMinMaxDefault(member, options)` → `[min, max, def, typeTitle, haveMin, haveMax, parsedDef]` | Same as `EditTypeInteger.GetMinMaxDefault`, but parses `Min`, `Max` and `Default` with `parseFloat` after replacing the first `,` with `.`, without truncating. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Same structure as `EditTypeInteger.AddEditor`. The key filter also accepts one `.`. The maximum length is `MaxLen`, minus one when `min >= 0`. When a slider is shown, the step is `member.EditParams` (`parseFloat`) or a power of ten (multiplied or divided by 10) that gives 10–100 steps. `decimals` becomes the number of decimals (up to 5) needed to show step, min and max exactly; the text formatter then uses `toFixed(decimals)` and slider values are rounded to that many decimals. The cell gets the class `EditSlider`. |

#### Inner functions of `EditTypeNumber.AddEditor`

| Function | Signature | Description |
|---|---|---|
| `fromText` | `fromText(v)` → number | `parseFloat(v)`, 0 for `NaN`/0, clamped to `[min, max]`. |

### `EditTypeString.js`

Type handler for `System.String` and (read-only) `System.Object`. Loaded by `Edit.Init()` through `includeJs`; defines the global class `EditTypeString`. `Edit.GetTypeMap()` creates `new EditTypeString()` and `new EditTypeString(true)`. Supports the member flags `Multiline` (textarea with 3 rows), `Password` (password input, single line) and the `Min`/`Max` length limits.

#### class `EditTypeString`

| Member | Kind | Signature | Description |
|---|---|---|---|
| `constructor` | constructor | `new EditTypeString(forceReadOnly)` | Sets `ForceReadOnly`. |
| `ForceReadOnly` | field | bool | Forces the editor to read-only (used for `System.Object`). |
| `CreateDefault` | method | `CreateDefault(member, options)` → string or `null` | `GetDefault(member, options)`. |
| `IsOfType` | method | `IsOfType(obj)` → bool | True for anything that is not `undefined` or an object. |
| `Condition` | method | `Condition(obj, member, options)` → string | Converts to a string, truncates to `member.Max` (`Edit.GetIntParam`) when positive, and replaces line breaks with spaces unless the member is multi-line (and not a password). |
| `Validate` | async method | `Validate(obj, member, options, name)` → `null` or `[name, message]` | `null` is allowed only with `AcceptNull`. Errors "Text to long!" / "Text to short!" when the length is outside `member.Max` / `member.Min` (counts formatted with `ValueFormat.countSuffix`). |
| `ToString` | method | `ToString(obj, member, options)` → string | `"" + obj`. |
| `GetDefault` | method | `GetDefault(member, options)` → string or `null` | `member.Default` when valid (`"\t"` → `""`); `null` with `AcceptNull`; otherwise `""`. |
| `AddEditor` | async method | `AddEditor(obj, editor, editContext, member, title, options)` → editor record | Adds "Minimum length", "Maximum length" and "Default value" (quoted) tooltip lines. Initialises `obj[mn]` to the default when `undefined`. Creates `Edit.CreateTextInput` with identity parse/format, the summary as placeholder, `maxLength` as the length limit, read-only from `options.ReadOnly`, the `ReadOnly` flag or `ForceReadOnly`, and 3 rows when multi-line. Sets `type="password"` for password members. The setter records an undoable change via `editor.Invoke`. Returns `{Focus, Title, DefValue, DefValueText, SetValue, UpdateValue}` as for the other primitive handlers. |

### `text.js`

Script of the text / code editor page `text.html` (static `<script defer>`; the body calls `textMain()` on load). It needs `common.js`, `app/application.js`, Ace (`external/ace.js`, `ext-language_tools.js`) and the Prettier standalone build with its parser plugins. Defines the global functions `SanitizeHtml` and `textMain`. The button handlers also assign an undeclared global `beautifyButton`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `beautifyButton` | implicit global variable | Assigned without a declaration inside `textMain` when a beautifier exists for the file type; holds the Beautify `Button`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `SanitizeHtml` | `SanitizeHtml(html)` → string | Adds double quotes around unquoted HTML attribute values that contain `/` or `:` (other unquoted values are left alone), so that the Prettier HTML parser accepts them. |
| `textMain` | async `textMain()` | Page entry point. Reads URL parameters: `r` (read URL, required; fails with an error message if missing), `u` (update URL; enables Save and editing), `d` (delete URL; enables Delete), `download` (`false` hides Download), `scrolltoend` (`true` scrolls to the last line), `b` (`true` beautifies automatically on load and reload), `t` (Ace mode) and `n` (file name; defaults to the last segment of `r`). Sets `document.title` to the file name. When `t` is missing, the Ace mode is chosen from the extension (c/c++/cpp/h/hpp → `c_cpp`, cs → `csharp`, css, csv, glsl, htm/html → `html`, js → `javascript`, json, md → `markdown`, err/txt/log/cfg → `plain_text`, bat/cmd → `batchfile`, sql → `mysql`, svg, xml/config/csproj → `xml`; anything else `plain_text`). Builds a map of Prettier formatters for `json`, `json5` (parser `json5`), `css`, `html` (after `SanitizeHtml`), `markdown`, `javascript` (parser `typescript`) and `xml`, all with tabs. Creates a button row (`Button.CreateRow()`, a `SysWeaver-CenterBlock`) and a `SysWeaver-AceEditor` element in `body`. Buttons: **Reload** (fetches `r` again with `getRequest`; if the text changed, beautifies it if enabled, replaces the editor content and optionally scrolls to the end), **Beautify** (only when a formatter exists; formats the current content), **Download** (`downloadText(fname, content)`), **Save** (only with `u`; `sendRequest(u, {Url, Name, Content})`, then `InterOp.Post("FileSaved", m)` and an info message; Delete is disabled meanwhile) and **Delete** (only with `d`; asks with `Confirm`, then `sendRequest(d, {Url, Name})`, `InterOp.Post("FileDeleted", m)`, disables the buttons and removes the editor). Then fetches the text, creates the Ace editor (`ace.edit`) with read-only mode when there is no `u`, indent guides, folding, live autocompletion, SVG gutter icons and the syntax worker only when editable; adds the class `ace-sysweaver`, sets the mode, a scroll margin of 2 and the (optionally beautified) text, and hides the print margin. A `MutationObserver` on the editor's parent adds `ace-sysweaver` to any child `DIV` with the class `ace_editor` or `ace_tooltip` (Ace popups). Any exception clears the body and shows `Fail(message, 30000, true)`. `PageLoaded()` is always called at the end. Translated texts use `_TF` and `_T`. |

#### Inner functions of `textMain`

| Function | Signature | Description |
|---|---|---|
| `DoBeautify` | async `DoBeautify(text)` → string | Returns `text` unchanged if there is no formatter for the type or `b=true` was not given. Otherwise returns the Prettier result, or the original text (with a console warning) if formatting fails. |

### `type.js`

Script of the type information page `type.html` (static `<script defer>`, body calls `typeMain()`), and also loaded by `Edit.Init()` through `includeJs`, because it defines `TypeMemberFlags` and `TypeInfoCache`, which the editor uses. Defines the globals `TypeMemberFlags`, `TypeInfoCache`, `AddTextElement`, `BuildLink`, `BuildTypeNameRec`, `AddTypeName`, `typeAddSection`, `typeAdd` and `typeMain`. Translated texts use `_TF` and `_T`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `TypeMemberFlags` | frozen object (const) | Bit flags of a type or member description from `TypeService`: `Multiline` 1, `Password` 2, `AcceptNull` 4, `Slider` 8, `ReadOnly` 16, `Collection` 32, `Indexed` 64, `IsEnum` 128, `IsPrimitive` 256, `IsObject` 512, `Hide` 1024, `IsFlags` 2048, `DateUnspecified` 2048 (the last two share the same bit; one applies to enums, the other to date types). |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `AddTextElement` | `AddTextElement(target, text, title, className)` | Appends a `SysWeaver-Text` element with `innerText = text`, `title = title ?? ""` and the optional class. |
| `BuildLink` | `BuildLink(name, link)` → element | Creates a link (`ValueFormat.createLink`, target `_self`) with the text `name` to `TypeInfoCache.TypeLink + "?q=" + (link ?? name)`, with a translated tooltip. |
| `BuildTypeNameRec` | `BuildTypeNameRec(name, link)` → array of strings / elements | Splits a .NET type name into display parts. A name not ending in `]` is one part (a link when `link` is true). An array suffix like `[]` or `[,]` is split off and the element type is processed recursively. A generic name ``Base`N[[Arg1],[Arg2]]`` becomes the base name (linked to the full name when `link`), `<`, the recursively processed arguments (always linked) with separators, and `>`. A name ending in `]` with no backtick is returned as one part. |
| `AddTypeName` | `AddTypeName(target, name, link, ext)` | Appends the parts from `BuildTypeNameRec` to `target`: strings as `SysWeaver-Text` elements, elements as they are. When `link` is false and `ext` is set, the first part becomes an external link (target `_blank`) to `ext`. |
| `typeAddSection` | `typeAddSection(target, title, text, className)` → element | Appends a `SysWeaver-TypeSection` with a `SysWeaver-TypeTitle` child and optional class, plus a text node when `text` is given. Returns the section. |
| `typeAdd` | async `typeAdd(target, data)` | Renders a type description. `data` may be a type name, which is resolved with `TypeInfoCache.GetType`. Adds a "Type name" section (class `TypeCol` for collections, else `TypeCode`) with the formatted name and, when `data.Ext` is set, a `ColorIcon` (`SysWeaverTypeIconExternal`) that calls `Open(ext)`. Adds "Summary" and "Remarks" (class `TypeItalic`) and "Assembly" sections when present. Stops for collections. For enums (`IsEnum`), parses `data.Default` like `EditTypeEnum` and adds an "Enum values"/"Enum flags" section with a `table.TypeEnumValues` (value — hex for flags, else decimal — name and summary; each row has a tooltip with name, values, summary and remarks). Stops for primitives. Otherwise adds a "Members" section with a `table.TypeMembers` (type, name, summary): simple type names become links to `type.html?q=`, complex ones are formatted with `AddTypeName(…, true)`. Header texts are escaped with `makeHtmlSafe`. |
| `typeMain` | async `typeMain()` | Entry point of `type.html`. Shows a loading indicator (`AddLoading`). Reads the `q` URL parameter (error if missing), sets `document.title` to `"TYPE: <name>"`, fetches the type with `TypeInfoCache.GetType` (errors shown with `Fail`), creates a `SysWeaver-Page` with the class `Wide` in `body` and calls `typeAdd`. Removes the loading indicator in all cases. |

#### class `TypeInfoCache`

Static cache of type information and default instances fetched from the Edit service. Never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Types` | static field | `Map<typeName, info or null>` | Cached results of `GetTypeInfo`; `null` is cached for unknown types. |
| `InstanceTypes` | static field | `Map<typeName, [found, json]>` | Cached default instances as JSON text, so every call returns a fresh copy. |
| `TypeLink` | static field | string | Absolute URL of `type.html` next to this script. |
| `Req` | static field | string | Absolute URL of the `GetTypeInfo` API next to this script. |
| `ReqNew` | static field | string | Absolute URL of the `GetDefaultInstance` API next to this script. |
| `GetType` | static async method | `GetType(typeName)` → Promise of type info or `null` | Returns the cached entry or calls `sendRequest(Req, typeName, false)` and caches the result (`null` when the server returns nothing). |
| `GetInstance` | static async method | `GetInstance(typeName)` → Promise of object or `null` | Returns a parsed copy of the cached default instance, or calls `sendRequest(ReqNew, typeName, false)`, caches `[true, JSON]` (or `[false, null]` for a `null` result) and returns the object. |

## CSS reference

All sheets in this folder are static (not templated). They use the theme variables from `common/theme.css` (`--ThemeMain`, `--ThemeBackground`, `--ThemeAcc1`, `--ThemeAcc2`, their `RGB` and opacity variants such as `--ThemeMain25`, `--ThemeAcc250`, plus `--ThemeRad*`, `--ThemeFont*`, `--ThemeError`, `--ThemeWarning`, `--ThemeFadeDuration`). None of them defines `@keyframes`, media queries or `[data-theme]` variants; light/dark switching comes entirely from the theme variables.

### `Edit.css`

Shared base styles of the object editor (`Edit.js`). Loaded statically by every page that uses the editor, always together with `EditDesktop.css` and `EditMobile.css`: `edit/index.html`, `explore/api.html` (ExploreModule) and `ServerManager/services.html`. Defines the fade duration used by the desktop, mobile and language sheets, and the mask icons of the editor menus. A commented-out block once styled `html`/`body`.

#### Elements and state classes

| Selector(s) | Description |
|---|---|
| `SysWeaver-MemberTitle` | Member name label, created by `EditDesktop.js` / `EditMobile.js` (row title). Inline-block, 5px padding, selectable text. |
| `SysWeaver-TypeTitle` | Type title label created by `Edit.js` (header of an editor). 80% font, 24px line height, floated left, selectable. |
| `SysWeaver-EditInfo` | Italic inline info text (item count of a collection or object) created by `EditTypeCollection.js` and `EditTypeObject.js`. |
| `td.EditReadOnly SysWeaver-MemberTitle`, `input.EditReadOnly` | Italic text for read-only members. `EditReadOnly` is added by `Edit.js` (inputs) and `EditDesktop.js` / `EditMobile.js` (title cells). |
| `td.EditDraggable`, `td.EditDraggable:active` | Cells that can be dragged (collection items, set by `Edit.js`): `grab` cursor (fallback `move`), `grabbing` while pressed. |
| `td > .EditFloatRight` | Floats an element right (`!important`); added by `EditTypeCollection.js` to item icons. |
| `tr.Hide` | Hides a row; toggled by `Edit.js` and `EditDesktop.js` for hidden or conditionally hidden members. |

#### Icons

All are `mask-image` classes used as `IconClass` / `ColorIcon` image names by `Edit.js` and `EditTypeCollection.js`.

| Selector(s) | Description |
|---|---|
| `.SysWeaverEditIconMenu` | `../icons/menu.svg`, the per-editor menu icon. |
| `.SysWeaverEditIconDefault` | `../icons/reload.svg`, "reset to default" menu entry. |
| `.SysWeaverEditIconCopy`, `.SysWeaverEditIconPaste` | `../icons/copy.svg`, `../icons/paste.svg`, copy / paste menu entries. |
| `.SysWeaverEditIconExpand`, `.SysWeaverEditIconCollapse` | `../icons/expand.svg`, `../icons/collapse.svg`, expand/collapse toggle of objects and collections. |
| `.SysWeaverEditIconAddItem`, `.SysWeaverEditIconClearCollection` | `../icons/plus.svg`, `../icons/clear.svg`, add an item / clear a collection. |
| `.SysWeaverEditIconAddItemBefore`, `.SysWeaverEditIconAddItemAfter` | `../icons/AddAbove.svg`, `../icons/AddBelow.svg`, insert an item before / after (collection item menu). |
| `.SysWeaverEditIconMoveItemTop`, `.SysWeaverEditIconMoveItemUp`, `.SysWeaverEditIconMoveItemDown`, `.SysWeaverEditIconMoveItemBottom` | `../icons/MoveTop.svg`, `MoveUp.svg`, `MoveDown.svg`, `MoveBottom.svg`, move a collection item. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--SysWeaverEditFadeDuration` | `0.1s` (`:root`) | Colour/border transitions in `EditDesktop.css`, `EditMobile.css` and `EditLanguage.css`. |
| `--white` | `#ffffff` (`:root`) | Defined only; not referenced by the sheets in this folder. |

### `EditDesktop.css`

Desktop layout of the object editor: styles the `SysWeaver-EditDesktop` element that `Edit.js` creates (`"SysWeaver-Edit" + style`) when the desktop style (`EditDesktop.js`) is used. Loaded statically with `Edit.css` (see there). A desktop editor is a table with a title row, a value row and an optional expandable data row per member. Depends on `Edit.css` for `--SysWeaverEditFadeDuration` and on the theme variables.

#### Container

| Selector(s) | Description |
|---|---|
| `SysWeaver-EditDesktop` | Bordered (`--ThemeMain50`), rounded box with padding, background and 4px margin. |
| `SysWeaver-TabContent > SysWeaver-EditDesktop` | No border, radius, padding or background when the editor is the content of a tab (`SysWeaver-TabContent` from `common/tab.js`). |
| `SysWeaver-EditEntryValue > SysWeaver-EditDesktop` | No border for nested editors inside a collection/object entry (`SysWeaver-EditEntryValue`, created by `EditTypeCollection.js` / `EditTypeObject.js`). |
| `SysWeaver-EditDesktop.NoBorder` | Inline, unboxed editor; class added by `Edit.js` when no border is requested. |
| `SysWeaver-EditDesktop > table` | Full-width, collapsed table. |
| `SysWeaver-EditDesktop SysWeaver-TypeTitle` | Bold, `--ThemeAcc1` type title. |
| `SysWeaver-EditDesktop SysWeaver-PopupMenuIcon` | 24×24 floated menu icon (element created by `Edit.js`). |
| `SysWeaver-EditDesktop > table.EditorCollapsed tr.SeparatorRow`, `… tr.ValueRow`, `… tr.DataRow` | Hide the member rows while the editor is collapsed (`EditorCollapsed` toggled by `Edit.js`). |

#### Rows and cells

Row classes are set by `EditDesktop.js` (`TitleRow`, `ValueRow`, `SeparatorRow`) and by `EditTypeCollection.js` / `EditTypeObject.js` (`DataRow`, `EditorExpanded`).

| Selector(s) | Description |
|---|---|
| `SysWeaver-EditDesktop tr.TitleRow > td`, `tr.TitleRow > td > .IconButton` | Title cells: no-wrap, vertically centred; icon buttons (`IconButton`, the default `ColorIcon` button class from `common.js`) float left. |
| `SysWeaver-EditDesktop tr.SeparatorRow > td` | 2px spacer row. |
| `SysWeaver-EditDesktop tr.ValueRow > td` | Value cells with `--ThemeMain25` background; first cell rounded on the left, last cell (99% width) rounded on the right. |
| `tr.ValueRow > td.EditButton` | Cell holding a member menu icon (`EditButton`, set by `Edit.js`). |
| `tr.ValueRow > td.EditContainer` | Cell of an object/collection value (`EditContainer`, set by `EditTypeCollection.js`). |
| `tr.ValueRow > td.EditBoolean` | Padding of boolean cells (`EditBoolean`, set by `EditTypeBoolean.js`). |
| `tr.ValueRow.EditorExpanded > td:first-child`, `… > td:last-child` | Removes the bottom rounding when the row below (data row) is expanded. |
| `SysWeaver-EditDesktop tr.DataRow`, `tr.DataRow.EditorExpanded`, `tr.DataRow.EditorExpanded.Hide`, `tr.DataRow.EditorExpanded > td` | Data rows holding nested editors: hidden unless `EditorExpanded`, hidden again with `Hide`; expanded cells share the value-row background with rounded bottom corners. |

#### Inputs

| Selector(s) | Description |
|---|---|
| `tr.ValueRow > td input` (+ `:focus`, `:hover`, `:hover:focus`, `::placeholder`) | Full-width 125% input on `--ThemeBackground` with a 2px border; border `--ThemeAcc2` on focus (text `--ThemeAcc1`), half-transparent `--ThemeAcc2` on hover; italic small placeholder. |
| `tr.ValueRow > td input.HalfWidth`, `input.HalfWidth:first-child` | Date and time inputs side by side (`HalfWidth`, set by `EditTypeDateTime.js`): `calc(50% - 0.5em)` each, 1em gap. |
| `tr.ValueRow > td input[type="date"]`, `input[type="time"]` | Pointer cursor. |
| `tr.ValueRow > td textarea` (+ `.EditReadOnly`, `:focus`, `:hover`, `:hover:focus`, `::placeholder`) | Multi-line string editor: vertical resize, 4em–20em high, same border behaviour as inputs, italic when read-only. |
| `td.EditSlider input:first-child`, `td.EditSlider input:last-child` (+ `:disabled`, `::-webkit-slider-thumb`, `:focus::-webkit-slider-thumb`, `::-moz-range-thumb`, `:focus::-moz-range-thumb`) | Number with slider (`EditSlider`, set by `EditTypeInteger.js` / `EditTypeNumber.js`): text 28%, range input 70% floated right, custom thumb in `--ThemeMain` (`--ThemeAcc1` when focused), default cursor when disabled. |
| `tr.ValueRow > td.EditBoolean input` (+ `:disabled`), `td.EditBoolean input::-webkit-slider-thumb`, `::-moz-range-thumb`, `td.EditBoolean.EditTrue input::-webkit-slider-thumb`, `::-moz-range-thumb` | Boolean as a 3.5em two-position range with a 2em thumb in `--ThemeMain`, `--ThemeAcc1` when `EditTrue` (toggled by `EditTypeBoolean.js`). |
| `tr.ValueRow > td select` (+ `[multiple]`, `:disabled`, `:focus`) | Enum selector: full width, 125%, themed; `select[multiple]` (flag enums) scrolls, 4em to 75vh, resizable; disabled shows italic, full opacity. |
| `select option`, `option:checked`, `option:disabled`, `select:focus option`, `select:focus option:checked`, `select:disabled option`, `select:disabled option:checked` | Option colours: checked inverted (`--ThemeMain` background), `--ThemeAcc2` when the select has focus; disabled options italic (colour `rgba(var(--ThemeMainRGB), 0.1)`). |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--SysWeaverEditDesktopRad` | `var(--ThemeRad)` | Editor box and row cell radius. |
| `--SysWeaverEditDesktopRadInp` | `var(--ThemeRadInp)` | Input, textarea, select and option radius; also used by `EditLanguage.css`. |
| `--SysWeaverEditDesktopRadInp2` | `var(--ThemeRadInp2)` | Slider thumb radius. |

### `EditMobile.css`

Mobile layout of the object editor: styles `SysWeaver-EditMobile`, created by `Edit.js` when the mobile style (`EditMobile.js`) is used. Loaded statically with `Edit.css`. In the mobile layout each member has a header row (title and menu button) above a full-width value row. Otherwise the rules mirror `EditDesktop.css`. Depends on `Edit.css` and the theme variables.

#### Container

| Selector(s) | Description |
|---|---|
| `SysWeaver-EditMobile` | Bordered, rounded box with padding and margin. |
| `SysWeaver-TabContent > SysWeaver-EditMobile`, `SysWeaver-EditEntryValue > SysWeaver-EditMobile` | Unboxed inside a tab; no border inside a collection/object entry. |
| `SysWeaver-EditMobile > table`, `SysWeaver-EditMobile > table td` | Full-width collapsed table; cells with 5px padding, no-wrap, hidden overflow. |
| `SysWeaver-EditMobile SysWeaver-TypeTitle` | Bold `--ThemeAcc1` type title. |
| `SysWeaver-EditMobile SysWeaver-MemberTitle` | Member title without padding. |
| `SysWeaver-EditMobile SysWeaver-PopupMenuBack` | Shifts the pop-up menu backdrop (`SysWeaver-PopupMenuBack`, created by `application.js`) 30px to the right. |
| `SysWeaver-EditMobile SysWeaver-PopupMenuIcon` | Menu icon floated right. |
| `SysWeaver-EditMobile > table.EditorCollapsed tr.HeaderRow`, `… tr.SeparatorRow`, `… tr.ValueRow`, `… tr.DataRow` | Hide all member rows while collapsed. |

#### Rows and cells

Row classes are set by `EditMobile.js` (`TitleRow`, `HeaderRow`, `ValueRow`, `SeparatorRow`) and by `EditTypeCollection.js` / `EditTypeObject.js` (`DataRow`, `EditorExpanded`).

| Selector(s) | Description |
|---|---|
| `SysWeaver-EditMobile tr.TitleRow > td`, `tr.TitleRow > td > .IconButton` | Title row; 24×24 icon buttons floated left. |
| `SysWeaver-EditMobile tr.SeparatorRow > td` | 2px spacer. |
| `tr.HeaderRow > td`, `tr.HeaderRow > td:first-child`, `tr.HeaderRow > td.EditButton`, `tr.HeaderRow > td:last-child` | Member header: bold 80% text on `--ThemeMain25`, top corners rounded; the `EditButton` cell (32px) holds the menu icon. |
| `tr.ValueRow > td`, `tr.ValueRow.EditorExpanded > td` | Value cell below the header, bottom corners rounded; square when the data row is expanded. |
| `tr.DataRow`, `tr.DataRow.EditorExpanded`, `tr.DataRow.EditorExpanded.Hide`, `tr.DataRow.EditorExpanded > td` | Same expand/hide logic as the desktop layout. |

#### Inputs

| Selector(s) | Description |
|---|---|
| `tr.ValueRow > td input` (+ `:focus`, `:hover`, `:hover:focus`, `::placeholder`) | Same as desktop, with `--SysWeaverEditMobileRadInp`. |
| `tr.ValueRow > td input.HalfWidth`, `input.HalfWidth:first-child` | Date and time inputs are stacked (full width, block) with a 0.25em gap. |
| `tr.ValueRow > td textarea` (+ `.EditReadOnly`, `:focus`, `:hover`, `:hover:focus`, `::placeholder`) | Multi-line editor 5.125em–24em high. |
| `tr.ValueRow > td.EditSlider input:first-child`, `input:last-child` (+ `:disabled`, `::-webkit-slider-thumb`, `:focus::-webkit-slider-thumb`, `::-moz-range-thumb`, `:focus::-moz-range-thumb`) | Number text plus slider, as on desktop. |
| `tr.ValueRow > td.EditBoolean input` (+ `:disabled`), `td.EditBoolean input::-webkit-slider-thumb`, `::-moz-range-thumb`, `td.EditBoolean.EditTrue input::-webkit-slider-thumb`, `::-moz-range-thumb` | Boolean slider, as on desktop. |
| `tr.ValueRow > td select` (+ `[multiple]`, `:disabled`, `:focus`), `select option`, `option:checked`, `option:disabled`, `select:focus option`, `select:focus option:checked`, `select:disabled option`, `select:disabled option:checked` | Enum selector and options, as on desktop. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--SysWeaverEditMobileRad` | `var(--ThemeRad)` | Box, header and value cell radius. |
| `--SysWeaverEditMobileRadInp` | `var(--ThemeRadInp)` | Input, textarea, select and option radius. |
| `--SysWeaverEditMobileRadInp2` | `var(--ThemeRadInp2)` | Slider thumb radius. |

### `EditLanguage.css`

Styles of the language editor and picker from `EditTypeLanguage.js`. Loaded dynamically by `EditTypeLanguage.Init()` (`includeCss`). Uses `--SysWeaverEditDesktopRadInp` (from `EditDesktop.css`), `--SysWeaverEditFadeDuration` (from `Edit.css`) and the theme variables.

| Selector(s) | Description |
|---|---|
| `SysWeaver-EditLanguage` | The editor value: a 29px flex row like a text input (background, 2px border, radius), italic by default. |
| `SysWeaver-EditLanguage.Enabled` | Editable state (class `Enabled` set by `EditTypeLanguage.AddEditor`): normal text, pointer cursor. |
| `SysWeaver-EditLanguage:focus`, `:hover`, `:focus:hover` | `--ThemeAcc2` border and `--ThemeAcc1` text on focus; half-transparent border on hover. |
| `SysWeaver-EditLanguage img`, `SysWeaver-EditLanguage SysWeaver-EditLangText` | Flag image (max 48×23px) and selectable language name. |
| `SysWeaver-EditLangSel` | One entry of the language pick list (`EditTypeLanguage.LanguageSel`): 35px flex row, light `--ThemeMainRGB` background, pointer cursor. |
| `SysWeaver-EditLangSel.Recent` | Recently used language (class `Recent`): half-transparent `--ThemeAcc1` border. |
| `SysWeaver-EditLangSel:focus`, `:hover` | `--ThemeAcc2` border and `--ThemeAcc1` text. |
| `SysWeaver-EditLangSel img`, `SysWeaver-EditLangSel SysWeaver-EditLangText` | Fixed 48px wide flag; 125% language name in `--ThemeMain`. |

### `text.css`

Styles of the text / code editor page `text.html` (static `<link>`, after `common/theme.css` and `app/application.css`). Lays out the page as a two-row grid and defines a complete Ace editor theme under the class `.ace-sysweaver`, which `text.js` adds to the editor and (via a `MutationObserver`) to Ace popups. No Ace `theme-*.js` file is loaded; these rules override Ace's default theme and map it onto the SysWeaver theme variables.

#### Page layout

| Selector(s) | Description |
|---|---|
| `body` | Full-viewport (`100svh`) grid with rows `min-content auto`, no scrolling, theme background and font. |
| `html.Embedded body` | Height reduced by 8px when the page is embedded (class `Embedded` set by `app/application.js`). |
| `SysWeaver-CenterBlock` | The button row (`Button.CreateRow()` in `common.js`): grid row 1, shadow, `--ThemeBackground50`. |
| `SysWeaver-AceEditor` | The editor host created by `text.js`: grid row 2. |

#### Ace theme: popups, search box and autocomplete

| Selector(s) | Description |
|---|---|
| `.ace-sysweaver.ace_tooltip`, `.ace-sysweaver .ace_tooltip`, `.ace-sysweaver .ace_search` | Tooltips and the search box: themed background, `--ThemeAcc1` text, rounded `--ThemeMain75` border. |
| `.ace-sysweaver .ace_button`, `.ace_button.checked`, `.ace_button:hover` | Search option buttons: `--ThemeAcc2` outline style; checked has a tinted background; hover inverts. |
| `.ace-sysweaver .ace_searchbtn_close` | Close button on `--ThemeAcc1`. |
| `.ace-sysweaver .ace_searchbtn`, `.ace_searchbtn:hover`, `.ace_searchbtn:last-child`, `.ace_searchbtn.prev:after`, `.ace_searchbtn.next:after` | Search navigation buttons on `--ThemeMain`, slightly transparent until hover; last one rounded on the right; the `prev`/`next` arrows are drawn with background-coloured borders. |
| `.ace-sysweaver .ace_search_form`, `.ace_replace_form`, `.ace_search_form.ace_nomatch` | Search/replace field groups: rounded, thin border; red (`--ThemeError`) outline when there is no match. |
| `.ace-sysweaver .ace_search_field`, `.ace_search_field::placeholder` | Borderless themed input; italic faded placeholder. |
| `.ace-sysweaver.ace_editor.ace_autocomplete` | Autocomplete popup: rounded, bordered, monospace, `--ThemeAcc1` text. |
| `… .ace_autocomplete .ace_marker-layer .ace_active-line`, `… .ace_marker-layer .ace_line-hover` | Selected and hovered completion rows. |
| `… .ace_autocomplete .ace_completion-highlight`, `… .ace_completion-meta`, `… .ace_selected`, `… .ace_selected .ace_completion-highlight`, `… .ace_selected .ace_completion-meta` | Matched characters in `--ThemeAcc2`; meta text small in `--ThemeAcc175` (`--ThemeBackground75` on the selected row). The `.ace_selected` rule itself is empty (its colour is commented out). |

#### Ace theme: editor, gutter and markers

| Selector(s) | Description |
|---|---|
| `.ace-sysweaver` | Editor background `--ThemeBackground25`, text `--ThemeMain`, monospace font. |
| `.ace-sysweaver .ace_layer`, `.ace-sysweaver .ace_content` | `overflow: visible !important`. |
| `.ace-sysweaver .ace_gutter`, `.ace_gutter-cell`, `.ace_gutter-active-line` | Gutter on the theme background with small faded line numbers; full-width cells; faint highlight of the active line. |
| `.ace-sysweaver .ace_icon_svg`, `.ace_icon_svg.ace_error` | 16px gutter SVG icons; error icons tinted `--ThemeError`. |
| `.ace-sysweaver .ace_print-margin` | 1px faded print margin (hidden by `text.js` anyway). |
| `.ace-sysweaver .ace_cursor` | `--ThemeAcc1` cursor. |
| `.ace-sysweaver .ace_marker-layer .ace_selection`, `.ace-sysweaver.ace_multiselect .ace_selection.ace_start` | Selection in `--ThemeAcc225`; multi-selection start has a glow. |
| `.ace-sysweaver .ace_marker-layer .ace_step`, `.ace_marker-layer .ace_bracket`, `.ace_marker-layer .ace_active-line`, `.ace_marker-layer .ace_selected-word` | Debug step marker (fixed dark yellow), matching bracket box (`--ThemeAcc150`), active line tint, and other occurrences of the selected word (`--ThemeAcc150` border; its background uses `rgba(var(--ThemeAcc1), 0.25)`). |
| `.ace-sysweaver .ace_invisible` | Invisible characters in 25% white. |
| `.ace-sysweaver .ace_fold` | Fold marker in `--ThemeAcc2` colours. |
| `.ace-sysweaver .ace_indent-guide`, `.ace_indent-guide-active` | Indent guides as a 1px right border (`--ThemeMain25`, active `--ThemeAcc125`). |

#### Ace theme: syntax tokens

| Selector(s) | Description |
|---|---|
| `.ace-sysweaver .ace_keyword`, `.ace_meta` | Bold `--ThemeAcc275`. |
| `.ace-sysweaver .ace_comment`, `.ace_comment.ace_doc`, `.ace_comment.ace_doc.ace_tag` | Italic comments (`--ThemeAcc250`; doc comments `--ThemeAcc275`; doc tags `--ThemeAcc250`). |
| `.ace-sysweaver .ace_lparen.ace_doc`, `.ace_rparen.ace_doc`, `.ace_variable.ace_doc`, `.ace_text.ace_doc` | Tokens inside doc comments: parentheses italic `--ThemeAcc225`, variables `--ThemeAcc175`, text `--ThemeMain75`. |
| `.ace-sysweaver .ace_variable`, `.ace_variable.ace_language` | Variables `--ThemeMain75`; language variables (`this`) bold. |
| `.ace-sysweaver .ace_paren`, `.ace_support`, `.ace_operator`, `.ace_support.ace_type` | Parentheses, support names and operators `--ThemeMain50`; support types `--ThemeMain`. |
| `.ace-sysweaver .ace_constant`, `.ace_constant.ace_character`, `.ace_constant.ace_other`, `.ace_constant.ace_numeric`, `.ace_heading`, `.ace_markup.ace_heading`, `.ace_support.ace_constant` | Constants, numbers and headings in `--ThemeAcc2`. |
| `.ace-sysweaver .ace_constant.ace_language`, `.ace_constant.ace_boolean` | Italic `--ThemeAcc2` (`true`, `null`, …). |
| `.ace-sysweaver .ace_string`, `.ace_string.ace_regexp`, `.ace_constant.ace_escape` | Strings `--ThemeAcc1`; regular expressions `--ThemeAcc2`; escapes bold `--ThemeAcc2`. |
| `.ace-sysweaver .ace_invalid.ace_illegal`, `.ace_invalid.ace_deprecated` | Illegal tokens light on purple; deprecated underlined italic `--ThemeWarning`. |
| `.ace-sysweaver .ace_support.ace_function` | Bold `--ThemeAcc175`. |
| `.ace-sysweaver .ace_list`, `.ace_markup.ace_list`, `.ace_storage` | Bold `--ThemeAcc175` (lists, storage keywords such as `var`/`function`). |
| `.ace-sysweaver .ace_entity.ace_name.ace_function`, `.ace_meta.ace_tag` | Function names and markup tags `--ThemeAcc2`. |
| `.ace-sysweaver .ace_xml-pe` | XML processing entities in fixed grey `#494949`. |

### `type.css`

Styles of the type information page `type.html` (static `<link>`, after `common/theme.css` and `common/tab.css`). Styles the sections and tables that `type.js` (`typeAddSection`, `typeAdd`, `AddTextElement`, `AddTypeName`) creates. Uses the theme variables only.

#### Sections

| Selector(s) | Description |
|---|---|
| `SysWeaver-TypeSection` | One information block: full width, padded, thin rounded border, `--ThemeAcc1` text, horizontal scroll. |
| `SysWeaver-TypeTitle` | Section title: small, bold, upper-case, 50% opacity, not selectable. (Also styled by `Edit.css` when both are loaded.) |
| `SysWeaver-TypeSection.TypeCol` | Type name of a collection: monospace, 70% `--ThemeAcc1`. |
| `SysWeaver-TypeSection.TypeCode` | Type name of other types: monospace. |
| `SysWeaver-TypeSection.TypeCode SysWeaver-Text` | Name fragments at 80% opacity. |
| `SysWeaver-TypeSection.TypeItalic` | Summary and remarks sections in italics. |
| `SysWeaver-TypeSection a` | Links (from `ValueFormat.createLink`) as 20px inline blocks. |
| `SysWeaver-TypeSection > SysWeaver-Link` | Link elements (`SysWeaver-Link`, `common.js`) as 24px inline blocks. |
| `SysWeaver-TypeSection > SysWeaver-Text`, `SysWeaver-TypeSection SysWeaver-Text` | Text fragments created by `AddTextElement`: 24px inline blocks with small vertical padding. |
| `SysWeaver-TypeSection > Icon-24x24`, `SysWeaver-TypeSection icon-24x24` | The external-documentation `ColorIcon` (24×24 element `icon-24x24`): top margin and 16px left gap. |
| `.SysWeaverTypeIconExternal` | Mask icon `../icons/newtab.svg` for that external link (used by `typeAdd`). |

#### Tables

| Selector(s) | Description |
|---|---|
| `SysWeaver-TypeSection table.TypeMembers`, `tr`, `tr:hover`, `tr:nth-child(2n)`, `tr:nth-child(2n):hover`, `th`, `th:nth-child(1)` | Members table (`table.TypeMembers`): full width, striped rows with a hover tint, small bold upper-case headers. |
| `table.TypeMembers td`, `td:nth-child(1)`, `td > *`, `td:nth-child(2)`, `td:nth-child(3)`, `td:nth-child(1) SysWeaver-Text`, `td:nth-child(2) SysWeaver-Text`, `td:nth-child(3) SysWeaver-Text` | Cells: type column monospace 90%, name column `--ThemeAcc2` and selectable, summary column italic `--ThemeMain`; children laid out as 24px inline blocks. |
| `SysWeaver-TypeSection table.TypeEnumValues`, `tr`, `tr:hover`, `tr:nth-child(2n)`, `tr:nth-child(2n):hover`, `th`, `th:nth-child(1)` | Enum values table (`table.TypeEnumValues`): same look; the value header is right-aligned. |
| `table.TypeEnumValues td`, `td:nth-child(1)`, `td:nth-child(2)`, `td:nth-child(3)` | Value column right-aligned monospace, name `--ThemeAcc2`, summary italic. |
