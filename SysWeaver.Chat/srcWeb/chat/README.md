# chat

[⬆ srcWeb](../README.md)

> The web chat UI of SysWeaver.Chat: the chat page, its script and styles, served under `chat/`.

| | |
|---|---|
| **Project** | [SysWeaver.Chat](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`Chat.html` is the single page that shows any chat of any provider (simple rooms, MySQL rooms, AI chats). All logic lives in `chat.js`, which talks to the web APIs of [`ChatService`](../../ChatService.cs) and receives new messages, edits and removals through the HTTP server's session push messages. The page builds on the shared scripts and styles of the HTTP server (`../common/`, `../app/`) and uses images served by other projects (`../icons/`, the `../iso_data/language/` flags, the `../auth/UserImages/` user images).

The page can be embedded in an iframe and controlled with `postMessage` (e.g. `chat.clear`, `chat.scrolltop`, `chat.showall`), which `ChatTest.html` demonstrates.

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | Chat specific mask icons (speech input, text format, preview) |
| [`Chat.html`](Chat.html) | The chat page (`chatMain`) |
| [`ChatTest.html`](ChatTest.html) | Test page that hosts `Chat.html?id=Simple.Generic` in an iframe, with buttons that send `postMessage` commands |
| [`chat.js`](chat.js) | The chat client: message list, input, menus, speech input, translation, file and link sharing |
| [`chat.css`](chat.css) | Styles of the chat UI, including the `IconChat…` icon classes |

## Relationships

- **Part of:** [SysWeaver.Chat](../../README.md)
- **Uses:** [`ChatService.cs`](../../ChatService.cs) (web APIs and push messages), the common web assets of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md)

## Behavior (web analysis)

### Loading and initialization

1. `Chat.html` loads (all `defer`) `../common/common.js`, `../app/application.js` and `chat.js`, plus `../common/theme.css`, `../app/application.css` and `chat.css`. `<body class="ChatMain" onload="chatMain()">` is empty; everything is built from script. (The `<meta §set="utf-8">` tag is not a valid charset declaration; the page relies on the server's content type.) A commented-out `marked.js` include remains; markdown rendering goes through `ValueFormat.MarkDownToHTML` in common.js.
2. `chatMain()` shows a loader (`AddLoading`), reads URL params `id` (required, the chat id), `options` (JSON merged into a `ChatOptions` with `Object.assign`) and `nobackground` (flag, adds class `NoBackground`). It creates `<SysWeaver-Chat>` in `body` and calls `Chat.addChat(chatBackground, chatId, null, options)`. Errors go to `Fail(e.message)`.
3. `Chat.addChat` (one ~2500 line async static method) does, in order:
   - cancels any ongoing `speechSynthesis`;
   - computes localStorage key names (see State);
   - creates `<SysWeaver-ChatMain>` (the scrolling message list);
   - calls `Join` `{ChatId, MaxCount: 10, FromId: 0}` and gets the session config plus the latest messages;
   - loads dynamic deps in parallel: `GetInputLanguages` (if `CanTranslate`) and, if `UploadRepo` is set, `includeJs(null, "md5.js"|"UZIP.js"|"fileUploader.js", true)`, which resolves relative to common.js (so `common/fileUploader.js` from SysWeaver.MicroService.FileUploader). Upload is enabled only if `fileUploader` ends up defined;
   - if `CanPost`: builds the input area, the preview message, drag and drop, the language picker, speech input and upload handlers, and restores the draft;
   - registers `postMessage` commands, server push events and speech synthesis;
   - adds the top-right menu `ColorIcon`;
   - renders the initial messages, installs `StickToBottom` with lazy loading of older messages, restores the saved split height, and scrolls to the bottom (`StickForAwhile(true)`), then focuses the input (`doLast`).

### Files

#### `Chat.html`
The host page described above. The `body.ChatMain` CSS makes it full-viewport (`100svh`, `html.Embedded` shrinks by 8px when embedded in the app shell).

#### `ChatTest.html`
A standalone test harness (no SysWeaver scripts). It embeds `Chat.html?id=Simple.Generic&options={"UserColor":false}` in an iframe and has buttons that call `iframe.contentWindow.postMessage({Type: "chat.clear" | "chat.scrolltop" | "chat.scrollbottom" | "chat.showall" | "chat.hideall"})`.

#### `chat.js`
Classic script (no modules). Four top-level classes and one entry function, all global.

**`SpeechGen`** (static only): text-to-speech on top of `window.speechSynthesis`.
- `supportSpeechGen()`: true if `speechSynthesis` exists.
- `findVoice(language, male, exact)`: scores every `getVoices()` entry. Language similarity counts ×10, then gender similarity (voice names in `MaleNames`/`FemaleNames`, e.g. "George", "Hazel", are rewritten to "male"/"female" first), then +0.8 for `localService`. An exact name match on a local voice wins outright.
- `filterText(text)`: makes text speakable. It keeps letters, digits and `! ? . ,`, collapses whitespace, joins digit groups, drops thousands commas after digits, turns math operators between numbers into words (`MathOps`: "plus", "times", ...), and replaces `(` with ", ". Uses `isLetter` from common.js.
- `speakOnce(...)`, `speakNew(text, voice, pitch, rate, volume, lang)` and `speakContinue(text, isCompleted)` support incremental speech of a streaming message. `speakNew` stores an utterance initializer in `window.SpeakCurrentInit` and resets `window.SpeakCurrentText`. `speakContinue` speaks only the new suffix up to the last sentence end char (`EndChars`), or everything when `isCompleted`. While already speaking, it appends to `window.SpeakCurrentQueue`, which `onSpeakEnd` flushes. If the new text no longer starts with the spoken prefix, speech is cancelled and restarted. `cancelSpeak()` clears the queue.

**`SpeechInput`** (static only): speech recognition via `SpeechRecognition`/`webkitSpeechRecognition` plus `SpeechGrammarList`.
- `supportSpeechRec()`; `supportOpenMic()` (false on mobile, via `isMobile()`).
- `triggerOnSpeech(keyword, onActivate, onSentance, language, ignoreKeyword, noOpenMic)` returns a `stop` function, or null when unsupported.
  - Keywords: entries like `"name|prefix"`. The part before `|` is the wake word and the part after is a prefix added to the produced sentence. A JSGF grammar is built from them.
  - Recognition is continuous with interim results. On `onend` it restarts (open mic), or stops when `noOpenMic`. If `start()` throws (no user gesture yet), it retries on the common.js `UserInteracted` document event.
  - Each result checks for a keyword (or accepts anything when `ignoreKeyword`) and calls `onActivate(key, true)`. Interim text is streamed through `onSentance(filter(...), false)`.
  - On a final result: if the last word is a stop word (`abort/stop/cancel/reset/restart`), the input is aborted. Otherwise a 1.5 s debounce timer delivers `onSentance(text, true)`.
  - `filter()` strips the keyword, capitalizes sentences, turns `.` into `?` when the sentence contains a question word (`QuestionWordMap`, `QuestionWordStartMap` for the first word), and ensures the text ends with ". " or "? ".

**`ChatOptions`**: `MenuIconSize`, `AudioIconSize`, `SendIconSize` (48), `UserColor` (true: per-user colors), `SettingsName` (null: shared settings for all chats; `"*"`: per chat id; any other string: a named settings group).

**`Chat`**:
- Statics: `FlagIsWorking = 1` (the message is still being produced, e.g. AI streaming) and `FlagCanRemove = 256`; these mirror the C# `ChatMessageFlags`. Also `IsMobile`, `Platform`, and `chatColor(text)`, which hashes the user name with `legacySha256` into three HSL colors (border, header background, header text), cached in `window.ChatColors`.
- `static async addChat(chatBackground, chatId, apiBase, options)` returns the `SysWeaver-ChatMain` element. `apiBase` defaults to `""`, so API calls are page-relative (see Server interaction). Everything else is closures inside this method:
  - **Input** (only when `CanPost`): `<SysWeaver-ChatInput>` holds a `<textarea>`, the send `ColorIcon` (`IconSend`) and, if speech recognition is supported, a mic `ColorIcon` (`IconChatMicOn`; CSS targets it as `IconButton:nth-child(3)` and swaps its mask to `ChatInput.svg`/`ChatRecording.svg` when disabled or working).
    - `Send(doFocus)`: posts `UserMessage`, with input history (last 100 entries, browsed with Alt+Up/Down) and Alt/Option+Enter to send. While sending it shows the text as the placeholder and makes the input read-only. On failure the text and data are restored. On success it clears `tempFiles` and calls `StickForAwhile()`.
    - `sendEnable()`: toggles the send button, mirrors the draft into localStorage and updates the preview.
    - The input's background image is the current language flag `../iso_data/language/<lang>.svg`, and `maxLength` is `MaxTextLength`.
  - **Preview message**: a pseudo message (`Id: 0`, `FromImage: ../auth/UserImages/Current/small`), rendered through the same `addMessage(..., onTop=true, isPreview=true)` and given class `Preview` (CSS draws a dashed "Preview" header). It is hidden (`Hide` class) unless it has data, or has text and preview is on. Preview defaults to on for markdown and off for text; `showPreview` is tri-state (null = auto). `setPreviewText`, `markDownChanged` and `previewChanged` keep it in sync.
  - **Drag and drop** on the textarea: files are uploaded (if enabled). The first string item is inserted at the cursor (`insertAtCursor`); in markdown mode, http(s) URLs with an image extension (`imageExtensions` from common.js) are wrapped as `![](url)`. Class `Dragging` gives visual feedback.
  - `selectLanguage()`: a `PopUpSelection` (app shell) with search over `Name/LocalName/EnName`. Items are `<SysWeaver-ChatLangItem>` with a flag background. Selecting one sets `currentLang`, persists it only when it differs from the server default, refreshes the preview flag and restarts open-mic listening.
  - `uploadFiles(files)`: enforces `MaxDataCount` and `MaxDataLength`, calls the global `fileUploader(uploadRepo, files)` → `{Error, Urls, Status}`, and appends `"../" + url` entries to `previewMessage.Data` (`;`-separated). Newly uploaded (not `UploadStatus.AlreadyUploaded`) URLs are remembered in `tempFiles` so they can be deleted again (`../Api/UserStorage/DeleteStoredFile`) when removed from the draft or on clear (`clearCurrentMessage`).
  - **Speech input**: `startListening(ignoreKeyword, noOpenMic)` and `stopListening()` wrap `SpeechInput.triggerOnSpeech` with `data.SpeechName` as keywords. The mic button is push-to-talk (`ignoreKeyword`, `noOpenMic`). Open mic (keyword triggered) starts automatically if the per-chat listen flag is true; it defaults from the server. Recognized text is inserted live (the third arg of `insertAtCursor` marks the inserted range as replaceable). When final it is sent automatically: always for open mic, and for push-to-talk only if auto-send is enabled. Class `Listening` on the textarea marks activity.
  - `addMessage(m, onTop, isPreview)` builds a row and returns its `SysWeaver-ChatContent` element:
    ```
    SysWeaver-ChatRow(.Mine|.Working|.Preview|.Hide)  (expando: Msg, Body, Translated, UpdateBody, UpdateData, UpdateLang, UpdateFormat, Show, Hide)
      SysWeaver-ChatMsg            (border color from chatColor)
        SysWeaver-ChatHeader       (click toggles hide/show for this sender; contextmenu opens menu)
          SysWeaver-ChatLanguage   (only if CanTranslate; flag img, click translates)
          SysWeaver-ChatName
          SysWeaver-ChatTime       (live "x ago" or absolute time)
          SysWeaver-ChatIcon       (user image or IconMenu ColorIcon; click opens message menu)
        SysWeaver-ChatContent      (.Hidden)
        SysWeaver-ChatData         (.Hidden; attachments)
    ```
    Rows are inserted at the top, or before the last child when posting is enabled, so the preview row always stays last.
    - `UpdateBody(text, format, isWorking)` renders by format: 0 → `innerText`, 1 → `ValueFormat.MarkDownToHTML`, 2 → raw `innerHTML`. It skips work if nothing changed and toggles the `Working` class.
    - `UpdateData(data)` splits `;`-separated URLs and builds one `<SysWeaver-ChatHolder>` per URL:
      - images → `<img>` (click opens; downloadable);
      - `.html/.htm` → sandboxed `<iframe>` (`allow-forms allow-presentation allow-scripts allow-same-origin allow-downloads allow-popups`, src passed through `appendTheme`);
      - anything else → `../icons/ext/<ext>.svg` plus a `<SysWeaver-ChatLabel>` (click downloads; label copies the URL).

      Each holder has a `<SysWeaver-ChatExpand>` menu: preview-only items (copy image markdown, copy URL, delete file, hide file, which removes it from Data and copies a markdown reference), plus "Open in new tab" and "Download". With `CanPost` it adds "Store file" (`StoreFile`, scopes Private/Protected/Public) unless the URL is already in the user's store (`../storage/<UserStore>/`), and "Create link" (`StoreLink`, scope 0/1/2) for non-absolute URLs. A stored result is copied to the clipboard and opened.
    - **Message menu** (`PopUpMenu` + `WebMenu`/`WebMenuItem` from application.js):
      - preview: attach file, copy, clear;
      - others: view profile (`parent.postMessage({Type:"ViewProfile", Name, Image}, "*")` if `CanShowProfile`), copy text/markdown/HTML depending on format, copy time stamp, copy user name, hide/show content;
      - server-defined `m.MenuItems` (via `addRec`), and remove message (if `CanClear` or the `FlagCanRemove` flag; a confirm dialog when only `CanClear`).
    - **Translation**: a click on the flag calls `Translate`, caches the result in `row.Translated`, and toggles between original and translated text (classes `Translating`, `Translated`).
    - **Hide/show per sender**: header click or menu item; persisted list of hidden sender names.
  - `addRec(dest, menuList, messageId, close)` converts server `ChatMenuItem[]` (`Id, Name, Desc, Icon, Value, Children`) recursively into `WebMenuItem`s. Leaf items call `SetValue {ChatId, Key: Id, Value, Id: messageId}`. This is the generic channel for provider-specific actions, such as AI debug options; `chat.css` defines `IconDebug*` classes for them.
  - **Top-right menu** (`IconMenu`, `<SysWeaver-Chat> > .IconButton`): scroll top/bottom (disabled when already there), show all, time vs age toggle, text/markdown toggle (if `AllowMarkDown`) with a markdown help link, show/hide preview, set input language, clear draft, attach file, auto-send toggle, open-mic toggle (lists the keywords), speech synthesis toggle, a volume submenu (25/50/75/100; flag `3` marks the selected one), server `data.Menus` via `addRec`, and "Remove ALL messages" (if `CanClear`, confirmed unless `DoNotConfirmClear`).
  - **History paging**: `StickToBottom(chatE, true, onStick, onUnstick, onTop)` from common.js toggles the `Stick` class (a CSS flash animation). When scrolled to the top it calls `GetMessages {ChatId, MaxCount: -10, FromId: <first message id>}`, prepends the results while keeping the scroll position, shows a delayed (200 ms) `IconWorking` spinner, and sets `isAtTop` once the server returns none.
  - `StickForAwhile(direct)` forces scroll-to-bottom on every animation frame for 500 ms, to cover async layout such as images and markdown.
  - Split size: `SysWeaver-ChatMain` is CSS `resize: vertical`. A `ResizeObserver` saves its height fraction, and it is restored on load.
- `chatMain()`: the entry point (see above).

#### `chat.css`
Layout uses custom elements only (`SysWeaver-Chat`, `-ChatMain`, `-ChatInput`, `-ChatRow`, `-ChatMsg`, `-ChatHeader`, `-ChatLanguage`, `-ChatName`, `-ChatTime`, `-ChatIcon`, `-ChatContent`, `-ChatData`, `-ChatHolder`, `-ChatExpand`, `-ChatLabel`, `-ChatLangItem`). It relies on theme variables from `theme.css` (`--ThemeMain`, `--ThemeAcc1/2(RGB)`, `--ThemeBackground(RGB)`, `--ThemeRad`, `--ThemeIconSize`, `--ThemeFadeDuration`, `--ThemeFont`). State classes set by JS: `NoPost`, `Stick`, `Mine`, `Working`, `Preview`, `Hide`, `Hidden`, `Translating`, `Translated`, `Listening`, `Dragging`, `Clickable`, `Extension`, `NoBackground`, and `Chat`/`ChatData` on the popup menu root (`SysWeaver-PopupMenu.Chat`). Content styling covers markdown output (headings, tables, code, blockquote). About 50 `.Icon*` classes map to masks in `../icons/` (shared) and `icons/` (chat-local).

#### `icons/`
Mask SVGs (see `icons/README.md`). The `.opt` copies are SvgOpt output.

### Server interaction

All endpoints are on `ChatService` (`[WebApiUrl("../chat")]`), so they live at `/chat/<Method>`, next to the page. That is why `apiBase` is `""` and calls are page-relative (`sendRequest("Join", ...)`). All are POST JSON through `sendRequest` from common.js.

| Call | Request | Response / use |
|---|---|---|
| `Join` | `{ChatId, MaxCount:10, FromId:0}` | `ChatJoinResponse`: `UserName, UserGuid, UserStore, Lang, MaxTextLength, MaxDataLength, MaxDataCount, Messages[], CanClear, CanPost, DoNotConfirmClear, Menus[], SpeechName[], Voices[], AllowMarkDown, CanTranslate, CanShowProfile, UploadRepo, AllowPublicStore` (the JS also reads `EnableListenByDefault` and `EnableGenByDefault`; the C# type declares `EnableSpeechByDefault`) |
| `GetMessages` | `{ChatId, MaxCount:-10, FromId}` | older messages (negative count = before `FromId`) |
| `UserMessage` | `{ChatId, Body:{Format, Data, Text, Lang}}` | bool |
| `Translate` | `{ChatId, MessageId}` | translated text (server cached per message+language) |
| `RemoveMessage` | `{ChatId, MessageId}` | bool |
| `ClearAllMessages` | `chatId` (plain string) | bool |
| `SetValue` | `{ChatId, Key, Value, Id}` | bool (server menu actions) |
| `StoreFile` / `StoreLink` | `{Url, Scope}` (0 private, 1 protected, 2 public) | stored URL, made absolute with `GetAbsolutePath` |
| `GetInputLanguages` | none | `LanguageInfo[] {Iso, Name, LocalName, EnName, Comment}` |
| `../Api/UserStorage/DeleteStoredFile` | url without the leading `../` | bool (cleans up uploaded temp files) |

Uploads go through the global `fileUploader()` (FileUploader project) into the `UploadRepo` repository.

**Message model** (`ChatMessage`): `Id, From, FromImage, Time, Flags, Text, Data, Format (0 text, 1 markdown, 2 HTML), Lang, MenuItems`. `Data` is a `;`-separated list of relative URLs. Server-generated attachments are served under `chat/file/...` by `ChatService`; other projects build URLs into it, for example ChartJs produces `../chart/chart.html?q=../chat/...`, which is shown as an iframe.

**Push**: the chat registers four session events with `SessionManager.AddServerEvent(name)`: `chat.post.<id>`, `chat.replace.<id>`, `chat.remove.<id>` and `chat.clear.<id>`, where `<id>` is `chatId` lowercased (the chat id is `<provider>.<providerChatId>`, and the C# side builds `chat.<op>.<provider>.<providerChatId>` lowercased). The top window's SessionManager long-polls `Api/application/GetMessages` and fans messages out over `BroadcastChannel` (`InterOp`). The chat listens with `InterOp.AddListener` / `InterOp.GetMessage`:
- `post` `{Type, Chat: ChatMessage}`: append unless the id already exists. It is spoken if speech is enabled and the message is from another user; the voice comes from `Voices` by `From`, falling back to the entry with a null `Name`.
- `replace` `{Chat}`: replace `Msg` and re-render the body and data. This is how streaming/AI output updates in place, with `FlagIsWorking` set until done. Speech continues incrementally.
- `remove` `{Value: messageId}`: remove the row.
- `clear`: empty the list but keep the preview row.

**postMessage from the parent window** (`window` `message` event, `{Type}`): `chat.scrolltop`, `chat.scrollbottom`, `chat.showall`, `chat.hideall`, and `chat.clear` (only registered if `CanClear` and `CanPost`). The origin is logged, not checked. Outgoing: `parent.postMessage({Type:"ViewProfile", Name, Image}, "*")`.

**Images**: user images come from `../auth/UserImages/<guid>/small` (the current user's own image is rewritten to `.../Current/small`), flags from `../iso_data/language/<iso>.svg`, and extension icons from `../icons/ext/<ext>.svg`.

### State and persistence

localStorage keys. `<s>` is `""`, `"." + chatId` or `"." + SettingsName`, depending on `ChatOptions.SettingsName`:
- Settings: `SysWeaver.Chat.ShowTimeStamp<s>`, `SysWeaver.Chat.SpeechGenVol<s>`, `SysWeaver.Chat.SpeechGen<s>`, `SysWeaver.Chat.SpeechSend<s>` (auto-send), `SysWeaver.Chat.UseMarkDown<s>`, `SysWeaver.Chat.Preview<s>` (stored as `"true"`/`"false"`/`"null"`), `SysWeaver.Chat.Size<s>` (height fraction).
- Always per chat: `SysWeaver.Chat.SpeechListen<chatId>` (no dot), `SysWeaver.Chat.Hide.<chatId>` (JSON array of hidden sender names), `SysWeaver.Chat.Input.<chatId>` (draft text), `SysWeaver.Chat.Data.<chatId>` (draft attachments), `SysWeaver.Chat.Lang.<chatId>` (input language override).

Globals on `window`: `ChatColors`, `SpeakCurrentText`, `SpeakCurrentQueue`, `SpeakCurrentInit`. In-memory state: message rows hold the state as expando properties (`row.Msg`, `row.Translated`), `tempFiles` (a Map of uploaded URLs not yet sent), input history, `isAtTop`/`isLoading` paging flags.

### Dependencies

- **common.js**: `sendRequest`, `getUrlParams`, `AddLoading`, `Fail`, `Confirm`, `_T`/`_TF` (i18n), `ColorIcon`, `includeJs`, `SessionManager`, `InterOp`, `StickToBottom`, `IsAtMaxScrollV`, `ValueFormat` (`MarkDownToHTML`, `updateDateTime(Live)`, `copyToClipboardInfo`, `copyOnClick`, `stringFormat`), `legacySha256`, `isLetter`, `isMobile`, `getPlatform`, `insertAtCursor`, `imageExtensions`, `createImg`, `createIFrame`, `appendTheme`, `SetImageSource`, `GetAbsolutePath`, `Open`, `downloadFile`, `badClick`, `keyboardClick`, and the `UserInteracted` event.
- **application.js**: `PopUpMenu`, `PopUpSelection`, `WebMenu`, `WebMenuItem` (the shell is loaded for the menu and popup machinery; the page does not call `PageLoaded`).
- **Optional** (FileUploader project, loaded on demand into `common/`): `fileUploader`, `fileUploaderStatusText`, `UploadStatus`, `selectFiles`, plus `md5.js` and `UZIP.js`.
- **Consumers**: no in-repo page links to `Chat.html`. It is meant to be embedded by host apps (iframe, `postMessage` control, `?id=`, `?options=`, `?nobackground`). `Chat.addChat` can also be called directly from another page that includes `chat.js`; that is what `apiBase` is for. Providers (SimpleChatService, MySQL rooms, AI chats) differ only in server data (flags, menus, voices, attachments).

### Structural observations

- Almost the whole client is one closure-heavy async method (`Chat.addChat`, ~2500 lines). Helpers are declared with `let` and assigned later inside `if (canPost)` (`selectLanguage`, `uploadFiles`, `previewChanged`, ...), so code outside that block depends on them being set. The non-posting path checks `previewElement`/`uploadRepo` before using them.
- The DOM rows double as the state store (expando methods and properties on custom elements). `findMessageE` does a linear scan of `chatE.children` by `Msg.Id`.
- There is heavy duplication: the Store file and Create link menu blocks repeat the same try/`sendRequest`/`GetAbsolutePath`/copy/`Open` body six times with different scopes (the "Public" store item sends `Scope: 1`), the "attach file" item exists in both the preview menu and the main menu, and the `Fail(... + "Error:" ...)` message pattern is repeated many times.
- Speech classes keep their state in `window` globals (only one active TTS stream per page). `SpeechInput` and `SpeechGen` are general-purpose and could live outside the chat.
- Implicit contracts: numeric `Format` and `Flags` values mirror C# enums; push event names must match `ChatService.Controller.MessageName` (case-folded); `Data` is a `;`-separated string of `../`-prefixed URLs, and the code strips `../` with `substring(3)`; `WebMenuItem.Flags` bit 1 = disabled, bit 2 = checked (so `3` = disabled+checked); `ChatInput` CSS relies on the mic being the 3rd child; `ChatJoinResponse` field names are read loosely, including names that are not on the C# type.
- Message HTML (format 2) and markdown output are injected as `innerHTML`; HTML attachments run in a sandbox that allows scripts and same-origin.
- Classic globals, no modules; `includeJs` handles runtime loading.

## JavaScript reference

Conventions used in this reference: "async" marks functions returning a Promise; "inner" marks functions local to an enclosing function (closures); "expando" marks functions stored as properties on DOM elements.

### `chat.js`

Classic (non-module) script loaded with `<script defer src="chat.js">` by `Chat.html`; it can also be included by any other page that wants to call `Chat.addChat`. It depends on `common.js` (`sendRequest`, `ColorIcon`, `ValueFormat`, `InterOp`, `SessionManager`, `StickToBottom`, `isMobile`, `getPlatform`, `isLetter`, `legacySha256`, ...) and `application.js` (`PopUpMenu`, `PopUpSelection`, `WebMenu`, `WebMenuItem`), which must be loaded first. It defines four global classes (`SpeechGen`, `SpeechInput`, `ChatOptions`, `Chat`) and the global entry function `chatMain`. Static field initializers run at load time and call `isMobile()` / `getPlatform()` from common.js.

#### Globals

| Name | Kind | Description |
|---|---|---|
| `SpeechGen` | class | Static-only text-to-speech helper (see below). |
| `SpeechInput` | class | Static-only speech recognition helper (see below). |
| `ChatOptions` | class | Options bag for `Chat.addChat`. |
| `Chat` | class | Chat UI factory (`Chat.addChat`). |
| `chatMain` | function | Page entry point for `Chat.html` (`<body onload="chatMain()">`). |
| `window.ChatColors` | window global (Map) | Cache `user name -> [border, headerBackground, headerText]` colors, created lazily by `Chat.chatColor`. |
| `window.SpeakCurrentText` | window global (string) | The filtered text of the current streamed message that has already been handed to speech synthesis. Written by `SpeechGen.speakNew` / `speakContinue`. |
| `window.SpeakCurrentQueue` | window global (string) | Text waiting to be spoken once the current utterance ends. Appended by `speakContinue`, flushed by `onSpeakEnd`, cleared by `cancelSpeak`. |
| `window.SpeakCurrentInit` | window global (function) | `u => {...}` initializer that applies lang/voice/pitch/rate/volume/onend to a `SpeechSynthesisUtterance`; set by `speakNew`, used by `speakContinue` and `onSpeakEnd`. |

#### Functions

| Function | Signature | Description |
|---|---|---|
| `chatMain` | async `chatMain()` → Promise&lt;void&gt; | Shows a loader (`AddLoading`), reads URL params `id` (required, else throws "No id supplied!"), `options` (JSON merged into a new `ChatOptions` via `Object.assign`) and `nobackground` (adds class `NoBackground`). Creates `<SysWeaver-Chat>` in `document.body` and awaits `Chat.addChat(chatBackground, chatId, null, options)`. Errors are shown with `Fail(e.message)`; the loader is always removed. |

#### class `SpeechGen`

Static-only wrapper around `window.speechSynthesis`, supporting one-shot speech and incremental speech of a message that is still being streamed. All state is kept in the `window.Speak*` globals, so there is a single TTS stream per page. Never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `supportSpeechGen` | static method | `supportSpeechGen()` → bool | True if `window.speechSynthesis` exists. |
| `MaleNames` | static field | `string[]` | `["George", "Bengt"]`: substrings of voice names replaced by "male" before gender scoring. |
| `FemaleNames` | static field | `string[]` | `["Hazel", "Susan"]`: substrings replaced by "female". |
| `similarity` | static method | `similarity(s1, s2)` → number | Case-insensitive: if the shorter string occurs in the longer one at index `f`, returns `1 / (1 + 0.02*f)`, else 0. |
| `findVoice` | static method | `findVoice(language, male, exact)` → `SpeechSynthesisVoice` or null | Scores every `getVoices()` entry as `similarity(language, voice.lang)*10 + similarity("male"/"female", fixedName) + (localService ? 0.8 : 0)` and returns the best. If `exact` equals a voice name and that voice is local, its score is 10000. |
| `EndChars` | static field | `Map<char,true>` | `! ? . ,`: phrase end characters; while a message is incomplete `speakContinue` only speaks up to the last of these. |
| `ValidChars` | static field | `Map<char,true>` | `! ? . ,`: punctuation kept by `filterText`. |
| `WhiteSpaces` | static field | `Map<char,true>` | Space, tab, `\n`, `\r`. |
| `MathOps` | static field | `Map<char,string>` | `* + - / % ^ = ≈` → " times ", " plus ", " minus ", " divided by ", " modulo ", " to the power of ", " equals ", " is approximately ". |
| `filterText` | static method | `filterText(text)` → string | Produces speakable text: collapses whitespace (and removes whitespace between two digits), keeps digits, `ValidChars` (a comma directly after a digit is dropped) and letters (`isLetter`); turns a math operator followed by a number into words (if preceded by a number, or always for `-`); replaces `(` with ", "; drops everything else. |
| `NextIsNumber` | inner function of `filterText` | `NextIsNumber(pos)` → bool | Skipping whitespace, true if the next non-white character after `pos` is a digit. |
| `PrevIsNumber` | inner function of `filterText` | `PrevIsNumber(pos)` → bool | Same, scanning backwards before `pos`. |
| `onSpeakEnd` | static method | `onSpeakEnd()` | `onend` handler of every utterance: if `SpeakCurrentQueue` has non-empty trimmed text, clears the queue and speaks it as a new utterance initialized by `SpeakCurrentInit`. |
| `cancelSpeak` | static method | `cancelSpeak()` | Clears `SpeakCurrentQueue` and calls `speechSynthesis.cancel()` if currently speaking. |
| `speakOnce` | static method | `speakOnce(text, voice, pitch, rate, volume, lang)` | Filters `text`, builds one utterance (`lang` default "en", optional `voice`, `pitch`, optional `rate`, `volume` default 0.5, `onend = onSpeakEnd`), cancels any current speech and speaks it. Returns without speaking when the filtered text is empty. Not called inside this file. |
| `speakNew` | static method | `speakNew(text, voice, pitch, rate, volume, lang)` | Starts a new incremental stream: filters the text, resets `SpeakCurrentText` to "", stores an utterance initializer with the given settings in `SpeakCurrentInit`, then calls `speakContinue(text)`. Used for newly posted messages. |
| `speakContinue` | static method | `speakContinue(text, isCompleted)` | Filters `text`; if it no longer starts with `SpeakCurrentText`, resets it and cancels speech. Unless `isCompleted`, returns while already speaking and only takes the new suffix up to the last `EndChars` character. Appends the taken part to `SpeakCurrentText`, then either appends it to `SpeakCurrentQueue` (if speaking) or speaks it immediately (cancelling first when it is the first chunk). Used for `replace` push events of streaming messages. |

#### class `SpeechInput`

Static-only wrapper around `SpeechRecognition` / `webkitSpeechRecognition` with `SpeechGrammarList`; `triggerOnSpeech` creates a recognizer and returns its stop function. Never instantiated.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `IsMobile` | static field | bool | `isMobile()` evaluated at load. |
| `supportOpenMic` | static method | `supportOpenMic()` → bool | `!IsMobile` (continuous keyword listening is disabled on mobile). |
| `supportSpeechRec` | static method | `supportSpeechRec()` → bool | True if both a `SpeechRecognition` and a `SpeechGrammarList` constructor (standard or webkit-prefixed) exist. |
| `StopWords` | static field | `Map<string,1>` | `abort, stop, cancel, reset, restart`: a final result whose last word is one of these aborts the current input. |
| `NonWordChars` | static field | `Map<char,1>` | Whitespace and `! ? . , ¡ ¿`, used by `GetLastWord`. |
| `QuestionWordMap` | static field | `Map<string,1>` | who/what/where/when/why/which/whose/how and their "'s" forms: any occurrence makes a sentence a question. |
| `QuestionWordStartMap` | static field | `Map<string,1>` | are/aren't/am/is/isn't/do/don't/does/doesn't: makes a sentence a question only as its first word. |
| `GetLastWord` | static method | `GetLastWord(text)` → string | Returns the last run of non-`NonWordChars` characters (case preserved), or "". |
| `triggerOnSpeech` | static method | `triggerOnSpeech(keyword, onActivate, onSentance, language, ignoreKeyword, noOpenMic)` → async stop function, or null if unsupported | `keyword`: string or array of `"wakeword|prefix"` entries (wake word lowercased; prefix prepended to produced text). `onActivate(key, isWorking)`: called with the detected keyword on activation, `(null, true)` after a sentence completes in open-mic mode, `(null, false)` when stopped. `onSentance(text, isFinal)`: interim (`false`) and final (`true`) filtered text. `language` default "en-US". `ignoreKeyword`: any speech activates (first keyword is used). `noOpenMic`: stop after one sentence instead of restarting. Builds a JSGF grammar `public <keyword> = k1,k2...`, sets `continuous`, `interimResults`, `maxAlternatives = 1`, and starts. If `start()` throws (no user gesture yet), retries on the `UserInteracted` document event. With `noOpenMic` it calls `onActivate(true, true)` immediately. `onend` restarts (open mic) or stops (`noOpenMic`); `onerror` ignores "no-speech", treats "aborted" as stop (`noOpenMic`) or deactivation, and stops on other errors (`console.warn`). `onresult`: detects the keyword once per sentence, streams `filter(part)` as interim text, and on a final result either aborts on a stop word (`onSentance("", true)`) or starts a 1500 ms timer that delivers `onSentance(filter(final), true)`; new results reset the timer. On non-mobile, consecutive final results of one activation are joined with ". ". |
| `haveKeyword` | inner function of `triggerOnSpeech` | `haveKeyword(str)` → string or null | First keyword contained in `str` (lowercased); with `ignoreKeyword` returns the first keyword when none matches. |
| `start` | inner function (const) of `triggerOnSpeech` | `start()` | `recognition.start()` and removes itself as a `UserInteracted` listener. |
| `stop` | inner async function (const) of `triggerOnSpeech` | `stop()` → Promise | Detaches handlers, stops the recognizer, nulls it and calls `onActivate(null, false)`. No-op if already stopped. This is the returned function. |
| `hasWhiteSpace` | inner function of `triggerOnSpeech` | `hasWhiteSpace(s)` → bool | Regex `\s` test. |
| `isChar` | inner function of `triggerOnSpeech` | `isChar(s)` → bool | Unicode letter test at the start of `s`; not used. |
| `isQuestionWord` | inner function of `triggerOnSpeech` | `isQuestionWord(word, isFirstWord)` → bool | Checks `QuestionWordStartMap` (first word only) and `QuestionWordMap`. |
| `filter` | inner function of `triggerOnSpeech` | `filter(text, keyw)` → string | Removes text up to and including the keyword, trims, capitalizes the first letter of each sentence, replaces `.` with `?` for question sentences, strips trailing space/`.`/`!`/`?`, appends "? " or ". ", and prepends the keyword's prefix. Returns "" for empty text. |

#### class `ChatOptions`

Plain options object; instances are created with `new ChatOptions()` (by `chatMain`, or by `addChat` when `options` is omitted) and may be overridden via `Object.assign`.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `MenuIconSize` | field | number = 48 | Pixel size of the top-right menu icon (scaled by theme). |
| `AudioIconSize` | field | number = 48 | Pixel size of the microphone button. |
| `SendIconSize` | field | number = 48 | Pixel size of the send button. |
| `UserColor` | field | bool = true | Color each message border/header by sender (`Chat.chatColor`). |
| `SettingsName` | field | string or null = null | localStorage settings scope: null → shared by all chats, `"*"` → per chat id, other string → named group (key suffix `"." + name`). |

#### class `Chat`

Static-only factory. `Chat.addChat` builds the whole UI for one chat inside a container element and returns the message list element; everything else is closures inside it.

| Member | Kind | Signature | Description |
|---|---|---|---|
| `FlagIsWorking` | static field | 1 | Message flag: the message is still being produced (streaming); rows get class `Working`. |
| `FlagCanRemove` | static field | 256 | Message flag: the current user may remove the message (no confirmation). |
| `chatColor` | static method | `chatColor(text)` → string[3] | Hashes `text` (the sender name) with `legacySha256` into hue/saturation/lightness and returns `["hsl(... / 0.5)", "hsl(... / 0.05)", "hsl(...)"]` (border, header background, header text). Cached in `window.ChatColors`. |
| `IsMobile` | static field | bool | `isMobile()` at load; on mobile no "ALT+Enter" hint is added to the placeholder. |
| `Platform` | static field | string | `getPlatform()` at load; a value starting with "mac" selects the "Option+Enter" hint. |
| `addChat` | async static method | `addChat(chatBackground, chatId, apiBase, options)` → Promise&lt;HTMLElement&gt; | `chatBackground`: container (normally `<SysWeaver-Chat>`); `chatId`: chat id (`provider.chatId`); `apiBase`: prefix for API calls (default "", page-relative); `options`: `ChatOptions` (default new). Cancels speech, creates `<SysWeaver-ChatMain>`, calls `Join`, loads optional dependencies, builds input/preview/menus when `CanPost`, registers postMessage commands and push events, adds the top-right menu, renders initial messages, installs history paging, and returns the `SysWeaver-ChatMain` element. See inner functions below and the Behavior section above. |

#### Inner functions of `Chat.addChat`

Closure state used by these functions: `chatE` (`SysWeaver-ChatMain`), `data` (`Join` response), `canPost`, `canTranslate`, `userName`, `currentImage`, `currentLang`, `useMarkDown`, `showPreview` (null/true/false), `canSend`, `previewMessage`, `previewElement`, `uploadRepo`, `tempFiles` (Map of uploaded-but-unsent URLs), `hideFrom` (Map of hidden senders), `commandMap` (postMessage commands), `showTimeStamp`, `enableSpeech`, `speechVolume` (0-100, default 50), `userVoices`, `isAtTop`, `isLoading`, `languages` / `languageMap`, and the localStorage key names listed under State and persistence. Functions declared with `let` (e.g. `uploadFiles`) are only assigned when `canPost`.

| Function | Signature | Description |
|---|---|---|
| `GetLanguages` | async `GetLanguages()` → Promise | Once only: `sendRequest("GetInputLanguages")` into `languages` and builds `languageMap` (Iso → LanguageInfo). Awaited at startup when `CanTranslate`, and lazily by `selectLanguage`. |
| `isHidden` | `isHidden(name)` → bool | True if sender `name` is in `hideFrom`. |
| `setHideFrom` | `setHideFrom(name, hide)` | Adds/removes `name` in `hideFrom` and stores the key list as JSON in `SysWeaver.Chat.Hide.<chatId>`. |
| `setHideNone` | `setHideNone()` | Clears `hideFrom` and removes the localStorage key. |
| `doLast` | `doLast()` (let, default no-op) | Run at the end of `addChat`. With `canPost`: `sendEnable()`, then focuses the textarea (temporarily read-only, so no on-screen keyboard is forced). |
| `Send` | async `Send(doFocus)` → Promise | Sends the draft. Returns if text and data are empty or already sending. Pushes the text into the input history (max 100), clears the input (showing the sent text as placeholder, read-only), clears preview data/text, and posts `UserMessage {ChatId, Body:{Format: useMarkDown?1:0, Data, Text, Lang: currentLang}}`. On success clears `tempFiles` and calls `StickForAwhile()`; on false/exception shows `Fail(...)` and restores text/data if the input is still empty. Finally restores the input and placeholder, calls `sendEnable()` and focuses unless `doFocus === "NoFocus"`. Bound to the send `ColorIcon`, Alt+Enter and speech auto-send. |
| `SetFromHistory` | `SetFromHistory()` | Sets the textarea from `inputHistoryPos` (0 = the text being typed, -1 = empty, n = n-th last history entry), updates `write.NextLast` and calls `sendEnable()`, flagging the change as internal. |
| `sendEnable` | `sendEnable()` | Recomputes `canSend` (trimmed text or data present), saves or removes the draft keys `SysWeaver.Chat.Input.<chatId>` / `SysWeaver.Chat.Data.<chatId>`, calls `setPreviewText(canSend ? text : null)` and enables/disables the send button. |
| `onInputChangeFn` | `onInputChangeFn()` (const) | Textarea `oninput` handler (also called after drops): `sendEnable()` and, unless the change came from history browsing, resets the history position and remembers the current text. |
| `setPreviewText` | async `setPreviewText(newText)` → Promise | If the preview text changed, stores it on `previewElement.Msg`, re-renders the preview body and, if the preview is visible (`previewChanged()`), smooth-scrolls to the bottom. |
| `previewChanged` | `previewChanged()` → bool (let) | Shows the preview row (removes `Hide`, scrolls to bottom) if it has data, or has text and preview is on (`showPreview`, or `useMarkDown` when `showPreview` is not a boolean); otherwise adds `Hide`. Returns the visibility. |
| `markDownChanged` | async `markDownChanged()` → Promise (let) | Applies `useMarkDown` to the preview message format and to the textarea placeholder (text vs markdown, plus the platform send hint), re-renders the preview and calls `previewChanged()`. |
| `selectLanguage` | async `selectLanguage()` → Promise (let) | Opens `PopUpSelection` with a searchable list (Name/LocalName/EnName) of `<SysWeaver-ChatLangItem>` elements (flag background, tooltip with names, ISO code and comment). Clicking one sets `currentLang`, stores it in `SysWeaver.Chat.Lang.<chatId>` (removed when equal to the server default `data.Lang`), updates the input flag background and the preview flag (`UpdateLang`), restarts open-mic listening if enabled, and closes the popup. |
| `clearCurrentMessage` | async `clearCurrentMessage()` → Promise (let) | Clears the draft. Deletes every `tempFiles` entry referenced by the preview data via `../Api/UserStorage/DeleteStoredFile` (failures are logged with `console.warn`). Then clears the preview data, the textarea and preview text, and calls `previewChanged()` and `sendEnable()`. |
| `defStopListening` | `defStopListening()` (const) | Default `stopListening`: re-enables the mic button. |
| `stopListening` | `stopListening()` (let) | Initially `defStopListening`; after a successful `startListening` it stops the mic's working state, re-enables it and calls the recognizer's stop function. |
| `startListening` | `startListening(ignoreKeyword, noOpenMic)` (let) | Calls `SpeechInput.triggerOnSpeech(data.SpeechName, onActivate, onSentance, currentLang, ignoreKeyword, noOpenMic)`. Disables the mic button in open-mic mode. Activation toggles class `Listening` on the textarea and the mic button's working state; deactivation stores `"false"` in the listen key (open mic only) and calls `stopListening()`. Sentences are inserted with `insertAtCursor(write, text, !isLast)`; a final non-empty sentence is sent with `Send("NoFocus")` in open-mic mode, or in push-to-talk mode when `SysWeaver.Chat.SpeechSend<s>` is "true"; otherwise `sendEnable()`. The mic button calls `startListening(true, true)`. |
| `uploadFiles` | async `uploadFiles(files)` → Promise (let) | Fails if the attachment count would exceed `data.MaxDataCount`; calls `fileUploader(uploadRepo, files)`; on `Error` or no URLs shows `Fail`. For each URL: shows `fileUploaderStatusText` for negative status, adds non-`AlreadyUploaded` URLs to `tempFiles`, appends `"../" + url` to the `;`-separated data unless that exceeds `data.MaxDataLength` (then `Fail` and stop). Updates `previewMessage.Data`, the preview attachments, `previewChanged()` and `sendEnable()`. Used by drop and the "Attach file" menu items. |
| `cmdClear` | async `cmdClear()` → Promise (let) | `sendRequest("ClearAllMessages", chatId)`; `Fail` on false/exception. Registered as the `chat.clear` postMessage command when `CanClear` (and `canPost`), and used by "Remove ALL messages". |
| `canListen` | `canListen()` → bool | Speech recognition supported and open mic supported (not mobile). |
| `canSpeak` | `canSpeak()` → bool | Server sent `Voices` and speech synthesis is supported. |
| `getLangText` | `getLangText(langId)` → string | `"Name [iso]"` from `languageMap`, or `"[iso]"` if unknown (requires `languageMap`, i.e. `CanTranslate`). |
| `addRec` | `addRec(dest, menuList, messageId, close)` → array | Recursively converts server `ChatMenuItem[]` (`Id, Name, Desc, Icon, Value, Children`) into `WebMenuItem`s appended to `dest` (created if null). Leaf items call `SetValue {ChatId, Key: Id, Value, Id: messageId ?? 0}`, show `Fail` on failure, then `close()`. Returns `dest`. |
| `addMessage` | async `addMessage(m, onTop, isPreview)` → Promise&lt;HTMLElement&gt; | Creates a `SysWeaver-ChatRow` for message `m` (inserted first if `onTop`, else before the last child when `canPost` so the preview stays last, else appended), with `SysWeaver-ChatMsg`, header (`-ChatLanguage` if translating, `-ChatName`, `-ChatTime`, `-ChatIcon`), `-ChatContent` and `-ChatData`. Adds class `Mine` for own messages, sender colors (if `UserColor`), `Hidden` for hidden senders, header click (hide/show + `setHideFrom`, not for preview) and context menu. The sender image uses `SetImageSource` and falls back to an `IconMenu` `ColorIcon`. Attaches expando members `Msg`, `Body`, `Translated`, `UpdateLang`, `UpdateBody`, `UpdateData`, `UpdateFormat`, `Show`, `Hide` (below), renders time, body and data, and returns the `SysWeaver-ChatContent` element. |
| `onMenuClick` | async `onMenuClick(ev)` (const, inner of `addMessage`) | Opens the message `PopUpMenu` at the sender icon (root gets class `Chat`; a `ResizeObserver` on `body` keeps it aligned). Items: preview → "Attach file" (if uploads), copy items, "Clear"; others → "View profile" (`parent.postMessage({Type:"ViewProfile", Name, Image}, "*")` if `CanShowProfile`), copy text / markdown / HTML (by `Format`; HTML items hidden on mobile), copy time stamp, copy user name, hide/show content, server `m.MenuItems` via `addRec`, and "Remove message" (`RemoveMessage {ChatId, MessageId}`; confirmation dialog unless the message has `FlagCanRemove`). Bound to the icon click and to `contextmenu` on header and image. |
| `Copy` | async `Copy(text)` (inner of `onMenuClick`) | `ValueFormat.copyToClipboardInfo(text)` then closes the menu. |
| `row.UpdateLang` | `UpdateLang()` (expando) | With translation: sets the flag image to `m.Lang` and the tooltip. Preview row: flag click opens `selectLanguage`. Other rows: flag click toggles between the original and the translation from `Translate {ChatId, MessageId}` (cached in `row.Translated`), toggling classes `Translating` (while requesting) and `Translated`, and switching the flag to `data.Lang`. Without translation it is a no-op. |
| `row.UpdateBody` | async `UpdateBody(newText, newFormat, isWorking)` (expando) | Toggles class `Working` on the row; if text/format changed, renders into `SysWeaver-ChatContent`: 0 → `innerText`, 1 → `innerHTML = await ValueFormat.MarkDownToHTML(text)`, 2 → raw `innerHTML`, with a format tooltip; falls back to `innerText` on error. Stores `CurrentText` / `CurrentFormat` on the content element. |
| `row.UpdateData` | `UpdateData(newData)` (expando) | Rebuilds `SysWeaver-ChatData` from the `;`-separated URL list (skips if unchanged). Per URL a `<SysWeaver-ChatHolder>` with: image → `<img>` (click opens with `Open`); `.html/.htm` → sandboxed `<iframe>` with `appendTheme(src)`; other → `../icons/ext/<ext>.svg` plus `<SysWeaver-ChatLabel>` (copies URL on click), class `Extension`, click downloads (`downloadFile`). Clickable holders get class `Clickable`. Each holder has a `<SysWeaver-ChatExpand>` with a menu `ColorIcon` opening a `PopUpMenu` (root class `ChatData`): preview-only "Copy image Mark Down", "Copy url", "Delete file" (removes from data and deletes a temp upload via `DeleteStoredFile`), "Hide file" (removes from data and copies a markdown reference); "Open in new tab" (non-download items); "Download" (temporary `<a download>`); with `canPost`, "Store file" submenu (`StoreFile {Url, Scope}`: Private 0, Protected 1, Public also sends 1 and is shown only if `AllowPublicStore`; not for HTML or URLs already under `../storage/<UserStore>/`) and "Create link" submenu (`StoreLink {Url, Scope 0/1/2}`, non-absolute URLs only). Store results are made absolute, copied to the clipboard and opened. |
| `row.UpdateFormat` | `UpdateFormat()` (expando) | Clears a pending `msgTime.Updater` timer and renders the time with `ValueFormat.updateDateTime` (`showTimeStamp`) or live "x ago" with `ValueFormat.updateDateTimeLive`. |
| `row.Show` / `row.Hide` | `Show()` / `Hide()` (expando) | Remove/add class `Hidden` on content and data, set `msgHeader.IsHidden` and the show/hide tooltip. Do not persist (callers use `setHideFrom` / `setHideNone`). |
| `findMessageE` | `findMessageE(msgId)` → HTMLElement or null | Linear scan of `chatE.children` for `Msg.Id === msgId`. |
| `onAllMessages` | `onAllMessages(fn)` | Calls `fn(row)` for each child of `chatE`. |
| `updateMessageFormat` | `updateMessageFormat()` | `UpdateFormat()` on every row (after toggling time/age). |
| `GetDataCount` | `GetDataCount()` → number | Number of `;`-separated entries in `previewMessage.Data`. |
| `StickForAwhile` | `StickForAwhile(direct)` | For 500 ms scrolls `chatE` to the bottom on every animation frame (`behavior` "instant" if `direct`, else "smooth"), then once more. Called after sending and at startup. |
| `stickFn` | `stickFn()` (const, inner of `StickForAwhile`) | The per-frame scroll callback; re-queues itself while `stick` is true. |

All user-visible strings go through the common.js i18n helpers `_TF(text, description)` and `_T(format, arg, description)`. Anonymous handlers set inside `addChat` (no own row): header and sender image `oncontextmenu` (open `onMenuClick`), textarea `onfocus` (scrollIntoView), `onkeydown` (pure Alt: Enter → `Send`, ArrowUp/ArrowDown → history), `drop`/`dragover`/`dragenter`/`dragleave` (upload files, insert the first string item, markdown image wrapping, class `Dragging`), the `ResizeObserver` saving the split height fraction to `SysWeaver.Chat.Size<s>`, the window `message` listener dispatching `commandMap` (`chat.scrolltop`, `chat.scrollbottom`, `chat.showall`, `chat.hideall`, `chat.clear`), the `InterOp.AddListener` handler for `chat.post|replace|remove|clear.<chatid lowercased>` (adds/updates/removes rows; speaks with `SpeechGen.speakNew` / `speakContinue` using `findVoice` on the sender's `Voices` entry and `speechVolume * 0.01`), the top-right menu `ColorIcon` handler (items listed in Behavior; root class `Chat`), and the `StickToBottom` callbacks (toggle class `Stick`; at the top, load `GetMessages {MaxCount: -10, FromId}` with a delayed 64x64 `IconWorking` spinner).

### `ChatTest.html` (inline script)

Standalone harness (no SysWeaver scripts). Each function posts a command to the embedded `Chat.html?id=Simple.Generic&options={"UserColor":false}` iframe (`document.body.getElementsByTagName("iframe")[0].contentWindow.postMessage({Type})`) and is bound to a button's `onclick`.

| Function | Signature | Description |
|---|---|---|
| `Clear` | `Clear()` | Posts `{Type: "chat.clear"}` (handled only if the user can post and clear). |
| `ScrollTop` | `ScrollTop()` | Posts `{Type: "chat.scrolltop"}`. |
| `ScrollBottom` | `ScrollBottom()` | Posts `{Type: "chat.scrollbottom"}`. |
| `ShowAll` | `ShowAll()` | Posts `{Type: "chat.showall"}`. |
| `HideAll` | `HideAll()` | Posts `{Type: "chat.hideall"}`. |

`Chat.html` has no inline script (only `onload="chatMain()"`); both pages have an inline `<style>` making `html, body` transparent.

## CSS reference

### `chat.css`

Styles the chat UI built by `chat.js`. Loaded by `Chat.html` after `../common/theme.css` and `../app/application.css`; not templated. It relies on theme variables from `theme.css` (`--ThemeMain`, `--ThemeMainRGB`, `--ThemeMain25`, `--ThemeAcc1`, `--ThemeAcc1RGB`, `--ThemeAcc2`, `--ThemeAcc2RGB`, `--ThemeBackground`, `--ThemeBackgroundRGB`, `--ThemeRad`, `--ThemeRadInp`, `--ThemeRadInp2`, `--ThemeIconSize`, `--ThemeFadeDuration`, `--ThemeFont`, `--ThemeFontMono`) and on the `ColorIcon` elements of common.js (`icon-WxH` custom elements with classes `IconButton`, `IconDisabled`, `IconWorking`, and a mask image from an `.Icon*` class). Mask icon paths are relative to `chat/`: `../icons/` is the shared HttpServer icon set, `icons/` the chat-local set.

#### Page and container

| Selector(s) | Description |
|---|---|
| `html.Embedded body.ChatMain` | When the page runs inside the app shell (`Embedded` class added to `<html>` by application.js), height `calc(100svh - 8px)`. Written as CSS nesting. |
| `html` | Sets `--Margin: 0`. |
| `body.ChatMain` | `Chat.html` body: full width, `100svh`, no padding/margin, no text selection, no vertical overflow. |
| `body.ChatMain SysWeaver-Chat` | Limits the chat width to `min(125vh, 100vw)` (min 320px) with a soft accent shadow. |
| `body.ChatMain SysWeaver-Chat.NoBackground` | `?nobackground` (class set by `chatMain`): transparent, no shadow, no max width. |
| `SysWeaver-Chat` | Root container created by `chatMain`: flex column, relative, semi-transparent background, defines `--ics: 48px`, font-size 125%. |
| `SysWeaver-Chat > *.IconButton` (`:focus`, `:hover`, `:focus:hover`) | The top-right menu `ColorIcon` (`IconMenu`) added by `addChat`: absolute top-right, opacity 0.25, 0.6 focused, 1 hovered. |

#### Message list

| Selector(s) | Description |
|---|---|
| `SysWeaver-ChatMain` | Scrolling message list created by `addChat`: `resize: vertical` (split with the input; height saved by JS), height `100% - max(4.5em, --ics)`, min 10%. |
| `SysWeaver-ChatMain::after` | Inset top shadow overlay (non-interactive). |
| `SysWeaver-ChatMain.NoPost` | Added when `CanPost` is false: full height, no resize. |
| `SysWeaver-ChatMain icon-64x64` | The 64x64 `IconWorking` spinner shown while loading older messages: half opacity, centered, `LoadingFade` animation. |
| `SysWeaver-ChatMain.Stick` | Added/removed by the `StickToBottom` callbacks: plays `SysWeaver_ChatStick` (bottom accent flash). |

#### Input area

| Selector(s) | Description |
|---|---|
| `SysWeaver-ChatInput` | Input container (only when `CanPost`): top shadow; its `background-image` is set by JS to the current language flag, positioned bottom-right (`right 72px bottom 16px`, 32px high). |
| `SysWeaver-ChatInput > *.IconButton` | Send button (and mic): absolute top-right. |
| `SysWeaver-ChatInput > *.IconButton:nth-child(3)` | The mic button (third child after textarea and send): bottom-right with `--ThemeAcc2` background. |
| `SysWeaver-ChatInput > *.IconButton:nth-child(3).IconDisabled` | Mic disabled (open mic active, `listen.SetEnabled(false)`): mask `icons/ChatInput.svg`, opacity 0.6. |
| `SysWeaver-ChatInput > *.IconButton:nth-child(3).IconWorking` | Mic working (`listen.StartWorking()`): mask `icons/ChatRecording.svg`, full opacity. |
| `SysWeaver-ChatInput textarea` (`::placeholder`) | The message textarea: width leaves room for the icons (`--ics`), themed background/border/font; placeholder faint, 80%, italic. |
| `SysWeaver-ChatInput textarea.Listening` | Set by `startListening` while speech is active: solid `--ThemeAcc2` border. |
| `SysWeaver-ChatInput textarea.Dragging` | Set on `dragenter`, removed on `dragleave`/`drop`: dashed `--ThemeAcc1` border. |

#### Message rows and header

| Selector(s) | Description |
|---|---|
| `SysWeaver-ChatRow` (`:first-child`, `:last-child`) | One row per message (`addMessage`): defines `--si` and `--fi`; dashed bottom divider; the first row has a 48px top margin (room for the menu icon); the last row has no divider. |
| `SysWeaver-ChatRow.Hide` | `display: none`; used for the preview row when it has nothing to show (`previewChanged`). |
| `SysWeaver-ChatRow.Preview` (`:before`, `SysWeaver-ChatHeader`) | The preview row: dashed accent top border, a centered "Preview" label via `:before`, default cursor on its header. |
| `SysWeaver-ChatRow.Mine` | Own messages (`m.From === UserName`): right aligned; the bubble's square corner moves to the top-right; header columns mirrored (`.Mine SysWeaver-ChatHeader`: `--fi auto --si`), `.Mine SysWeaver-ChatLanguage` in column 1, `.Mine SysWeaver-ChatIcon` in column 3; the `.Mine SysWeaver-ChatName` and `.Mine SysWeaver-ChatTime` rules are empty. `.Mine SysWeaver-ChatIcon img` zooms from the right. |
| `SysWeaver-ChatRow.Working` (`SysWeaver-ChatData > *`, `SysWeaver-ChatData img`, `SysWeaver-ChatExpand`) | While a message has `FlagIsWorking` (`UpdateBody`): attachments shrink (max 32px, images 64px, no background) and attachment menus are hidden. |
| `SysWeaver-ChatMsg` | The message bubble: theme background, border (color overridden per user by JS), rounded with a square top-left corner, max 95% width. |
| `SysWeaver-ChatHeader` | Grid header (`--si auto --fi`, two rows), light accent background (overridden per user by JS), pointer cursor (click toggles hide/show). |
| `SysWeaver-ChatLanguage` (`img`) | Language flag cell (only when `CanTranslate`), spans both rows; flag image `--fi` x `--si`, pointer. |
| `SysWeaver-ChatLanguage.Translating` (`img`) | During a `Translate` request: shows a `../icons/Loading.svg` mask in `--ThemeMain` and hides the flag. |
| `SysWeaver-ChatLanguage.Translated img` | Showing the translation: accent drop-shadow glow on the flag. |
| `SysWeaver-ChatName` | Sender name, bold, row 1 column 2, no wrap. |
| `SysWeaver-ChatTime` | Time / age, smaller and faded, row 2 column 2. |
| `SysWeaver-ChatIcon` (`img`, `img:hover`) | Sender image cell (or `IconMenu` fallback) spanning both rows; image `--si` square, scales 2x on hover. |

#### Message content (markdown / HTML output)

| Selector(s) | Description |
|---|---|
| `SysWeaver-ChatContent` (`:focus`) | Message body: selectable text, max 60vh, overflow hidden (scrollable when focused; `tabIndex` 0 set by JS). |
| `SysWeaver-ChatContent.Hidden` | Hidden sender content (`row.Hide`). |
| `SysWeaver-ChatContent h1`, `h2`, `h3`, `h4`, `h5`, `h6`, `strong` | Accent-1 colored; headings get tight margins. |
| `SysWeaver-ChatContent p`, `a`, `em`, `mark`, `hr` | Paragraph margins; inline-block links; accent-2 emphasis; highlighted `mark`; dashed `hr`. |
| `SysWeaver-ChatContent ol,ul` (the `ul` part is unscoped and matches every `ul` on the page), `li`, `li:hover`, `li::marker` | List margins; list items with hover background and accent markers. |
| `SysWeaver-ChatContent img`, `a img`, `p img` | Images limited to 35vw height with subtle background; inline inside paragraphs. |
| `SysWeaver-ChatContent blockquote`, `pre`, `code` | Quote with a left bar; wrapped `pre` blocks; monospace accent-2 `code`. |
| `SysWeaver-ChatContent table`, `th`, `tr`, `tr:hover`, `tr:nth-child(2n)` (and `:hover`), `td`, `td:nth-child(2n)`, `td:last-child` | Collapsed bordered tables, accent headers, zebra rows with hover highlight, accent-2 even columns. |

#### Attachments

| Selector(s) | Description |
|---|---|
| `SysWeaver-ChatData` (`.Hidden`, `> *`) | Attachment list (`UpdateData`): centered; hidden with `Hidden`; children resizable (`resize: both`), full width, max 100vh. |
| `SysWeaver-ChatData SysWeaver-ChatHolder` | One attachment box: defines `--size: 80vh` (height is half of it), border, relative, overflow hidden. |
| `SysWeaver-ChatData SysWeaver-ChatHolder.Clickable > *:first-child` (`:hover`, `:focus`) | Clickable images / extension icons (class `Clickable` set by `UpdateData`): pointer and accent border on hover/focus. |
| `SysWeaver-ChatData SysWeaver-ChatHolder iframe` | HTML attachments fill the holder without border. |
| `SysWeaver-ChatData SysWeaver-ChatHolder SysWeaver-ChatExpand` (`:hover`) | 32x32 attachment menu button at the top-right, faded until hovered. |
| `SysWeaver-ChatData SysWeaver-ChatHolder.Extension` (`img`) | Non-image/non-HTML files (class `Extension`): small 6em box with the `../icons/ext/<ext>.svg` icon. |
| `SysWeaver-ChatData SysWeaver-ChatHolder SysWeaver-ChatLabel` | File name label below the extension icon, small, selectable. |
| `SysWeaver-ChatData img` | Attachment images: contained, limited to `--size - 4px` height. |

#### Popups

| Selector(s) | Description |
|---|---|
| `SysWeaver-PopupMenu.Chat SysWeaver-PopupMenuBack` | Main and message menus (the `PopUpMenu` root gets class `Chat` from JS): absolutely positioned (JS sets `left`), normal font size. |
| `SysWeaver-PopupMenu.ChatData SysWeaver-PopupMenuBack` | Attachment menus (root class `ChatData`): absolute, shifted `right: -32px`. |
| `SysWeaver-ChatLangItem` (`:hover`) | Language entries in the `selectLanguage` popup: flag as left background image (set by JS), rounded bordered pill; hover accent colors. |

#### Icon classes

All are `mask-image` classes used with `ColorIcon` / `WebMenuItem.IconClass`.

| Selector(s) | Description |
|---|---|
| `.IconSend`, `.IconMenu` | `../icons/send.svg` (send button), `../icons/menu.svg` (top-right menu, message icon fallback, attachment menu). |
| `.IconChatSave`, `.IconChatStore`, `.IconChatLink`, `.IconChatLink0`, `.IconChatLink1`, `.IconChatLink2` | Attachment menu: Download (`disc.svg`), Store file (`db_add.svg`), Create link (`share.svg`), scope Private (`keep.svg`), Protected (`user_check.svg`), Public (`table_country.svg`). |
| `.IconChatClear`, `.IconChatRemove`, `.IconChatKeep` | Remove all messages and remove message (`trash.svg`), confirm-dialog keep button (`fav_on.svg`). |
| `.IconChatTop`, `.IconChatBottom`, `.IconChatAge`, `.IconChatTime` | Main menu: scroll top (`MoveTop.svg`) / bottom (`MoveBottom.svg`), show age (`hour_glass.svg`) / time (`clock.svg`). |
| `.IconDebug` (declared twice), `.IconDebugNone`, `.IconDebugStats`, `.IconDebugOverview`, `.IconDebugParameters`, `.IconDebugDetails` | Not referenced in chat.js; for server-defined menu items (`ChatMenuItem.Icon`, e.g. AI debug options): `table_api.svg`, `minus.svg`, `fingerprint.svg`, `rating_off.svg`, `rating_half.svg`, `rating_on.svg`. |
| `.IconChatMicOn`, `.IconChatMicOff` | Mic button and "enable open mic" (`mic_on.svg`), "disable open mic" (`mic_off.svg`). |
| `.IconCopy`, `.IconChatCopy`, `.IconChatCopyImage` | Copy items in the message menu and "Copy url" (`copy.svg`), "Copy image Mark Down" (`image.svg`). |
| `.IconChatShow`, `.IconChatHide`, `.IconChatExpand` | Show content / show all (`eye.svg`), hide content (`eye_off.svg`), open in new tab (`newtab.svg`). |
| `.IconChatSpeechGenEnable`, `.IconChatSpeechGenDisable`, `.IconChatSpeechVolume`, `.IconChatSpeechVolume25`, `.IconChatSpeechVolume50`, `.IconChatSpeechVolume75`, `.IconChatSpeechVolume100` | Speech synthesis enable (`preview_vol_loud.svg`) / disable (`preview_mute.svg`), volume submenu (`preview_unmute.svg`), 25 and 50 (`preview_vol_low.svg`), 75 and 100 (`preview_vol_loud.svg`). |
| `.IconChatHelp`, `.IconProfile` | Markdown syntax help (`help.svg`), view profile (`user.svg`). |
| `.IconChatText`, `.IconChatMD`, `.IconChatHidePreview`, `.IconChatShowPreview` | Chat-local icons for the format and preview toggles: `icons/ChatFormatText.svg`, `icons/ChatFormatMD.svg`, `icons/ChatHidePreview.svg`, `icons/ChatShowPreview.svg`. |
| `.IconChatDeleteFile`, `.IconChatHideFile`, `.IconChatAttachFile` | Preview attachment delete (`db_remove.svg`), hide (`eye_off.svg`), attach file (`db_add.svg`). |
| `.IconChatLanguage`, `.IconChatClearCurrent`, `.IconChatAutoSendOn`, `.IconChatAutoSendOff` | Set input language (`table_language.svg`), clear draft (`clear.svg`), enable auto send (`send.svg`), disable auto send (`edit.svg`). |

Classes used by JS but styled elsewhere: `IconWorking` (spinner, common.js), `IconColorThemeAcc1`, `IconColorThemeAcc2`, `IconColorThemeMain` (`ColorIcon` colors).

#### Custom properties

| Variable | Default / source | Used for |
|---|---|---|
| `--Margin` | `0` on `html` | Overrides the shared theme margin for this page. |
| `--ics` | `48px` on `SysWeaver-Chat` | Input icon size; reserves space in the list height and textarea width. |
| `--si` | `calc(var(--ThemeIconSize) * 40px)` on `SysWeaver-ChatRow` | Sender icon size and header grid column. |
| `--fi` | `calc(var(--ThemeIconSize) * 32px)` on `SysWeaver-ChatRow` | Flag width and header grid column. |
| `--size` | `80vh` on `SysWeaver-ChatHolder`, `6em` on `.Extension`, `4em` on `.Extension img` | Attachment holder height (half of it) and max image height. |

#### Animations (@keyframes) and media queries / theme variants

| Name | Description |
|---|---|
| `@keyframes SysWeaver_ChatStick` | Inset bottom accent shadow fading to transparent; 0.5s on `SysWeaver-ChatMain.Stick`. |
| `@keyframes LoadingFade` | Opacity 0 to 0.5; used by the loading spinner (`SysWeaver-ChatMain icon-64x64`). |

No media queries or `[data-theme]` variants; theming comes entirely from the theme variables.
