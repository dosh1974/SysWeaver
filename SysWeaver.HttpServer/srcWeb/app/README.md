# app

[⬆ srcWeb](../README.md)

> The web application shell, served under `app/`: main menu and pop-up framework, home and welcome pages, the web app manifest and a tag gallery control.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`index.html` loads `application.js` / `application.css`, which build the single-page application: the main menu (from the server's web menu), tabs and pop-ups. Applications customise it with `app/app.js` and `app/app.css`; when no module provides these, the server serves empty files. The Home and Welcome pages are registered as menu entries on [`HttpServerBase`](../../HttpServerBase.cs) (`app/Home.html` for logged in users, `app/Welcome.html` for everyone).

## Contents

| Item | Description |
|---|---|
| [`application.js`](application.js), [`application.css`](application.css) | The shell: `WebMenu` / `MainMenu`, pop-ups (`PopUp`, `PopUpMenu`, `PopUpSelection`, `PopUpEdit`, `Confirm`, …), searchable selections and the `App` class |
| [`Home.html`](Home.html), [`home.js`](home.js), [`home.css`](home.css) | Home page (template): welcomes the logged in user by name, shows the user image and app description; if the custom user image service is available, the user image becomes an upload target (loads `md5.js`, `UZIP.js` and `fileUploader.js`) |
| [`Welcome.html`](Welcome.html), [`welcome.js`](welcome.js), [`welcome.css`](welcome.css) | Welcome page with the logo; when login is available the logo opens `auth/Login.html` |
| [`manifest.json`](manifest.json) | Web app manifest (template), name and start URL from server variables, icons `icon-180.png` / `icon-192.png` |
| [`tags.js`](tags.js), [`tags.css`](tags.css) | Tag gallery control (`TagGalleryParams`, `Tag`, filtering and removal); no reference to these files by name was found in the repositories |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md)
- **Uses:** [`common/`](../common/README.md), the icons of [SysWeaver.HttpServer.IconModule](../../../SysWeaver.HttpServer.IconModule/srcWeb/icons/README.md)
- **Used by:** pages in other projects that load `../app/application.js` / `../app/application.css`

## Behavior (web analysis)

### Loading and initialization

- **Shell boot (root page `index.html`, see [`../README.md`](../README.md)).** `common/common.js`, `app/application.js` and `app/app.js` are loaded with `defer` (in that order); `<body onload="appMain()">` starts the shell.
- **`appMain(settingMenus)`** (end of `application.js`; the `settingMenus` argument is unused):
  1. `AddLoading(document.body)` shows the full-page loading overlay.
  2. `await App.Create()` with a default `AppParams` (no way to pass parameters through `appMain`).
  3. If a global function `window.AppMain` exists (typically defined by the application specific `app/app.js`), it is awaited. This is the main extension hook for applications.
  4. The loader is removed, `await delay(1)`, then `app.InitPage()` runs. `InitPage` is the function returned by `App.Init`: it opens the initial content (the page named in the URL hash, or the first item of the main menu).
- **`App.Init(params)` build order:** sets `window.SysWeaverAppTitle = document.title` and `window.SysWeaverApp = app`; creates `<app-header>`; an optional back button (`params.Navigation`, or `window.Mobile` when `Navigation` is `null`); the pin button; `<app-ContentTitle>`; the user button (requests `GetMenu("User")` and `auth/GetUser`); the settings button (requests `GetMenu("Theme")`, or `"User,Theme"` when there is no user button); `<app-content>`; and finally `<app-menu>` with the main menu (`GetMenu("Default")`). It also registers the `popstate` and `message` listeners and reads `location.hash`. The server requests run one after another, so header buttons appear before the menu loads.
- **Dynamic includes:** `home.js` loads `../common/md5.js`, `../common/UZIP.js` and `../common/fileUploader.js` with `includeJs` only when `CustomUserImageService` exists. Pages in other projects (`explore/table.js`, `chart/chart.js`) load `../app/application.js` themselves with `includeJs` to get the pop-up functions.
- `application.js` only declares classes and functions at script top level; nothing runs until `appMain` is called. All of its declarations become globals of a classic script.

### Files

#### `application.js`

**Menu data model**
- `WebMenu { RootUri, Items[], Name }`, `static From(obj, name)` turns a server JSON object into class instances, converting `Items` recursively. Note that `Name` is set only when `Items` is non-empty.
- `WebMenuItem { Id, Name, Type, Title, IconClass, Data, Children, Flags }`, `static From(obj)` (recursive). `Flags`: `1` = disabled, `2` = checked. `Data` is either a string (from the server) or, for menus built on the client, a **JS function** `async (item, style, isUserClick) => bool`; a truthy result hides the menu.
- `Type` is the numeric value of the C# enum `WebMenuItemTypes`: `0` Path (folder), `1` Table, `2` Embedded, `3` Link, `4` LinkExternal, `5` Js, `6` Chart.

**`MainMenuStyle`**: icon sizes (32/20), expand tab width, icon colour classes, expander icon class pairs (`"IconCollapse,IconExpand"` and `"IconCollapseMain,IconExpandMain"`), `MenuStyle` CSS class (`DefaultMenu`), plus the callbacks used to plug a menu into a host:
- `Content`: the element that hosts the embedded iframe.
- `SelectFn(itemOrNull)`: called when a row is selected.
- `ExternalContentFn(url?, title?)`: called with no arguments when navigation starts, and with `(url, title)` when the new content is revealed.
- `HideFn()`: closes or collapses the menu after an action.

**`MainMenu(menu, style, toElement)`**: renders a `WebMenu` as nested `<table>`s with one row per item (cells: indent, expander icon, name, item icon). It is used for the main menu and for every pop-up menu.
- `Add(table, item, depth, style)` builds a row (`tr.MenuData`, with `row.MenuItem = item` and `row.MenuIcon = ColorIcon`).
  - Separators: `Id === "-"` gives `tr.MenuSeparator`, with an optional label `<span>`.
  - Children are rendered in a following `tr.MenuNested` that holds a nested table.
  - The click action depends on `Type`:
    - 1 Table: embeds `explore/table.html?q=../<Data>`.
    - 2 Embedded: embeds `<Data>`.
    - 3 Link: `window.location.href = Data`, which replaces the whole shell.
    - 4 LinkExternal: `window.open(Data)`, a new tab.
    - 5 Js: calls `window[Data](item, style, isUserClick)` if `Data` names a global function, otherwise `eval(Data)`; a truthy result hides the menu.
    - 6 Chart: embeds `chart/chart.html?q=../<Data>`.
    - A function-valued `Data`: the function is called.
  - URL items are resolved against the directory of the current `window.location` and stored in `RowMap` (absolute URL to row), which is used to find the row to highlight when a frame finishes loading.
  - A row without its own action but with children toggles expand/collapse. The collapsed state is saved in `localStorage`.
  - Disabled rows get `MenuDisabled` and lose their click handler; checked rows get `MenuChecked`. `item.OnClick` is set to the handler, so code can trigger an item programmatically (used for "first item" navigation).
- `EmbeddContent(url, style, noNewState)` is the central content navigation:
  1. Makes the URL absolute and clears the `sessionStorage` history state for it (`ClearOtherHistoryState`).
  2. Calls `ExternalContentFn()`.
  3. Fades out the previous iframe (`.Hidden`) and removes it after 25 ms.
  4. Creates a hidden iframe (`createIFrame`, `frameborder=0`, `allowtransparency`) whose `src = appendTheme(url)` (adds `?useTheme=` when a theme is forced) and appends it to `style.Content`.
  5. Pushes a history entry, unless `noNewState` is set or this is the very first embed (`window.SysWeaverDoneFirst`).

  The frame is revealed by `DoRemove`, either 1 s after the iframe `load` event or earlier when the page posts `LoaderRemoved`. `DoRemove` does the following:
  - adds the class `Embedded` to the frame's `<html>` element (same origin only);
  - matches the frame's `location.href` against `RowMap` to select the menu row. The title is the menu item name, or else the frame's `document.title`;
  - shows the iframe and calls `ExternalContentFn(newUrl, title)`.
- `LoaderRemoved(style)` runs `DoRemove` on the last iframe early. `Select(row)` updates the highlight (`MenuSel`), disables the row icon and calls `SelectFn`.
- `SetUrl(key)` / `ReplaceUrl(key)`: `history.pushState` / `replaceState` with `state = url` and `hash = Base64EncodeString(url)`.
- `static GetEmbeddedUrl(content)` returns the current frame URL (the live `contentWindow.location.href` if accessible, else `src`). `SetIcon` / `SetExpIcon` pick an icon from a `"collapsed,expanded"` class pair.

**Pop-up framework.** Elements use the custom tag names `SysWeaver-*` and are appended to `document.body`.
- `CreateTabBlock()` creates the `<SysWeaver-TabBlock>` focus sentinel.
- `CreatePopUpBlocker(dontAllowClose, onClose)` returns `[blockerEl, Close]`. The `<SysWeaver-Blocker>` overlay:
  - traps focus (`BlockTab`);
  - closes on a mouse-down plus mouse-up on the backdrop, or on Escape, unless `dontAllowClose`;
  - `Close()` awaits `onClose`, then calls `UnblockTab`, removes the element and calls `Close.EndFn` (used to resolve blocking waits).
- `CreatePopUpAtElement(el, wScale, hScale, offX, offY, dontAllowClose, onClose)` creates a `<SysWeaver-PopupMenu>` positioned once (fixed coordinates computed from `getBoundingClientRect`) inside a blocker and returns `[popup, close, block]`.
- `PopUpElement(...)` (anchored pop-up) and `PopUpElementMenu(el, build, block, dontAllowClose, onClose, rightAlign, bottomAlign)` (adds a `<SysWeaver-PopupMenuBack>`, `.Right` when right aligned).
- `PopUpMenu(el, build, rightAlign, bottomAlign)`: `build(close, backEl)` returns a `WebMenu`, which is rendered with a `MainMenu` whose `Content` is the page's `<app-content>` (looked up by tag name). Embedded items picked from pop-up menus (user, settings) therefore load into the main content area. Its `ExternalContentFn` updates `<app-ContentTitle>` and `document.title` using `window.SysWeaverAppTitle`, and assigns an undeclared global `contentUrl`.
- `PopUp(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton)`: centred `<SysWeaver-PopUpBackground>` (with `.Close`, `.Block`, `.MakeFullWidth()`, `.MakeMaxHeight()`) and an optional `<SysWeaver-BlockerCloseButton>` holding the `IconClosePopUp` icon. A `ResizeObserver` toggles `Scroll` on the blocker. It either awaits until closed or returns `Close`.
- `PopUpWorking(title, text, iconClass, w, h)`: a pop-up that cannot be dismissed and shows `IconWorking`. It returns its close function.
- `PopUpSelection(text, title, getItems(search, close), startText, getAll, ...)`: filter input plus a `<SysWeaver-SelectList>`. Enter clicks the first item; there is a clear button; stale results are ignored.
- `PopUpEdit(type, obj, buttonText, buttonIcon, buttonToolTip, title, titleToolTip, primName)`: if `type` is a string, it requests `../edit/GetTypeInfo` first. It then builds an `Edit` property editor and returns the edited object, or `null`. **The function is declared twice with the same body**; the second declaration wins.
- `Confirm(title, text, okText, cancelText, okIcon, cancelIcon, okTitle, cancelTitle, okStyle, cancelStyle)` returns `true` if OK was pressed. Clicking the title or text copies it to the clipboard.
- `SearchablePoupSelection(id, getRecentIdFn, createElementFn, findFn, maxRecent=10)` with `.Show(titleText, onSelect, maxView=20, ...)`: a paged searchable list (`findFn(filter, recentMap, max+1, offset)`, "more" button `IconMoreData`). The last filter and an MRU list of ids are saved in `localStorage["SysWeaver.PopUpRecent.<id>"]`. Used by `edit/EditTypeLanguage.js`.

**`AppParams`**: `PageElement`, `MainMenu`, `MainMenus` (comma separated menu names, default `"Default"`; `""` means none), `MainMenuPinned`, `Content`, `Navigation` (`null` = automatic on mobile), `UserButton`, `SettingsButton`, `SettingMenus`, `SettingsFs`, `SettingsNewTab`, `OnSettingsMenuOpenFn(webMenu, close)`, `OnMainMenuDefFn(menuDef, mainMenuStyle, hideFn)` (async, may modify the menu before rendering).

**`App`** (`static Create(params)`, fields `Page`, `AppHeader`, `AppMenu`, `Content`, `AppMenuButton`, `AppBackButton`, `PinButton`, `Title`, `InitPage`). It builds the DOM:

```
body
 ├ app-header
 │   ├ app-MenuButton   (IconMainMenu: toggle / unpin)
 │   ├ app-BackButton   (history.back(), optional)
 │   ├ app-PinButton    (IconLock: pin menu)
 │   ├ app-ContentTitle (click copies title)
 │   ├ app-UserButton   (pop-up "User" menu)
 │   └ app-SettingsButton (pop-up: full screen, Theme menu, "Open in new tab", custom items)
 ├ app-content          (iframe host)
 ├ app-menu             (MainMenu, name "Main")
 └ app-capture          (transparent overlay, only while the collapsed menu is shown)
```

- **Menu states** are set as classes on `<body>`: `SwPinned` (menu always visible, content padded 400 px) or `SwCollapsed` (menu translated out, only a 52 px icon strip visible; on narrow screens it slides up out of view).
  - On narrow layouts (`window.Narrow`) the menu is always unpinned. The `SwLayout` body event (from common.js) re-applies the state.
  - Mobile starts unpinned; desktop uses `localStorage["SysWeaver.Pinned"]`, or `params.MainMenuPinned` if unset.
  - When collapsed, moving the mouse within 8 px of the left edge of the menu strip shows it. Mouse-over outside the menu and header, or leaving the body, hides it again (`app-capture` plus capture-phase listeners). Auto-show is disabled for 250 ms after hiding.
- **User button**: if `auth/GetUser` succeeds, the icon is `IconCurrentUserFill` with class `WithUser`; otherwise it is `icons/user.svg`. The tool tip says "Logged in as …". The `User` menu result is unwrapped when it has a single root (the "User" folder). It listens to `InterOp` messages of type `UserImageChanged` and then sets the background image to `../auth/UserImages/Current/small?<n>` (cache-busting counter).
- **Settings menu**: "Enter/Exit full screen" (`FullScreen` from common.js), the server "Theme" items, "Open in new tab" (`Open(MainMenu.GetEmbeddedUrl(app.Content))`, which opens `_blank`), then `OnSettingsMenuOpenFn`.
- **Navigation listeners**:
  - `popstate` with a string `state` re-embeds that URL without a new history entry. A `null` state resets `SysWeaverDoneFirst` and clicks the first main-menu item.
  - `message` with `{Type:"IframeNavigating", Url}` embeds `Url` (with a new history entry). `{Type:"LoaderRemoved"}` reveals the loading frame early.
- **Initial page**: a non-empty hash is Base64-decoded to a URL and embedded. Otherwise the first menu item's `OnClick` runs (on the server this is "Welcome" without a user and "Home" with one, both at order -100).

#### `application.css`
- Native CSS nesting (`body.SwNarrow { app-header app-PinButton {...} }`).
- Layout variables on `body`: `--h` (header button size from `--ThemeIconSize`), `--imh`, `--mh` (header height).
- Absolute layout for `app-header` (z 2), `app-menu` (z 1, 400 px wide, `direction: rtl` so the scrollbar sits on the left with content reset to ltr, faint `../logo.svg` watermark via `:before`), `app-content` (z 0), and `app-content iframe` / `iframe.Hidden` (fade and slide).
- State classes: `SwPinned`, `SwCollapsed`, `SwNarrow`, `SwMobile`.
- `.DefaultMenu` table styling: `MenuData`, `MenuSel`, `MenuClick`, `MenuDisabled`, `MenuChecked` (check mark `::after`), `MenuSeparator`, `SubMenu`, `SubMenuHide`, `MenuNested`, `MenuHide`, `MenuNoExpand`.
- Pop-up element styles: `SysWeaver-Blocker` (`.Scroll`, `.FullWidth`, `.MaxHeight`), `SysWeaver-PopUpBackground` (`.Wide`, `.Resize`), `SysWeaver-PopupMenu`, `SysWeaver-PopupMenuBack`, `SysWeaver-PopUpText`, `SysWeaver-PopUpFilter`, `SysWeaver-SelectList`, `SysWeaver-PopSelData`, `SysWeaver-PopSelEnd`, `SysWeaver-ConfTitle`, `SysWeaver-ConfText`, `SysWeaver-ConfButtons`, `SysWeaver-CenterBlock`, `SysWeaver-PopUpWorking*`.
- A large set of `.Icon*` classes defined as `mask-image: url("../icons/*.svg")`, used by server menu `IconClass` values: theme, auth/user, table, debug and data icons. `IconMainMenu` changes its arrow per menu state. **Pages that use the pop-ups or menu icons must also link `app/application.css`.**

#### `Home.html` / `home.js` / `home.css`
- `Home.html` is a template: `${#Session.NickName}` in the `<h2>` (with a `data-translation_context` attribute that names the variable using `${@Session.NickName}`), `${#EnvInfo.AppDescription}`, `<img class="UserImage" src="../auth/UserImages/Current/small">` and the logo. The background is transparent so the page can be embedded.
- `homeMain()`:
  - Shows `AddLoading`.
  - Asks `../Api/HaveService("SysWeaver.MicroService.CustomUserImageService")`.
  - If the service exists, it loads the uploader scripts and makes the image an upload target with `fileUploaderSetup(el, "UserImage", onSelect, onDone)`.
  - On a successful upload it switches the image to `.../large` (or reloads it via `SetImageSource`) and broadcasts `InterOp.Post("UserImageChanged")`, which the shell's user button picks up. It also does this for `AlreadyUploaded`.
- `home.css`: centred layout; `img.UserImage`, `.CanUpload`, and the `.Dragging`, `.Selecting` and `.Uploading` states (the state classes are set by fileUploader.js).

#### `Welcome.html` / `welcome.js` / `welcome.css`
- A static page with the logo.
- `welcomeMain()` probes `../Api/auth/GetUserSalt("dummy")`. If the call returns a truthy result (login is available), the logo and an added `<p>` ("Click to sign in", `keyboardClick`, focused) open `../auth/Login.html` in `_self`. Inside the shell that is the iframe, so the login page replaces the welcome page in the content area.
- `welcome.css`: centred absolute layout, hover glow `img.Click`, `p.KeyClick` focus/hover.

#### `manifest.json`
- A template (`$` JS-string transforms): `start_url` = `${$Request.Prefix}`, `short_name`/`name`/`description` from `EnvInfo`, `theme_color`/`background_color` from `${$Color.Color}`/`${$Color.Background}`, icons `../icon-180.png` and `../icon-192.png` (generated by the server), `display: fullscreen`.

#### `tags.js` / `tags.css`
- `TagGalleryParams` (translated texts, icons `IconTagAdd`/`IconTagRemove`, `ShowAllTags`, `AddAtFront`, `Class`, `Embedded`), `Tag { Code, Name, Desc, Class }`, `FilterTags(tags, search, max)` (ranks by name position and description hit count), `SetTagClassName(tagRet, cls)`.
- `CreateTagsGallery(findTags, getTags, removeTag?, addTag?, maxTagCount, options)`:
  - Renders `<SysWeaver-TagsGallery>` with one `<SysWeaver-TagEdit>`/`<SysWeaver-TagText>` per tag plus remove icons.
  - Adds an "Add" `Button` while there are fewer tags than `maxTagCount`. It opens `PopUpSelection` over `findTags(search)` and excludes existing codes.
  - Re-renders after each change and returns `{ Element, Update, GetTags }`.
- Depends on `PopUpSelection` from `application.js`. `tags.css` has `.Keep` and `.Ignore` colour variants.

### Server interaction

| Call (from) | Server side | Purpose |
|---|---|---|
| `sendRequest("Api/application/GetMenu", "Default" \| "User" \| "Theme" \| "User,Theme")` (`App.Init`) | `WebMenuService.GetMenu` (SysWeaver.MicroService.HttpServer, `[WebApiUrl("application")]`), client cache 1 s, request cache 60 s | Returns a `WebMenu { RootUri, Items }` tree for the session |
| `getRequest("Api/auth/GetUser")` (`App.Init`) | `AuthManagerService.GetUser` | `{ Succeeded, NickName, ... }` for the user button |
| `../Api/HaveService` (`home.js`) | `ServiceManager.HaveService` | Feature detection of `CustomUserImageService` |
| `../Api/auth/GetUserSalt` (`welcome.js`) | `LoginService.GetUserSalt` | Used only to detect whether login is available |
| `../edit/GetTypeInfo` (`PopUpEdit`) | `TypeService.GetTypeInfo` (`[WebApiUrl("../edit")]`) | Type descriptor for the editor |
| `../auth/UserImages/Current/small` (img) | user image service | User picture |
| `app/app.js`, `app/app.css` | `HttpServerBase` optional end points (`HandleEmpty`) | Empty unless a module provides real files |

**How the menu is built on the server (`WebMenuService`):**
- Menu items come from attributes in `SysWeaver.MicroServices/WebMenu`:
  - on service classes: `WebMenuEmbedded`, `WebMenuLink`, `WebMenuLinkExternal`, `WebMenuJs`, `WebMenuPath`, `WebMenuTable`;
  - on API methods: `WebMenuTable` / `WebMenuChart`, where `Data` defaults to the API URI.
- Each attribute names a menu (`null` = `"Default"`) and a `/`-separated `Id` path that defines the tree. Missing parents are created automatically as Path items.
- `GetMenu(menu)`:
  - concatenates the requested comma-separated menus and sorts by key, so parents come before children;
  - drops an item if its `Auth` does not match the session (`""` = any logged in user, `null` = no requirement), if it is a `NoUser` item while a user is logged in, if its parent was dropped, or if its optional `Dynamic` callback returns false;
  - sorts siblings by `Order`, then by `Name`, and removes empty Path folders.
- `HttpServerBase` itself registers:
  - `Welcome` (`app/Welcome.html`, NoUser, order -100) and `Home` (`app/Home.html`, auth `""`, order -100), so exactly one of them is the first item;
  - the `Theme` menu (Js items `if(setTheme('…'));false`, which return false so the menu stays open);
  - the `Debug/Http Server` and `Debug/Data` folders.
- The auth services fill the `User` menu with Embedded `auth/*.html` pages (Login, Logout, SignUp, passkeys, password, email and phone management, …).
- Templates used here: `app/Home.html` and `app/manifest.json` (registered with `AddTemplateMatch` in `HttpServerBase`).

### State and persistence

| Key / mechanism | Owner | Content |
|---|---|---|
| `localStorage["SysWeaver.Pinned"]` | `App` | `true` / `false`: main menu pinned |
| `localStorage["SysWeaver.MenuCollapsed.<menuName>.<itemId\|Name>"]` | `MainMenu` | `"1"` when a sub-menu is collapsed. Menu names: `Main` for the main menu, `AppSettings` for the settings pop-up, `NoName` for the user pop-up |
| `localStorage["SysWeaver.PopUpRecent.<id>"]` | `SearchablePoupSelection` | JSON `{ Filter, Recent[] }` |
| `sessionStorage[<url>]` | common.js history state; cleared by `EmbeddContent` via `ClearOtherHistoryState` | Per-page state of embedded pages (scroll position etc.) |
| `history.state` + `location.hash` | `MainMenu.SetUrl` | state = absolute content URL; hash = `Base64EncodeString(url)` (deep link) |
| `window.SysWeaverApp`, `window.SysWeaverAppTitle`, `window.SysWeaverDoneFirst` | `App` / `MainMenu` | Global shell references and flags |
| `window.postMessage` to `window.top` | Embedded pages via common.js `PostTop` | `IframeNavigating {Url}` (from `Open(url, "_self")`), `LoaderRemoved` (from `PageLoaded` when the page's `AddLoading` overlay is removed) |
| `InterOp` (BroadcastChannel `"SysWeaver"`, with a fallback over windows the page opened) | home.js to the shell | `UserImageChanged` |
| `?useTheme=` URL parameter | `appendTheme` | Forwards a forced theme to embedded pages and new tabs |

### Dependencies

- **Needs from common.js:** `ColorIcon`, `Button`, `ValueFormat`, `FullScreen`, `InterOp`, `AddLoading`, `PageLoaded`, `BlockTab`/`UnblockTab`, `badClick`, `isPureClick`, `keyboardClick`, `waitFor`, `delay`, `sendRequest`, `getRequest`, `includeJs`, `createIFrame`, `appendTheme`, `GetAbsolutePath`, `ClearOtherHistoryState`, `Base64EncodeString`/`Base64DecodeString`, `Open`, `SetImageSource`, `Fail`, `Info`, `_T`/`_TF` (translation), and the globals `window.Mobile`/`window.Narrow` plus the `SwLayout` body event. `common/theme.css` supplies the CSS variables and `html.Embedded`.
- **Needs from elsewhere:** `Edit`, `EditOptions` and `TypeMemberFlags` from `edit/` (SysWeaver.MicroService.Edit, only for `PopUpEdit`); `fileUploader.js`, `UploadStatus`, `fileUploaderStatusText` (SysWeaver.MicroService.FileUploader, only for Home); icons from IconModule; `explore/table.html` and `chart/chart.html` for Table and Chart menu items.
- **Consumers:**
  - `index.html` (the shell);
  - many pages that load `../app/application.js` and `../app/application.css` for `PopUp`, `Confirm`, `PopUpMenu`, `PopUpSelection` and `SearchablePoupSelection`: explore table/api, chart, edit, chat, media view, server manager, log file, passkeys, KeyMan, user storage;
  - `app/app.js` provided by applications (`AppMain` hook).

### Structural observations

- **Classic global scripts.** There are no modules: everything in `application.js` is global, and other pages use it as a library of pop-up functions. The shell part (`App`, `appMain`) and the reusable pop-up and menu part live in the same file.
- **Fixed paths in the shell.** `App.Init` uses root-relative paths (`Api/application/GetMenu`, `Api/auth/GetUser`, `explore/table.html`, `chart/chart.html`), so it only works from a page at the server root. Pop-up helpers use paths relative to one folder level down (`../edit/GetTypeInfo`, `../auth/UserImages/...`).
- **Implicit contract for embedded pages.** Hosted pages run in an iframe inside `<app-content>` and are expected to:
  - use common.js `AddLoading()` and remove it when done, which posts `LoaderRemoved` so the frame is shown early (otherwise it is shown 1 s after load);
  - navigate with `Open(url, "_self")`, which posts `IframeNavigating` so the shell records history (otherwise the frame navigates on its own and the shell hash is not updated);
  - support the `html.Embedded` class and a transparent background.

  The shell reaches into same-origin frames (`contentWindow.location`, `document.documentElement`, `document.title`).
- **Exact URL match for menu highlighting.** The menu row is found by exact absolute-URL comparison (`RowMap`) against the frame's `location.href`. Query differences, including the added `useTheme`, mean no row is selected and the frame's `document.title` is used.
- **Pop-up menus are separate `MainMenu` instances** bound to the same `<app-content>`. Their `RowMap`/`Select` act on the pop-up's own (removed) table, not on the main menu, and the `PopUpMenu` title handler duplicates the main one. `PopUpMenu` writes an undeclared global `contentUrl`.
- **Two client item kinds.** Server-defined (`Type` + string `Data`) and client-defined (function `Data`) items share one class. Js items rely on `eval` of server strings.
- **Duplication:** `PopUpEdit` is defined twice (identical), the `Confirm` JSDoc header is duplicated, `.IconLock` is defined twice in the CSS, the `MainMenu.Add` click handlers for types 1, 2 and 6 are near-identical, `SetUrl`/`ReplaceUrl` duplicate code, and the user and settings buttons repeat the same build pattern.
- **State lives in many places:** `<body>` classes, module-level closure variables inside `App.Init` (`pinned`, `setPinned`, `menuShow`/`menuHide`), `window.*` globals, element expando properties (`row.MenuItem`, `row.MenuIcon`, `iframe.DoRemove`, `iframe.RemoveTimer`, `page.Close`, `Close.EndFn`).
- **Extension points:** `AppParams` hooks are only reachable by calling `App.Create(params)` directly; `appMain` always uses defaults and offers only the `window.AppMain` hook. Since `app/app.js` loads after `application.js`, an application could also redeclare `appMain`.
- **Tags gallery:** `tags.js`/`tags.css` are not referenced by any first-party page (an optional library). `FilterTags` calls `slice` without using the result, so `maxCount` has no effect.
