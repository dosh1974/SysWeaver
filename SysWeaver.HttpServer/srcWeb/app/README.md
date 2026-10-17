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
- **Tags gallery:** `tags.js`/`tags.css` are not referenced by any first-party page (an optional library). `FilterTags` truncates the sorted result to `maxCount` when more tags match.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function or method; all top-level declarations are globals of classic (non-module) scripts.

### `application.js`

The application shell and the reusable pop-up / menu library. Loaded with a static `<script defer>` by the root `index.html` (started by `<body onload="appMain()">`), and by many pages in other projects (statically or via `includeJs`, e.g. `explore/table.js`, `chart/chart.js`) only for the pop-up functions. Defines the global classes `WebMenu`, `WebMenuItem`, `MainMenuStyle`, `MainMenu`, `SearchablePoupSelection`, `AppParams`, `App` and the global functions listed below. Nothing executes at load time.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `window.SysWeaverAppTitle` | window global | Set by `App.Init` to the original `document.title`; used by `PopUpMenu`'s `ExternalContentFn` to build `"<app title> - <content title>"`. |
| `window.SysWeaverApp` | window global | Set by `App.Init` to the `App` instance. |
| `window.SysWeaverDoneFirst` | window global | Set to `true` by `MainMenu.EmbeddContent` after the first embed; while falsy no history entry is pushed. Reset to `false` by the `popstate` handler in `App.Init` when the state is `null`. |
| `contentUrl` | implicit global | Assigned (undeclared) by the `ExternalContentFn` set in `PopUpMenu`; holds the last URL reported to that callback. Not read anywhere in this file. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `CreateTabBlock` | `()` → `HTMLElement` | Creates a `<SysWeaver-TabBlock>` element with `tabIndex = 0`, used as a focus sentinel at the end of a pop-up (appended by `PopUpElement`). |
| `CreatePopUpBlocker` | `(dontAllowClose, onClose)` → `[block, Close]` | Creates a `<SysWeaver-Blocker>` overlay (not yet attached), calls `BlockTab(block)` and sets `tabIndex = 0`. Sets `block.onmousedown` / `block.onmouseup` (a mouse-down and mouse-up both directly on the backdrop, not a `badClick`, closes unless `dontAllowClose`) and `block.onkeydown` (stops propagation; a pure Escape closes unless `dontAllowClose`). Returns the element and the inner `Close`. |
| `Close` (inner of `CreatePopUpBlocker`) | async `()` | Awaits `onClose` if given, calls `UnblockTab(block)`, removes the blocker and calls `Close.EndFn` if set (used by `PopUp`/`PopUpElement` to resolve `waitFor`). |
| `CreatePopUpAtElement` | `(element, widthScale=0, heightScale=0, offsetX=0, offsetY=0, dontAllowClose, onClose)` → `[popup, close, block]` | Creates a `<SysWeaver-PopupMenu>` positioned once at `left = rect.x + rect.width*widthScale + offsetX`, `top = rect.y + rect.height*heightScale + offsetY` (non-number arguments become 0), creates a blocker with `CreatePopUpBlocker` and appends the pop-up to it. The blocker is not attached to the document. |
| `setPos` (inner of `CreatePopUpAtElement`) | `()` | Reads `element.getBoundingClientRect()` and writes `style.left`/`style.top` of the pop-up element; called once. |
| `PopUpMenu` | `(element, build, rightAlign, bottomAlign)` | Opens a pop-up menu anchored at `element` via `PopUpElementMenu` (not blocking, closable). The menu back element gets `DefaultMenu`. A `MainMenuStyle` is created with `HideFn = close`, `Content` = the first `<app-content>` and an `ExternalContentFn(url, title)` that hides/shows and sets `<app-ContentTitle>` text and title, sets `document.title` to `SysWeaverAppTitle + " - " + title` and assigns global `contentUrl`. Calls `build(close, backElement)` (synchronously) to get a `WebMenu`, then renders it with `new MainMenu(menu, style, backElement)`. Returns nothing. |
| `PopUpElement` | async `(element, build, blockUntilClosed, dontAllowClose, onClose, widthScale, heightScale, offsetX, offsetY)` | Creates an anchored pop-up via `CreatePopUpAtElement`, sets `page.Close = close`, adds a `ResizeObserver` that toggles `Scroll` on the blocker when the pop-up overflows, appends the blocker to `document.body` and focuses it, awaits `build(page, close)`, appends a `CreateTabBlock()` sentinel, and if `blockUntilClosed` waits (via `waitFor`, `close.EndFn`) until closed. |
| `PopUpElementMenu` | async `(element, build, blockUntilClosed, dontAllowClose, onClose, rightAlign, bottomAlign)` | Calls `PopUpElement` with `widthScale = rightAlign ? -0.25 : -0.75`, `heightScale = bottomAlign ? 0 : 1`, `offsetY = bottomAlign ? -8 : 8`; inside, creates a `<SysWeaver-PopupMenuBack>` (class `Right` when `rightAlign`, `.Close = close`) and awaits `build(backElement, close)`. |
| `PopUp` | async `(build, blockUntilClosed, dontAllowClose, onClose, haveCloseButton = !dontAllowClose)` → `Close` or `undefined` | Centered modal: creates a blocker and a `<SysWeaver-PopUpBackground>` (`tabIndex 0`, `page.onclick` stops propagation, expandos `page.Close`, `page.Block`, `page.MakeFullWidth()` adds `FullWidth` to the blocker, `page.MakeMaxHeight()` adds `MaxHeight` to the blocker). A `ResizeObserver` toggles `Scroll` on the blocker. Appends to `document.body`, creates a `<SysWeaver-BlockerCloseButton>` if `haveCloseButton`, awaits `build(page, Close, closeButtonContainerOrNull)`, then appends the container with an `IconClosePopUp` `ColorIcon` (32x32) that calls `Close`, and focuses the page. If `blockUntilClosed` it resolves when closed (returns `undefined`), otherwise returns `Close`. A commented-out block once created `<SysWeaver-BlockerClose>` with its own `blockClose.onclick`. |
| `PopUpWorking` | async `(title, text, iconClass, iconWidth=64, iconHeight=64)` → `Close` | Non-closable, non-blocking `PopUp` showing optional `<SysWeaver-PopUpWorkingTitle>`, a `<SysWeaver-PopUpWorkingIcon>` with an `IconWorking` icon (256x256, `IconColorThemeMain`), optional `<SysWeaver-PopUpWorkingText>` and an optional second icon (`iconClass`). Resolves to the close function. |
| `PopUpSelection` | async `(text, title, getItems, startText, getAll, blockUntilClosed, dontAllowClose, onClose)` | `PopUp` with a `<SysWeaver-PopUpText>` header (`text`, tool tip `title`), a `<SysWeaver-PopUpFilter>` row with an `<input>` (placeholder `title`, initial `startText`) and an `IconClearInput` clear button, and a `<SysWeaver-SelectList>`. `getItems(search, close)` (may be async) returns the HTMLElements to list; it is skipped for empty search unless `getAll`, skipped for an unchanged search, and results are dropped if the input changed meanwhile. `inputE.oninput`/`onchange` run the inner `filterChangeFn`; `inputE.onkeydown` with a pure Enter clicks the first list element. |
| `update` (inner of `PopUpSelection`) | async `()` | Runs `getItems` for the current input value and replaces the list contents (see above). |
| `filterChangeFn` (inner of `PopUpSelection`) | async `()` | Enables/disables the clear button by whether the input is non-empty, then calls `update`. |
| `PopUpEdit` | async `(type, obj, buttonText, buttonIcon, buttonToolTip, title, titleToolTip, primName)` → edited object or `null` | Blocking `PopUp` with an optional `<SysWeaver-ConfTitle>`. If `type` is a string, requests `sendRequest("../edit/GetTypeInfo", type)` (on failure or empty result shows `Fail` and returns, leaving an empty pop-up); for primitive types (`TypeMemberFlags.IsPrimitive`) sets `DisplayName` of the type and first member to `primName ?? title ?? "Value"`. Creates an `Edit(type, undefined, EditOptions{Title:false, Border:false})`, awaits `SetObject(obj)`, and adds a `<SysWeaver-ConfButtons>` with a `Button` (`buttonText` default "OK") that stores `pe.GetObject()` and closes. Returns the stored object, or `null` when closed otherwise. Declared twice with identical bodies (lines 1037 and 1094); the later declaration is the effective one. |
| `Confirm` | async `(title, text, okButtonText="Ok", cancelButtonText="Cancel", okButtonImageClass="IconOk", cancelButtonImageClass="IconCancel", okButtonTitle="", cancelButtonTitle="", okStyle=null, cancelStyle=null)` → `boolean` | Blocking `PopUp` with optional `<SysWeaver-ConfTitle>` and `<SysWeaver-ConfText>` (both copy their text on click via `ValueFormat.copyOnClick`, which adds `MouseClick`) and a `<SysWeaver-ConfButtons>` row with OK and Cancel `Button`s. Resolves `true` only when OK was pressed. |
| `appMain` | async `(settingMenus)` | Shell entry point (called from `index.html`'s `onload`). `AddLoading(document.body)`, `await App.Create()` (default params; `settingMenus` is ignored), awaits global `window.AppMain()` if it is a function, removes the loader, `await delay(1)`, then calls `app.InitPage()`. |

#### class `WebMenu`
Client-side menu tree as returned by `Api/application/GetMenu`; created with `new WebMenu()` (client menus) or `WebMenu.From(json)`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `RootUri`, `Items`, `Name` | field | | Server root URI string, array of `WebMenuItem`, menu name (used by `MainMenu` for the `localStorage` collapse key; default `""`). |
| `From` | static method | `(obj, name)` → `WebMenu` | `Object.assign`s `obj` onto a new `WebMenu` and converts each `Items` entry with `WebMenuItem.From`. `name` is applied only when `Items` is non-empty. |

#### class `WebMenuItem`
One menu entry (server C# `WebMenuItem` mirror, or a client-built item whose `Data` is a function).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Id`, `Name`, `Type`, `Title`, `IconClass`, `Data`, `Children`, `Flags` | field | | Id (`"-"` = separator), display name, numeric type (0 Path, 1 Table, 2 Embedded, 3 Link, 4 LinkExternal, 5 Js, 6 Chart), tool tip, icon class name, type-dependent data (URL, JS, or a function `async (item, style, isUserClick) => bool`), child items, flags (1 = disabled, 2 = checked). |
| `OnClick` | field | `(ev?)` | Set by `MainMenu.Add` to the row's click handler (or `null`); used by `App.Init` to open the first item programmatically. |
| `From` | static method | `(obj)` → `WebMenuItem` | `Object.assign`s `obj` onto a new instance and converts `Children` recursively. |

#### class `MainMenuStyle`
Plain options object passed to `MainMenu`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `IconWidth`, `IconHeight` | field | | Item icon size (32x32). |
| `ExpandIconWidth`, `ExpandIconHeight`, `ExpandTab` | field | | Expander icon size (20x20) and per-depth indentation in px (16). |
| `IconColorClass`, `IconSelColorClass`, `SubIconColorClass`, `SubIconSelColorClass` | field | | Color classes for leaf/sub-menu icons, normal and selected (defaults `IconColorThemeBackground` / `IconColorThemeAcc1`). |
| `IconExpander`, `IconExpanderMain` | field | | Comma-separated icon class pairs `"expanded,collapsed"`: `"IconCollapse,IconExpand"` for the expander cell, `"IconCollapseMain,IconExpandMain"` for item icons of folders without own icon/action. |
| `MenuStyle` | field | | CSS class added to the target element (`"DefaultMenu"`). |
| `Content` | field | | Element that hosts embedded iframes. |
| `SelectFn` | field | `(itemOrNull)` | Called from `MainMenu.Select`. |
| `ExternalContentFn` | field | `(url?, title?)` | Called with no args when navigation starts and with `(url, title)` when the frame is revealed. |
| `HideFn` | field | `()` | Called after an item action to hide/close the menu. |

#### class `MainMenu`
Renders a `WebMenu` into nested `<table>`s and handles navigation into an iframe host. Created with `new MainMenu(menu, style, toElement)` by `App.Init` (main menu) and `PopUpMenu` (pop-ups).

| Member | Kind | Signature | Description |
|---|---|---|---|
| constructor | constructor | `(menu, style = new MainMenuStyle(), toElement)` | Stores `Data`/`Style`; returns early if `menu.Items` is empty. Sets `ExpKey = "SysWeaver.MenuCollapsed.<menu.Name or NoName>."`, builds a `<table cellspacing=0 cellpadding=0>` by calling `Add` per item, adds `MenuNoExpand` if no item has children, stores it in `Element` and, if `toElement` is given, adds `style.MenuStyle` to it and appends the table. |
| `Data`, `Style`, `Selected`, `SelTitle`, `RowMap`, `ExpKey`, `Element` | field | | Source `WebMenu`, style, selected `<tr>`, saved tool tip of the selected row, `Map` absolute URL → row, `localStorage` key prefix, root table. |
| `SetIcon` | static method | `(icon, iconClasses)` | Sets a `ColorIcon` image to the first class of a comma-separated list (no-op if empty). |
| `SetExpIcon` | static method | `(icon, isExpanded, iconClasses)` | Picks class 0 when expanded, 1 when collapsed (or the only one) and calls `icon.ChangeImage`. |
| `GetEmbeddedUrl` | static method | `(content)` → `string` or `null` | URL of the last child iframe of `content`: `contentWindow.location.href` if accessible, else `src`; `null` if there is none. |
| `LoaderRemoved` | method | `(style)` | If the last child of `style.Content` is an iframe with a pending `DoRemove`, calls it (reveals the frame early). |
| `EmbeddContent` | method | `(url, style, noNewState)` | Makes `url` absolute (`GetAbsolutePath`), unless `noNewState` calls `ClearOtherHistoryState(url)`, calls `ExternalContentFn()`, fades out (`Hidden`) and removes after 25 ms the previous iframe (or clears the content), creates a hidden iframe (`createIFrame`, `frameborder=0`, `allowtransparency`) with `src = appendTheme(url)`. Sets `e.DoRemove` (adds `Embedded` to the frame's `<html>`, selects the matching `RowMap` row or `null`, picks a title from the item name or frame `document.title`, clears `RemoveTimer`, removes `Hidden`, calls `ExternalContentFn(newUrl, title)`) and `e.onload` (schedules `DoRemove` after 1000 ms in `e.RemoveTimer`). Pushes a history entry via `SetUrl` unless `noNewState` or before the first embed; sets `window.SysWeaverDoneFirst = true`. |
| `Select` | method | `(row)` | Un-highlights the previous row (removes `MenuSel`, restores tool tip, re-enables its icon with `IconColorClass`), highlights `row` (adds `MenuSel`, clears tool tip, disables icon with `IconSelColorClass`) and calls `style.SelectFn(row.MenuItem or null)`. |
| `SetUrl` | method | `(key)` | `history.pushState(key, "", url with hash = Base64EncodeString(key))`. |
| `ReplaceUrl` | method | `(key)` | Same as `SetUrl` but `history.replaceState`. |
| `Add` | method | `(toElement, data, depth, style)` → `boolean` | Inserts a `tr.MenuData` row (expandos `MenuItem`, `MenuIcon`) with cells indent / expander / name / icon into `toElement`; separators (`Id "-"`) get `MenuSeparator` and an optional `<span>` label. Builds the click handler by `Type` (function `Data` is called; 1 Table embeds `explore/table.html?q=../Data`; 2 embeds `Data`; 3 sets `location.href`; 4 `window.open`; 5 calls `window[Data]` or `eval(Data)`; 6 embeds `chart/chart.html?q=../Data`; truthy results / embeds call `HideFn`; a `badClick` is ignored; re-clicking the selected row is ignored). URL items are made absolute and stored in `RowMap`. Items with children get `SubMenu`, a nested `tr.MenuNested` row with a sub-table (recursive `Add`), an expander `ColorIcon` and a toggle (inner `expandFn`) that flips `MenuHide`/`SubMenuHide`, updates tool tips ("Click to collapse/expand") and icons, and stores `"1"` in `localStorage[ExpKey + (Id or Name)]` while collapsed. Adds `MenuDisabled` (no handler), `MenuChecked`, `MenuClick`; sets `data.OnClick`. Also sets the background position of the nested-table guide lines by depth. Returns `true` if the item has children. |

#### class `SearchablePoupSelection`
Reusable paged, searchable pop-up list with a most-recently-used memory. Created with `new SearchablePoupSelection(...)` and shown with `Show` (used by `edit/EditTypeLanguage.js`).

| Member | Kind | Signature | Description |
|---|---|---|---|
| constructor | constructor | `(id, getRecentIdFn, createElementFn, findFn, maxRecent = 10)` | Stores the callbacks; `Key = "SysWeaver.PopUpRecent." + id`. |
| `Key`, `CreateElementFn`, `FindFn`, `MaxRecent`, `GetRecentIdFn` | field | | `localStorage` key; `createElementFn(data, onSelect, isRecent)` → element; `findFn(filter, recentMap, count, offset)` → array (may be async); MRU size; `getRecentIdFn(data)` → id. |
| `Show` | async method | `(titleText, onSelect, maxView = 20, titleToolTip, filterPlaceholder, blockUntilClose, dontAllowClose, onClose)` | Loads `{Filter, Recent[]}` from `localStorage[Key]` (reset on parse error, `Recent` truncated to `MaxRecent`), builds `recentMap` (id → 1-based rank) and opens a `PopUp` with a `<SysWeaver-PopUpText>` title, `<SysWeaver-PopUpFilter>` input (initial value = saved filter) with `IconClearInput` clear button, and a `<SysWeaver-PopSelData>` list. Pure Enter (`inputEl.onkeydown`) selects the first result. |
| `onSelectFn` (inner of `Show`) | async | `(val)` | Awaits `onSelect(val)` (errors ignored), moves `getRecentIdFn(val)` to the front of `Recent`, trims to `MaxRecent`, saves state to `localStorage` and closes the pop-up. |
| `updateList` (inner of `Show`) | async | `()` | Skips if filter and offset are unchanged; saves the filter when re-querying from offset 0; calls `findFn(filter, recentMap, maxView + 1, offset)`; at offset 0 replaces the list, otherwise removes the previous end marker; appends up to `maxView` elements and a `<SysWeaver-PopSelEnd>` that holds either an `IconMoreData` icon (loads the next page, `offset += maxView`) or "No more data!"; with no results shows "Nothing found!" (or "No more data!" on later pages). Tracks the first result as `topSearch`. |
| `filterChangedFn` (inner of `Show`) | async | `()` | Updates clear-button state, resets `offset` to 0 and calls `updateList`; bound to `inputEl.onchange`/`oninput`. |

#### class `AppParams`
Options for `App.Create` / `App.Init`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `PageElement` | field | | Container element (default `null` = `document.body`). |
| `MainMenu`, `MainMenus`, `MainMenuPinned` | field | | Create the main menu (`true`); comma-separated menu names to request (`null` = `"Default"`, `""` = none); initial pinned state when nothing is stored (`false`). |
| `Content` | field | | Create `<app-content>` (`true`). |
| `Navigation` | field | | Back button: `true`/`false`, or `null` = only when `window.Mobile`. |
| `UserButton`, `SettingsButton` | field | | Create the header user / settings buttons (`true`). |
| `SettingMenus` | field | | Menu names for the settings pop-up (`null` = `"Theme"` or `"User,Theme"`; `""` = none, also disables the user menu request). |
| `SettingsFs`, `SettingsNewTab` | field | | Add the full-screen item / "Open in new tab" item to the settings menu (`true`). |
| `OnSettingsMenuOpenFn` | field | `(webMenu, closeFn)` | Called each time the settings menu is built, to add items. |
| `OnMainMenuDefFn` | field | async `(menuDef, mainMenuStyle, hideFn)` | Called before rendering the main menu, may modify the definition and style. |

#### class `App`
The shell. Created via `App.Create(params)` (from `appMain`).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Page`, `AppHeader`, `AppMenu`, `Content`, `AppMenuButton`, `AppBackButton`, `PinButton`, `Title`, `InitPage` | field | | Container element, `<app-header>`, `<app-menu>`, `<app-content>`, `<app-MenuButton>`, `<app-BackButton>`, `<app-PinButton>`, `<app-ContentTitle>`, and the function that opens the initial page. |
| `Create` | static async method | `(params)` → `App` | `new App()`, `InitPage = await Init(params)`. |
| `Init` | async method | `(params = new AppParams())` → `function` | Sets `window.SysWeaverAppTitle`/`SysWeaverApp`; builds `<app-header>` with optional `<app-BackButton>` (`IconNavBack`, `history.back()`), `<app-PinButton>` (`IconLock`, pins and stores `localStorage["SysWeaver.Pinned"] = true`), `<app-ContentTitle>` (`appHeaderMiddle.onclick` copies its title via `ValueFormat.copyToClipboardInfo`), `<app-UserButton>` (requests `GetMenu("User")` and `Api/auth/GetUser`; icon `IconCurrentUserFill` + `WithUser` or `icons/user.svg`; opens a `PopUpMenu` of the user items, unwrapping a single root; listens on `InterOp` for `UserImageChanged` to refresh the background image), `<app-SettingsButton>` (`IconSettings`; `PopUpMenu` named `AppSettings` with full-screen enter/exit, theme items, "Open in new tab", `OnSettingsMenuOpenFn`), `<app-content>`, and `<app-menu>` (main menu). The main-menu part defines the inner functions below, listens to the body `SwLayout` event, requests `GetMenu(MainMenus ?? "Default")`, sets `menuStyle.SelectFn` (no-op) and `menuStyle.ExternalContentFn` (updates `<app-ContentTitle>`, `Hidden` when no title, and `document.title`), calls `OnMainMenuDefFn`, renders `MainMenu` named `Main`, and registers `popstate` (string state → `EmbeddContent(s, style, true)`; null → reset `SysWeaverDoneFirst`, click first item) and `message` (`IframeNavigating` → `EmbeddContent(Url)`, `LoaderRemoved` → `LoaderRemoved`) listeners. Returns a function embedding the Base64-decoded hash URL, else the first item's `OnClick`, else a no-op. |

#### Inner functions of `App.Init`

| Function | Signature | Description |
|---|---|---|
| `setPinned` | `(p)` | On `window.Narrow`: always adds `SwCollapsed`, removes `SwPinned`, remembers `p`. Otherwise applies `p` by toggling `SwPinned`/`SwCollapsed` on `<body>`. Used by the pin button, `menuToggle` and the `SwLayout` listener. |
| `tempDisableAutoShow` | `()` | Removes the `mousemove` listener `mouseOverFn` from `<app-menu>` and re-adds it after 250 ms. |
| `menuHide` | `()` | If the menu is shown and not pinned (or narrow): removes `<app-capture>` and the body capture listeners, adds `SwCollapsed`, calls `tempDisableAutoShow`. Also the main menu's `HideFn`. |
| `menuCapture` | `(ev)` | Capture-phase body `mouseover`: hides the menu unless the target is inside `<app-menu>` or `<app-header>`. |
| `menuCaptureLeave` | `(ev)` | Capture-phase body `mouseleave`: hides the menu when the body itself is left. |
| `menuShow` | `()` | If collapsed: appends `<app-capture>`, registers `menuCapture`/`menuCaptureLeave`, removes `SwCollapsed`. |
| `menuToggle` | `()` | Menu button click: if pinned, unpins and stores `localStorage["SysWeaver.Pinned"] = false`; else shows or hides the collapsed menu. |
| `mouseOverFn` | `(ev)` | `<app-menu>` `mousemove` handler: shows the collapsed menu when `ev.clientX < 8`. |

### `home.js`

Script of the `Home.html` template page (loaded with a static `<script>`; `homeMain` is the page's load entry). Needs `common.js`.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `CS` | const | `document.currentScript.src`, captured at load time; base for the relative `includeJs` calls. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `homeMain` | async `()` | Shows `AddLoading()`. If `sendRequest("../Api/HaveService", "SysWeaver.MicroService.CustomUserImageService")` is truthy, loads `../common/md5.js`, `../common/UZIP.js`, `../common/fileUploader.js` in parallel with `includeJs(CS, ...)`, adds `CanUpload` and a tool tip to the first `.UserImage` element and calls `fileUploaderSetup(el, "UserImage", onSelect, onDone)`. `onSelect` shows an `Info` "Uploading user image"; `onDone` shows `Fail` on `res.Error`, otherwise on `UploadStatus.AlreadyUploaded` shows `Info` and posts `InterOp.Post("UserImageChanged")`, on `UploadStatus.None` swaps a `/small` `src` to `/large` (or reloads via `SetImageSource(el, src, null, true)`) and posts `UserImageChanged`, else shows `Fail` with `fileUploaderStatusText`. Errors show `Fail`; the loader is always removed. |

### `welcome.js`

Script of `Welcome.html` (static `<script>`; `welcomeMain` is the page's load entry). Needs `common.js`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `welcomeMain` | async `()` | Shows `AddLoading()`. If `sendRequest("../Api/auth/GetUserSalt", "dummy")` is truthy, makes the first `<img>` clickable (class `Click`, tool tip "Click to sign in") and appends a `<p>` with the same text made keyboard-clickable (`keyboardClick`, adds `KeyClick`) and focused. Both use the inner `fn` handler. Errors show `Fail`; the loader is always removed. |
| `fn` (inner of `welcomeMain`) | `(ev)` | Ignores `badClick`; otherwise `open("../auth/Login.html", "_self")`. |

### `tags.js`

Tag gallery control library. Not loaded by any first-party page; a consumer must load it after `common.js` and `application.js` (uses `PopUpSelection`) and link `tags.css`. Defines `TagGalleryParams`, `Tag`, `FilterTags`, `SetTagClassName`, `CreateTagsGallery`.

#### Functions

| Function | Signature | Description |
|---|---|---|
| `FilterTags` | `(tags, searchFor, maxCount)` → `Tag[]` | Returns `tags` unchanged for an empty search. Otherwise keeps tags whose lower-cased `Name` contains the search or whose `Desc` contains it, sets `s.Rank = (0.25/searchLength) * removedLength + (100 - namePos)` (where `namePos` < 0, i.e. no name match, is replaced by 100, so earlier name matches rank higher, and `removedLength` is the search length when the first occurrence in `Desc` exists, else 0) on each kept tag (mutates input objects) and returns them sorted by `Rank` descending. When more than `maxCount` tags match, the sorted list is truncated to `maxCount`. Requires `Desc` to be a string. |
| `SetTagClassName` | `(tagRet, className)` → `tagRet` | Sets `Class = className` on every element of `tagRet.Tags`; returns `tagRet`. |
| `CreateTagsGallery` | async `(findTags, getTags, removeTag, addTag, maxTagCount, options = new TagGalleryParams())` → `{ Element, Update, GetTags }` | Creates `<SysWeaver-TagsGallery>` (class `Embedded` when `options.Embedded`, plus `options.Class`; expando `AllTags`) and renders it with the inner `update`. `findTags(search)` and `getTags()` return `{ Tags: [] }`; `removeTag(tag)`/`addTag(tag)` are optional and return truthy on success. `Update` re-renders, `GetTags()` returns the last fetched tags. |
| `update` (inner of `CreateTagsGallery`) | async `()` | Awaits `getTags()`, clears the gallery and adds a `<SysWeaver-TagEdit>` (tag `Class`, tool tip `Desc`) with a `<SysWeaver-TagText>` per tag; with `removeTag` adds a remove `ColorIcon` (`options.RemoveIcon`, `RemoveIconSize`, `RemoveClass`) that shows working state, awaits `removeTag`, shows `Fail(RemoveError)` on failure and re-renders. While fewer than `maxTagCount` tags and `addTag` is given, adds an add `Button` (front or end per `AddAtFront`, class `AddClass`) that opens `PopUpSelection` (texts from Search* or Filter* depending on `ShowAllTags`, which is also passed as `getAll`); its item builder calls `findTags`, skips existing codes and creates `<SysWeaver-TagEdit class="TagSel">` items (`tabIndex 0`) whose `tagE.onclick` / `tagE.onkeyup` (Enter) await `addTag`, report `Fail(AddError)` on failure, re-render and close the pop-up. |

#### class `TagGalleryParams`
Options for `CreateTagsGallery`; create with `new TagGalleryParams()` and override fields. Text fields are translated (`_TF`) at construction.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `ShowAllTags` | field | | `false`: the add pop-up searches (needs text); `true`: lists all tags and filters (`getAll`). |
| `AddAtFront` | field | | Insert the add button first (`true`) or last. |
| `Class` | field | | Extra class on the gallery element (e.g. `Keep`, `Ignore`). |
| `AddText`, `AddTitle`, `AddError`, `AddIcon`, `AddClass` | field | | Add button text, tool tip, error prefix, icon (`IconTagAdd`), extra class. |
| `RemoveTitle`, `RemoveError`, `RemoveIcon`, `RemoveClass`, `RemoveIconSize` | field | | Remove icon tool tip format (`{0}` = tag name), error prefix, icon (`IconTagRemove`), extra class, size (24). |
| `SearchText`, `SearchTitle`, `SearchError` | field | | Pop-up header / placeholder / error used when `ShowAllTags` is false. |
| `FilterText`, `FilterTitle`, `FilterError` | field | | Same when `ShowAllTags` is true. |
| `Embedded` | field | | Adds class `Embedded` (no border/padding/background). |

#### class `Tag`
Shape of a tag object (documentation only; plain objects are used).

| Member | Kind | Signature | Description |
|---|---|---|---|
| `Code`, `Name`, `Desc`, `Class` | field | | Unique code (names may be localized), displayed name, tool tip, optional CSS class. `FilterTags` also adds `Rank`. |

## CSS reference

### `application.css`

Styles the application shell (`index.html`) and the pop-up/menu library from `application.js`; also linked by every page in other projects that uses those pop-ups or the `.Icon*` classes. Depends on theme variables from `common/theme.css` (`--ThemeIconSize`, `--ThemeBackground*`, `--ThemeMain*`, `--ThemeAcc1*`, `--ThemeAcc2*`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeFont`, `--ThemeFadeDuration`, `*RGB` variants). Not templated. Uses native CSS nesting under `body.Sw*` selectors. Icons sized via `icon-WxH` / `Icon-32x32` elements whose base rules are inserted by `ColorIcon` in `common.js`.

#### Page and header layout

| Selector(s) | Description |
|---|---|
| `html`, `body` | `html` is empty; `body` fills the viewport and defines `--h`, `--imh`, `--mh`. |
| `app-header` | Absolute top bar (height `--mh`, z-index 2) with gradient background and shadow; defines the slot positions `--mw`, `--me`, `--s0`, `--s1`, `--s2`. Created by `App.Init`. |
| `app-header app-MenuButton`, `app-BackButton`, `app-PinButton` | Square buttons at `--s0` (menu) and `--s1` (back / pin, same slot); the pin button fades with `--ThemeFadeDuration`. |
| `body.SwNarrow app-PinButton`, `body.SwPinned app-PinButton`, `body.SwCollapsed app-PinButton`, `body.SwMobile app-PinButton` | Pin button hidden (narrow/collapsed, opacity 0 and no pointer events), dimmed to 0.25 and inert (pinned), or `display:none` (mobile). `SwNarrow`/`SwMobile` are set by `common.js` layout detection; `SwPinned`/`SwCollapsed` by `App.Init`. |
| `app-header app-ContentTitle`, `app-ContentTitle.Hidden` | Centered uppercase bold title between `--s2` margins; `Hidden` (toggled by the `ExternalContentFn`s) fades it out. |
| `app-header app-SettingsButton`, `app-header app-UserButton` | Right-side buttons at `--s0` and `--s1`. |
| `app-UserButton .WithUser` (`:hover`, `:focus`) | Rounded outlined user-image icon when logged in (`WithUser` added by `App.Init`); thicker outline on hover/focus. |

#### Main menu and content

| Selector(s) | Description |
|---|---|
| `app-menu` | Absolute 400 px side panel below the header (z-index 1), gradient background, `direction: rtl` so the scrollbar is on the left, `transform` transition. |
| `app-menu:before` | Fixed, faint (`opacity 0.05`) `../logo.svg` watermark at the bottom. |
| `app-menu > *` | Restores `direction: ltr` for the content. |
| `body.SwCollapsed app-menu` (and its nested `app-menu.DefaultMenu td`, `td:last-child`, `tr:hover`, `tr.SubMenu:hover`, `tr.MenuSel`, `tr.MenuNested` rules) | Translates the menu so only a 52 px × `--ThemeIconSize` icon strip shows; in the strip only the icon cell gets borders/highlight backgrounds. |
| `body.SwNarrow app-menu`, `body.SwNarrow.SwCollapsed app-menu` | Full-width menu on narrow screens; collapsed slides it up out of view (`translate(0,-110%)`). |
| `app-capture` | Full-viewport, slightly tinted overlay below the menu (z-index -2) appended by `menuShow` while the collapsed menu is shown. |
| `app-content`, `body.SwPinned app-content`, `body.SwNarrow app-content` | Absolute content area below the header with left padding of the icon strip; 400 px when pinned; 0 when narrow. |
| `app-content iframe`, `app-content iframe.Hidden` | Full-size iframe with opacity/transform transition; `Hidden` (set by `MainMenu.EmbeddContent`) fades and offsets it. |

#### Menu tables (`.DefaultMenu`)

`DefaultMenu` is the `MainMenuStyle.MenuStyle` class added by `MainMenu` to `<app-menu>` and by `PopUpMenu` to `<SysWeaver-PopupMenuBack>`. Row classes are set by `MainMenu.Add` / `Select`.

| Selector(s) | Description |
|---|---|
| `.DefaultMenu table`, `.DefaultMenu table tr` | Full-width separated tables with 0.5em row spacing. |
| `.DefaultMenu table.MenuNoExpand td:first-child`, `td:nth-child(2)` | When no item has children (`MenuNoExpand`): fixed 10 px indent cell and zero-width expander cell. |
| `.DefaultMenu tr`, `.DefaultMenu td`, `td:first-child`, `td:nth-child(2)`, `td:nth-child(3)`, `td:last-child`, `td:last-child > *` | Cell layout: indent cell, expander cell sized by `--ThemeIconSize`, name cell (left rounded, `nowrap`), icon cell (right rounded). |
| `.DefaultMenu td Icon-32x32.IconDisabled` | Keeps full opacity for the selected row's icon that `Select` disables (`IconDisabled` is `ColorIcon`'s disabled class). |
| `.DefaultMenu tr.MenuNested > td`, `> td > table`, `> td > table > tbody`, `> td:last-child > *`, `> td:nth-child(2)` | Container row for children: no padding/border; 2 px vertical and horizontal guide lines drawn with gradients (horizontal position set inline by `Add`). |
| `.DefaultMenu tr.MenuHide` | Hidden collapsed children row. |
| `.DefaultMenu tr.SubMenu`, `tr.SubMenuHide td:last-child` | Folder rows in `--ThemeMain`; collapsed folders get an inset right-edge marker. |
| `.DefaultMenu tr.MenuData:hover td`, `tr.MenuData.SubMenu:hover td` | Hover highlight (accent, or main color for folders). |
| `.DefaultMenu tr.MenuData.MenuSel td`, `.MenuSel.MenuClick`, `.MenuSel:hover td`, `.MenuSel.MenuDisabled:hover td` | Selected row highlight, default cursor. |
| `.DefaultMenu tr.MenuDisabled`, `tr.MenuData.MenuDisabled:hover td` | Disabled rows at 0.4 opacity, no hover background. |
| `.DefaultMenu tr.MenuChecked td`, `tr.MenuChecked td:nth-child(2)::after` | Checked rows show a "✓" in the expander cell. |
| `.DefaultMenu tr.MenuSeparator td:nth-child(3)`, `tr.MenuData.MenuSeparator td:nth-child(3) > span` | Separator: centered 1 px line with an optional italic label on `--ThemeAcc2` background. |
| `.DefaultMenu tr.MenuClick`, `tr.MenuClick:hover` | Clickable rows: pointer, 0.8 opacity, 1 on hover. |
| `SysWeaver-PopupMenuBack.DefaultMenu tr.SubMenu`, `tr.SubMenuHide td:last-child`, `tr.MenuNested > td > table` (and `> tbody`), `tr.MenuData.SubMenu:hover td`, `tr.MenuData:hover td` | Pop-up menu variants of the above using `--ThemeBackground*` colors (pop-up menus have an accent background). |
| `SysWeaver-PopupMenu.DefaultMenu table` | Auto-sized tables inside a `SysWeaver-PopupMenu` with `DefaultMenu`. |

#### Pop-ups

| Selector(s) | Description |
|---|---|
| `SysWeaver-PopupMenu`, `SysWeaver-PopupMenu.Bottom SysWeaver-PopupMenuBack` | Fixed-position anchor created by `CreatePopUpAtElement`. `Bottom` anchors the back to the bottom; no JS setting `Bottom` was found. |
| `SysWeaver-PopupMenuBack`, `SysWeaver-PopupMenuBack.Rel`, `SysWeaver-PopupMenuBack.Right` | Accent-colored scrollable menu panel (max 75vw × 85vh) created by `PopUpElementMenu`; uses the scroll-driven animation below. `Rel` makes it `position: relative` (set by `ServerManager/serviceInfo.js`); `Right` (set when `rightAlign`) aligns it right. |
| `SysWeaver-Blocker`, `SysWeaver-Blocker:focus` | Fixed full-screen dimmed, blurred overlay (z-index 100) from `CreatePopUpBlocker`. |
| `SysWeaver-Blocker SysWeaver-BlockerClose`, `SysWeaver-Blocker.FullWidth SysWeaver-BlockerClose` | Centered close-bar container; only created by commented-out code in `PopUp`. |
| `SysWeaver-Blocker SysWeaver-BlockerCloseButton`, `> *` | Close button container at the top right of the pop-up (`PopUp` with `haveCloseButton`). |
| `SysWeaver-Blocker.FullWidth SysWeaver-PopupBackground` | Near-full-width pop-up when the blocker has `.FullWidth` (set by `MakeFullWidth()`). Element names are case-insensitive, so `SysWeaver-PopupBackground` matches `SysWeaver-PopUpBackground`. |
| `SysWeaver-Blocker.MaxHeight SysWeaver-PopupBackground` | Full-height pop-up (`MakeMaxHeight()`). |
| `SysWeaver-Blocker.Scroll SysWeaver-PopupBackground`, `SysWeaver-Blocker.Scroll SysWeaver-BlockerCloseButton` | When the pop-up overflows (`Scroll` toggled by the `ResizeObserver`): square right corners, close button shifted left of the scrollbar. |
| `SysWeaver-PopUpBackground`, `:focus`, `.Wide`, `.Resize` | Centered 40em dialog box (top padding for the close button), scrollable; `Wide` = 75em (set by `edit/type.js`, `Translator.js`), `Resize` = user-resizable (set by `common.js`). |
| `SysWeaver-PopUpBackground SysWeaver-PopUpText` | Small uppercase accent header (`PopUpSelection`, `SearchablePoupSelection`). |
| `SysWeaver-PopUpBackground SysWeaver-PopUpFilter`, `input`, `input::placeholder`, `textarea`, `textarea::placeholder` | Filter row (flex) and styled inputs/textareas inside dialogs. |
| `SysWeaver-PopUpBackground SysWeaver-SelectList` | Scrollable result list of `PopUpSelection`. |
| `SysWeaver-PopUpBackground SysWeaver-PopSelData`, `SysWeaver-PopSelEnd` | Result list and end marker of `SearchablePoupSelection` (end has 50vh bottom margin). |
| `SysWeaver-ConfTitle`, `SysWeaver-ConfText`, `.MouseClick` | Dialog title/text (`Confirm`, `PopUpEdit`); `MouseClick` (added by `ValueFormat.copyOnClick`) gives a pointer cursor. |
| `SysWeaver-ConfButtons` | Wrapped, evenly spaced button row. |
| `SysWeaver-CenterBlock`, `SysWeaver-CenterBlock > *:first-child:last-child`, `SysWeaver-CenterBlockSpace` | Flex rows (space-between / space-around); a single child is centered. `CenterBlock` is created by `common.js` and `auth/SimpleAuth.js`; no creator of `CenterBlockSpace` was found in JS. |
| `SysWeaver-PopUpWorkingTitle`, `SysWeaver-PopUpWorkingIcon`, `SysWeaver-PopUpWorkingText` | Centered blocks of `PopUpWorking`. |
| `hr` | Global thin `--ThemeMain25` rule. |

#### Icons

All are `mask-image` icons (applied to `ColorIcon` elements via `IconClass`/class name) from `../icons/` (SysWeaver.HttpServer.IconModule).

| Selector(s) | Description |
|---|---|
| `.IconMainMenu`, `body.SwCollapsed .IconMainMenu`, `body.SwNarrow .IconMainMenu`, `body.SwCollapsed.SwNarrow .IconMainMenu` | Menu button arrow: `left.svg` default, `right.svg` collapsed, `up.svg` narrow, `down.svg` narrow+collapsed. |
| `.IconMenu`, `.IconExpand`, `.IconCollapse`, `.IconExpandMain`, `.IconCollapseMain`, `.IconClearInput` | `more`, `expand`, `collapse`, `plus`, `minus`, `clear_input` — menu expanders (`MainMenuStyle`) and filter clear buttons. |
| `.IconHome`, `.IconSignUp`, `.IconLogin`, `.IconUser`, `.IconUsers`, `.IconInvite`, `.IconLogout`, `.IconDelete`, `.IconRecover` | `home`, `user_add`, `user`, `user`, `users`, `user_add`, `user_logout`, `user_delete`, `user_check` — auth/user menu items. |
| `.IconPassword`, `.IconChangePassword`, `.IconDeletePassword`, `.IconAddPassword`, `.IconAddEmail`, `.IconDeleteEmail`, `.IconChangeEmail`, `.IconAddPhone`, `.IconDeletePhone`, `.IconChangePhone` | `user_password*`, `user_email_*`, `user_phone_*` — account management items. |
| `.IconAddPasskey`, `.IconUsePasskey`, `.IconAddPasskeyOther`, `.IconKey`, `.IconLock`, `.IconUnlock` | `passkey_add`, `passkey_use`, `qr_code`, `key`, `lock` (defined twice, identical; also the pin button), `unlock`. |
| `.IconSettings`, `.IconNavBack`, `.IconFullScreenEnter`, `.IconFullScreenExit`, `.IconNewTab` | Header and settings-menu icons (`settings`, `left`, `fullscreen_enter`, `fullscreen_exit`, `newtab`). |
| `.IconTheme`, `.IconThemeAuto`, `.IconThemeLight`, `.IconThemeDark`, `.IconThemeHacker` | Theme menu icons (`theme*.svg`). |
| `.IconExplore`, `.IconTable`, `.IconDbTable`, `.IconChart` | `explore`, `table`, `db`, `chart`. |
| `.IconTableSerializer`, `.IconTableLanguage`, `.IconTableCurrency`, `.IconTableCountry`, `.IconTablePhone`, `.IconTableCompression`, `.IconTableStats`, `.IconTableServices`, `.IconTablePerfSystem`, `.IconTablePerformance`, `.IconTableLog`, `.IconTableTables`, `.IconTableApi`, `.IconTableTranslateTo`, `.IconTableTranslateFrom`, `.IconTableCache`, `.IconTableTemplates`, `.IconTableSession`, `.IconTableUsers`, `.IconTableMemory` | Debug/data table menu icons (`table_*.svg`, except `phone`, `user`, `memory`). |
| `.IconOk`, `.IconCancel`, `.IconClosePopUp` | `fav_on`, `close`, `close` — `Confirm` buttons and the `PopUp` close button. |
| `.IconDb`, `.IconDbRemove`, `.IconDbAdd`, `.IconDbMinus` | `db*.svg`. |
| `.IconCashBill`, `.IconCashBills`, `.IconCoin`, `.IconRefund` | `cash_bill`, `cash_bills` (also `IconCoin`), `refund`. |
| `.IconImage`, `.IconChat`, `.IconNotes`, `.IconDisc`, `.IconSync`, `.IconUpload`, `.IconDownload` | `image`, `chat`, `notes`, `disc`, `sync`, `upload`, `download`. |

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--h` | `body`: `calc(var(--ThemeIconSize) * 44px)` | Header button size, title height. |
| `--imh` | `body`: `5px` | Inner header margin (button top). |
| `--mh` | `body`: `--h + 2*--imh` | Header height; top of `app-menu` / `app-content`. |
| `--mw`, `--me` | `app-header`: `4px`, `8px` | Outer margin and spacing between header buttons. |
| `--s0`, `--s1`, `--s2` | `app-header`: computed from `--mw`, `--h`, `--me` | Horizontal slot positions of header buttons/title. |
| `--maxWidth` | `SysWeaver-PopUpBackground`: `calc(100vw - 16px)` | Upper bound for dialog widths. |

#### Animations (@keyframes) and media queries / breakpoints / theme variants

- `@keyframes SysWeaver-PopupMenuBack-Scroll`: used by `SysWeaver-PopupMenuBack` with `animation-timeline: scroll(y self)`; squares the right corners while the panel is scrollable.
- No media queries or `[data-theme]` rules; responsive behaviour comes from the `SwNarrow`, `SwMobile`, `SwPinned`, `SwCollapsed` body classes.

### `home.css`

Styles `Home.html` (linked statically). Uses theme variables from `common/theme.css`; not templated.

| Selector(s) | Description |
|---|---|
| `body` | Centered text, theme font/colors, full size. |
| `img` | Centered block image (logo) at most `min(60vw,60vh)` (`--size`). |
| `img.UserImage` | Small (`min(10vw,10vh)`) rounded user picture with accent outline. |
| `img.CanUpload`, `.CanUpload.Dragging`, `.CanUpload.Selecting`, `.CanUpload.Uploading` | Upload target (`CanUpload` added by `homeMain`): pointer cursor; dashed outline while dragging; half opacity while selecting/uploading (`Dragging`, `Selecting`, `Uploading` set by `fileUploader.js`). |

| Variable | Default / source | Used for |
|---|---|---|
| `--size` | `img`: `min(60vw,60vh)`; `img.UserImage`: `min(10vw,10vh)` | Max image size and user image corner radius. |

### `welcome.css`

Styles `Welcome.html` (linked statically). Uses theme variables; not templated.

| Selector(s) | Description |
|---|---|
| `body`, `div` | Centered, non-selectable page; the `div` is absolutely centered (40% from top). |
| `img` | Logo sized to `min(70vw,70vh)` (`--size`). |
| `p` | Inline-block rounded label with transparent outline. |
| `img.Click`, `img.Click:hover` | Clickable logo (`Click` added by `welcomeMain`) with an accent drop-shadow glow on hover. |
| `p.KeyClick`, `:focus`, `:hover` | "Click to sign in" text (`KeyClick` added by `keyboardClick`): accent outline on focus, accent background on hover. |

| Variable | Default / source | Used for |
|---|---|---|
| `--size` | `img`: `min(70vw,70vh)` | Logo width / max size. |

### `tags.css`

Styles the tag gallery created by `tags.js` (consumer must link it). Uses theme variables and `--SysWeaverEditDesktopRad` (from the edit module CSS).

| Selector(s) | Description |
|---|---|
| `SysWeaver-TagsGallery`, `SysWeaver-TagsGallery.Embedded` | Wrapping flex box with border and background; `Embedded` removes padding, border, background and margin. |
| `SysWeaver-TagsGallery.Keep`, `.Keep SysWeaver-TagEdit`, `SysWeaver-TagsGallery.Ignore`, `.Ignore SysWeaver-TagEdit` | Color variants selected via `TagGalleryParams.Class`: min height 66 px and green (`#4c4`) or blue (`#48c`) tags. |
| `SysWeaver-TagEdit`, `SysWeaver-TagEdit:focus` | Pill-shaped accent tag; focus outline (focusable items in the add pop-up). |
| `SysWeaver-TagEdit SysWeaver-TagText`, `SysWeaver-TagEdit icon-24x24` | Bold tag name floated left; 24x24 remove icon (`ColorIcon` element) floated right. |
| `SysWeaver-TagsGallery SysWeaver-Button` | Vertically centers the add `Button` (`SysWeaver-Button` element from `common.js`). |
| `SysWeaver-PopUpBackground SysWeaver-TagEdit`, `:hover` | Selectable tags in the add pop-up: pointer, 0.8 → 1 opacity on hover. |
| `.IconTagRemove`, `.IconTagAdd` | Mask icons `../icons/minus.svg` and `../icons/plus.svg` (defaults of `TagGalleryParams.RemoveIcon` / `AddIcon`). |
