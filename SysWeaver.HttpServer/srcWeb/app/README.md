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
