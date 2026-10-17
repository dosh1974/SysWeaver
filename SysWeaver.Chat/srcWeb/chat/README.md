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
| [`ChatTest.html`](ChatTest.html) | Test page that hosts `Chat.html?id=Test` in an iframe, with buttons that send `postMessage` commands |
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
A standalone test harness (no SysWeaver scripts). It embeds `Chat.html?id=Test&options={"UserColor":false}` in an iframe and has buttons that call `iframe.contentWindow.postMessage({Type: "chat.clear" | "chat.scrolltop" | "chat.scrollbottom" | "chat.showall" | "chat.hideall"})`.

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
