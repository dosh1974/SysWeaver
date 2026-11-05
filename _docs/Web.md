# The SysWeaver web client

[⬆ _docs](README.md)

> An overview of the JavaScript, HTML and CSS shipped with SysWeaver: the `Sw` library (UI-less core, UI components, app shell), how it is bundled and served, the page format, how pages talk to the server and to each other, the compatibility layer that keeps old pages working, and the rules the server imposes on web files.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Architecture overview (front end rewrite, 2026-10) |

New here? Start with **[Your first page in 5 minutes](WebQuickStart.md)**, then browse the **component gallery** (`/ui/gallery.html` on any SysWeaver server, source [`ui/gallery.html`](../SysWeaver.HttpServer/srcWeb/ui/gallery.html)).

The detailed references are the folder READMEs:

| Read | For |
|---|---|
| [`core/README.md`](../SysWeaver.HttpServer/srcWeb/core/README.md) | The UI-less core `Sw.*` API (requests, session messages, InterOp, theme, layout, page protocol, formatting) and its wire contract |
| [`ui/README.md`](../SysWeaver.HttpServer/srcWeb/ui/README.md) | The UI library: every component, helper, token and icon class |
| [`shell/README.md`](../SysWeaver.HttpServer/srcWeb/shell/README.md) | The app shell (`index.html`): menus, content frame, history, keyboard |
| [`srcWeb/README.md`](../SysWeaver.HttpServer/srcWeb/README.md) | The folder layout, `index.html`, the bundles and the migrated page format |
| [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) | The build / lint / migration tool (`bundle`, `check`, `migrate`) |
| [`UX_Guide.md`](UX_Guide.md) | The UX rules every page follows (`UX-<area>-<n>`) and what the framework does for each ("Framework support") |
| Each `srcWeb/<folder>/README.md` | Per feature folder: behavior, endpoints, url parameters, storage keys, JavaScript and CSS reference |

## Contents

- [Architecture at a glance](#architecture-at-a-glance)
- [Bundles and build rules](#bundles-and-build-rules)
- [Page format](#page-format)
- [Page life cycle](#page-life-cycle)
- [The application shell](#the-application-shell)
- [Server contract highlights](#server-contract-highlights)
- [Deploying and operating](#deploying-and-operating)
- [Working offline](#working-offline)
- [Push messages and the session manager](#push-messages-and-the-session-manager)
- [Window, frame and tab messaging](#window-frame-and-tab-messaging)
- [Theming and layout](#theming-and-layout)
- [Feature libraries](#feature-libraries)
- [Translation](#translation)
- [Legacy pages and the compatibility layer](#legacy-pages-and-the-compatibility-layer)
- [Client state and storage keys](#client-state-and-storage-keys)
- [Contracts mirrored between C# and JS](#contracts-mirrored-between-c-and-js)

## Architecture at a glance

One namespace, `Sw`, in three layers. Each layer is a set of small source modules in `SysWeaver.HttpServer/srcWeb/` and a generated bundle in `sw/`:

| Layer | Sources | Bundle | Contents |
|---|---|---|---|
| **core** (UI-less) | [`core/`](../SysWeaver.HttpServer/srcWeb/core/README.md) | `sw/core.js` | `Sw.Net` (api calls, errors, redirects, includes), `Sw.Session` (server push messages, server time), `Sw.InterOp` / `Sw.Frame` (cross window messages), `Sw.Theme`, `Sw.Layout`, `Sw.Page` (loading protocol, navigation), `Sw.Url`, `Sw.Storage` / `Sw.History`, `Sw.Format`, `Sw.Util`, `Sw.Platform`, `Sw.Events`, the translation fallbacks |
| **ui** | [`ui/`](../SysWeaver.HttpServer/srcWeb/ui/README.md) | `sw/ui.js`, `sw/ui.css` | Components (`Sw.Button`, `Sw.TextBox`, `Sw.Select`, `Sw.Dialog`, `Sw.Menu`, `Sw.Tabs`, `Sw.Split`, `Sw.PropertyEditor` ...), toasts, loaders, the server block overlay, value cells (`Sw.Cells`), markdown (`Sw.Markdown`), icons (`Sw.Icon`, `Sw.IconClass`), design tokens (`--sw-*`). `ui/code.js` (`Sw.CodeEditor`) is loaded on demand |
| **shell** | [`shell/`](../SysWeaver.HttpServer/srcWeb/shell/README.md) | `sw/shell.js`, `sw/shell.css` | `Sw.Shell`: the root page with header, main menu, content frame, history, the global `appMain` |

- A page (or another project) can use the **core alone** and still get server push messages, InterOp, themes, layout classes and requests. The UI adds the visible parts (loader, error toasts, server block) through core events and hooks.
- A page that renders **its own UI** and only needs the messaging (server events, session keep-alive, InterOp, server time, layout classes, the legacy `SessionManager` / `SysWeaverInit` names) loads the small bundle `sw/session.js` (no theme, no components); `sw/theme.js` adds the theme, `sw/net.js` the requests. The bundles share their source files and each source runs once per page. See [WebMessaging.md](WebMessaging.md).
- The core is **wire compatible** with the old `common.js`: old and new pages in the same browser share one session master, exchange InterOp messages and use the same storage keys.
- **Vanilla JavaScript**, classic scripts (no ES modules, no frameworks, no external resources), each file an IIFE with `"use strict"`, JSDoc on every file, class and member. Touch, mouse and keyboard everywhere; desktop, phone portrait and phone landscape; every theme.
- **Conventions**: classes take one options object with PascalCase keys (`new Sw.Button({ Text, Icon, OnClick })`), the root element is `.E`, values use `Get()` / `Set(value, silent)`, callbacks `OnInput` / `OnChange`. Public members are PascalCase; see "API conventions" in the [core](../SysWeaver.HttpServer/srcWeb/core/README.md#api-conventions) and [ui](../SysWeaver.HttpServer/srcWeb/ui/README.md#api-conventions) READMEs.

**Dependency layers** (an arrow means "loads or calls"):

```
sw/core.js  + common/theme.css         (no dependencies)
  ▲
sw/ui.js    + sw/ui.css                (components, toasts, overlays, cells)
  ▲
sw/shell.js + sw/shell.css             (index.html only)
  ▲
feature libraries: explore/table.js (Sw.Table), edit/Edit.js (Sw.Edit), chart, chat, mediaPlayer, auth, common/fileUploader.js
  ▲
pages
```

## Bundles and build rules

- **Bundles are generated and committed.** `SwWeb bundle <srcWeb>` concatenates the sources listed in [`sw/bundles.json`](../SysWeaver.HttpServer/srcWeb/sw/bundles.json) into `sw/core.js`, `sw/ui.js`, `sw/ui.css`, `sw/shell.js`, `sw/shell.css`, the migration helpers `sw/compat.js` / `sw/compat.css` and the legacy bundles `common/common.js`, `app/application.js`, `app/application.css`. Each output starts with a "GENERATED by SwWeb bundle ... do not edit" header. Edit the sources, then run the tool again (`bundle --check` verifies that the bundles are current).
- **Packaging.** At build time an `Exec` step of each project runs `Compress.exe`, which minifies and Brotli-compresses `srcWeb/` into `web/`, and `web\**\*.br` is embedded in the assembly. Files are served at their path relative to `srcWeb/`, from the server root (`srcWeb/sw/core.js` is `/sw/core.js`, `srcWeb/app/Home.html` is `/app/Home.html`).
- **Which sub-folders are embedded is decided by that `Exec` line**, per project:
  - `Compress.exe -Methods br .\srcWeb\*+ .\web`: the trailing `+` makes it **recursive**, every folder under `srcWeb/` is embedded. This is the line of the [QuickStart](WebQuickStart.md#1-embed-a-web-folder-in-your-project) and of SysWeaver.HttpServer, HttpServer.ExploreModule, Auth.Core, MicroService.Media, FileUploader, UserManager, MicroServices.Log, and FakeEmailService / FakeTextService.
  - `Compress.exe -Methods br .\srcWeb\chat\* .\web\chat` (no `+`): only the files of that one folder. Chat, MicroService.ChartJs, Edit, MicroService.Auth, ServerManager, TranslationDbCache and Email.Fake work this way (their vendored `external/` folders have an extra line). A new sub-folder there is served while you develop from disk, but **is missing (404) in the built app** until the project gets a recursive line or one more `Exec` line.
- **Served folders are merged across projects.** The URL namespace is shared: `auth/` gets files from SysWeaver.Auth.Core, MicroService.Auth, MicroService.PassKey and MicroService.UserManager; `common/` from SysWeaver.HttpServer and MicroService.FileUploader. File names in a shared folder must not collide (the assembly with the highest `[assembly: ResourceOrder]` wins). Files reference each other by relative names; a missing optional project shows up as a 404 and pages feature-test for its globals.
- **Where pages live**: in `srcWeb/<folder>/` of their project, **one folder below the site root**, so `../sw/`, `../common/` and `../Api/` resolve (a page in a deeper folder works too, but must use `../../`). Pick a folder name of your own (`hello/`), or a shared one (`auth/`) only with unique file names.
- **Build and serving rules** (`SwWeb check` reports each of them, except url case):
  - **one dot per file name** (`x.min.js` is the only exception): the embedded resource name joins folders with dots, so `my.page.js` is served as `my/page.js`; folder names have no dots;
  - **no `-` in folder names** (MSBuild embeds `my-pages/` as `my_pages/`, so the urls change) and no folder names starting with a digit (embedded with a `_` prefix); `-` in file names is fine;
  - only compressible types are embedded: `.js`, `.css`, `.html`, `.svg`, `.json`, `.txt` ... plus `.jpg` / `.ico` (and `.png` in SysWeaver.HttpServer only); `.mjs`, `.md`, fonts, `.gif`, `.webp` and `.wasm` are **not**; urls are case sensitive;
  - **classic scripts only, nothing from the internet**: no `<script type="module">`, `import` / `export` or `import()`, and no `http(s)://` or `//` urls in `<script src>`, `<link href>`, `<img src>`, CSS `@import` / `url()` (rules `es-module`, `external-resource`; a root relative `/x.js` is a `root-relative-url` warning because it breaks below a reverse proxy path);
  - `//DEBUG_BEGIN` ... `//DEBUG_END` lines are removed by the build (files served from disk keep them).
- **Templates.** A few files are served with `${Variable}` substitution (see [`help/Templates.txt`](../SysWeaver.HttpServer/srcWeb/help/Templates.txt)): `index.html` / `debug.html`, `app/manifest.json`, `app/Home.html` and `common/theme.css` (the colors of the `application` theme). Only known template variables may use `${` there, and no JavaScript template literal anywhere may contain `${Session.`, `${Env.`, `${EnvInfo.`, `${Color.`, `${Server.` or `${Request.` (the template engine would replace it). The srcData templates of UserStorage use `${@Base Url}` prefixed includes.

## Page format

```html
<script defer src="../sw/core.js"></script>          <!-- always -->
<script defer src="../sw/ui.js"></script>            <!-- pages with UI -->
<script defer src="page.js"></script>
<link rel="stylesheet" href="../common/theme.css">   <!-- always (templated theme colors) -->
<link rel="stylesheet" href="../sw/ui.css">          <!-- pages with UI -->
<link rel="stylesheet" href="page.css">
<body class="sw-root">
```

```js
Sw.Page.Main(async () => {
    const data = await Sw.Net.Send("../Api/my/Get", 1);
    const page = Sw.PageLayout({ Type: "form", Title: _T("My page"), Description: _T("What this page is for."),
        Actions: [new Sw.Button({ Text: _T("Reload"), OnClick: () => Sw.Page.Reload() })] });   // the Design v2 page header + container
    page.Add(Sw.Card({ Title: _T("Data"), Body: Sw.el("pre", { text: JSON.stringify(data) }) }));
}, { Loading: true });
```

(`<body onload="pageMain()">` works too; never call `Sw.Page.Main` from an inline `<script>`: it runs before the deferred bundles.) Every page builds the Design v2 page anatomy (`Sw.PageLayout` and the parts in [ui/README "Page anatomy"](../SysWeaver.HttpServer/srcWeb/ui/README.md#page-anatomy-pagejs-pagecss-design-v2)) and uses the role tokens in its css. Pages live one folder below the root so `../sw/`, `../common/` and `../Api/` resolve (see [where pages live](#bundles-and-build-rules)), build their DOM in JavaScript, and use `Sw.Page.Navigate(url, target)` for navigation. Feature libraries are loaded by tag or on demand (`Sw.Net.IncludeJs`). New-format pages never use legacy globals, except the permanent ones: `_T`, `_TF`, `_TH`, `_TFH`, `setTheme`, `resetTheme`, `toggleTheme`, `Build` (explore listing) and `appMain` (index.html).

## Page life cycle

1. `sw/core.js` evaluates and `Sw.Init()` runs once: url flags (`settheme`, `usetheme`, `css`, `mobile`, `colorscheme`, `ignoreuserchanges`, `transparent`, `fillscreen`, `noloader`), theme, layout classes (`SwMobile`, `SwNarrow`, `SwPortrait` ...) and events (`SwLayout`, `SwOrientation`), the InterOp theme messages and the session manager (master election and long poll).
2. `Sw.Page.Main(fn, { Loading })` runs `fn` after DOM ready. `Sw.Page.Loading()` returns a remover; `sw/ui.js` adds the visual loader through the `Sw.Page.OnLoading` hook.
3. When loading ends, `Sw.Page.Loaded()` posts `PageLoaded` to the parent and `LoaderRemoved` to the top window (the shell reveals its content frame), posts InterOp `WindowLoaded` and restores the scroll position (`Sw.History`).
4. In-place navigation inside a frame (`Sw.Page.Navigate(url, "_self")`) posts `IframeNavigating {Url}` to the top window, so the shell records history.

## The application shell

- `index.html` (a template, also served as `debug.html`) loads `sw/core.js`, `sw/ui.js`, `sw/shell.js` and the optional application hooks `app/app.js` / `app/app.css` (served empty unless an application supplies them; `app.js` may define `window.AppMain()`; `app.css` also reaches every other page, see [WebBranding.md](WebBranding.md)), and calls `appMain()`.
- `Sw.Shell.App` builds the header (menu, back, pin, title, user, settings), the main menu and the content frame, and loads `Api/application/GetMenu` (`User`, `Theme`, `Default`) and `Api/auth/GetUser`.
- **Menus come from the server** (C# attributes on registered services): `[WebMenuEmbedded]`, `[WebMenuLink]`, `[WebMenuLinkExternal]`, `[WebMenuJs]`, `[WebMenuPath]`, `[WebMenuTable]`, `[WebMenuChart]`. `GetMenu` returns `WebMenu{RootUri, Items}` with `WebMenuItem{Id, Name, Type, Title, IconClass, Data, Children, Flags}`, filtered per session (Auth `null` = public, `""` = any signed in user, `NoUser` items only when signed out). Item types: 0 folder, 1 table (`explore/table.html?q=../<Data>`), 2 embedded, 3 link (whole window), 4 external (new tab), 5 js, 6 chart (`chart/chart.html?q=../<Data>`). Welcome (signed out) and Home (signed in) have order -100, so one of them is the start page.
- **Hosting**: all content runs in one iframe; history is `#Base64(url)`; the frame is revealed on `LoaderRemoved` (or a time out); `html.Embedded` is set on the embedded document. Phones get a drawer menu (portrait) or a slim rail (landscape); F6 cycles header / menu / page (and the toasts while one is shown); `?` shows the keys. The shell implements the framework side of the [UX guide](UX_Guide.md) (leave prompts for unsaved edits, the activity list, the offline line, the time-out warning, Back closes pop-ups: [shell/README "UX guide support"](../SysWeaver.HttpServer/srcWeb/shell/README.md)).

### Main menu structure

The `Default` menu is one tree. Item ids are paths (`"Monitoring/{0}"`), folders are `[WebMenuPath]` items and are only sent when the user may see at least one item inside. The folder ids are constants on `HttpServerBase` (`MenuMonitoring`, `MenuOperations`, `MenuAdministration`, `MenuData`, `MenuDevelopment`):

| Order | Folder (id) | What goes there | Typical role |
|---|---|---|---|
| -100 | Home / Welcome (root) | The start page | everyone |
| < 200 | root items | The pages the application exists for (ex: "Server", "Managed services", "Current rates", "Emails", "Domains") | users of the app |
| 200 | Monitoring (`Monitoring`) | Recent log entries, Log file, Problems, Performance, Performance monitors, Method calls chart, Statistics, Active sessions / users, Loaded services, Scheduled tasks | Ops |
| 300 | Operations (`Operations`) | Cache entries, Session cache entries, Cached transformers / recent files, Managed / Shared / Remote folders, Reverse proxy connection / end points / clients | Ops |
| 400 | Administration (`Admin`) | Users, API keys, Generate password hash, Audit (API calls, Clients) | Admin, Ops |
| 900 | Development (`Debug`) | Explore, APIs, Data tables, Data table references, Template variables, Icons, App theme, File upload test, Chat test, Fake email inbox, Media demos | Dev, Debug |
| 1000 | Data (`Data`), always last | Countries, Currencies, Languages, Phone number prefixes, Exchange rates, MIME mapping / types, Translation, AI models, Compression formats, Serializers | anyone allowed by the api |

- The Development folder keeps the id `Debug`, so items of other services that still use `"Debug/..."` land there; `HttpServerBase.MenuPath` (`"Debug/Http Server/{0}"`, shown as Development / Web server) and `"Debug/Data"` (Static data) are kept for them. New items should use the folders above.
- Names are plain words, consistent everywhere (the same page has the same name in the menu, in links and in the table title, ex: "Process information"), no jargon or abbreviations ("APIs", not "Api's"), and unique (two items never share a name, also across folders: "OpenAI LLM models" / "OpenAI image models").
- The shell starts the `Debug` (Development) and `Data` folders collapsed (`Params.CollapsedMenus`).

**Menu auth rule:** a menu item must never be shown to a user who cannot use the page it opens.
- `[WebMenuTable]` / `[WebMenuChart]` on an api method: leave `auth` null, the item then gets the auth of the api (`[WebApiAuth]`, including run time overrides). Only pass an auth to show the item to *fewer* users than the api allows.
- Items on a class (`[WebMenuEmbedded]`, `[WebMenuLink]`, `[WebMenuJs]`, `[WebMenuTable]` with a data url) declare their auth themselves: use the same `Roles` constant as the api(s) the page needs to work (ex: "Server" uses `Roles.AdminOps` like `GetServerInfo`; "Key files" uses `Roles.Admin` like `GetKeys`). When the api auth is configurable, use a `dynamic` method that checks the same tokens (ex: `SimpleAuthorizer.CanManageKeys`, `FakeEmailService.IsMenuAllowed`).
- Buttons inside a page that need more rights than the page (ex: delete a log file) are the page's job (hide or disable them), not the menu's.
- Automatic derivation of class item auth from the target page's apis may come later. Until then verify with a probe that signs in as every role, walks `Api/application/GetMenu` and calls the main api of each leaf (no 401 / 403 / 302 may come back).

## Server contract highlights

The full contract (what must keep working) is listed per folder. The parts every page author meets:

- **Api calls**: `[WebApi]` methods are published under `/Api/<class url>/<method>`; many services put their url next to their pages (`[WebApiUrl("../chat")]`), so pages call bare relative names. A method takes one argument: `Sw.Net.Send(url, arg)` sends small arguments as GET (`?json`, `?_d<base64url deflate>`, `?_u<base64url>`), larger ones as a JSON POST with `$type` first (`Sw.Net.ToTypedJson`). Debug builds POST by default (`//DEBUG_BEGIN` block); set the web server Params `"OptimizedSend": true` (`*.Services.json`, default false) to use the release encodings in a debug build too (the server publishes it in the templated `app/manifest.json` as `sysweaver_optimized_send`).
- **Auth**: the client never sends tokens; everything relies on the session cookie `SysWeaver.Session.<AppName>`. Unauthenticated api calls are answered with 401 and the sign in page in the `X-Login-Url` header (browser navigations and pages / files get a 302 to `auth/Login.html`; older servers send the 302 for api calls too): `Sw.Net` emits `auth.redirect` and navigates there.
- **Errors**: a non-200 response throws `Sw.Net.Error` (`Status`, `StatusText`, `Text`, `Url`); `Retry` / `ErrHandler` options retry.
- **Tables**: `TableDataRequest{Cc, Row, MaxRowCount, LookAheadCount, Order[], Filters[], Param, SearchText}` → `TableData{Cols[], Rows[], RowCount, Title, Cc, RefreshRate}`, rendered by `Sw.Table` (paging, sorting, filters, search, export, phone cards, live refresh by the `Cc` change counter). Tables may hold billions of rows, so the server never counts more than asked: `Sw.Table` sends `LookAheadCount: 1` and `RowCount` means "at least this many rows" ("1-25, more follow" while more rows follow, the exact total only on the last page). Don't ask for a large look ahead just to show a total.
- **File viewers / editors**: `Api/GetExtensionViewer` / `Api/GetExtensionEditor` (registered by services with `TryAddExtensionViewer` / `TryAddExtensionEditor`), opened with `Sw.Page.View` / `Sw.Page.Edit`.
- **Table row links**: `[TableDataUrl]` with a target prefix `*`, `+`, `^` or `-` (`_self`, `_blank`, `_top`, `_parent`).
- **Native hosts**: Thumbnail.Web renders `mediaView/MediaPreview.html` / `MapView.html` in CefSharp / WebView2 with an injected `ScreenShotHost`, so those url formats are an external contract.
- **Site urls in url parameters** (`q` of `explore/table.html` and `explore/api.html`) follow one rule, `Sw.Url.SiteUrl` / `SitePath`: a value starting with `./`, `../`, `/` or a scheme resolves like a link on the page, any other value is relative to the site root. So `table.html?q=../Api/debug/ApiTable` (the menu form) and `api.html?q=Api/debug/GetApiInfo` (the form the server lists apis in) both work on both pages, and only urls of this site are loaded. An unknown url is named in the error ("The server has no API with the url ...", "Could not load the data: 404 - Not Found (Api/...)").

## Deploying and operating

For architects and operators: what a deployed app serves, how to work on the pages of a running app, and who may see what.

- **`index.html` and `debug.html`**: `index.html` (root of SysWeaver.HttpServer `srcWeb/`) is the app shell; the build of SysWeaver.HttpServer copies its compressed file to `debug.html` (`copy web\index.html.br web\debug.html.br` in the csproj). Both are templates (`${#EnvInfo.AppDisplayName}`) and behave the same, but `debug.html` is a **build-time snapshot**: it only changes when SysWeaver.HttpServer is rebuilt, and it is never read from disk. Use `index.html` (or the site root) as the app url; when `debug.html` looks different, the binaries are older than the sources.
- **Serving `srcWeb` from disk while developing**: add a `FileHttpServerModule` to the manifest (`*.Services.json`) with one entry per `srcWeb` folder and no caching, before the embedded file service, so files on disk win over the embedded copies:

  ```json
  { "Type": "SysWeaver.Net.FileHttpServerModule, SysWeaver.HttpServer",
    "Params": { "Folders": [
      { "DiscFolder": "D:\\SysWeaver\\SysWeaver.HttpServer\\srcWeb", "WebFolder": "", "ClientCacheDuration": 0, "RequestCacheDuration": 0 },
      { "DiscFolder": "D:\\MyApp\\srcWeb", "WebFolder": "", "ClientCacheDuration": 0, "RequestCacheDuration": 0 } ] } }
  ```

  A page edit is then live on the next reload (no build). The later folder is searched first, folders that don't exist are ignored. Disk files are served as they are: not minified and **with** their `//DEBUG_BEGIN` blocks, and a sub-folder is served even when the project's build would not embed it (see [Bundles and build rules](#bundles-and-build-rules)), so test the built app before shipping. Never ship a manifest that points at source folders. Without such a module a host (debug builds too, ex: the test host) serves the copies embedded at build time: an edit of a file that existed when the host was built (`index.html`, `app/application.*`, the `sw/` bundles, `common/common.js`) only shows after rebuilding the host. That is intended (the host serves its own copy of the shared files): when testing web files, share **all** the `srcWeb` folders involved this way, with 0 caching.
- **Roles** ([`SysWeaver.Common/Roles.cs`](../SysWeaver.Common/Roles.cs)): auth requirements are comma separated token lists and a user with any one token gets access. `Roles.Debug` is the back-end developer, `Roles.Admin` the non-technical responsible person, `Roles.Ops` the IT technician, `Roles.Dev` the front-end / api consumer developer (`Roles.Service` for service clients, plus combinations such as `Roles.AdminOps`, `Roles.OpsDev`). **Every role includes `Debug`** (`Admin` is `"Admin,Debug"`), so a Debug user can call and see everything that is protected by a `Roles` constant (not `Roles.Disabled`, and not custom tokens); give the Debug token to developers of the service only. Menu items (`[WebMenu*]`, auth `null` = public, `""` = any signed-in user) and apis (`[WebApiAuth]`) declare their auth independently: give a page's menu item the same roles as the apis the page calls, or users get a menu entry that ends in "not allowed".
- **What the front end needs from the server** (the C# side of each item is in the [HttpServer README](../SysWeaver.HttpServer/README.md) and the module READMEs):
  - the static files of every registered project (embedded, merged by folder), and the templated files `index.html`, `debug.html`, `app/Home.html`, `app/manifest.json`, `common/theme.css`;
  - `Api/application/GetMenu`, `Api/auth/GetUser` and the long poll `Api/application/GetMessages` (one per browser; a reverse proxy must let it stay open), plus `serverTime` at the root;
  - the session cookie `SysWeaver.Session.<AppName>` (http-only; the client never sends tokens) and `SysWeaver.DeviceId`; unauthenticated api calls get a 401 with `X-Login-Url: <sign in page>`, other requests a 302 to `auth/Login.html`;
  - `app/app.js` / `app/app.css` (served empty unless the application supplies them) and the generated icons (`icon.svg`, `favicon.ico`, `logo.svg` ...);
  - optional services are optional on the client too: pages ask the server before they load or show an optional feature (`Api/HaveService "<service type>"`, once per page: the sign in page loads the passkey plug-in only when `SysWeaver.MicroService.PassKeyService` runs, tables show language / country values as plain text without `IsoDataService`), so an app without them gets no 404s and works without the feature;
  - `Sw.Access` asks the server which apis the session may call and which role they need (`Api/auth/CanCall`, open to everyone; older servers: `Api/debug/GetApiInfo`, now Dev / Debug only); when the role can't be read the pages still work and only become less specific (actions are disabled after their first refusal, the not-allowed state says "your role" instead of naming it; see `core/README.md`, "Dependency on GetApiInfo");
  - the site may run below a path (reverse proxy): pages use relative urls only (`SwWeb check` warns about `/...` urls), and the core derives the site root from the location of `sw/core.js` (`Sw.Url.Root`).

## Working offline

**Rule: a SysWeaver app works without internet.** Every font, icon, script, style sheet and image a page needs is served by the app itself (`srcWeb`, embedded at build time); nothing is loaded from a CDN, Google Fonts or another site (`SwWeb check` rule `external-resource`).

- **Fonts**: local only. The brand fonts Barlow (text) and Saira Semi Condensed (headings) come from `brand/fonts.css` as `data:` urls (`--sw-sans` / `--sw-head` through `--ThemeFont` / `--ThemeFontHead`, with system fallbacks; `--sw-mono` is a system stack; see `ui/tokens.css`). Font files (`.woff2`, `.ttf` ...) are not embedded by the build (see [Bundles and build rules](#bundles-and-build-rules)), so a font file in `srcWeb` works from disk but is a 404 in the deployed app (`SwWeb check` rule `font-file`). If a page or brand really needs its own font, put it in a `.css` file as a `data:` url (`@font-face { src: url("data:font/woff2;base64,...") }`), check the font license allows embedding, and keep it small.
- **Vendored libraries** (Chart.js, Ace, Prettier, marked, md5 ...) are local copies in their project's `srcWeb` (`chart/external/`, `edit/external/`, `common/`). The urls inside them are comments, license headers or snippet text, not requests.
- **Links are fine**: an `<a href="https://...">` (docs, "Search the web", Google Maps "View" / "Directions", the markdown help in chat) only opens a page when the user selects it.
- **Inherently online, optional features** degrade quickly and quietly:

  | Feature | Online | Offline |
  |---|---|---|
  | Password leak check (`auth/Auth.js` `CheckLeak`, sign in, password hash, user manager pages): `api.pwnedpasswords.com` | "Password is safe (not leaked)!" / "This password is part of N data leaks" | As before the rewrite: the request fails (offline it fails fast; behind a firewall that drops it, "Checking password .." stays until the browser gives up) and the result line says "Password leak check failed!" (a warning with the reason as tool tip). The request and its timing are the original ones (one GET per changed valid password, at once, no time limit). It never blocks signing in or saving a password. **A deployment can turn it off** (an offline / air-gapped site, or a policy against third party requests): `:root { --sw-password-leak-check: off; }` in the app's `app/app.css` reaches every page, also the stand-alone sign in page (alternatives: `<meta name="sw-password-leak-check" content="off">` in a page, `window.SysWeaverPasswordLeakCheck = false` in `app/app.js`). Then no request is made and no result is shown. Default: on. |
  | Map view / map demo (`mediaView/MapView.html`): Google Maps embed / OpenStreetMap | The map | The map site is probed first (`no-cors` favicon GET, at most 4 s, remembered 30 s per session): a neutral "Map not available" placeholder instead of an empty frame. |
  | YouTube media (`mediaPlayer/media_player_youtube.js`): `youtube-nocookie.com` embed | The video | Same probe (`MediaPlayerTools.Reachable`): a "Video not available" placeholder; the player counts as loaded and its commands do nothing. The embed handshake gives up after 10 s per try. |
  | Content that points to another site (a `FontUrl` or media url in a link, an image in a chat / markdown message) | Loaded | User content, not part of the app: the usual load error, or the default font with a notice. |

- **Server side**, outside the front end: services that call internet apis (exchange rates, OpenAI / Google AI, ACME certificates, IP location sources ...) fail or time out on the server; their tables show the server error.
- **Testing offline**: block every request to a host other than the app's (Playwright `context.route` that aborts them) and open the shell, the sign in page and every menu page: no hang, no broken layout, no blocked workflow. Also try "requests are dropped" (the route never answers) to see the time-outs.

## Push messages and the session manager

- **One master window per browser origin** (Web Lock `SysWeaver.SessionManager` plus a Ping / Pong); it long-polls `Api/application/GetMessages {Cc, MessageTypes}` and rebroadcasts each message over InterOp. When the master closes it hands over (`MasterClosed {Cc, ServerEvents}`). When the master dies without a handover (crashed / killed tab), the new master asks every window for its subscriptions (`SessionManager.GetEvents`, answered with `SessionManager.OwnedEvents {To, Events}`), so no window loses its messages. Old (`common.js`) and new windows share the same master.
- **Frames** get server messages through their top window's session manager. A same origin host page that does NOT load the SysWeaver core (ex: a plain test page embedding `chat/chat.html`) has none: add `top=true` to the frame url (`chat.html?...&top=true`) and the frame acts as a top window (`Sw.Page.IsTop`, it takes part in the master election and gets the top window reactions: reloads on login / logout / new server instance). Hosts that load `sw/core.js` don't need it.
- **Subscriptions** are reference counted: `Sw.Session.Subscribe(type, fn)` / `Unsubscribe`. Types are lower case; the server drops messages of types nobody subscribed to. Example: chat uses `chat.{post|replace|remove|clear}.<provider>.<chatId>`.
- **Server side**: `request.Session.PushMessage(...)`, `request.Server.PushMessageUser(...)`, `PushMessageAllUsers(...)`, `PushMessageAllSessions(new PushMessage("my.type"))`.
- **Forced types** reach every session: `user.login`, `user.logout`, `server.*`, `reload`, `refresh`, `filereload`. Default reactions in the core (top windows): reload on a new server instance (a window that knows no instance yet, ex: a new tab, does not reload; it learns the instance from the master), on `reload` / `refresh` / `user.login` / `user.logout` (unless `Sw.Session.IgnoreUserChanges()`; auth pages opt out), cache flush and `src` / `href` refresh on `filereload`. The ui shows the full page block overlay (`Sw.ServerBlock`) on `server.pause` / `restart` / `shutdown` / `error`.
- `Sw.Session.ServerTime()` is the latency corrected server time.
- The full description (requests, election, reactions, theme hand-shake, classes, differences from the original): [WebMessaging.md](WebMessaging.md).

## Window, frame and tab messaging

1. **InterOp** (`Sw.InterOp`): the BroadcastChannel `"SysWeaver"`, envelope `{Type, From, MagicXyz: "SysWeaver"}` (postMessage fallback). Types include `Theme.Changed`, `UserImageChanged`, `WindowLoaded`, `FileSaved` / `FileDeleted`, the media editor protocol and the session manager messages.
2. **Frame messages** (`Sw.Frame.PostParent` / `PostTop` / `PostAll`): raw `{Type}` postMessage between frames: `PageLoaded`, `LoaderRemoved`, `IframeNavigating {Url, Id}` (the app shell answers `IframeNavigationHandled {Id, Url, Handled}`, so the page does not also navigate itself); chat (`chat.scrolltop` ... and `ViewProfile`), chart (`ChartClick`), media players (`MediaPlay`, `MediaPause` ...).
3. **Same origin DOM access** in a few places (the shell sets `html.Embedded` on the frame document).

## Theming and layout

- **Theme resolution**: `?usetheme` → `localStorage["SysWeaver.Theme"]` (set by `?settheme` or the server Theme menu via `setTheme()`) → the OS color scheme (followed live) → `"dark"`. `light` = no `data-theme` attribute; `dark`, `hacker`, `application` (colors from the server template) and `pro` are defined in `common/theme.css`. `Sw.Theme.Register` adds custom themes. The scheme of the tokens (`data-sw-scheme` = `light` / `dark` on `html`) comes from what a theme declares (its `Scheme`, its css `color-scheme`, else the lightness of its background), never from the mere presence of `data-theme`.
- **Branding a deployed app** (name, logo / icons, colors, own themes, page styles, without forking the framework): [WebBranding.md](WebBranding.md). `app/app.css` is applied to every page (the shell links it, `Sw.Theme` adopts it in every other page when it has content), themes registered in `app/app.js` reach every page, and `--sw-theme-light` / `--sw-theme-dark` in `app/app.css` choose the automatic themes.
- **Colors**: the four base colors `--ThemeBackground/Main/Acc1/Acc2` (plus derived `R/G/B/RGB/25/50/75` variables) feed the UI tokens `--sw-bg`, `--sw-fg`, `--sw-accent1`, `--sw-accent2` and everything derived from them. Focus rings use a fixed blue per scheme.
- **Brand (default)**: `light` and `dark` are the SysWeaver brand (copper and slate; text on accents = the background), fonts Barlow (text) and Saira Semi Condensed (headings) from the local `brand/fonts.css` (data urls: the build embeds only `.br` and `.png` files, so `.ttf` / `.woff2` files would not reach a deployed app), the brand marks `--sw-brand-icon` / `--sw-brand-logo` (on-bright / on-dark by scheme), the browser and install icons at the srcWeb root (they override the server generated ones), and the weave behind the stand-alone sign-in and Welcome pages. Pro, Hacker and Application stay as alternatives. Details and how a deployment overrides each part: [WebBranding.md](WebBranding.md).
- **Icons** are CSS mask images of `icons/*.svg` (`.IconX` classes, `Sw.IconClass`), plus an inline svg set (`Sw.Icon`).
- **Layout** state is classes on `html` / `body` (`SwMobile`/`SwDesktop`, `SwPortrait`/`SwLandscape`, `SwNarrow`/`SwWide`, `SwSelf*`) and events (`SwLayout`, `SwOrientation`); `?mobile=true` forces the phone layout. Control heights follow the density (`html[data-density]` compact / comfortable / touch, `--sw-control-h` 28 / 34 / 44 px; touch on touch screens).

## Feature libraries

| Library | Entry | Notes |
|---|---|---|
| Data table | `Sw.Table.Create({ Url, Element })` (`explore/table.js`), or the page `explore/table.html?q=<api>` | Legacy `Table.addTable(...)` kept. See [explore](../SysWeaver.HttpServer.ExploreModule/srcWeb/explore/README.md) |
| Object editor | `Sw.Edit` (`edit/Edit.js`) | Driven by `edit/GetTypeInfo` and `GetDefaultInstance`; type handlers per .NET type, plug-ins from C# `[EditType]`. See [edit](../SysWeaver.MicroService.Edit/srcWeb/edit/README.md) |
| Text editor | `edit/text.html?r=&u=&d=...` | Ace and Prettier (vendored) |
| Chart | `chart/chart.html?q=<url>&...` | Chart.js config plus `ChartJsConfig`; polls and merges. See [chart](../SysWeaver.MicroService.ChartJs/srcWeb/chart/README.md) |
| Chat | `chat/Chat.html?id=...` or `chat/chat.js` in any page | Push messages, attachments, AI streaming, speech. See [chat](../SysWeaver.Chat/srcWeb/chat/README.md) |
| Uploader | `common/fileUploader.js` | MD5 dedupe, parallel deflated upload |
| Media | `MediaPlayer.Create(type, data, params)`, `mediaView/*` | Players, WebGL effects (`.glsl`) |
| Auth | `auth/*` | Login, passwords, passkeys, account pages; shared base `Auth.js` / `AuthUi.js` |

## Translation

The global functions **`_T`, `_TF`, `_TH`, `_TFH`** keep exactly these names and signatures (runtime fallbacks in `core/i18n.js`); the server rewrites calls to them to translate (FileTranslation `JsTranslation`). Therefore:

- user visible strings go through them with a **literal** first argument (and a literal context argument): `_TF("Save", "Text of a button that saves the form")`;
- never rename, alias, wrap or namespace them, and never route user text through a helper that hides the call;
- never write the call pattern (`_T(` and friends, also as the suffix of another identifier) anywhere else: not in comments, strings or HTML attributes;
- no backtick literals, `\n` or mixed quotes inside translated literals (the minifier turns them into backtick literals, which are not translated).

`SwWeb check` enforces all of this.

## Legacy pages and the compatibility layer

Old pages, other projects and the srcData templates keep working unchanged:

| Old include | Now | Contents |
|---|---|---|
| `common/common.js` | GENERATED: core + ui + `ui/compat_common.js` | Every global of the old common.js (`sendRequest`, `getRequest`, `Fail`, `Info`, `ValueFormat`, `ColorIcon`, `AddLoading`, `PageLoaded`, `InterOp`, `SessionManager`, `includeJs`, `Open` ...) mapped onto `Sw` |
| `app/application.js` / `.css` | GENERATED: shell + `shell/compat_application.js` | `PopUp`, `Confirm`, `PopUpMenu`, `PopUpSelection`, `PopUpEdit`, `WebMenu`, `MainMenu`, `App`, `AppParams` ... |
| `explore/table.js`, `edit/Edit.js`, `auth/*.js` ... | rewritten internally | Old global names kept next to the new `Sw.*` names |

- **Migrating**: `SwWeb migrate <folder>` switches a folder to the new format (includes, token level renames of old globals to `Sw.*`, idempotent, reports what it can't rewrite). Pages that still use old names after the run also load `sw/compat.js` / `sw/compat.css` (the old globals on top of the already loaded core / ui) until `SwWeb check --strict` reports no `legacy-compat` warnings. See [WebQuickStart](WebQuickStart.md#7-migrate-an-old-page) and [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md). The rule by rule porting guide (old UI and Design v2, with what `migrate` / `check --design` handle): [`PORTING.md`](../SysWeaver.HttpServer/srcWeb/PORTING.md).
- The old to new name maps are in the core, ui and shell READMEs (LEGACY sections).
- Before the rewrite (analysis of 2026-10-17) the client was one ~200-global `common.js` plus `application.js` (shell and pop-ups in one file), pages built DOM in large closures, and naming mixed `sendRequest` / `Fail` styles. The rewrite split it into core / ui / shell, kept every page path, url parameter, message type, CSS variable and icon class, and moved old names into the generated compatibility bundles.

## Client state and storage keys

| Store | Key | Owner |
|---|---|---|
| localStorage | `SysWeaver.Theme`, `SysWeaver.ThemeDefs`, `SysWeaver.ThemeAuto`, `SysWeaver.Mobile` | core |
| localStorage | `SysWeaver.Tabs.<key>` | common/tab.js |
| localStorage | `SysWeaver.Pinned`, `SysWeaver.MenuCollapsed.<menu>.<id>`, `SysWeaver.PopUpRecent.<id>`, `SysWeaver.RecentPages` | shell / ui |
| localStorage | `SysWeaver.Density`, `SysWeaver.Dialog.<Id>` (dialog position / size), `SysWeaver.Draft.<site>.<user>.<context>` (`Sw.Draft`) | ui / core |
| localStorage | `SysWeaver.Table.<hash(url)>`, `ExploreSettings`, last argument per API | explore |
| localStorage | `SysWeaver.Chat.*` (drafts, attachments, hidden senders, language, speech, input format, split size) | chat |
| localStorage | `SysWeaver.PassKey.Hint` / `.Local` / `.UserOnDevice` | PassKey |
| localStorage | `SysWeaver.Media.EffectDpiScale`, `SysWeaver.EffectInfo.<cache>.<hash>`, `QuizzWeaver.Media*` | media |
| localStorage | `fakeEmail.*` | fakeEmail |
| sessionStorage | `SysWeaver.ServerInstance`; `<location.href>` (page state of a frame, `Sw.History`) | core |
| history / hash | Shell: `#Base64(url)` of the embedded page; top windows: `history.state` (scroll position) | shell / core |

## Contracts mirrored between C# and JS

None of these JS definitions are generated; they are hand-written copies of C# types or conventions (keep them in sync):

- table and type info: `Sw.Table.Props` / `Ops` (TableDataColumnProps, TableDataFilterOps), `TableDataFilter`, `TableDataState`, `HttpServerEndpointTypes`, `TypeMemberFlags`;
- media: `MediaTypes` and the `MediaPlayerParams*` defaults;
- status and format values: upload status codes, chat message Format and Flags, StoreFile / StoreLink scopes, menu item Types and Flags;
- response shapes: `ApiInfo`, `ExploreData`, `ChatJoinResponse`, `ChartJsConfig`;
- string-encoded metadata: enum metadata in `Default` (`def>v<name<summary<remarks|...`), `Hide:<js expr>`, effect parameter comments `// var:{...}`, the `[TableDataUrl]` target prefixes;
- names derived by string replacement: the `*HistoryChart` / `*HistoryShortChart` urls, the repository names `Keys`, `Current_<svc>`, `Master_<svc>`.
