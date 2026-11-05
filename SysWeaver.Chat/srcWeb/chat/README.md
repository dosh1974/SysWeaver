# chat

[⬆ srcWeb](../README.md)

> The web chat UI of SysWeaver.Chat: the chat page, its scripts and styles, served under `chat/`.

| | |
|---|---|
| **Project** | [SysWeaver.Chat](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`Chat.html` is the single page that shows any chat of any provider (simple rooms, MySQL rooms, AI chats). The client (`chat.js`, plus the speech helpers in `speech.js`) talks to the web APIs of [`ChatService`](../../ChatService.cs) and receives new messages, streamed edits, removals and clears through the HTTP server's session push messages. It is built on the SysWeaver web core and UI library (`../sw/core.js`, `../sw/ui.js`, namespace `Sw`) and uses images served by other projects (`../icons/`, the `../iso_data/language/` flags, the `../auth/UserImages/` user images).

The page is normally embedded in an iframe by a host app and can be controlled with `postMessage` (`chat.clear`, `chat.scrolltop`, `chat.scrollbottom`, `chat.showall`, `chat.hideall`), which `ChatTest.html` demonstrates. Any other page can also create a chat in one of its own elements with `Sw.Chat.Add` (legacy `Chat.addChat`).

## Contents

| Item | Description |
|---|---|
| [`icons/`](icons/README.md) | Chat specific mask icons (mic states, text format, preview) |
| [`Chat.html`](Chat.html) | The chat page (`chatMain()` = `Sw.Chat.Main`) |
| [`ChatTest.html`](ChatTest.html) | Developer test page: hosts `Chat.html?id=Simple.Generic` in an iframe (with the page's theme, `Sw.Url.AppendTheme`) below one row of Sw ghost buttons (Clear, Top, Bottom, Show all, Hide all; scrolls sideways when narrow) that post the frame commands |
| [`chat.js`](chat.js) | The chat client (`Sw.Chat`): message list, input, attachments, menus, translation, file and link sharing, push messages, speech output |
| [`speech.js`](speech.js) | Speech output (`Sw.Chat.SpeechGen`, text to speech) and speech input (`Sw.Chat.SpeechInput`, recognition) |
| [`chat.css`](chat.css) | Styles of the chat (classes `chat-*`) and the four chat local icon classes |

## Relationships

- **Part of:** [SysWeaver.Chat](../../README.md)
- **Uses:** [`ChatService.cs`](../../ChatService.cs) (web APIs and push messages), the web core / UI library of [SysWeaver.HttpServer](../../../SysWeaver.HttpServer/README.md) (`sw/core.js`, `sw/ui.js`, `sw/ui.css` incl. the `IconChat*` icon classes, `common/theme.css`, `common/marked.js` on demand), `common/fileUploader.js` of SysWeaver.MicroService.FileUploader (`Sw.Upload`, on demand).
- **Consumers:** host apps embed `Chat.html?id=<provider>.<chatId>[&options=<json>][&nobackground][&transparent=true][&fillscreen]`; providers (SimpleChatService, MySQL rooms, AI chats) only differ in server data (flags, menus, voices, attachments). No in-repo page links to it. Embedding (the page classes come from `core/layout.js`):
  - **Default:** any child frame is transparent unless the url has `transparent=false`.
  - `transparent`: no page background and no tint; the page owning the iframe is responsible for a background that suits the theme. The bubbles and the input keep their own surfaces.
  - Outside the app shell (`html.Transparent:not(.Embedded)`): the list fades at the top edge while scrolled (`.chat-scrolled`), the row separators and the splitter line are left out, and the Send / Speak column gets the input's surface.
  - Example: `<iframe src="https://host/chat/Chat.html?id=AI.Fluffy.Session&transparent=true&fillscreen" title="Chat with Fluffy" allow="microphone"></iframe>` (the `title` names the frame for screen readers).
  - `fillscreen`: the chat fills the frame (no width limit or side hairlines); the bubbles keep a reading width of 36em.
  - Another origin: the server's `frame-ancestors` policy (default `'self'`) must allow the host, and a session cookie in a cross-site frame needs a matching SameSite setting.
  - Speech input: the host's iframe needs `allow="microphone"` for the Speak button.

## Behavior

### Loading

1. `Chat.html` (new page format) loads `../sw/core.js`, `../sw/ui.js`, `speech.js`, `chat.js` (all `defer`) and `../common/theme.css`, `../sw/ui.css`, `chat.css`; `<body class="ChatMain sw-root" onload="chatMain()">` is empty.
2. `chatMain()` (`Sw.Chat.Main`) shows the loader (`Sw.Page.Loading`, removed with the page protocol `Loaded` when done), reads the url parameters `id` (required: without it the page shows an in-page explanation "No chat selected" with the usage, no toast / console error), `options` (json; only the known `Sw.Chat.Options` keys with matching value types are taken) and `nobackground`, creates `div.chat-page` and calls `Sw.Chat.Add(root, id, null, options)`. The textarea gets the focus after the loader is gone (read only while focusing, so phones don't open the keyboard). If opening fails the page shows the reason and a "Try again" button (`.chat-page-message`).
3. `Sw.Chat.Add` creates a `ChatView` and runs `Init()`: loads `speech.js` if the page did not, cancels any speech, calls `Join {ChatId, MaxCount: 10, FromId: 0}`, loads the input languages (`CanTranslate`) and the upload library (`UploadRepo` + `CanPost`) in parallel, builds the list, the chat menu button, the splitter and the input (`CanPost`), renders the messages, subscribes the push messages and the frame commands, restores the split size, starts the open mic when enabled, and keeps the list at the bottom for 500 ms.

### Messages

- A row (`MessageRow`) has a header (sender image button that opens the message menu, name, time / live age, the language flag when translation is enabled), the content and the attachments. Accessibility: the row (`role=article`) is named "Message from <sender>, <time>" and the menu button "Message menu, <sender>, <time>"; the list is a `role=log`. Age mode (default; the menu toggles time vs age) shows the age in readable units, refreshed every second: "1 second ago", "12 seconds ago", "1 minute ago", "3 hours 49 minutes ago", "1 day 1 hour ago", "2 days 4 hours ago", "12 days ago" (one style in words; every singular / plural combination is its own translatable literal, words are never built by adding an "s"); the tool tip has the full local time stamp and UTC. Time mode shows the time stamp (`Sw.Cells.DateTime`). Own messages are right aligned and mirrored. With `Options.UserColor` the bubble and header colors come from a SHA-256 of the sender name.
- Content formats: 0 text (`innerText`, line breaks kept; markdown syntax is shown as typed: a chat that does not allow markdown rejects markdown messages on the server, so text is never rendered as markdown), 1 markdown (`Sw.Markdown.ToHtml`, `common/marked.js` with a raw html allow list), 2 html (only the server application may send it, the server refuses html from clients with 400; still sanitized with `Sw.Markdown.Sanitize`, the markdown allow list). A render that finishes after a newer one started is discarded, so streamed `replace` messages never show an older text. Content is limited to 60vh and scrollable while focused.
- `Flags & 1` (working, ex: a streaming AI answer) adds `chat-working` (small attachments, no attachment menus); `Flags & 2` (`ChatMessageFlags.FromAi`: an AI answer, or a message sent as the AI agent) shows an "AI" tag after the sender name and names the row "Message from the AI <sender>, <time>" (EU AI Act art. 50: people know they talk to an AI); `Flags & 256` allows removing without confirmation.
- Clicking a header hides / shows the content of that message only; the new state becomes the default for new messages of that sender (persisted: if the last message toggled was hidden, new messages from that sender, and all of them after a reload, start hidden). The message menu offers the same per message toggle ("Show this message" / "Hide for me") plus "Hide all messages from this user" / "Show all messages from this user" (all rows of the sender and the default); long press (touch), right click, the context menu key or Shift+F10 on the header, or a click on the sender image open the message menu. Every icon only button (sender image, message and input language flags, Menu, Send, Speak, attachment menus) has a tool tip and an accessible name, and shows its name on long press (touch / pen) and keyboard focus (`Sw.Hint`; a long press on a header button shows its name instead of opening the message menu). On touch devices short text labels show below the input icons (Menu, the language code, Send, Speak) while the input is high enough (`.chat-labels`, `Composer.WatchSize`); the icons shrink so the buttons keep their size.
- The list sticks to the bottom while new content arrives (also when images / markdown change the size), flashes an accent when it sticks again, and loads 10 older messages at a time when scrolled to the top (`GetMessages {ChatId, MaxCount: -10, FromId}`, spinner after 200 ms, the scroll position is kept).

### Input (CanPost)

- Textarea (`MaxTextLength`), send button (disabled while there is nothing to send; its word stays readable), push-to-talk mic button (speech recognition only), the input language flag (click selects the language), a splitter between the list and the input (drag with mouse or touch, keyboard arrows / Shift / Home / End, Enter or double click resets; the height fraction is saved and re-applied when the chat is resized).
- Automatic split (`.chat-autosize`, until the user drags the splitter, and again after a reset): the text box is sized to its text (`Composer.AutoGrow`: at least three lines and the height of the buttons beside it, growing while typing up to 40% of the chat height) and the list takes the rest. Layout (a grid, nothing over the text): [text box] [Attach, Language, Speak and Send] [Menu]. The buttons (`Composer.Pad`, `.chat-input-pad`) are a 2 x 2 square right of the text box ([Attach][Language] over [Speak][Send]) and the Menu button (the "more" icon) is as high as the square; in a narrow input (below 480 px, `.chat-pad-row` from `Composer.WatchSize`) the text box has the whole width and the buttons and Menu are a row below it. A user split keeps at least `Composer.MinHeight` for the input (the buttons and two lines of text). Every button shows a short word below its icon (`.chat-labels`) and keeps its tool tip and accessible name; Attach (with an upload repository) is the menu's "Attach file", disabled when the message has the maximum number of files. A low chat (below 540 px, phone landscape) is `.chat-compact` (less padding). A very low chat (below 320 px, ex: a phone with the on screen keyboard open) is `.chat-tight`: one row, the text box (one line, growing while typing up to 40%) beside Send and Menu, always in view; Attach, Language and Speak (unless recording) and the splitter step aside, a user split is not applied (it is back with the height), the messages get the rest.
- Alt+Enter (Option+Enter on mac) sends, Alt+Up / Alt+Down browse the last 100 sent texts, plain Enter is a new line. The placeholder is "Write a message..." (markdown: "Write a message (Markdown)..."); on devices with a keyboard and mouse (not phones / tablets, `any-pointer: fine`) a second line "Alt+Enter to send" ("Option+Enter to send" on mac) is added and the send button tool tip names the key; the textarea has `aria-keyshortcuts="Alt+Enter"`.
- Sending (`UserMessage {ChatId, Body: {Format, Data, Text, Lang}}`) shows the sent text as placeholder while the input is read only; a failure restores the draft and shows a fail toast.
- Input format: text or markdown (chat menu, saved in `.UseMarkDown<s>`). Until the user picks one, a chat that allows markdown (`AllowMarkDown`) uses the automatic format: a draft with markdown syntax (`**bold**`, `*italic*`, `` `code` ``, `~~strike~~`, `[text](url)`, `# heading`, a code block; `LooksLikeMarkDown`) is sent and previewed as markdown, other drafts as text; the menu then offers both "Use Mark Down text" and "Use regular text".
- The draft (text and attachments) is saved per chat and restored on load. The preview row (last row, "Preview") shows the draft rendered like a message: on with attachments, else per the preview setting (auto = on for markdown).
- Attachments: "Attach file" (main and preview menus) picks files (`Sw.Upload.Pick`) and uploads them to `UploadRepo` (`Sw.Upload.Files`); files dropped anywhere on the chat are uploaded too (only with an upload repository; a dashed "Drop files to attach" frame shows while files are dragged over it, `.chat-drop-files` / `.chat-drop-full` on the chat root); `MaxDataCount` / `MaxDataLength` are enforced; files uploaded for the draft are deleted on the server (`../Api/UserStorage/DeleteStoredFile`) when removed from the draft or when the draft is cleared. Dropped text is inserted at the cursor (image urls as `![](url)` in markdown mode).
- Language: a searchable selection over Name / LocalName / EnName with flags; stored only when it differs from the server language; restarts an open mic.

### Attachments

`DataViews` (optional, C# `ChatDataView[]`, one per `Data` entry in the same order: `{Raw, Viewer, Source}`, chat page relative urls) say how an entry is shown. The `ChatService` fills them when it sends messages (`ChatDataViews.Fill`, from `ServiceManager.GetExtensionViewer` / `GetExtensionEditor`; a provider may set its own): html / htm / svg are shown as they are with `Source` = the read only text view (`edit/text.html?r=...`); other files with a registered viewer or editor get `Viewer` (shown in a frame); images and other origins only have `Raw`. Without views (older servers, the draft preview) an entry is shown by its extension. An entry with a viewer, a source view or html gets an info bar above its content: collapse / expand, the name (the document title of an html page, else the file name), a "source" toggle (with a source view: switches the content between the page / image and the source view), open in a new tab (the view that is shown) and the menu button. The menu operations (copy url, store file, create link, download) always use `Raw`, the original asset.

`Data` is a `;` separated list of `../` relative urls. Images are shown with the same info bar as embedded pages (collapse, name, source for svg, open in a new tab, menu; a right click or a long press on the bar shows the menu at the pointer) when `Sw.Markdown.CanAutoLoadImage` allows it (same origin static image files, `data:` images), else a "Show image" placeholder loads them on click (other origins / `Api/` / query urls: no tracking pixels or GET requests; the same rule as markdown images, see ui/README.md), `.html` / `.htm` run in a sandboxed iframe (`allow-forms allow-presentation allow-scripts allow-downloads allow-popups allow-popups-to-escape-sandbox allow-modals`, theme appended; no `allow-same-origin`, so the page has a unique origin and cannot use the session, cookies, storage or apis of the site; the UserStorage service also sends a CSP `sandbox` header for stored html / svg / xml; above the frame an info bar as wide as it, so no button covers the page: collapse / expand, the document title of the frame when it can be read, else the file name, open in a new tab and the menu button), other files show the `../icons/ext/<ext>.svg` icon and the name (click downloads, the name copies the url). Boxes are resizable. The menu button of an attachment offers: (preview only) copy image markdown, copy url, delete file, hide file (copies a markdown reference); open in new tab; download; (CanPost) store file (`StoreFile {Url, Scope}`: private 0, protected 1, public 2 when `AllowPublicStore`; not for html or files already in the user's storage) and create link (`StoreLink {Url, Scope}`, relative urls only). Stored results are copied and opened.

### Menus

- Message menu: view profile (`CanShowProfile`: posts `{Type: "ViewProfile", Name, Image}` to the parent), copy markdown / html (desktop) / text, copy time stamp, copy user name, hide / show this message (and the default for new messages of the sender), hide / show all messages from this user, the server items of the message (`ChatMenuItem`, a leaf sends `SetValue {ChatId, Key, Value, Id}`), remove message (`RemoveMessage {ChatId, MessageId}` when `CanClear` or `Flags & 256`; confirmation unless `Flags & 256`). The preview row: attach file, copy, clear.
- Chat menu (button never over the messages: top right inside the text box when the user can post, beside the language flag; else a slim bar above the list sized by `MenuIconSize`): scroll to top / bottom (disabled when there), show all, time vs age; (CanPost) text vs markdown + markdown help, show / hide preview, set input language, clear draft, attach file, auto send toggle, open mic toggle (tool tip lists the wake words); speech synthesis toggle and volume (25 / 50 / 75 / 100, radio items); the server menus (`Join.Menus`); remove ALL messages (`ClearAllMessages`, confirmation unless `DoNotConfirmClear`).
- Menus use `Sw.Menu` (keyboard: arrows, type ahead, Enter, Escape; touch: drill down submenus), confirmations `Sw.Dialog.Confirm`.

### Push messages and frame commands

- Session messages (`Sw.Session.Subscribe`, the top window long polls `Api/application/GetMessages` and fans out over the `SysWeaver` BroadcastChannel; a same origin host page must load `sw/core.js` for frames to get them): `chat.post.<id>` `{Chat}` adds (unless known) and speaks it, `chat.replace.<id>` `{Chat}` updates in place (speech continues), `chat.remove.<id>` `{Value: messageId}`, `chat.clear.<id>` (the preview row stays). `<id>` = the chat id lower cased.
- `postMessage` commands `{Type}` from the parent / top window: `chat.scrolltop`, `chat.scrollbottom`, `chat.showall`, `chat.hideall` (show / hide all content and forget the hidden senders), `chat.clear` (`CanPost` and `CanClear`, no confirmation). Messages from other windows are ignored.

### Translation (CanTranslate)

The flag of a message toggles between the original and the translation (`Translate {ChatId, MessageId}`, cached; spinner while translating, glow while translated). The preview flag opens the language selection.

### Speech

- Output: when the server sends `Voices` and the browser has speech synthesis. New messages of other users are spoken with the voice of the sender (or the default voice entry), matched by language / gender / exact local name; streamed replaces speak only complete phrases and queue while speaking; volume = setting / 100.
- Input: needs `SpeechRecognition` + `SpeechGrammarList`. Push-to-talk (mic button) records one sentence (auto send optional); open mic (desktop only, wake words from `SpeechName` `"word|prefix"`) listens continuously and sends each sentence. Interim text is inserted live; sentences are capitalized, questions get "?", a final stop word (abort, stop, cancel, reset, restart) aborts.

### Persistence (localStorage, unchanged keys)

`<s>` = "" (default), "." + chatId (`SettingsName` "*") or "." + `SettingsName`: `SysWeaver.Chat.ShowTimeStamp<s>`, `.SpeechGenVol<s>`, `.SpeechGen<s>`, `.SpeechSend<s>` (auto send), `.UseMarkDown<s>`, `.Preview<s>` ("true" / "false" / "null"), `.Size<s>` (list height fraction). Per chat: `SysWeaver.Chat.SpeechListen<chatId>` (no dot), `.Hide.<chatId>` (json array of senders whose new messages start hidden), `.Input.<chatId>`, `.Data.<chatId>` (draft), `.Lang.<chatId>` (input language override).

## JavaScript reference

### `chat.js` (namespace `Sw.Chat`)

| Member | Description |
|---|---|
| `Add(container, chatId, apiBase, options)` → `Promise<HTMLElement>` | Create a chat in `container` (gets class `chat-root`). `apiBase` prefixes the api calls (`""` / null for pages in `chat/`, `"../chat/"` from a sibling folder). Returns the list element; its expando `SwChatView` is the `ChatView`. |
| `Main()` | The page entry of Chat.html (url parameters `id`, `options`, `nobackground`). |
| `Options` | Class: `MenuIconSize`, `AudioIconSize`, `SendIconSize` (px, 48), `UserColor` (true), `SettingsName` (null / "*" / name). |
| `Color(name)` | `[border, headerBackground, headerText]` colors of a sender (cached). |
| `Flags`, `Formats`, `LooksLikeMarkDown(text)` | `{IsWorking: 1, CanRemove: 256}`, `{Text: 0, MarkDown: 1, Html: 2}`; markdown syntax test of the automatic input format (`MarkDownSyntax` patterns). |
| `Settings` | Class: `Keys` (the storage key names), `Get(name)`, `Set(name, value)` (null removes), `Bool(name, def, store)`. |
| `ChatView` | Class (one chat): `Init()`, `Api(method)`, `GetLanguages()`, `LangText(iso)`, `IsHidden(name)`, `SetHidden(name, hide)` (the default for new messages only), `ToggleRow(row, hide)`, `SetSenderHidden(name, hide)`, `SetAllHidden(hide)`, `Clear()`, `SetTimeStamp(on)`, `SetSpeech(on)`, `SpeakNew(row)`, `MainMenuItems()`, `InputItems()`, `SpeechItems()`, `MessageMenuItems(row)`, `ServerItems(list, messageId)`, `AttachItem()`, `ClearDraftItem()`, `CreateSplit()`, `SetListHeight(px)`, `SaveSize()`, `RestoreSize()`, `InitPush()`, `InitCommands()`, `LoadUploader()`, `Dispose()`; state `Data` (the Join response), `UserName`, `CanPost`, `CanTranslate`, `CurrentLang`, `ShowTimeStamp`, `SpeechOn`, `Volume`, `UploadRepo`, `List`, `Composer`, `CanOpenMic`, `CanSpeak`. |
| `MessageList` | Class: `E`, `Rows` (Map id → row), `Preview`, `Insert(row, onTop)`, `Add(msg, onTop)`, `Get(id)`, `Remove(id)`, `Clear()`, `All()`, `ToTop()`, `ToBottom(instant)`, `StickForAwhile(instant)`, `LoadOlder()`, `IsAtTop`, `IsAtBottom`. |
| `MessageRow` | Class: `E` (expando `SwChatRow`), `Msg`, `IsPreview`, `Hidden`, `Body`, `DataE`, `Update(msg)`, `UpdateBody(text, format, working)`, `UpdateData(data)`, `UpdateFormat()`, `UpdateLang()`, `SetHidden(hide)`, `SetIcon(src)`, `OnFlag()`, `ShowMenu(ev, at)` (at the pointer, else at the element: the ⋮ button or the sender icon), `Text` (rendered text), `CurText` (source text). |
| `Composer` | Class (CanPost): `E`, `Write` (textarea), `Send`, `Mic`, `Attach` (Sw.Button), `Pad` (the buttons beside the text box), `MinHeight` (the least input height of a user split), `Preview` (row), `PreviewMsg`, `UseMarkDown`, `AutoMarkDown`, `FormatOf(text)`, `ShowPreview`, `PreviewOn`, `DataCount`, `CanSend`, `DoSend(noFocus)`, `Changed()`, `InsertText(text, select)`, `SetData(data)`, `SetMarkDown(md)`, `TogglePreview()`, `Clear()`, `RemoveAttachment(url, deleteFile)`, `AttachFiles()`, `Upload(files)`, `SelectLanguage()`, `StartListening(ignoreKeyword, noOpenMic)`, `StopListening()`, `Focus()`, `Ready()`, `WatchSize()` (layout classes `.chat-compact`, `.chat-tight`, `.chat-pad-row`, `.chat-labels`), `AutoGrow()` (text box height in the automatic split), `Flag` / `FlagLabel` (input language button and its touch label). Helpers: `setButtonName(b, text)`, `attachHint(b)`, `addTouchLabel(b, text)` name icon buttons and add the touch labels. |
| `Attachments` | Static: `Describe(view, url)`, `Render(row, data)`, `Item(row, url)`, `MenuItems(row, info)`, `ScopeItems(view, method, url)`, `Store(view, method, url, scope)`. |

Legacy globals (old API, kept for other pages): `Chat` (`addChat` = `Add`, `chatColor` = `Color`, `FlagIsWorking`, `FlagCanRemove`, `IsMobile`, `Platform`), `ChatOptions` (= `Options`), `chatMain` (= `Main`).

### `speech.js`

| Member | Description |
|---|---|
| `Sw.Chat.SpeechGen` (legacy global `SpeechGen`) | Static: `Supported()`, `FindVoice(lang, male, exact)`, `FilterText(text)`, `SpeakOnce(text, voice, pitch, rate, volume, lang)`, `SpeakNew(text, voice, pitch, rate, volume, lang, isCompleted)`, `SpeakContinue(text, isCompleted)`, `Cancel()`, `Similarity(a, b)`; state `CurrentText`, `Queue`, `Init`; tables `MaleNames`, `FemaleNames`, `EndChars`, `ValidChars`, `WhiteSpaces`, `MathOps`. Old lower case names (`speakNew`, `cancelSpeak` ...) are aliases. |
| `Sw.Chat.SpeechInput` (legacy global `SpeechInput`) | Static: `Supported()`, `SupportOpenMic()`, `Listen(keywords, onActivate, onSentence, lang, ignoreKeyword, noOpenMic)` → stop function, `FilterSentence(text, keyword, prefix)`, `GetLastWord(text)`, `IsQuestionWord(word, first)`; tables `StopWords`, `NonWordChars`, `QuestionWords`, `QuestionStartWords`. Old names (`triggerOnSpeech`, `supportSpeechRec`, `supportOpenMic`, `IsMobile`) are aliases. |

### `ChatTest.html`

Inline script: the nav buttons post `{Type: data-cmd}` to the frame; `ViewProfile` messages from the frame are logged. Loads `../sw/core.js` so that the embedded chat receives push messages.

## CSS reference (`chat.css`)

| Selector | Description |
|---|---|
| `body.ChatMain` | The Chat.html body: full height, no margins, transparent. |
| `.chat-page`, `.chat-nobackground` | The page root: at most min(125vh, 100vw) wide (full width on low screens, max-height 540px: phone landscape), framed by hairlines (no glow, Design v2); `?nobackground`: transparent, full width. |
| `.chat-root`, `.chat-nopost` | Any chat container (flex column); without input the list takes the full height. Custom properties `--chat-si` (sender image, 40 px × `--ThemeIconSize`, 44 px on touch), `--chat-fi` (flag width). |
| `.chat-list`, `.chat-stick`, `.chat-loading` | The scrolling list (default height 100% - 7.5em; the rest of the height in the automatic split; the splitter sets it); the stick flash; the older messages spinner. |
| `.chat-row`, `.chat-mine`, `.chat-preview`, `.chat-preview-label`, `.chat-hide`, `.chat-hidden`, `.chat-working` | Rows: own (right aligned), the preview row, hidden preview, hidden content, still working. |
| `.chat-msg`, `.chat-header`, `.chat-toggle`, `.chat-who`, `.chat-name`, `.chat-time`, `.chat-icon`, `.chat-icon-fallback` | The bubble and the header grid (areas icon / who / lang, mirrored for own messages); name and time sized from `--sw-size` (grow with `--sw-scale`; the time as large as the name, at least 13 px, in the text color `--sw-fg`, not the pale user color). |
| `.chat-lang`, `.chat-translating`, `.chat-translated` | The language flag button and its states. |
| `.chat-content` (+ markdown element rules) | The message content (60vh, scrollable when focused); headings, emphasis (bold / italic in the text color, like `.sw-md`), lists, code, quotes, tables. |
| `.chat-data`, `.chat-att`, `.chat-att-image`, `.chat-att-file`, `.chat-att-html`, `.chat-att-label`, `.chat-att-menu`, `.chat-att-bar`, `.chat-att-title`, `.chat-att-toggle`, `.chat-att-open`, `.chat-att-source`, `.chat-att-view`, `.chat-att-on-source`, `.chat-att-collapsed` | Attachments (resizable boxes) and their menu buttons. |
| `.chat-menu-button`, `.chat-send`, `.chat-mic`, `.chat-attach`, `.chat-recording` | Icon buttons (the Menu button outside the text box, `--chat-ics`; the others in the pad, 46 px cells), the recording state. |
| `.chat-labelled`, `.chat-tlabel`, `.chat-labels` | Short words of the icon buttons, below the icons (`.chat-labels` on the input; the slim menu bar on touch devices). |
| `.chat-split` | The splitter (grip, focus ring, 20 px on touch). |
| `.chat-input`, `.chat-write-box`, `.chat-write`, `.chat-listening`, `.chat-dragging`, `.chat-input-pad`, `.chat-input-flag`, `.chat-pad-row` | The input area: the text box, the button pad beside it (a square, or a row below it when narrow) and the Menu button. |
| `.chat-autosize`, `.chat-compact`, `.chat-tight` | Chat root: automatic split (text box sized to its text), low chat (less padding), very low chat (one input row: text box, Send, Menu). |
| `.IconChatText`, `.IconChatMD`, `.IconChatHidePreview`, `.IconChatShowPreview` | Chat local icon classes (`icons/`); the other `IconChat*` classes are in `sw/ui.css`. |
| `@keyframes chat-stick`, `chat-fade`, `chat-pulse` | Animations (`chat-pulse` is off with `prefers-reduced-motion`; `chat-fade` and the `chat-stick` flash are opacity fades and stay, fades are kept for reduced motion). |

Colors use the Sw tokens (`--sw-bg`, `--sw-fg`, `--sw-accent1/2`, `--sw-line*`, `--sw-focus` ...), so every theme (light, dark, hacker, application, pro, custom) applies.
