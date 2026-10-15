# fakeEmail

[⬆ srcWeb](../README.md)

> The fake email web inbox: a self-contained single page that lists, shows, composes and deletes the emails held by `FakeEmailService`.

| | |
|---|---|
| **Project** | [SysWeaver.Email.Fake](../../README.md) |
| **Kind** | Embedded web assets (source) |

## Purpose

A standalone page (it does not use the SysWeaver web application shell or any external library) titled "Fake Mail". It talks to the web API of [`FakeEmailService`](../../FakeEmailService.cs), by default at `../Api/fakeEmail/` relative to the page; an `?api=` query parameter can point it at another API base. It polls `GetEmails` for live updates (with a connection status indicator), fetches single emails with `GetEmail`, and uses `SendEmail` / `FakeIncoming` to compose emails as the system or as a user, and `DeleteEmail` to remove them.

At build time the files are Brotli-compressed into `web/fakeEmail/` and embedded (see [srcWeb](../README.md)); the page is opened at `fakeEmail/index.html`.

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Page layout: header with sender address, search and theme toggle, mailbox side bar, message list, reading pane and composer (inline SVG icons) |
| [`email.js`](email.js) | All behaviour: API calls, polling, search with highlighting, html bodies in a sandboxed iframe or as source, compose (text or html, with preview), reply, delete, read state and settings kept in local storage, keyboard shortcuts |
| [`email.css`](email.css) | Styles, including light and dark themes |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.Email.Fake](../../README.md)
- **Uses:** [`FakeEmailService`](../../FakeEmailService.cs) (`Api/fakeEmail/`)
