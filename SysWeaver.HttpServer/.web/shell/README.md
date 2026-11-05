# shell

[⬆ srcWeb](../README.md)

> The SysWeaver application shell (namespace `Sw.Shell`): the root page `index.html` with header, main menu and the content frame that hosts every other page, plus the legacy compatibility layer of the old `app/application.js`.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../../README.md) |
| **Kind** | Embedded web assets (JavaScript / CSS source modules) |

## Purpose

These files are the sources of two bundles (built by `SwWeb bundle` from [`../sw/bundles.json`](../sw/bundles.json)):

| Bundle | Files | Used by |
|---|---|---|
| `sw/shell.js`, `sw/shell.css` | `menu.js`, `content.js`, `shell.js`; `shell.css` | the root page [`../index.html`](../index.html) (new format) |
| `app/application.js`, `app/application.css` (LEGACY) | the same + `compat_application.js`; `shell.css` + `compat_application.css` | old format pages, other projects and srcData templates that load `../app/application.js` after `../common/common.js` |

The shell needs the core (`sw/core.js`) and the ui library (`sw/ui.js`). Nothing runs at load time except defining `Sw.Shell` (and the global `appMain`), so the legacy bundle is safe to load in any page.

```html
<!-- index.html (template, copied to debug.html by the build) -->
<script defer src="sw/core.js"></script>
<script defer src="sw/ui.js"></script>
<script defer src="sw/shell.js"></script>
<script defer src="app/app.js"></script>          <!-- served empty unless an application supplies it -->
<link rel="stylesheet" href="common/theme.css">
<link rel="stylesheet" href="sw/ui.css">
<link rel="stylesheet" href="sw/shell.css">
<link rel="stylesheet" href="app/app.css">       <!-- served empty unless an application supplies it -->
<body onload="appMain()">
```

## Contents

| File | Contents |
|---|---|
| [`menu.js`](menu.js) | `Sw.Shell.MenuTypes`, `MenuFlags`, `GetMenu`, `ItemUrl`, `NormalizeUrl`, `HasAction`, `RunItem`, `ThemeOfItem`, `ThemeIcons`, `ToMenuItems`, `ShowMenu`, `MenuTree`, `CollapsedPrefix` |
| [`content.js`](content.js) | `Sw.Shell.ContentHost` (the content frame and its reveal protocol), `EncodeHash`, `DecodeHash` |
| [`shell.js`](shell.js) | `Sw.Shell.Params`, `Sw.Shell.App` (incl. `GetQuickLinks`, `RecordRecent`, `StartEntry`, `CycleFocus`, `FocusContent`, `ShowKeys`, `MarkUser`), `Sw.Shell.Main` (= the global `appMain`), `Sw.Shell.Current`, `PinnedKey`, `RecentKey`, `HasKeyboard()` |
| [`shell.css`](shell.css) | skip link, header, main menu (filter box, tree, rail, drawer, logo below the items), backdrop, content frame, loading bar |
| [`compat_application.js`](compat_application.js) | every global of the old `application.js` (see below) |
| [`compat_application.css`](compat_application.css) | the old pop-up elements (`SysWeaver-Blocker`, `SysWeaver-PopupMenu(Back)`, `SysWeaver-PopUpBackground` content ...) |

## Behavior

### Start (`appMain(params)`)

1. **Nested shell guard:** inside a frame of a same-origin SysWeaver shell (ex: a sign in page in the content frame navigated to the site root) the top window is reloaded instead (`top.ReloadAll(true, true)`: hash cleared).
2. The loading overlay (`Sw.Page.Loading`) shows until the menus are loaded and the optional hook `window.AppMain()` (typically defined by `app/app.js`) finished.
3. `Sw.Shell.App.Create(params)` builds the shell. `params` is a `Sw.Shell.Params` (the old `AppParams`), or `window.SysWeaverAppParams` when `appMain` is called without one.
4. The initial page opens: the deep link in the hash, else the first main menu item (the server registers `app/Welcome.html` for anonymous users and `app/Home.html` for signed in users at the top).

Globals set in the shell window: `SysWeaverApp` (the `App`), `SysWeaverAppTitle` (the original document title), `SysWeaverDoneFirst`, `ReloadAll(flushCache, clearHash)` (when not defined by the legacy `common.js`; pages call `top.ReloadAll` to reload the shell, ex: `AuthStartPage` after signing in).

### Layout

```
<body class="sw-shell sw-root [SwPinned | SwCollapsed] [sw-shell-drawer] [sw-shell-overlay]">
  <a.sw-shell-skip>          "Skip to content" link (first tab stop, visible while focused)
  <app-header role=banner>   [menu][back]   <app-ContentTitle>   [user][settings]
  <app-menu role=navigation> the menu tools (div.sw-shell-mtools: pin, collapse all, expand all), the menu filter box (div.sw-shell-filter), the main menu tree (Sw.Shell.MenuTree), the brand logo below the items (div.sw-shell-menulogo: --sw-brand-logo unaltered, 160 px wide with brand clear space; hidden in the rail and on low screens)
  <app-content role=main>    the content iframe(s)
  <app-capture>              backdrop while the menu is shown over the content
```

The custom element names are kept from the old shell, so application style sheets (`app/app.css`) keep working.

| State | Wide screens (> 600 px) | Narrow screens (<= 600 px) |
|---|---|---|
| `SwPinned` | menu always shown, content offset by the menu width | (never: the pinned choice is kept for wide screens) |
| `SwCollapsed` | a rail of icons (click an icon to open its page), content offset by the rail | menu hidden |
| neither (temporary) | menu over the content with a faint backdrop; moving the mouse away hides it | a drawer with a dark backdrop |

* **Menu button:** unpins a pinned menu, else shows / hides the temporary menu (`aria-expanded`).
* **Menu tools** (`div.sw-shell-mtools`, a row of icon buttons above the menu filter box; `role=group` "Menu tools"; each button has a short accessible name, a tool tip / long press hint, Tab, Enter / Space, 36 px, the 44 px touch size on touch screens): **Pin** at the left, **Collapse all** and **Expand all** at the right (every folder, `MenuTree.SetAllExpanded`, stored like a user toggle, announced to screen readers). Per mode: the expanded menu (pinned or shown over the page) shows all three; the icon rail only the pin (centered above the search button, a click pins the menu open); the phone drawer and the landscape phone menu only the folder tools (phones have no pin); a menu without folders has no folder tools; nothing to show: no row. (The pin was in the header before.)
* **Pin button** (the first menu tool; desktops; a pin icon, filled while pinned, tool tip "Pin the menu: keep it open next to the page"; hidden by `body.sw-shell-nopin` when the menu has at most one entry): a toggle (`aria-pressed`, name "Keep the menu open"), stored in `localStorage["SysWeaver.Pinned"]` (JSON bool). Without a stored choice the menu starts pinned (expanded with labels) when `Params.MainMenuPinned` is set or the window is at least `Params.AutoPinWidth` (1400) css px wide; a stored choice always wins. Phones always start unpinned and have no pin button.
* **Mouse:** moving to the left edge (8 px) of the rail shows the menu (not again within 250 ms after it hid); moving over the backdrop hides it.
* **Touch:** tap the backdrop or swipe the menu to the left to close it.
* **Keyboard:** the header buttons, then the menu (one tab stop), then the page. In the menu: Up / Down, Home / End, Right (expand / first child), Left (collapse / parent), Enter / Space (activate; a folder without an action toggles), `*` (expand all siblings), letters (type ahead). Escape in the menu (tree or empty filter box) clears an active filter (focus back in the box), else hides a temporary menu and moves the focus into the page; Escape elsewhere closes a temporary menu and returns to the menu button. A page chosen from a temporary menu (or with Enter in the filter box) gets the focus once shown.
* **F6 / Shift+F6:** cycle the focus between the header (first button), the menu (a hidden drawer opens) and the page (a temporary menu hides); also from inside same origin pages.
* **`?` keyboard help:** outside text inputs, a dialog lists the shell short cuts (Alt+M, F6, skip link, tree keys, filter keys, Escape). A page's own `?` (ex: the table help, `Sw.Keys` prevents the default) wins; the tree's type ahead ignores `?`.
* **Accessible names:** header buttons have short names ("Show the menu" / "Hide the menu" / "Collapse the menu", "Back", "User menu, <name>", "Settings"), the menu tools too ("Keep the menu open" with `aria-pressed`, "Collapse all", "Expand all"); tool tips may say more. The content frame's `title` is the shown page's title.
* **Skip link:** the first tab stop, "Skip to content" (shown while focused) moves the focus into the page.
* **Menu filter** (`Params.MenuFilter`, default on): a search box above the tree. Typing filters the items by name (parents of matches and the children of matching folders stay; folders open while filtering, the stored state is kept; a "No matching menu items" note shows, the count is announced). ArrowDown moves to the first matching page in the tree (not a folder; ArrowUp on the first item returns), Enter opens the first matching page, Escape clears the text; while filtering folders are never collapsed (Enter / click / Left only expand or move to the parent), so the matches stay visible (an empty box lets Escape close a temporary menu). The rail shows only a search button that expands the menu and focuses the box. The filter is cleared when the menu collapses to the rail / drawer. Type ahead in the tree is unchanged.
* **Hot key Alt+M** (documented in the box's place holder and tool tip only on devices with a keyboard, `Sw.Shell.HasKeyboard()`: not the mobile layout and not touch only; always in `aria-keyshortcuts`): focuses the menu filter (showing a collapsed menu first), also while the focus is inside a same origin page in the content frame (a capture key listener is added to each revealed page window).
* **Rail:** every item has its name as `aria-label`; on phones / tablets (`SwMobile`, ex: a phone in landscape) the rail is a bit wider and shows each name as a short caption below its icon (up to two lines, wrapped only between words: the row text is `span.sw-shell-word` spans; a too long word ends with an ellipsis, the tool tip has the full name); in the rail the row tool tip starts with the name, a long press (touch) shows it as a hint, and keyboard focus shows it in a hint next to the icon.
* **Collapsed folders by default:** the top level folders named in `Params.CollapsedMenus` (default `["Debug", "Data"]`: Development and Data, see `_docs/Web.md` "Main menu structure") start collapsed until the user expands them (stored as `"0"`).
* **Back button** (phones, or `Params.Navigation: true`): `history.back()`, shown only when the previous history entry is this shell document (Navigation API; fallback: a count of the shell's own entries), so it never leaves the application (hidden on the first page). It never goes back across a sign in / sign out: `App.MarkUser` keeps the user of the tab in `sessionStorage["SysWeaver.ShellUser"]`; when a reloaded shell has another user, the current history entry key becomes the boundary (`sessionStorage["SysWeaver.ShellBackFrom"]`).
* **Current item in view:** after a page is shown and on every menu mode change, `App.ShowSelected()` scrolls the menu (tree, rail, drawer) so the current page's item is fully visible when it is not, aligned to the top of a row (the first visible item is never cut); re-checked 400 ms later (late icons / fonts); a visible item never moves the menu.
* **Temporary menu:** any page change (menu, link inside a page, back / forward, hash change) closes a shown temporary menu in every mode (portrait drawer, landscape phone / desktop menu over the rail), so it never covers the next page.
* **Selection:** the selected menu item is the best match of the shown url (`MenuTree.FindBest`: the exact url, else an item of the same page whose query parameters all match), or none; a focus left on the previous page's item moves to the new one (or is dropped). Only exact menu pages count as recent pages.
* **Recent pages / quick links:** every shown main menu page (not the start page, not a re-show of the same page) is counted in `localStorage["SysWeaver.RecentPages"]`; `App.GetQuickLinks({MaxRecent, MaxLast, MaxTop, MinCount})` (MinCount: Recent only lists pages opened at least that often, default 1) returns `{Recent, Last, Top}` links `{Name, Title, Icon, Url, Open(ev)}` (most used pages still in the menu, the same pages by last visit, the top level pages) for `app/Home.html`.
* **Title:** while a page loads, the menu item name of its url (hidden for urls without an item); when shown, the menu item name of the shown page, else the page's `document.title`, else its path, shown as written (never upper cased: titles can be case sensitive paths); a click copies it (the copy toast quotes the full title). The title text is the shell document's `h1` (`h1.sw-shell-title`, styled as header text). On narrow screens (`SwNarrow`) a long title wraps to two lines (then an ellipsis; the tool tip and a tap show the full title). The document title becomes `"<app title> - <page title>"`.
* **User button:** the user image (`auth/UserImages/Current/small`, refreshed on the InterOp message `UserImageChanged`; a server generated picture is shown as a brand monogram, `Sw.UserPicture`) when signed in ("Logged in as ..." tool tip), else a user icon; click, Enter / Space, long press or right click open the server `User` menu (a single root folder is unwrapped).
* **Icons (Design v2 3.12, F49-Left):** the shell chrome (top bar buttons: menu, back, theme, settings; menu tools; the settings menu items full screen and open in new tab; the plus / minus of folders without an icon) uses the line set (`Sw.Icon`); server menu icons are masks in `--sw-text-3` (hover `--sw-text-2`, the accent on the current item) in the sidebar, the rail and the shell's popup menus: a full colour icon url (`...svg*`) is shown as the mask of the same image (`MonoIcon`) and the colour logo class `IconLogo` (ex: the Application theme item) as a mask too; the colourful variants are for content only (Home tiles, tables). App css that styled the old header icon classes (`.IconSettings`, `.IconNavBack` inside `app-SettingsButton` / `app-BackButton`) must target `.sw-shell-hicon`.
* **Settings button:** full screen toggle, the server `Theme` menu as a section (the stored theme is checked; picking a theme closes the menu; a theme item whose icon an earlier theme item already uses gets a distinct one from `Sw.Shell.ThemeIcons`, ex: Pro; the app themes registered with a `Title` (`Sw.Theme.Custom()`, ex: from `app/app.js`) are added to that section, see `_docs/WebBranding.md`), the `User` menu too when there is no user button, "Open in new tab" (the shown page, with the forced theme) and the items added by `Params.OnSettingsMenuOpenFn(menu, close)`.

### Menus (`menu.js`)

The server menu model (`Api/application/GetMenu` with comma separated names `Default`, `User`, `Theme`) is `WebMenu {RootUri, Items}` of `WebMenuItem {Id, Name, Type, Title, IconClass, Data, Children, Flags}`.

| Type | Action |
|---|---|
| 0 Path | folder (expand / collapse) |
| 1 Table | shows `explore/table.html?q=../<Data>` |
| 2 Embedded | shows `<Data>` |
| 3 Link | the whole window navigates to `<Data>` |
| 4 LinkExternal | `<Data>` opens in a new tab |
| 5 Js | `window[Data](item, style, isUserClick)` when `Data` names a global function, else `Data` is evaluated as a statement in the global scope (the server's theme items: `if(setTheme('dark'));false`); a truthy result hides the menu |
| 6 Chart | shows `chart/chart.html?q=../<Data>` |
| (function `Data`) | client built menus: `await Data(item, style, isUserClick)`, a truthy result hides the menu |

Urls are relative to the site root. `Flags`: 1 disabled, 2 checked. `Id "-"`: a separator (with an optional label). `IconClass`: an icon class (`IconTable`) or an icon url (`icons/x.svg`, `../icons/x.svg`, a trailing `*` = full color). A folder without an icon or action shows plus / minus. Collapsed folders are stored as `localStorage["SysWeaver.MenuCollapsed.<menu>.<Id or Name>"] = "1"` (menus `Main`, `AppSettings`, `NoName` ...). Embedding items ignore a click on the already selected item; modifier clicks are ignored.

* `new Sw.Shell.MenuTree({Menu, Name, Context: {Navigate(url, item, ev), Hide(), Style}, Label, Class, OnActivate, DefaultCollapsed(item, level), OnTopUp()})`: `.E` (role tree), `.Entries`, `.UrlMap`, `Activate(entry, ev)` (a user activation of the already selected page item only calls `Context.Hide()`: a temporary menu closes), `Select(entry)`, `SelectUrl(url)`, `FindUrl(url)`, `FindBest(url)`, `Toggle(entry)`, `SetExpanded(entry, bool)`, `Focus()`, `Filter(text)` (-> `{Count, First, FirstLeaf}`; `Toggle` never collapses while filtering), `SetRail(bool)`, `Visible()`. `Style.SelectFn(item)` is called on selection changes.
* `Sw.Shell.RunItem(item, ctx, ev)` runs one action; `Sw.Shell.ShowMenu(anchor, items, ctx, {Sections, Align, Side, ...})` shows server items as a `Sw.Menu` (folders become submenus, or sections with `Sections`).

### Content frame (`content.js`)

* Each navigation creates a new hidden iframe (class `Hidden`, no generic load fade); the previous one is hidden and removed after 25 ms. The forced theme is forwarded (`?useTheme=`).
* The frame is revealed when its page posts `{Type: "LoaderRemoved"}` (`Sw.Page.Loaded`, the old `PageLoaded`) - only messages from the current frame's window count - or 1 s after its load event. On reveal the frame's `<html>` gets `Embedded`, the menu item of the url is selected (also for pages that redirect before they are shown) and the title is set.
* A shown page that navigates by itself (links, `location`, server redirects) is hidden instantly on `pagehide` and the next document is revealed like a new page (no unthemed flash).
* A page taking longer than 300 ms shows a thin loading bar and, fading in shortly after, a centered spinner with "Loading..." (`div.sw-shell-pageloading`, a polite status) until it is revealed.
* History: every navigation pushes `history.state = url`, hash `#` + Base64(url) (the deep link format); the first page replaces the entry of a deep link (so back to it works) and adds nothing otherwise. Back / forward re-show the url without a new entry; the state-less first entry re-opens the first menu item. Navigating clears the page state stored for that url (`Sw.History.ClearOther`).
* Pages navigate the shell with `Sw.Page.Navigate(url, "_self")` (the old `Open`), which posts `{Type: "IframeNavigating", Url, Id}` to the top window and answers it with `{Type: "IframeNavigationHandled", Id, Url, Handled}` (posted back to the content frame when the message has an `Id`; `Handled` true: the shell shows the url, the page cancels its own fallback navigation; false: the url was refused, the page navigates itself at once; a page that gets no answer navigates itself after 100 ms, 1 s inside the shell, B-C4); the shell only accepts it from the window of its content frame (`event.source === Host.Frame.contentWindow`) and from this origin, so a frame nested in the page, a popup or another tab of this site can't drive the shell by posting messages. This is not an isolation boundary: same-origin script can always reach the content frame (ex: `parent.Sw.Frame.PostTop(...)` from a nested frame posts with the content window as the source). The real guard is `SafeContentUrl`: every accepted url is still limited to http(s) urls of this site or an exact external Embedded menu item.
* Security: deep links (the hash), history states and `IframeNavigating` urls are untrusted. `App.SafeContentUrl(url)` accepts only page urls of this site (`Sw.Url.SafeSameOrigin` and `Sw.Url.IsPageUrl`, or the url of a main menu item: never a raw server method such as `Api/...` or `ServerManager/KillProcess`, which may perform an action on GET) and the exact url of an external `Embedded` item of the main menu; anything else (other sites, `javascript:`, `data:`, `blob:`, ...) is refused: a deep link opens the first menu item, and a toast says "This link can't be opened here". `ContentHost.Navigate` also refuses every non http(s) url (returns false). Defense in depth: a content frame that showed a page of this site and then navigates itself to another origin (a plain link or a script in the page) is never revealed; the shell shows the last page again (`OnForeign`) and tells the user. `LoaderRemoved` from a page of this site only counts from this origin. Every refusal is also announced to screen readers (`Sw.Page.Announce`). `Sw.Page.Navigate` itself opens other sites in a new tab (see core/README.md). Titles: a page title set from the url (`Sw.Page.SetLinkTitle`, ex: explore/table.html `n`, explore/api.html / edit/type.html `q`) that is not the name of the page's menu item is shown cut to 80 characters after a boxed "Text from the link:" caption (`span.sw-shell-linkcap`, `app-ContentTitle.FromLink` in normal weight; also in the tool tip, the frame name and the document title; a click copies only the title), so a crafted link can't put a message into the app header (`App.IsLinkTitle`).

### Legacy globals (`compat_application.js`)

| Old name | Implementation |
|---|---|
| `WebMenu`, `WebMenuItem` (+ `From`), `MainMenuStyle` | the same data classes |
| `MainMenu(menu, style, toElement)` | a `Sw.Shell.MenuTree`; `Element`, `Data`, `Style`, `Selected`, `RowMap`, `Select`, `EmbeddContent` (the shell's frame when `style.Content` is the shell's `<app-content>`, else a `ContentHost` on that element, else the page navigates through the shell), `LoaderRemoved`, `SetUrl`, `ReplaceUrl`, `Add`, static `GetEmbeddedUrl`, `SetIcon`, `SetExpIcon`; items get `OnClick` |
| `PopUpMenu(element, build(close, backEl), rightAlign, bottomAlign)` | a `Sw.Menu` (submenus, check items, keyboard, touch drill down); `backEl` is a detached `<SysWeaver-PopupMenuBack>` |
| `PopUp(build(el, close, tools), block, dontAllowClose, onClose, haveCloseButton)` | a `Sw.Dialog`; `el` is a `<SysWeaver-PopUpBackground>` with `Close`, `Block`, `MakeFullWidth()`, `MakeMaxHeight()` |
| `PopUpWorking`, `PopUpSelection`, `SearchablePoupSelection`, `Confirm`, `PopUpEdit` | `Sw.Dialog.Working`, `Sw.Dialog.Select`, `Sw.SearchSelection` (same storage key), `Sw.Dialog.Confirm` (default icons `IconOk` / `IconCancel`), a `Sw.Dialog` with the edit/ library's `Edit` |
| `CreateTabBlock`, `CreatePopUpBlocker`, `CreatePopUpAtElement`, `PopUpElement`, `PopUpElementMenu` | the old element based pop-ups (focus trap with `Sw.Focus`, kept on screen with `Sw.Place`) for code that builds into them (edit/Edit.js) |
| `AppParams`, `App`, `appMain` | `Sw.Shell.Params`, `Sw.Shell.App`, `Sw.Shell.Main` |

New format pages use `Sw.Menu`, `Sw.Dialog` and `Sw.Shell` instead (`SwWeb check --strict` reports legacy names).

## Relationships

- **Part of:** [SysWeaver.HttpServer](../../README.md) `srcWeb`.
- **Uses:** [`../core`](../core/README.md) (`Sw.Net`, `Sw.Url`, `Sw.Page`, `Sw.Layout`, `Sw.InterOp`, `Sw.Theme`, `Sw.Storage`, `Sw.History`), [`../ui`](../ui/README.md) (`Sw.Menu`, `Sw.Dialog`, `Sw.Icon`, `Sw.Focus`, `Sw.Hint`, `Sw.FullScreen`, `Sw.Clipboard`), the icon classes of `ui/icons.css` and the icons of the IconModule.
- **Server contract:** `Api/application/GetMenu`, `Api/auth/GetUser`, `auth/UserImages/Current/small`, the templated `index.html` (`${#EnvInfo.AppDisplayName}`), `app/app.js` / `app/app.css` (served empty unless supplied), the frame messages `IframeNavigating`, `LoaderRemoved`, the InterOp message `UserImageChanged`.

## Storage keys

`localStorage["SysWeaver.Pinned"]`, `localStorage["SysWeaver.RecentPages"]` (JSON `[{U: url, C: count, T: ms}]`, the last 20 pages), `localStorage["SysWeaver.MenuCollapsed.<menu>.<id>"]` (`"1"` collapsed, `"0"` expanded for folders collapsed by default), `localStorage["SysWeaver.PopUpRecent.<id>"]` (searchable selections); the theme keys belong to the core.
