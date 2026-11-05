# Your first web page in 5 minutes

[⬆ _docs](README.md)

> A copy-paste guide: add a web page to a SysWeaver service, call your API from it, get it into the menu, test it, and migrate old pages. Read [Web.md](Web.md) for the architecture behind it.

| | |
|---|---|
| **Repository** | [SysWeaver](../README.md) |
| **Kind** | Developer guide |

## Contents

- [0. What you need](#0-what-you-need)
- [1. Embed a web folder in your project](#1-embed-a-web-folder-in-your-project)
- [2. A page without UI (core only)](#2-a-page-without-ui-core-only)
- [2b. A page with its own UI: messaging only](#2b-a-page-with-its-own-ui-messaging-only)
- [3. A page with UI: a form and a table](#3-a-page-with-ui-a-form-and-a-table)
- [4. Put it in the menu](#4-put-it-in-the-menu)
- [5. Who may use it (roles and "not allowed")](#5-who-may-use-it-roles-and-not-allowed)
- [6. Test it](#6-test-it)
- [7. Migrate an old page](#7-migrate-an-old-page)
- [Rules that bite](#rules-that-bite)

## 0. What you need

- A service with a `[WebApi]` method, like `GreetingService` in the root README [Hello world](../README.md#hello-world) (`[WebApiUrl("hello")]`, `Greet(string name)`).
- The standard web UI services in the manifest (the app shell, menus, sign-in, explorer), as in [WebServerService](https://github.com/dosh1974/SysWeaver.Services/blob/main/WebServerService/README.md). Without them your page is still served, but there is no shell or menu around it.

The browser side needs no build tools: pages are plain HTML + classic JavaScript files loading the prebuilt bundles `sw/core.js` and `sw/ui.js`.

## 1. Embed a web folder in your project

Put the files in `srcWeb/<folder>/` and compress them into `web/` at build time (the same target the service projects use):

```xml
<Target Name="EmbedLocal" BeforeTargets="BeforeBuild">
  <Exec Command="..\..\SysWeaver\_tools\compress\Compress.exe -Methods br .\srcWeb\*+ .\web" />
  <ItemGroup>
    <EmbeddedResource Include="web\**\*.br" Visible="false" />
  </ItemGroup>
</Target>
```

A file `srcWeb/hello/hello.html` is served at `/hello/hello.html` (adjust the relative path of `Compress.exe` to where your project sits next to the SysWeaver repository).

- **Where pages live**: in a folder of your own **one level below the root** (`srcWeb/hello/`), so `../sw/core.js`, `../common/theme.css` and `../Api/...` resolve. The folder name is part of every url: no `-`, no dots, no leading digit.
- **Sub-folders**: the trailing `+` in `.\srcWeb\*+` makes the step recursive, so `srcWeb/hello/img/` is embedded too. Projects whose step names one folder without `+` (ex: `.\srcWeb\chat\* .\web\chat`) embed only that folder's files; see [Bundles and build rules](Web.md#bundles-and-build-rules).
- Folders are merged across projects by url (`auth/` has files from five projects), so in a shared folder your file names must be unique.

## 2. A page without UI (core only)

`sw/core.js` alone gives you requests, server push messages, cross window messaging, themes and the page protocol, with no visible components.

`srcWeb/hello/plain.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
    <title>Hello (core only)</title>
    <script defer src="../sw/core.js"></script>
    <script defer src="plain.js"></script>
    <link rel="stylesheet" href="../common/theme.css">
</head>
<body>
    <h1 id="greeting">...</h1>
    <p id="last"></p>
</body>
</html>
```

`srcWeb/hello/plain.js`:

```js
/**
 * @file hello/plain.js - a page that uses the UI-less core only.
 */
"use strict";

Sw.Page.Main(async () => {
    //  Call the api: GET ../Api/hello/Greet?"World" (small arguments go in the url, big ones are POSTed as json; debug builds POST unless the web server Params have "OptimizedSend": true)
    const text = await Sw.Net.Send("../Api/hello/Greet", "World");
    document.getElementById("greeting").textContent = text;

    //  Server push messages: one long poll (Api/application/GetMessages) per browser, shared by all tabs
    Sw.Session.Subscribe("hello.changed", msg => {
        document.getElementById("last").textContent = _T("Changed at {0}", Sw.Session.ServerTime().toLocaleTimeString(), "Status line: when the server last reported a change");
    });
});
```

- `Sw.Page.Main(fn)` runs `fn` when the DOM is ready, reports errors and (with `{ Loading: true }`) shows a loading phase; when the page is embedded in the shell it tells the shell when to reveal it.
- `Sw.Net.Send(url, arg)` returns the decoded json. Failures throw `Sw.Net.Error` (`Status`, `Text`). A session that is not signed in is redirected to the sign-in page.
- The server sends a push message to the sessions with, for example, `request.Server.PushMessageAllSessions(new PushMessage("hello.changed"))` (a `[WebApi]` method can take an `HttpServerRequest request` parameter). Types are lower case; the client must `Subscribe` to a type before the server delivers it.
- Theme, layout classes (`SwMobile`, `SwNarrow` ...) and url flags (`?settheme=dark`, `?mobile=true`) work automatically.

## 2b. A page with its own UI: messaging only

A page that renders everything itself (a WebGL editor, a console, a kiosk screen) and has its own look, but must still follow the SysWeaver server events (sign in / out, reload, server pause, its own push messages), keep the session alive and talk to the other tabs, loads the small messaging bundle `sw/session.js` instead of `sw/core.js`: no theme, no components, no framework css. It is what every old page got from `common/common.js` (`SessionManager.Create(); SysWeaverInit();`). Details: [WebMessaging.md](WebMessaging.md).

**Recipe 1: messaging only** (`srcWeb/hello/own.html`):

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
    <title>Hello (own UI)</title>
    <script defer src="../sw/session.js"></script>
    <script defer src="own.js"></script>
    <link rel="stylesheet" href="own.css">
</head>
<body>
    <canvas id="view"></canvas>
</body>
</html>
```

```js
/**
 * @file hello/own.js - a page with its own UI that follows the SysWeaver messaging.
 */
"use strict";

//  Server push messages of this page (the session started by itself: one long poll per browser, shared by all tabs)
Sw.Session.Subscribe("hello.changed", msg => redraw(msg.Value));
//  Sign in / out in another tab: this page reloads by itself; to handle it here instead:
//  Sw.Session.IgnoreUserChanges(); Sw.Session.Subscribe("user.logout", () => showSignedOut(), true);
//  The layout classes are on html and body (SwMobile, SwNarrow, SwPortrait ...): use them in own.css
```

- The old names work too: `SessionManager.AddMessageHandler(type, fn)`, `SysWeaverIgnoreUserChanges()`, `GetServerTime()`, `InterOp.Post(...)`, `PostTop(...)`.
- When the server is paused, restarts or the connection is lost ("Server communication lost.", after 3 failed polls: about 10-15 s), a small self-styled dialog blocks the page, and no key reaches your own listeners (as every page of the original). It is movable and resizable, × / Escape hide it so the page can be read meanwhile, and it goes when the server is back; the page reloads by itself only when the server that answers is a new instance (a restart), never after a network drop. Draw your own: `Sw.Events.On("server.block", info => ...)` and `Sw.Events.On("server.unblock", ...)` (your handler replaces the overlay), or `<html data-sw-no-block>` for none; `Sw.Session.BlockOnLost = false` keeps the page usable while the connection is lost.
- **Your own look**: such a page (`sw/session.js` without `sw/ui.css`) has no Sw components and no `--sw-*` tokens, so `SwWeb check --design` skips the Design v2 rules for it and its css. A page that loads the components but keeps its own look declares it: `<meta name="sw-own-look" content="why">` (or `data-sw-own-look` on html / body).
- **Follow the SysWeaver theme choice with your own theming**: `Sw.Session.RequestedTheme` (a theme forced for this page: `?useTheme=`, a host frame's `theme.use`) or `Sw.Session.StoredTheme` (the user's choice), and `Sw.Events.On("theme.request", e => applyMyTheme(e.Theme || e.Stored))` when it changes.
- **Sign in on such a page**: add `<script defer src="../sw/net.js"></script>` and `<script defer src="../auth/Auth.js"></script>` after `sw/session.js` (`Sw.Auth`, the old `loginRequest` / `logoutRequest`); `sw/net.js` also gives `Sw.Net.Send` for your own api calls.

**Recipe 2: messaging + the SysWeaver theme** (the page uses the theme colours in its own css, `var(--ThemeBackground)` ...):

```html
    <script defer src="../sw/session.js"></script>
    <script defer src="../sw/theme.js"></script>
    <link rel="stylesheet" href="../common/theme.css">
```

`sw/theme.js` applies the theme (`data-theme`, `data-sw-scheme` light / dark also without `common/theme.css`, `?setTheme`, `?useTheme`, the OS scheme, other tabs' choice) and defines `getTheme`, `applyTheme`, `setTheme`, `resetTheme`, `toggleTheme`, `appendTheme`. The order of the two scripts doesn't matter (`sw/theme.js` first starts with the messaging core). A page that needs components, dialogs or tables uses `sw/core.js` + `sw/ui.js` instead (section 3): they contain all of this.

## 3. A page with UI: a form and a table

Add `sw/ui.js` + `sw/ui.css` for components, and `explore/table.js` + `explore/table.css` for the live data table (`Sw.Table`). Every page builds the Design v2 **page anatomy** from the shared parts: a page header (title, one line description, actions) and a page container (`dash`, `form`, `read` or `fill`), so the next restyle of the framework reaches it too.

The C# side of the table: any `[WebApi]` method that takes a `TableDataRequest` and returns `TableData`:

```csharp
using SysWeaver.Data;

/// <summary>The greetings so far</summary>
[WebApi]
public TableData GreetingTable(TableDataRequest request)
    => TableDataTools.Get(request, 2000, Greetings, "Greetings");   // 2000 = refresh rate (ms) of the client

readonly List<Greeting> Greetings = new();
```

`srcWeb/hello/hello.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
    <title>Hello</title>
    <script defer src="../sw/core.js"></script>
    <script defer src="../sw/ui.js"></script>
    <script defer src="../explore/table.js"></script>
    <script defer src="hello.js"></script>
    <link rel="stylesheet" href="../common/theme.css">
    <link rel="stylesheet" href="../sw/ui.css">
    <link rel="stylesheet" href="../explore/table.css">
    <link rel="stylesheet" href="hello.css">
</head>
<body class="sw-root">
</body>
</html>
```

`srcWeb/hello/hello.js`:

```js
/**
 * @file hello/hello.js - a form that calls the hello api and a live table of the results.
 */
"use strict";

Sw.Page.Main(async () => {
    const name = new Sw.TextBox({ Label: _TF("Name", "Label of the text box where the user enters a name to greet"), Clear: true });
    const greet = new Sw.Button({
        Text: _TF("Greet", "Text of a button that sends a greeting to the entered name"), Icon: "send", Kind: "primary",
        //  A promise makes the button busy; a rejection shows a fail toast
        OnClick: async () => Sw.Info(await Sw.Net.Send("../Api/hello/Greet", name.Get())),
    });
    //  The Design v2 page anatomy: page header (the title is the menu item name inside the shell) + a fill container
    const page = Sw.PageLayout({
        Type: "fill",
        Title: _TF("Hello", "Title of the greeting page"),
        Description: _TF("Greet someone; every greeting so far is listed below.", "Description of the greeting page"),
    });
    const form = Sw.Card({ Title: _TF("Say hello", "Title of the greeting form"), Body: [name, Sw.el("div.sw-row.sw-start", null, greet.E)] });
    //  A fill page gives the rest of the height to its .sw-fill child: the table frame (pager at the bottom) goes there
    const tableBox = Sw.el("div.sw-fill");
    page.Add(form, tableBox);

    //  Paging, sorting, filters, search, export, phone card view and live refresh come with the table
    await Sw.Table.Create({ Url: "../Api/hello/GreetingTable", Element: tableBox });
}, { Loading: true });
```

`srcWeb/hello/hello.css` stays empty here: the parts bring their look. Own rules go on your own classes and use the role tokens (`color: var(--sw-text-2)`, `border: 1px solid var(--sw-border)`, `border-radius: var(--sw-radius-md)`, no shadows or gradients), see [ui/README.md "Theming"](../SysWeaver.HttpServer/srcWeb/ui/README.md#theming).

- Components take one options object with PascalCase keys and expose their root element as `.E`; the page parts (`Sw.PageLayout`, `Sw.Card`, `Sw.Grid`, `Sw.Facts`, `Sw.Kpi`, `Sw.ListCard`, `Sw.StateView`) also take components in `Add` / `Body`. See every component, its API name and a snippet in the **component gallery** (`/ui/gallery.html` on any SysWeaver server), the reference page `ui/design.html` and the [UI library README](../SysWeaver.HttpServer/srcWeb/ui/README.md) ("Page anatomy").
- Page types: `dash` (dashboards: facts, KPI tiles, cards in a `Sw.Grid`), `form` (forms and settings), `read` (text), `fill` (one table, log or chat that takes the height). One primary action per region; destructive actions are `Kind: "danger"` with a `Sw.Dialog.Confirm(..., { Danger: true })`.
- `<body class="sw-root">` gives the page the Sw font, colours and `box-sizing: border-box`; the components are built for it. The page code lives in its own deferred file: an inline `<script>` that calls `Sw.Page.Main` runs before the deferred bundles (`Sw is not defined`).
- User visible texts go through `_TF("text", "context")` (or `_T`, `_TH`, `_TFH`) with literal arguments: the server rewrites these calls to translate them. Never rename or wrap them.
- A page that only needs a table can skip all of this: `[WebMenuTable]` on the table method (next step) shows it in the generic `explore/table.html`.

## 4. Put it in the menu

Menus come from the server. Add an attribute to the service class (any type registered in the service manager); nothing on the client needs to change:

```csharp
[IsMicroService]
[WebApiUrl("hello")]
[WebMenuEmbedded(null, "Tools/Hello", "Hello", "hello/hello.html", "Greet someone", "IconHome", 0, "")]
public sealed class GreetingService
{
    /// <summary>The greetings so far</summary>
    [WebApi]
    [WebMenuTable(null, "Tools/Greetings", "Greetings", "All greetings")]
    public TableData GreetingTable(TableDataRequest request) => ...;
}
```

- `WebMenuEmbedded(menu, id, name, url, title, iconClass, order, auth, noUserRequired, dynamic)`: `null` = the main menu, `id` is a `/` separated path (folders are created), `url` is relative to the site root, `auth` `null` = public, `""` = any signed in user, else role tokens (`"Admin,Ops"`). Use the roles of the apis the page calls (next step).
- Other kinds: `[WebMenuTable]` / `[WebMenuChart]` (on an api method), `[WebMenuLink]` (whole window), `[WebMenuLinkExternal]` (new tab), `[WebMenuJs]`.
- The shell embeds the page in its content frame, highlights the menu item, keeps the browser history (`#Base64(url)`) and reveals the frame when `Sw.Page.Main` finished.

## 5. Who may use it (roles and "not allowed")

The server decides who may call an api; the menu only decides who sees the entry. Declare both, with the same roles ([Roles](../SysWeaver.Common/Roles.cs): `Roles.Admin`, `Roles.Ops`, `Roles.Dev`, combinations such as `Roles.AdminOps`; every role includes `Debug`):

```csharp
[IsMicroService]
[WebApiUrl("hello")]
[WebApiAuth("")]                       // every api of the service: any signed in user (null = public)
[WebMenuEmbedded(null, "Tools/Hello", "Hello", "hello/hello.html", "Greet someone", "IconHome", 0, "")]
public sealed class GreetingService
{
    /// <summary>The greetings so far</summary>
    [WebApi]
    [WebApiAuth(Roles.AdminOps)]       // this api: Admin or Ops only (wins over the class)
    [WebMenuTable(null, "Tools/Greetings", "Greetings", "All greetings", null, 0, Roles.AdminOps)]
    public TableData GreetingTable(TableDataRequest request) => ...;
}
```

- **No `[WebApiAuth]` means public**: anyone, also without signing in, can call the api, whatever the menu says. A menu item with auth `""` over a public api only hides the link.
- **Give the menu item the roles of the apis the page calls.** Otherwise users get an entry that ends in "not allowed".
- **The page handles a refusal for you.** A refused call is a `Sw.Net.Error` with `Status` 403 (`Sw.Access.IsDenied(e)`). The page then shows the shared "not allowed" state: "You don't have access to this page", the signed in user, the role needed and a *Go to Home* link. This happens when `Sw.Page.Main`'s function throws it, when a `Sw.Table` (or a chart, or the text viewer) is refused, or when you call `Sw.Page.ShowNotAllowed({ Error: e })` yourself. A `Sw.Button` whose `OnClick` is refused shows a toast that names the role, and the button is disabled.
- **Disable actions before the click** with `Sw.Access.Guard`. It reads the api's role and gives the button the tool tip "Requires Admin or Ops":

  ```js
  const clear = new Sw.Button({ Text: _TF("Clear", "Button that removes all greetings"), OnClick: () => Sw.Net.Send("../Api/hello/Clear", null) });
  Sw.Access.Guard(clear, { Url: "../Api/hello/Clear" });   // or { Auth: "Admin,Ops" }
  ```

- **Test with a user of each role.** Sign in as a user without the role, open the page from the menu and directly: you should see the not-allowed state or disabled buttons, and no error toasts.
- `Sw.Access` asks the server which apis the session may call and the role they need (`Api/auth/CanCall`, open to everyone). Older servers only have the api info (`Api/debug/GetApiInfo`, Dev / Debug only): there pages still work, a guarded button is disabled after its first refusal, and the texts say "your role" instead of the role name ([core README](../SysWeaver.HttpServer/srcWeb/core/README.md), "Dependency on GetApiInfo").

## 6. Test it

1. Build and run the service, open the site root, sign in, and pick **Tools > Hello**. Also open `/hello/hello.html` directly (pages work stand-alone too).
2. Try the themes and densities: `?settheme=dark` (or the Theme menu: Light, Dark, Pro, Application, Hacker), `?density=compact` / `touch`, `?mobile=true` (phone layout on a desktop), and a phone or the browser device toolbar in portrait and landscape.
3. Keyboard only: Tab / Shift+Tab, Enter / Space on buttons, Escape closes dialogs and menus.
4. Lint the folder with the [SwWeb](https://github.com/dosh1974/SysWeaver.Tools/blob/main/SwWeb/README.md) tool; it knows the server rules (translation calls, template variables, file names, legacy names, missing includes, script order) and the Design v2 rules (tokens, flat surfaces, page header, `sw-root`):

   ```text
   SwWeb check srcWeb --strict --design
   ```

   Run it on the project or repository folder to check every `srcWeb` / `srcData` folder in it (build outputs such as `web/` are left out).

5. Compare with the gallery (`/ui/gallery.html`) and `ui/design.html` when a component looks or behaves differently than expected.

## 7. Migrate an old page

Old pages (`common/common.js`, `app/application.js`) keep working: these files are generated compatibility bundles that define every old global on top of the new library. To move a folder to the new format and the Design v2 look:

```text
SwWeb migrate srcWeb --dry-run --level design --report port.md    (1. the worksheet: changes = automatic, warnings = manual)
SwWeb migrate srcWeb --backup C:\temp\srcWeb.bak                   (2. the automatic part)
SwWeb migrate srcWeb --level design                                (3. + the Design v2 rewrites)
SwWeb check srcWeb --strict --design                               (4. fix the rest until 0 findings)
```

- `migrate` switches the includes (`common.js` → `sw/core.js` + `sw/ui.js`, `application.js` → + `sw/shell.js` when shell features are used), adds the includes of the libraries a page uses (`explore/table.js` + `table.css` for `Sw.Table` ...), renames old globals to `Sw.*` at the token level (never inside strings or comments; locals that shadow an old name are left alone), renames the old design tokens, and is safe to run twice. `--level design` also adds the viewport tag and `class="sw-root"` and drops invalid shadows.
- The worksheet lists exactly what `check --strict --design` will report after the migration, with file and line (by rule at the top): names it can't rewrite (the page then also loads `sw/compat.js` / `sw/compat.css` so it keeps working), page css on bare elements, the page header, colours, shadows ... Rewrite those by hand (the warnings suggest the new call or token) until `check --strict --design` reports nothing, then remove the two compat includes.
- The old to new name tables: [`core/README.md`](../SysWeaver.HttpServer/srcWeb/core/README.md), [`ui/README.md`](../SysWeaver.HttpServer/srcWeb/ui/README.md), [`shell/README.md`](../SysWeaver.HttpServer/srcWeb/shell/README.md).
- The full porting guide, every rule with a before / after and what the tool does (tokens, flat surfaces, page anatomy, tables, editors, branding): [`PORTING.md`](../SysWeaver.HttpServer/srcWeb/PORTING.md).

## Rules that bite

The full list, with the reasons, is in [Web.md: Bundles and build rules](Web.md#bundles-and-build-rules).

| Rule | `SwWeb check` rule |
|---|---|
| **One dot per file name**: `my.page.js` is served as `my/page.js`. Use `myPage.js` (`x.min.js` is the exception) | `file-name-dots` |
| **No `-` in folder names** (embedded as `_`) and no folder names starting with a digit | `folder-name-dash`, `folder-name-digit` |
| **Translation calls** `_T`, `_TF`, `_TH`, `_TFH`: keep these exact names, a literal first (and context) argument, no `\n`, no mixed quotes, no backtick literals inside; never write `_T(` in comments or strings (the server rewrites it anywhere). `_T("{0} of {1}", a, b, "context")` formats (a trailing literal is the context), `_TF("text", "context")` never does | `tr-*` |
| **Templates**: a few files are served with `${Variable}` substitution (`index.html`, `app/Home.html`, `common/theme.css` ...). Avoid `${Session.`, `${Env.` etc. in JavaScript template literals in any file | `template-*` |
| **Classic scripts**: no ES modules (`<script type="module">`, `import` / `export`, `import()`; `.mjs` is not embedded) | `es-module`, `mjs-file` |
| **Nothing from the internet**: no `http(s)://` or `//` urls in `<script src>`, `<link href>`, `<img src>`, `<iframe src>`, `<video>` / `<audio>` / `<source>`, `<object>`, CSS `@import` / `url()` or `import`, and no literal internet url in `fetch(...)`, `xhr.open(...)`, `new WebSocket(...)`, `x.src = ...` or `Sw.Net.IncludeJs(...)` / `IncludeCss(...)`; copy the file into your folder. An optional online feature that degrades offline is marked with a `SwWeb:allow-online` comment. Relative urls only (`/x.js` breaks below a reverse proxy path) | `external-resource`, `external-request`, `root-relative-url` (warning) |
| **Fonts and brand files**: no font files in `srcWeb` (not embedded; use a `data:` url in css), no `@import` in `app/app.css`, mark urls in `app/app.css` written as `../app/<file>` ([WebBranding.md](WebBranding.md)) | `font-file` (warning), `css-import`, `brand-url` |
| A JSDoc `@file` header in every `.js` file (the brand hook `app/app.js` is exempt) | `jsdoc-header` |
| **Page css stays on your page**: no rules on bare `button`, `input`, `select`, `textarea`, table cells (`td`, `th` ...), `img` or `iframe` (scope them: `.my-page button`), no `font-size` / `zoom` on a bare `body`, and no `body { overflow: hidden }` (it clips the pager on phones). They also restyle the Sw components of the page | `element-css`, `body-overflow` (warnings) |
| **The page has the parts it uses**: `<body class="sw-root">`, the includes of the libraries it calls (`Sw.Table` needs `../explore/table.js` + `../explore/table.css`, `Sw.Shell` `../sw/shell.js`, `Sw.Edit` `../edit/Edit.js`), and its code in a deferred file (an inline `<script>` calling `Sw.Page.Main` runs before the deferred bundles) | `design-sw-root` (design), `missing-include`, `script-order` |
| **Token names exist**: a `var(--sw-...)` typo is dropped by css without a word (`--sw-accent` is `--sw-accent1`) | `unknown-token` (warning) |
| **The page is on the framework**: it loads `sw/core.js`, `sw/ui.js`, `sw/ui.css` (a plain html / js page: [PORTING.md A0](../SysWeaver.HttpServer/srcWeb/PORTING.md#a0-a-page-that-does-not-use-the-framework)) | `no-framework` (error with `--strict`) |
| **Design v2**: role tokens instead of colours (`--sw-text`, `--sw-surface`, `--sw-border` ...), no shadows on permanent surfaces, no gradients, radius tokens, a page header (`Sw.PageLayout`) | `design-*`, `a11y-*` (`--design`, warnings) |
| **UX guide** ([UX_Guide.md](UX_Guide.md)): act on `click`, hover rules in `@media (hover: hover)`, `aria-disabled` with a reason instead of `disabled` (`Sw.Button` `SetDisabled(true, reason)`), theme durations, labels, verb buttons, no native `confirm()` / `alert()`; call `Sw.Page.SetDirty` while a form has unsaved edits ([PORTING.md Part C](../SysWeaver.HttpServer/srcWeb/PORTING.md#part-c-ux-guide-rules)) | `ux-*` (`--design`, warnings) |
| `//DEBUG_BEGIN` ... `//DEBUG_END` lines are removed from release builds (keep them balanced) | `debug-markers` |
| Balanced brackets in every script (a missing `)` or `}` breaks the whole file) | `syntax` |
| `"use strict"` in every script; urls are case sensitive | not checked |

`SwWeb check` finds all of these except the last row. It only sees urls written in the files: an internet url built at run time from variables (map tiles, `fetch(base + path)`) needs a review, and its syntax check only pairs brackets and quotes (it is not a parser: open the page in a browser too).
