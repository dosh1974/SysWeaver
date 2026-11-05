/**
 * @file chat.js - the web chat client of SysWeaver.Chat (served as `chat/chat.js`).
 *
 * Loading: classic script. `Chat.html` loads `../sw/core.js`, `../sw/ui.js`, `speech.js` and this file and calls `chatMain()`
 * from `<body onload>`. Any other page (new format, or old format with common.js) may include this file (and chat.css) and call
 * `Sw.Chat.Add(container, chatId, apiBase, options)` (legacy name `Chat.addChat`); `speech.js` is then loaded on demand.
 *
 * Structure (namespace `Sw.Chat`):
 * - `Options`        options of a chat (legacy global `ChatOptions`).
 * - `Settings`       the persisted settings and drafts (localStorage keys `SysWeaver.Chat.*`, unchanged).
 * - `MessageRow`     one message (header, content, attachments) incl. the draft preview row.
 * - `Attachments`    renders the `;` separated attachment urls of a message and builds their menus.
 * - `MessageList`    the scrolling list: stick to bottom, lazy loading of older messages, lookup by id.
 * - `Composer`       the input: textarea, send / mic buttons, history, draft, drag and drop, uploads, language, speech input.
 * - `ChatView`       one chat: Join, menus, server push messages, postMessage commands, speech output.
 * - `Add(...)`       creates a ChatView (legacy `Chat.addChat`), `Color(name)` (legacy `Chat.chatColor`), `Main()` (legacy `chatMain`).
 *
 * Server: `ChatService` api methods relative to the page (`apiBase` + Join, GetMessages, UserMessage, RemoveMessage, ClearAllMessages,
 * SetValue, Translate, StoreFile, StoreLink, GetInputLanguages) and `../Api/UserStorage/DeleteStoredFile`; live updates are the
 * session messages `chat.post|replace|remove|clear.<provider>.<chatId lower cased>` (Sw.Session). Uploads use the FileUploader
 * library `common/fileUploader.js` (`Sw.Upload`, or the legacy global `fileUploader` of an older copy; loaded on demand when the
 * chat has an `UploadRepo`).
 *
 * Frame contract: commands from the parent window `{Type: "chat.scrolltop" | "chat.scrollbottom" | "chat.showall" | "chat.hideall" |
 * "chat.clear"}`; posts `{Type: "ViewProfile", Name, Image}` to the parent.
 *
 * Globals defined (legacy, kept for other pages): `Chat` (`addChat`, `chatColor`, `FlagIsWorking`, `FlagCanRemove`, `IsMobile`,
 * `Platform`), `ChatOptions`, `chatMain`.
 */
(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    /** @type {object} The chat namespace (shared with speech.js). */
    const C = Sw.Chat = Sw.Chat || {};

    /**
     * Create an element (Sw.el).
     * @param {...*} a The Sw.el arguments.
     * @returns {HTMLElement} The element.
     */
    const el = (...a) => Sw.el(...a);

    /** @type {string} The url of this script (base for loading speech.js on demand). */
    const ScriptUrl = (document.currentScript && document.currentScript.src) || (Sw.Url ? Sw.Url.Root + "chat/chat.js" : "chat.js");

    /** Message flags (C# ChatMessageFlags). */
    const Flags = Object.freeze({
        /** The message is not completed yet (ex: a streaming AI answer). */
        IsWorking: 1,
        /** The message is written by an AI (the sender gets an "AI" tag). */
        FromAi: 2,
        /** The message can be removed by this user (no confirmation). */
        CanRemove: 256,
    });

    /** Message formats (C# ChatMessageFormats). */
    const Formats = Object.freeze({ Text: 0, MarkDown: 1, Html: 2 });

    /**
     * Markdown syntax that is unlikely in plain text: `**bold**` / `__bold__`, `*italic*` (not `2*3*4`), `~~strike~~`, `` `code` ``,
     * a fenced code block, `[text](url)` links, `# headings`.
     */
    const MarkDownSyntax = [
        /\*\*[^*\s](?:[^*]*[^*\s])?\*\*/,
        /(^|[^\w])__[^_\s](?:[^_]*[^_\s])?__(?!\w)/,
        /(^|[\s(])\*[^*\s](?:[^*\n]*[^*\s])?\*(?=$|[\s.,;:!?)])/m,
        /~~[^~\s](?:[^~]*[^~\s])?~~/,
        /`[^`\n]+`/,
        /^```/m,
        /\[[^\]\n]+\]\((?:https?:\/\/|\.{0,2}\/)[^)\s]+\)/,
        /^#{1,6} \S/m,
    ];

    /**
     * Test if a text contains markdown syntax (the automatic input format sends such texts as markdown).
     * @param {?string} text The text.
     * @returns {boolean} True if it looks like markdown.
     */
    const LooksLikeMarkDown = text => !!text && MarkDownSyntax.some(r => r.test(text));

    /** Attachment extensions shown in a sandboxed iframe. */
    const HtmlExtensions = Object.freeze({ html: 1, htm: 1 });

    /**
     * The sandbox of html attachments: scripts, forms, popups and downloads work (LLM generated pages stay useful), but
     * WITHOUT allow-same-origin the page gets a unique origin: it can't read this site's cookies / storage, call its apis as the
     * user or reach this page. The server adds the same sandbox (a CSP header) to html files of the user storage.
     */
    const HtmlSandbox = "allow-forms allow-presentation allow-scripts allow-downloads allow-popups allow-popups-to-escape-sandbox allow-modals";

    const InternalHtmlSandbox = HtmlSandbox + " allow-same-origin";

    /** The menu icon (the hamburger of the icon module; the shared IconMenu class is a different image). */
    const MenuIcon = (Sw.Url ? Sw.Url.Root : "../") + "icons/menu.svg";

    /** The folder of the chat icons (mic states). */
    const ChatIcons = (Sw.Url ? Sw.Url.Root : "../") + "chat/icons/";

    /** A chat lower than this (px, ex: a phone with the on screen keyboard open) uses the tight input (.chat-tight, Composer.WatchSize). */
    const TightHeight = 320;

    /** Url of the image of the current user. */
    const CurrentUserImage = "../auth/UserImages/Current/small";

    /**
     * The url of a language flag.
     * @param {string} iso The iso 639-1 code.
     * @returns {string} The url.
     */
    const flagUrl = iso => "../iso_data/language/" + iso + ".svg";

    /**
     * Append an exception text to an error message (the old "<text>\nError:\n<message>" format).
     * @param {string} text The message.
     * @param {*} [e] The exception.
     * @returns {string} The text.
     */
    function withError(text, e) {
        if (!e)
            return text;
        return text + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + (e.message || e);
    }

    /**
     * Call an api; a falsy result or an exception shows a fail toast.
     * @param {string} url The api url.
     * @param {*} arg The argument.
     * @param {string} failText The fail text.
     * @returns {Promise<*>} The result, or null on failure.
     */
    async function call(url, arg, failText) {
        try {
            const r = await Sw.Net.Send(url, arg);
            if (!r)
                Sw.Fail(failText);
            return r || null;
        }
        catch (e) {
            Sw.Fail(withError(failText, e));
            return null;
        }
    }

    /**
     * Copy a text to the clipboard with an info toast.
     * @param {string} text The text.
     * @returns {Promise<boolean>} True on success.
     */
    const copy = text => Sw.Clipboard.Copy(text ?? "");

    /**
     * Parse a message time (a falsy value or "0001-01-01T00:00:00" gives null).
     * @param {string|number|Date} value The ChatMessage.Time.
     * @returns {?Date} The date.
     */
    function messageDate(value) {
        if (!value || value === "0001-01-01T00:00:00")
            return null;
        const d = value instanceof Date ? value : new Date(value);
        return isNaN(d.getTime()) ? null : d;
    }

    /**
     * The age text of a message (the age mode, ChatView.ShowTimeStamp off), in words with the largest readable units and
     * correct singular / plural forms: "1 second ago", "12 seconds ago", "5 minutes ago", "3 hours 49 minutes ago" (below a day),
     * "2 days 4 hours ago" (below a week), then "12 days ago". Every singular / plural combination is its own translatable
     * literal (words are never built by adding an "s"). The full time stamp is in the tool tip.
     * @param {?Date} d The message time (null = now).
     * @param {number} now The current time (ms).
     * @returns {string} The text.
     */
    function ageText(d, now) {
        const s = Math.max(0, Math.floor((d ? now - d.getTime() : 0) / 1000));
        if (s < 60)
            return s === 1
                ? _TF("1 second ago", "Text that indicates that a chat message was written one second ago")
                : _T("{0} seconds ago", s, "Text that indicates how old a chat message is (less than a minute, never 1).{0} is replaced with the number of seconds");
        const min = Math.floor(s / 60);
        if (min < 60)
            return min === 1
                ? _TF("1 minute ago", "Text that indicates that a chat message was written one minute ago")
                : _T("{0} minutes ago", min, "Text that indicates how old a chat message is (less than an hour, never 1).{0} is replaced with the number of minutes");
        const h = Math.floor(min / 60);
        const m = min % 60;
        if (h < 24) {
            if (h === 1)
                return m === 0
                    ? _TF("1 hour ago", "Text that indicates that a chat message was written one hour ago")
                    : (m === 1
                        ? _TF("1 hour 1 minute ago", "Text that indicates that a chat message was written one hour and one minute ago")
                        : _T("1 hour {0} minutes ago", m, "Text that indicates how old a chat message is (one hour and some minutes, never 1).{0} is replaced with the number of minutes"));
            return m === 0
                ? _T("{0} hours ago", h, "Text that indicates how old a chat message is (whole hours, less than a day, never 1).{0} is replaced with the number of hours")
                : (m === 1
                    ? _T("{0} hours 1 minute ago", h, "Text that indicates how old a chat message is (some hours, never 1, and one minute).{0} is replaced with the number of hours")
                    : _T("{0} hours {1} minutes ago", h, m, "Text that indicates how old a chat message is (less than a day, never 1 hour or 1 minute).{0} is replaced with the number of hours, {1} with the minutes"));
        }
        const days = Math.floor(h / 24);
        const hr = h % 24;
        if (days < 7) {
            if (days === 1)
                return hr === 0
                    ? _TF("1 day ago", "Text that indicates that a chat message was written one day ago")
                    : (hr === 1
                        ? _TF("1 day 1 hour ago", "Text that indicates that a chat message was written one day and one hour ago")
                        : _T("1 day {0} hours ago", hr, "Text that indicates how old a chat message is (one day and some hours, never 1).{0} is replaced with the number of hours"));
            return hr === 0
                ? _T("{0} days ago", days, "Text that indicates how old a chat message is in days (never 1).{0} is replaced with the number of days")
                : (hr === 1
                    ? _T("{0} days 1 hour ago", days, "Text that indicates how old a chat message is (some days, never 1, and one hour).{0} is replaced with the number of days")
                    : _T("{0} days {1} hours ago", days, hr, "Text that indicates how old a chat message is (less than a week, never 1 day or 1 hour).{0} is replaced with the number of days, {1} with the hours"));
        }
        return _T("{0} days ago", days, "Text that indicates how old a chat message is in days (never 1).{0} is replaced with the number of days");
    }

    /**
     * The full time stamp tool tip of a message time: local date and time, plus UTC.
     * @param {?Date} d The message time.
     * @returns {string} The text ("" without a time).
     */
    function timeTitle(d) {
        if (!d)
            return "";
        return d.toLocaleString([], { dateStyle: "full", timeStyle: "medium" }) + "\n" +
            _TF("UTC", "Tool tip row prefix to some time stamp written using js Date.toUTCString() syntax") + ": " + d.toISOString().replace("T", " ").substring(0, 19);
    }

    // ------------------------------------------------------------------ icon button names

    /**
     * Set the name of an icon only button: the tool tip (mouse hover) and the accessible name.
     * @param {HTMLElement} b The button.
     * @param {string} text The name.
     */
    function setButtonName(b, text) {
        if (b.title !== text)
            b.title = text;
        if (b.getAttribute("aria-label") !== text)
            b.setAttribute("aria-label", text);
    }

    /**
     * Show the name (tool tip) of a plain icon only button on long press (touch / pen) and keyboard focus (Sw.Hint; Sw.Button does
     * this itself). A touch press on the button does not start the long press of a surrounding element (the message header
     * menu), so a long press on the button shows its name.
     * @param {HTMLElement} b The button.
     */
    function attachHint(b) {
        if (Sw.Hint)
            Sw.Hint.Attach(b);
        b.addEventListener("pointerdown", ev => {
            if (ev.pointerType !== "mouse")
                ev.stopPropagation();
        });
    }

    /**
     * Add a short text label to an icon only button: below the icon (.chat-labels, the stacked layout), beside the icon where the
     * input has room (the slim menu bar), see Composer.WatchSize; hidden from assistive technology (the button has
     * its full accessible name). The class sw-button-extra keeps it when Sw.Button replaces its content (SetWorking, SetIcon).
     * @param {HTMLElement} b The button.
     * @param {string} text The label.
     * @returns {HTMLElement} The label element (span.chat-tlabel).
     */
    function addTouchLabel(b, text) {
        const l = el("span.chat-tlabel.sw-button-extra", { text, attrs: { "aria-hidden": "true" } });
        b.append(l);
        b.classList.add("chat-labelled");
        return l;
    }

    // ------------------------------------------------------------------ options, colors, settings

    /** Options of a chat (legacy global `ChatOptions`); the `options` url parameter of Chat.html is merged into it. */
    class Options {
        /** The size in pixels of the top right menu icon. @type {number} */
        MenuIconSize = 48;
        /** The size in pixels of the microphone icon. @type {number} */
        AudioIconSize = 48;
        /** The size in pixels of the send icon. @type {number} */
        SendIconSize = 48;
        /** If true every sender gets its own (stable) colors. @type {boolean} */
        UserColor = true;
        /**
         * Selects the local storage keys of the settings: null = shared by all chats, "*" = per chat id, any other string = a named group.
         * @type {?string}
         */
        SettingsName = null;
    }

    /** @type {Map<string, string[]>} Sender name -> colors cache. */
    const colorCache = new Map();

    /**
     * Stable colors of a sender name (sha-256 of the name -> hue / saturation / lightness).
     * @param {string} text The sender name.
     * @returns {string[]} `[border, headerBackground, headerText]`.
     */
    function Color(text) {
        text = text || "";
        let c = colorCache.get(text);
        if (c)
            return c;
        const h = Sw.Util.Sha256Sync(new TextEncoder().encode(text));
        const hue = (h[0] % 90) * 4;
        const sat = (h[1] % 40) + 50;
        const l = (Math.max(0, 40 - Math.abs(hue - 240)) + 40) | 0;
        const bc = "hsl(" + hue + " " + sat + " " + l;
        //  The name text leans to the theme's text color, so it keeps its contrast (4.5:1) on light and dark themes, the hue stays
        c = [bc + " / 0.5)", bc + " / 0.05)", "color-mix(in oklab, " + bc + ") 50%, var(--sw-fg))"];
        colorCache.set(text, c);
        return c;
    }

    /**
     * The persisted settings and drafts of a chat (localStorage, keys unchanged from the old client).
     * `<s>` = "" (shared), "." + chatId (SettingsName "*") or "." + SettingsName.
     */
    class Settings {
        /**
         * @param {string} chatId The chat id.
         * @param {?string} settingsName The SettingsName option.
         */
        constructor(chatId, settingsName) {
            const s = settingsName ? "." + (settingsName === "*" ? chatId : settingsName) : "";
            /**
             * The key names: time vs age, TTS volume, TTS on, auto send, markdown, preview, split size (per settings scope); open mic,
             * hidden senders, draft text, draft data, input language (per chat).
             * @type {Object<string, string>}
             */
            this.Keys = {
                TimeStamp: "SysWeaver.Chat.ShowTimeStamp" + s,
                Volume: "SysWeaver.Chat.SpeechGenVol" + s,
                Speech: "SysWeaver.Chat.SpeechGen" + s,
                AutoSend: "SysWeaver.Chat.SpeechSend" + s,
                MarkDown: "SysWeaver.Chat.UseMarkDown" + s,
                Preview: "SysWeaver.Chat.Preview" + s,
                Size: "SysWeaver.Chat.Size" + s,
                Listen: "SysWeaver.Chat.SpeechListen" + chatId,
                Hide: "SysWeaver.Chat.Hide." + chatId,
                Input: "SysWeaver.Chat.Input." + chatId,
                Data: "SysWeaver.Chat.Data." + chatId,
                Lang: "SysWeaver.Chat.Lang." + chatId,
            };
        }

        /**
         * Read a value.
         * @param {string} name The key name (a member of Keys).
         * @returns {?string} The stored string, or null.
         */
        Get(name) {
            return Sw.Storage.Local.Get(this.Keys[name], null);
        }

        /**
         * Write a value (null / undefined removes it).
         * @param {string} name The key name (a member of Keys).
         * @param {*} value The value (stored as a string).
         */
        Set(name, value) {
            const k = this.Keys[name];
            if (value === null || value === undefined)
                Sw.Storage.Local.Remove(k);
            else
                Sw.Storage.Local.Set(k, String(value));
        }

        /**
         * Read a "true" / "false" value.
         * @param {string} name The key name.
         * @param {boolean} def The default.
         * @param {boolean} [store] Store the default when missing (the old behavior of some keys).
         * @returns {boolean} The value.
         */
        Bool(name, def, store) {
            const v = this.Get(name);
            if (v === null) {
                if (store)
                    this.Set(name, def ? "true" : "false");
                return !!def;
            }
            return v === "true";
        }
    }

    // ------------------------------------------------------------------ attachments

    /**
     * Keeps images and embedded pages at least 16:9 (min-height = width * 9 / 16), again whenever their width changes (the chat is
     * resized, or the user resized the box); one observer for all of them.
     */
    const aspectObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            for (const e of entries) {
                const box = e.target;
                //  Removed (the message was rendered again or deleted): stop observing it
                if (!box.isConnected) {
                    aspectObserver.unobserve(box);
                    continue;
                }
                //  The width from the observer entry; the write waits for the next frame (a size change of an observed element inside
                //  its own callback would be reported as a ResizeObserver loop)
                const w = e.borderBoxSize && e.borderBoxSize[0] ? e.borderBoxSize[0].inlineSize : box.getBoundingClientRect().width;
                if (w > 0) {
                    const h = Math.round(w * 9 / 16) + "px";
                    if (box.style.minHeight !== h)
                        requestAnimationFrame(() => {
                            box.style.minHeight = h;
                        });
                }
            }
        })
        : null;

    /**
     * A message text is a Tab stop only while it is cut at the height limit (it scrolls while focused); else a click still focuses it.
     * @param {HTMLElement} body The .chat-content.
     */
    function checkTabStop(body) {
        const ti = body.scrollHeight > body.clientHeight + 1 ? 0 : -1;
        if (body.tabIndex !== ti)
            body.tabIndex = ti;
    }

    /** Re-checks the Tab stop of a message text when its size changes (one observer for all; removed rows are dropped). */
    const bodyObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => {
            for (const e of entries) {
                if (e.target.isConnected)
                    checkTabStop(e.target);
                else
                    bodyObserver.unobserve(e.target);
            }
        })
        : null;

    /** Renders the attachments of a message and builds their menus. */
    class Attachments {
        /**
         * Describe one attachment url.
         * @param {ChatView} view The chat.
         * @param {string} orgUrl The url as stored in the message data (normally "../" relative).
         * @returns {object} { Org, Url (absolute), Ext, Name, IsImage, IsHtml, CanSave, CanLink }.
         */
        static Describe(view, orgUrl) {
            const url = Sw.Url.Absolute(orgUrl);
            //  The name is for showing and saving: uri decoded ("My%20file.png" -> "My file"); the urls stay escaped
            const file = Sw.Url.FileName(url);
            const dot = file.lastIndexOf(".");
            const ext = dot >= 0 ? file.substring(dot + 1).toLowerCase() : "";
            const isHtml = !!HtmlExtensions[ext];
            return {
                Org: orgUrl,
                Url: url,
                Ext: ext,
                Name: dot >= 0 ? file.substring(0, dot) : file,
                IsImage: Sw.Format.IsImageExt(ext),
                IsHtml: isHtml,
                CanSave: !isHtml && !orgUrl.startsWith("../storage/" + view.Data.UserStore + "/"),
                CanLink: orgUrl.indexOf("://") < 0,
            };
        }

        /**
         * Render the attachments into the data element of a row. The message's DataViews (C# ChatDataView {Raw, Viewer, Source}, one
         * per data entry, filled by the server from the extension viewers) say how an entry is shown; without them (older servers, the
         * draft preview) an entry is shown by its extension alone.
         * @param {MessageRow} row The row.
         * @param {?string} data The `;` separated urls.
         */
        static Render(row, data) {
            const holder = row.DataE;
            holder.replaceChildren();
            if (!data)
                return;
            //  A message that is still being produced: its data is a status (ex: the AI's working.svg), shown as a plain small image,
            //  no info bar, menu or size rules (the real attachments come with the finished message)
            if (row.Msg && (row.Msg.Flags & Flags.IsWorking) !== 0) {
                for (const part of data.split(";")) {
                    const org = part.trim();
                    if (org)
                        holder.append(Attachments.Status(org));
                }
                return;
            }
            const views = (row.Msg && Array.isArray(row.Msg.DataViews)) ? row.Msg.DataViews : null;
            data.split(";").forEach((part, i) => {
                const org = part.trim();
                if (!org)
                    return;
                const v = views && views[i] && views[i].Raw === org ? views[i] : null;
                holder.append(Attachments.Item(row, org, v));
            });
        }

        /**
         * A status image of a message that is still being produced (ex: the working indicator): a plain decorative image.
         * @param {string} org The url as stored.
         * @returns {HTMLElement} The element (div.chat-att-status).
         */
        static Status(org) {
            const text = _TF("Working…", "Text alternative of the animated image shown while a chat answer is being produced");
            //  Reduced motion: the image may animate by itself (svg animations can't be stopped from css), so a quiet text instead
            if (matchMedia("(prefers-reduced-motion: reduce)").matches)
                return el("div.chat-att-status.chat-att-status-text", { text });
            const img = Sw.CreateImg();
            img.alt = text;
            img.src = Sw.Url.Absolute(org);
            return el("div.chat-att-status", null, img);
        }

        /**
         * Create the element of one attachment: an image (click opens it), an embedded page (html, or the viewer page of the asset)
         * under an info bar, or a file type icon with the file name (click downloads, the name copies the url); plus a menu button
         * (over the image / file, in the info bar of an embedded page). The menu always works on the raw asset.
         * @param {MessageRow} row The row.
         * @param {string} org The url as stored (the raw asset).
         * @param {?object} [view] The data view {Raw, Viewer, Source} from the server.
         * @returns {HTMLElement} The attachment element.
         */
        static Item(row, org, view) {
            const a = Attachments.Describe(row.View, org);
            const box = el("div.chat-att");
            const menu = new Sw.Button({
                Icon: "moreV", Kind: "ghost", Small: true, Class: "chat-att-menu",
                Title: _TF("Show menu", "Tool tip description of a button that when clicked will show a context menu with options"),
                Menu: () => Attachments.MenuItems(row, a), MenuOptions: { Align: "end" },
            });
            const viewer = view && view.Viewer ? Sw.Url.Absolute(view.Viewer) : null;
            const source = view && view.Source ? Sw.Url.Absolute(view.Source) : null;
            //  Same rule as markdown images: other origins / api / query urls load on click only (tracking pixels, GET requests)
            const blocked = a.IsImage && Sw.Markdown && Sw.Markdown.CanAutoLoadImage && !Sw.Markdown.CanAutoLoadImage(a.Url);
            if (a.IsHtml || viewer || (a.IsImage && !blocked)) {
                //  The main view: the viewer page, the html page itself, or the image (all with the same info bar)
                const main = viewer
                    ? { Url: viewer, Frame: true, ReadTitle: false }
                    : a.IsHtml ? { Url: a.Url, Frame: true, ReadTitle: true } : { Url: a.Url, Frame: false, ReadTitle: false };
                box.classList.add("chat-att-html");
                Attachments.Framed(box, a, menu, main, source);
                //  A right click (a long press on touch, the Menu key) on the info bar shows the same menu as its ⋮ button, at the pointer
                Sw.Menu.Context(box.querySelector(".chat-att-bar"), () => Attachments.MenuItems(row, a));
                if (aspectObserver)
                    aspectObserver.observe(box);
                return box;
            }
            else if (blocked) {
                box.classList.add("chat-att-image", "chat-att-blocked");
                box.append(Sw.Markdown.ImagePlaceholder(a.Url, a.Name));
            }
            else {
                //  Any other file: its type icon (a click downloads it) and its name (a click copies the url)
                const img = Sw.CreateImg();
                img.alt = a.Name;
                img.src = "../icons/ext/" + encodeURIComponent(a.Ext) + ".svg";
                box.classList.add("chat-att-file");
                img.title = _T('Download "{0}"', a.Url, "Tool tip description on some content that when clicked will download the content.{0} is replaced with the url");
                img.setAttribute("role", "button");
                Sw.KeyClick(img, ev => {
                    if (!Sw.BadClick(ev))
                        Sw.Net.Download(a.Url);
                });
                const label = el("div.chat-att-label", { text: a.Name, title: a.Url });
                Sw.Clipboard.CopyOnClick(label, a.Url, { Keyboard: true, AddTitle: true });
                box.append(img, label);
            }
            box.append(menu.E);
            if (aspectObserver && box.classList.contains("chat-att-image"))
                aspectObserver.observe(box);
            return box;
        }

        /**
         * Fill an embedded attachment: the info bar (as wide as the content, so nothing covers it) above the content:
         * [collapse / expand] [name] ... [source] [open in a new tab] [menu].
         * - The name: the document title of an html page when it can be read (same origin), else the file name (with extension).
         * - Source (only with a source view): switches the content between the main view and the source view (aria-pressed).
         * - Open in a new tab: the view that is shown (the raw page, the viewer or the source view); the menu works on the raw asset.
         * - Collapsed (.chat-att-collapsed) only the bar shows; the size the user gave the box is kept for expanding again.
         * @param {HTMLElement} box The attachment box (.chat-att-html).
         * @param {object} a The attachment info (Attachments.Describe, the raw asset).
         * @param {object} menu The menu button (Sw.Button).
         * @param {{Url: string, Frame: boolean, ReadTitle: boolean}} main The main view (Frame: an iframe, else an image).
         * @param {?string} source The source view url (an iframe), or null.
         */
        static Framed(box, a, menu, main, source) {
            const fileName = a.Ext ? a.Name + "." + a.Ext : a.Name;
            const title = el("span.chat-att-title", { text: fileName, title: a.Url });
            /**
             * Create the content element of a view.
             * @param {string} url The url.
             * @param {boolean} frame An iframe (else an image).
             * @param {boolean} readTitle Show the frame's document title as the name.
             * @returns {HTMLElement} The element.
             */
            const create = (url, frame, readTitle) => {
                if (!frame) {
                    const img = Sw.CreateImg();
                    img.alt = fileName;
                    img.src = url;
                    img.className = "chat-att-view";
                    return img;
                }
                const f = Sw.CreateIFrame();
                f.sandbox = InternalHtmlSandbox; // HtmlSandbox;
                f.title = fileName;
                f.className = "chat-att-view";
                f.src = Sw.Url.AppendTheme(url);
                if (readTitle)
                    f.addEventListener("load", () => {
                        let t = "";
                        try {
                            t = ((f.contentDocument && f.contentDocument.title) || "").trim();
                        }
                        catch {
                            //  Another origin: the title can't be read
                        }
                        if (t) {
                            title.textContent = t;
                            f.title = t;
                        }
                    });
                return f;
            };
            const content = create(main.Url, main.Frame, main.ReadTitle);
            /** @type {?HTMLElement} The source view (made on the first "Source"). */
            let srcView = null;
            let shown = main.Url;
            //  One name with the file ("Show <name>"), the state in aria-expanded (and the arrow), not a name that changes
            const flip = () => {
                const collapsed = box.classList.toggle("chat-att-collapsed");
                toggle.SetIcon(collapsed ? "right" : "down");
                toggle.E.setAttribute("aria-expanded", collapsed ? "false" : "true");
            };
            const toggle = new Sw.Button({
                Icon: "down", Kind: "ghost", Small: true, Class: "chat-att-toggle",
                Title: _T("Show {0}", fileName, "Name of the collapse / expand toggle of an embedded page or file in a chat message (its pressed state says if it is shown).{0} is replaced with the file name"),
                OnClick: flip,
            });
            toggle.E.setAttribute("aria-expanded", "true");
            const bar = el("div.chat-att-bar", null, toggle.E, title);
            //  A click anywhere on the bar (not on its buttons) collapses / expands too; the keyboard uses the toggle button
            bar.addEventListener("click", ev => {
                if (ev.target.closest && ev.target.closest("button, a, input"))
                    return;
                if (Sw.BadClick && Sw.BadClick(ev))
                    return;
                flip();
            });
            if (source) {
                //  A toggle with one name ("Source") and its state in aria-pressed (and the pressed look), not a name that changes
                let onSource = false;
                const src = new Sw.Button({
                    Icon: "code", Kind: "ghost", Small: true, Class: "chat-att-source",
                    Text: _TF("Source", "Text of a toggle button in the title bar of an embedded page or image in a chat message: shows its source code instead of the page while pressed"),
                    Title: _TF("Show the source code instead of the page", "Tool tip of a toggle button in the title bar of an embedded page or image in a chat message: shows its source code instead"),
                    OnClick: () => {
                        onSource = !onSource;
                        //  Both views are kept once made (shown / hidden), so the page keeps its state and scroll and is not loaded again
                        if (!srcView) {
                            srcView = create(source, true, false);
                            srcView.hidden = true;
                            content.after(srcView);
                        }
                        content.hidden = onSource;
                        srcView.hidden = !onSource;
                        shown = onSource ? source : main.Url;
                        src.E.setAttribute("aria-pressed", onSource ? "true" : "false");
                        box.classList.toggle("chat-att-on-source", onSource);
                    },
                });
                src.E.setAttribute("aria-pressed", "false");
                bar.append(src.E);
            }
            const open = new Sw.Button({
                Icon: "external", Kind: "ghost", Small: true, Class: "chat-att-open",
                Title: _TF("Open in a new tab", "Tool tip of a button that opens an embedded page of a chat message in a new browser tab"),
                OnClick: () => Sw.Page.Navigate(shown, "_blank"),
            });
            bar.append(open.E, menu.E);
            box.append(bar, content);
        }

        /**
         * Store a file / create a link on the server, then copy the result and open it.
         * @param {ChatView} view The chat.
         * @param {string} method "StoreFile" or "StoreLink".
         * @param {string} url The (absolute) url.
         * @param {number} scope 0 private, 1 protected, 2 public.
         * @returns {Promise<void>}
         */
        static async Store(view, method, url, scope) {
            const failText = method === "StoreFile"
                ? _TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure.")
                : _TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure.");
            const stored = await call(view.Api(method), { Url: url, Scope: scope }, failText);
            if (!stored)
                return;
            const abs = Sw.Url.Absolute(stored);
            await copy(abs);
            Sw.Page.Navigate(abs, "_blank");
        }

        /**
         * The scope sub menu of "Store file" / "Create link".
         * @param {ChatView} view The chat.
         * @param {string} method "StoreFile" or "StoreLink".
         * @param {string} url The url.
         * @returns {object[]} The items.
         */
        static ScopeItems(view, method, url) {
            const isFile = method === "StoreFile";
            const items = [
                {
                    Text: _TF("Private", "Text of menu item that indicates that this is private content, only accessable to the user"),
                    Icon: "IconChatLink0",
                    Title: isFile
                        ? _TF("Store the file on the server for later use, only available to you", "Tool tip description of menu item that when clicked will store some content on the server")
                        : _TF("Save as a link that only you can view", "Tool tip description of menu item that when clicked will store some content on the server and create a private (only accesible by this user) link to it"),
                    OnClick: () => Attachments.Store(view, method, url, 0),
                },
                {
                    Text: _TF("Protected", "Text of menu item that indicates that this is protected content, only accessable to logged in users"),
                    Icon: "IconChatLink1",
                    Title: isFile
                        ? _TF("Store the file on the server for later use, available to any logged in users", "Tool tip description of menu item that when clicked will store some content on the server")
                        : _TF("Save as a link that all logged in users can view", "Tool tip description of menu item that when clicked will store some content on the server and create a protected (only accesible to logged in users) link to it"),
                    OnClick: () => Attachments.Store(view, method, url, 1),
                },
            ];
            if (view.Data.AllowPublicStore)
                items.push({
                    Text: _TF("Public", "Text of menu item that indicates that this is public content, accessable to everyone"),
                    Icon: "IconChatLink2",
                    Title: isFile
                        ? _TF("Store the file on the server for later use, available to anyone", "Tool tip description of menu item that when clicked will store some content on the server")
                        : _TF("Save as a link that anyone can view", "Tool tip description of menu item that when clicked will store some content on the server and create a public (accesible to anyone) link to it"),
                    OnClick: () => Attachments.Store(view, method, url, 2),
                });
            return items;
        }

        /**
         * The menu of one attachment.
         * @param {MessageRow} row The row.
         * @param {object} a The attachment info (Attachments.Describe).
         * @returns {object[]} The menu items.
         */
        static MenuItems(row, a) {
            const view = row.View;
            const items = [];
            if (row.IsPreview) {
                const composer = view.Composer;
                if (a.IsImage && composer.UseMarkDown)
                    items.push({
                        Text: _TF("Copy image Markdown", "Text of a menu item that when clicked will copy the Markdown required to display this image"),
                        Icon: "IconChatCopyImage",
                        Title: _TF("Copy the Markdown required to show the file to the clipboard", "Tool tip description of a menu item that when clicked will copy the Markdown required to display this image"),
                        OnClick: () => copy("![](" + a.Org + ")"),
                    });
                items.push({
                    Text: _TF("Copy url", "Text of a menu item that when clicked will copy the url text"),
                    Icon: "IconChatCopy",
                    Title: _TF("Copy the url to the clipboard", "Tool tip description of a menu item that when clicked will copy the url text"),
                    OnClick: () => copy(a.Url),
                });
                items.push({
                    Text: _TF("Delete file", "Text of a menu item that when clicked will delete the file from the server"),
                    Icon: "IconChatDeleteFile",
                    Title: _TF("Delete this file from the server", "Tool tip description of a menu item that when clicked will delete the file from the server"),
                    OnClick: () => composer.RemoveAttachment(a.Org, true),
                });
                items.push({
                    Text: _TF("Hide file", "Text of a menu item that when clicked will hide the file"),
                    Icon: "IconChatHideFile",
                    Title: _TF("Hide this file", "Tool tip description of a menu item that when clicked will hide the file"),
                    OnClick: async () => {
                        await composer.RemoveAttachment(a.Org, false);
                        await copy(a.IsImage ? "![](" + a.Org + ")" : "[" + a.Name.replace(/[[\]\\]/g, "\\$&") + "](" + a.Url + ")");
                    },
                });
            }
            const download = !a.IsImage && !a.IsHtml;
            if (!download)
                items.push({
                    Text: _TF("Open in new tab", "Text of a menu item that when clicked will open some content in a new browser tab"),
                    Icon: "IconChatExpand",
                    Title: _TF("Show in a new tab", "Tool tip description of a menu item that when clicked will open some content in a new browser tab"),
                    OnClick: () => Sw.Page.Navigate(a.Url, "_blank"),
                });
            if (!a.IsHtml)
                items.push({
                    Text: _TF("Download", "Text of menu item that when clicked will download some content to a file"),
                    Icon: "IconChatSave",
                    Title: _TF("Save the file to your device", "Tool tip description of menu item that when clicked will download some content to a file"),
                    OnClick: () => Sw.Net.Download(a.Url),
                });
            if (view.CanPost) {
                if (a.CanSave)
                    items.push({
                        Text: _TF("Store file", "Text of a menu folder that contains items that when clicked store this file to the server"),
                        Icon: "IconChatStore",
                        Title: _TF("Store the file on the server for later use", "Tool tip description of menu folder that contains items that when clicked store this file to the server"),
                        Items: () => Attachments.ScopeItems(view, "StoreFile", a.Url),
                    });
                if (a.CanLink)
                    items.push({
                        Text: _TF("Create link", "Text of a menu folder that contains items that when clicked will create a link to this file"),
                        Icon: "IconChatLink",
                        Title: _TF("Create a link to the file for later use", "Tool tip description of menu folder that contains items that when clicked will create a link to this file"),
                        Items: () => Attachments.ScopeItems(view, "StoreLink", a.Url),
                    });
            }
            return items;
        }
    }

    // ------------------------------------------------------------------ message row

    /** One message row (also the draft preview row): header (flag, name, time, sender image / menu button), content, attachments. */
    class MessageRow {
        /**
         * Create the row elements (insert with MessageList.Insert, render with Update).
         * @param {ChatView} view The chat.
         * @param {object} m The ChatMessage { Id, From, FromImage, Time, Flags, Text, Data, Format, Lang, MenuItems }.
         * @param {boolean} [isPreview] The draft preview row.
         */
        constructor(view, m, isPreview) {
            const t = this;
            /** @type {ChatView} The chat. */
            t.View = view;
            /** @type {object} The current message. */
            t.Msg = m;
            /** @type {boolean} The draft preview row. */
            t.IsPreview = !!isPreview;
            /** @type {boolean} The content is hidden. */
            t.Hidden = false;
            /** @type {?string} The cached translation. */
            t.Translated = null;
            /** @type {boolean} The translation is shown. */
            t.IsTranslated = false;
            /** @type {boolean} A translation request is running. */
            t.Translating = false;
            /** @type {number} Body render sequence (a newer render wins). */
            t.Seq = 0;
            /** @type {?string|undefined} The rendered text (to skip unchanged updates). */
            t.CurText = undefined;
            /** @type {?number|undefined} The rendered format. */
            t.CurFormat = undefined;
            /** @type {?string|undefined} The rendered attachments. */
            t.CurData = undefined;
            /** @type {?SpeechSynthesisVoice} The voice speaking this message (speech output). */
            t.Voice = null;

            const isMine = m.From === view.UserName;
            /** The row element (expando SwChatRow = this). */
            t.E = el("div.chat-row" + (isMine ? ".chat-mine" : "") + (isPreview ? ".chat-preview.chat-hide" : ""), { attrs: { role: "article" } });
            t.E.SwChatRow = t;
            /** The bubble. */
            t.Bubble = el("div.chat-msg");
            /** The header (click toggles hiding, long press / right click open the menu; a long press on its buttons shows their names). */
            t.Header = el("div.chat-header");
            /** The sender name. */
            t.NameE = el("span.chat-name", { text: m.From });
            /** The "AI" tag after the name (shown while the message has the FromAi flag, see UpdateLabels). */
            t.AiTag = el("span.chat-ai-tag", {
                text: _TF("AI", "A short tag after the sender name of a chat message that was written by an AI (artificial intelligence)"),
                title: _TF("Written by an AI (artificial intelligence), not by a person", "Tool tip of the AI tag after the sender name of a chat message"),
                attrs: { hidden: "" },
            });
            /** The time / age. */
            t.TimeE = el("span.chat-time");
            /** The sender image (a button opening the message menu). */
            t.IconE = el("button.chat-icon", { type: "button", title: _TF("Show message menu", "Tool tip description on a button that when clicked will show a menu with options for a chat message") });
            /** The content. */
            t.Body = el("div.chat-content", { tabIndex: 0, attrs: { role: "group", "aria-label": _TF("Message text", "Accessible name of the text of one chat message (it can be focused to scroll a long message)") } });
            if (bodyObserver)
                bodyObserver.observe(t.Body);
            //  An image inside that loads later makes the text taller (load does not bubble: capture)
            t.Body.addEventListener("load", () => checkTabStop(t.Body), true);
            /** The attachments. */
            t.DataE = el("div.chat-data");
            if (isPreview)
                t.E.append(el("div.chat-preview-label", { text: _TF("Preview", "Tool tip description indicating that this is a chat preview message") }));
            if (view.Options.UserColor) {
                const cols = Color(m.From);
                t.Bubble.style.borderColor = cols[0];
                t.Header.style.backgroundColor = cols[1];
                t.Header.style.color = cols[2];
            }
            if (view.CanTranslate) {
                /** The language flag image. */
                t.FlagImg = Sw.CreateImg();
                t.FlagImg.alt = "";
                /** The language button (translation). */
                t.LangE = el("button.chat-lang", { type: "button" }, t.FlagImg);
                attachHint(t.LangE);
                t.LangE.addEventListener("click", ev => {
                    if (!Sw.BadClick(ev))
                        t.OnFlag();
                });
                t.Header.append(t.LangE);
            }
            t.Header.append(el("div.chat-who", null, el("span.chat-name-line", null, t.NameE, t.AiTag), t.TimeE), t.IconE);
            /** A visible menu button (the menu is also on the sender image, a long press and a right click) */
            t.MoreE = new Sw.Button({
                Icon: "moreV", Kind: "ghost", Small: true, Class: "chat-msg-more",
                Title: _TF("Message menu", "Tool tip of the button in a chat message header that shows the menu of the message"),
                OnClick: (b, ev) => t.ShowMenu(ev, b.E),
            });
            //  A menu button (a right click opens it too: the header's long press / context menu handler, at the pointer)
            t.MoreE.E.setAttribute("aria-haspopup", "menu");
            t.Header.append(t.MoreE.E);
            t.Bubble.append(t.Header, t.Body, t.DataE);
            t.E.append(t.Bubble);
            t.SetIcon(m.FromImage);
            attachHint(t.IconE);
            t.IconE.addEventListener("click", ev => {
                if (!Sw.BadClick(ev))
                    t.ShowMenu(ev, t.IconE);
            });
            //  Long press (touch), right click and the context menu key open the message menu (at the pointer, the key: at the ⋮ button)
            Sw.LongPress(t.Header, ev => t.ShowMenu(ev, t.MoreE.E));
            if (isPreview)
                t.Header.title = _TF("This is a preview of a message, it's not visible to anyone but you", "Tool tip description letting the user know what this is");
            else {
                t.Header.classList.add("chat-toggle");
                //  A header click toggles only this message; the new state becomes the default for new messages of the sender
                t.Header.addEventListener("click", ev => {
                    if (ev.target.closest("button") || Sw.BadClick(ev))
                        return;
                    view.ToggleRow(t);
                });
                t.SetHidden(view.IsHidden(m.From));
            }
            t.UpdateLang();
            t.UpdateFormat();
        }

        /**
         * Set the sender image (falls back to a menu icon when missing or broken).
         * @param {?string} src The image url (FromImage).
         */
        SetIcon(src) {
            const iconE = this.IconE;
            const fallback = () => iconE.replaceChildren(Sw.IconClass(MenuIcon, { Class: "chat-icon-fallback" }));
            if (src === this.View.CurrentImage)
                src = CurrentUserImage;
            if (!src) {
                fallback();
                return;
            }
            const img = el("img", { alt: "" });
            iconE.replaceChildren(img);
            Sw.SetImageSource(img, src, { OnLoaded: ok => ok || fallback() });
        }

        /**
         * Show or hide the content (no persistence, see ChatView.ToggleRow).
         * @param {boolean} hide True to hide.
         */
        SetHidden(hide) {
            const t = this;
            if (t.IsPreview)
                return;
            t.Hidden = !!hide;
            t.E.classList.toggle("chat-hidden", t.Hidden);
            const tip = t.Hidden
                ? _T("Show this message, new messages from {0} are shown too", t.Msg.From, "A tool tip message for a chat message header that when clicked will show the hidden content of that message (only that one), new messages from that user will also be shown.{0} is replaced with the user name. ")
                : _T("Hide this message, new messages from {0} are hidden too", t.Msg.From, "A tool tip message for a chat message header that when clicked will hide the content of that message (only that one), new messages from that user will also be hidden.{0} is replaced with the user name. ");
            t.Header.title = "#" + t.Msg.Id + "\n" + tip;
        }

        /**
         * Render the header time (ChatView.ShowTimeStamp: the absolute time stamp, else the age text of ageText, refreshed by
         * MessageList.UpdateAges) and the accessible names.
         */
        UpdateFormat() {
            const t = this;
            if (t.IsPreview) {
                t.UpdateLabels();
                return;
            }
            if (t.View.ShowTimeStamp)
                Sw.Cells.DateTime(t.TimeE, t.Msg.Time, null, null, false, null, null, false, true);
            else {
                //  Clears a previous time stamp renderer (classes, tool tip)
                Sw.Cells.Reset(t.TimeE, "ChatAge");
                const d = messageDate(t.Msg.Time);
                t.TimeE.title = timeTitle(d);
                t.UpdateAge(Date.now());
            }
            t.UpdateLabels();
        }

        /**
         * Refresh the age text (age mode only) and the accessible names when it changed.
         * @param {number} now The current time (ms).
         */
        UpdateAge(now) {
            const t = this;
            if (t.IsPreview || t.View.ShowTimeStamp)
                return;
            const text = ageText(messageDate(t.Msg.Time), now);
            if (t.TimeE.textContent === text)
                return;
            t.TimeE.textContent = text;
            t.UpdateLabels();
        }

        /** Set the accessible names of the row (sender and time) and of the message menu button. */
        UpdateLabels() {
            const t = this;
            let row, menu;
            if (t.IsPreview) {
                row = _TF("Preview of your message", "Accessible name of the chat row that previews the message being written");
                menu = _TF("Message menu, preview", "Accessible name of the button that shows the menu of the chat message preview");
            }
            else {
                const when = t.TimeE.textContent;
                const ai = (t.Msg.Flags & Flags.FromAi) !== 0;
                t.AiTag.hidden = !ai;
                row = ai
                    ? _T("Message from the AI {0}, {1}", t.Msg.From, when, "Accessible name of a chat message written by an AI (artificial intelligence).{0} is replaced with the sender name, {1} with the time (ex: \"14:02\" or \"5 minutes ago\")")
                    : _T("Message from {0}, {1}", t.Msg.From, when, "Accessible name of a chat message.{0} is replaced with the sender name, {1} with the time (ex: \"14:02\" or \"5 minutes ago\")");
                menu = _T("Message menu, {0}, {1}", t.Msg.From, when, "Accessible name of the button that shows the menu of a chat message.{0} is replaced with the sender name, {1} with the time (ex: \"14:02\" or \"5 minutes ago\")");
            }
            if (t.E.getAttribute("aria-label") !== row)
                t.E.setAttribute("aria-label", row);
            if (t.IconE.getAttribute("aria-label") !== menu)
                t.IconE.setAttribute("aria-label", menu);
        }

        /**
         * Render the content (text, markdown or html). A render that finishes after a newer one started is discarded (streaming).
         * @param {?string} text The text.
         * @param {number} format The format (Formats).
         * @param {boolean} isWorking The message is still being produced (row class chat-working).
         * @returns {Promise<void>}
         */
        async UpdateBody(text, format, isWorking) {
            const t = this;
            t.E.classList.toggle("chat-working", !!isWorking);
            if (t.CurText === text && t.CurFormat === format)
                return;
            t.CurText = text;
            t.CurFormat = format;
            const seq = ++t.Seq;
            const body = t.Body;
            if (!text) {
                body.textContent = "";
                body.removeAttribute("title");
                body.tabIndex = -1;
                return;
            }
            try {
                switch (format) {
                    case Formats.MarkDown: {
                        const html = await Sw.Markdown.ToHtml(text);
                        if (seq !== t.Seq)
                            return;
                        body.title = _TF("Message is markdown", "A tool tip for a chat message that is written using Markdown (MD)");
                        body.innerHTML = html;
                        break;
                    }
                    case Formats.Html:
                        body.title = _TF("Message is HTML", "A tool tip for a chat message that is written using HTML");
                        //  Only the server application may send html; it is still sanitized (the markdown allow list)
                        if (Sw.Markdown && Sw.Markdown.Sanitize)
                            body.innerHTML = Sw.Markdown.Sanitize(text);
                        else
                            body.textContent = text;
                        break;
                    default:
                        body.title = _TF("Message is plain text", "A tool tip for a chat message that is written using plain text");
                        body.innerText = text;
                        break;
                }
            }
            catch {
                if (seq === t.Seq)
                    body.innerText = text;
            }
            //  Every render (messages, translations, the draft preview); also on a resize and when an image inside loads (MessageRow)
            requestAnimationFrame(() => checkTabStop(body));
        }

        /**
         * Render the attachments (skipped if unchanged).
         * @param {?string} data The `;` separated urls.
         */
        UpdateData(data) {
            data = data || null;
            //  Rendered again when the data or the working state changes (a working message shows its data as a status)
            const working = !!this.Msg && (this.Msg.Flags & Flags.IsWorking) !== 0;
            const key = (working ? "w:" : "d:") + (data || "");
            if (this.CurData === key)
                return;
            this.CurData = key;
            Attachments.Render(this, data);
        }

        /**
         * Apply a (new version of the) message: content and attachments (a shown translation stays until toggled back).
         * @param {object} m The ChatMessage.
         * @returns {Promise<void>}
         */
        async Update(m) {
            const wasWorking = !!this.Msg && (this.Msg.Flags & Flags.IsWorking) !== 0;
            this.Msg = m;
            this.UpdateLabels();
            this.UpdateData(m.Data);
            if (this.IsTranslated) {
                this.E.classList.toggle("chat-working", (m.Flags & Flags.IsWorking) !== 0);
                return;
            }
            await this.UpdateBody(m.Text, m.Format, (m.Flags & Flags.IsWorking) !== 0);
            //  A streamed answer is done: the list (role log, aria-relevant additions) only announced the row when it was added (often
            //  still empty), so read the finished text once (the start of a long answer)
            if (wasWorking && (m.Flags & Flags.IsWorking) === 0 && !this.IsPreview && Sw.Page && Sw.Page.Announce) {
                const text = this.Text.trim();
                if (text) {
                    const ai = (m.Flags & Flags.FromAi) !== 0;
                    const who = m.From ? (ai ? _T("{0} (AI)", m.From, "Who wrote a chat answer that is read to screen reader users, when it was written by an AI.{0} is replaced with the sender name") : m.From) + ": " : "";
                    Sw.Page.Announce(who + (text.length > 400 ? text.substring(0, 400) + "…" : text));
                }
            }
        }

        /**
         * The text of the rendered content (for copying and speech).
         * @returns {string} The text.
         */
        get Text() {
            return this.Body.innerText;
        }

        /** Update the language flag and its tool tip (translation enabled only). */
        UpdateLang() {
            const t = this;
            if (!t.LangE)
                return;
            const view = t.View;
            const m = t.Msg;
            if (t.IsPreview) {
                t.FlagImg.src = flagUrl(view.CurrentLang);
                setButtonName(t.LangE, _TF("This should be set to the language used in your text.", "Tool tip description indicating that the language specified should match the one used to write the text") + "\n\n" +
                    _T("If the text is not written in \"{0}\", click to specify a different language", view.LangText(view.CurrentLang), "Tool tip description for a button that when clicked will show a list of languages that the user can select.{0} is the current language"));
                return;
            }
            const mLang = view.LangText(m.Lang);
            const dLang = view.LangText(view.Data.Lang);
            if (t.IsTranslated) {
                t.FlagImg.src = flagUrl(view.Data.Lang);
                setButtonName(t.LangE, _T("Message is translated to \"{0}\".", dLang, "A tool tip description indicating that a chat message have been translated.{0} is replaced by the ISO code of the language") + "\n" +
                    _T("Show the original message probably written using \"{0}\".", mLang, "A tool tip description on a button that when pressed will show the original message as opposed to a translated message.{0} is replaced by the ISO code of the language"));
            }
            else {
                t.FlagImg.src = flagUrl(m.Lang);
                setButtonName(t.LangE, _T("Message was probably written using \"{0}\"", mLang, "A tool tip description indicating what language was used to write a chat message.{0} is replaced by the ISO code of the language") + "\n" +
                    _T("Translate the message to \"{0}\".", dLang, "A tool tip description on a button that when pressed will translate a chat message to the specified language.{0} is replaced by the ISO code of the language"));
            }
            t.LangE.classList.toggle("chat-translated", t.IsTranslated);
        }

        /**
         * Flag click: the preview row opens the language selection, other rows toggle between the original and the translation
         * (`Translate {ChatId, MessageId}`, cached).
         * @returns {Promise<void>}
         */
        async OnFlag() {
            const t = this;
            if (t.IsPreview) {
                await t.View.Composer.SelectLanguage();
                return;
            }
            if (t.Translating)
                return;
            t.Translating = true;
            const m = t.Msg;
            const working = (m.Flags & Flags.IsWorking) !== 0;
            try {
                if (t.IsTranslated) {
                    t.IsTranslated = false;
                    await t.UpdateBody(m.Text, m.Format, working);
                }
                else {
                    if (!t.Translated) {
                        t.LangE.classList.add("chat-translating");
                        t.Translated = await Sw.Net.Send(t.View.Api("Translate"), { ChatId: t.View.ChatId, MessageId: m.Id });
                    }
                    t.IsTranslated = true;
                    await t.UpdateBody(t.Translated, m.Format, working);
                }
                t.UpdateLang();
            }
            catch (e) {
                Sw.Fail(withError(_TF("Failed to translate the message.", "An error message shown when a user tries to translate a chat message but the server returns a failure."), e));
            }
            finally {
                t.LangE.classList.remove("chat-translating");
                t.Translating = false;
            }
        }

        /**
         * Show the message menu at the pointer (a click, a right click or a long press), else (the keyboard) at the element.
         * @param {?Event} [ev] The triggering event.
         * @param {?HTMLElement} [at] The element to show it at without a pointer position (default the sender icon).
         */
        ShowMenu(ev, at) {
            const items = this.View.MessageMenuItems(this);
            if (!items.length)
                return;
            const point = ev && ev.clientX !== undefined && (ev.clientX || ev.clientY) ? { X: ev.clientX, Y: ev.clientY } : null;
            Sw.Menu.Show(point || at || this.IconE, items, { Align: !point && !at && this.E.classList.contains("chat-mine") ? "end" : "start" });
        }
    }

    // ------------------------------------------------------------------ message list

    /** The scrolling message list: rows by id, the preview row last, stick to bottom, lazy loading of older messages. */
    class MessageList {
        /**
         * @param {ChatView} view The chat.
         */
        constructor(view) {
            const t = this;
            /** @type {ChatView} The chat. */
            t.View = view;
            /** The scrolling element. */
            t.E = el("div.chat-list", {
                attrs: {
                    role: "log",
                    //  Announce new messages only, not the age texts ticking or a streamed answer growing
                    "aria-relevant": "additions",
                    "aria-label": _TF("Chat messages", "Accessible name of the list of messages in a chat"),
                    //  Shown by chat.css while the list has no messages (the hidden preview row does not count)
                    "data-empty": _TF("No messages yet", "Shown in an empty chat, where the messages will appear"),
                }
            });
            /** @type {Map<number, MessageRow>} Rows by message id. */
            t.Rows = new Map();
            /** @type {?MessageRow} The preview row (kept last). */
            t.Preview = null;
            /** True while stuck to the bottom. */
            t.Stuck = true;
            /** True once all older messages are loaded. */
            t.AtTop = false;
            /** True while older messages load. */
            t.Loading = false;
            /** True while scrolling from code. */
            t.Internal = false;
            t.E.addEventListener("scroll", () => t.OnScroll(), { passive: true });
            /** Keeps the bottom in view while the list or a row (images, markdown) changes size. */
            t.Resizer = new ResizeObserver(() => {
                if (t.Stuck)
                    t.ToBottom(true);
            });
            t.Resizer.observe(t.E);
            //  Rows and texts change many times while an answer streams: one scroll per frame
            let bottomQueued = false;
            new MutationObserver(() => {
                if (!t.Stuck || bottomQueued)
                    return;
                bottomQueued = true;
                requestAnimationFrame(() => {
                    bottomQueued = false;
                    if (t.Stuck)
                        t.ToBottom(true);
                });
            }).observe(t.E, { childList: true, subtree: true });
            /** Refreshes the age texts every second (only changed texts are written; stops once the list was removed from the document). */
            t.AgeTimer = setInterval(() => {
                if (t.E.isConnected)
                    t.UpdateAges();
                else if (t.WasConnected)
                    clearInterval(t.AgeTimer);
                t.WasConnected = t.WasConnected || t.E.isConnected;
            }, 1000);
            /** True once the list was in the document. */
            t.WasConnected = false;
        }

        /** Refresh the age texts of all rows (age mode only, see MessageRow.UpdateAge). */
        UpdateAges() {
            if (this.View.ShowTimeStamp)
                return;
            const now = Date.now();
            for (const row of this.Rows.values())
                row.UpdateAge(now);
        }

        /**
         * Scroll to the bottom.
         * @param {boolean} [instant] No smooth scrolling.
         */
        ToBottom(instant) {
            const e = this.E;
            this.Internal = true;
            e.scrollTo({ left: e.scrollLeft, top: e.scrollHeight, behavior: instant || (Sw.Motion && Sw.Motion.Reduced()) ? "instant" : "smooth" });
            requestAnimationFrame(() => this.Internal = false);
            if (instant)
                this.Stuck = true;
        }

        /** Scroll to the top (smooth). */
        ToTop() {
            this.E.scrollTo({ left: this.E.scrollLeft, top: 0, behavior: Sw.Motion && Sw.Motion.Reduced() ? "instant" : "smooth" });
        }

        /** @returns {boolean} True if scrolled to the top. */
        get IsAtTop() {
            return this.E.scrollTop <= 0;
        }

        /** @returns {boolean} True if scrolled to the bottom. */
        get IsAtBottom() {
            const e = this.E;
            return e.scrollTop >= e.scrollHeight - e.clientHeight - 1;
        }

        /**
         * Keep the list at the bottom for 500 ms (covers async layout after sending and at start up).
         * @param {boolean} [instant] No smooth scrolling.
         */
        StickForAwhile(instant) {
            let stick = true;
            /** Per frame scroll to the bottom while sticking. */
            const fn = () => {
                if (!stick)
                    return;
                this.ToBottom(instant);
                requestAnimationFrame(fn);
            };
            requestAnimationFrame(fn);
            setTimeout(() => {
                stick = false;
                this.Stuck = true;
                this.ToBottom(instant);
            }, 500);
        }

        /** Scroll handler: tracks sticking to the bottom (class chat-stick flash) and loads older messages at the top. */
        OnScroll() {
            const t = this;
            //  Scrolled away from the top: chat.css fades the top edge (transparent embeds), else the first message stays sharp
            t.E.classList.toggle("chat-scrolled", t.E.scrollTop > 2);
            if (t.Internal)
                return;
            if (t.E.scrollTop <= 1)
                t.LoadOlder();
            const s = t.E.scrollTop >= t.E.scrollHeight - t.E.clientHeight - 4;
            if (s === t.Stuck)
                return;
            t.Stuck = s;
            t.E.classList.toggle("chat-stick", s);
        }

        /**
         * Add a row element.
         * @param {MessageRow} row The row.
         * @param {boolean} [onTop] Insert first (older messages), else before the preview row.
         */
        Insert(row, onTop) {
            const t = this;
            const e = t.E;
            if (row.IsPreview)
                t.Preview = row;
            else
                t.Rows.set(row.Msg.Id, row);
            const before = onTop ? e.firstElementChild : (t.Preview && !row.IsPreview ? t.Preview.E : null);
            if (before && before.parentElement === e)
                e.insertBefore(row.E, before);
            else
                e.append(row.E);
            t.Resizer.observe(row.E);
        }

        /**
         * Create, insert and render a message.
         * @param {object} m The ChatMessage.
         * @param {boolean} [onTop] Insert first.
         * @returns {Promise<MessageRow>} The row.
         */
        async Add(m, onTop) {
            const row = new MessageRow(this.View, m);
            this.Insert(row, onTop);
            await row.Update(m);
            return row;
        }

        /**
         * Find a row by message id.
         * @param {number} id The message id.
         * @returns {?MessageRow} The row.
         */
        Get(id) {
            return this.Rows.get(id) || null;
        }

        /**
         * Remove a message.
         * @param {number} id The message id.
         */
        Remove(id) {
            const row = this.Rows.get(id);
            if (!row)
                return;
            this.Rows.delete(id);
            this.Resizer.unobserve(row.E);
            row.E.remove();
        }

        /** Remove all messages (the preview row stays). */
        Clear() {
            for (const row of this.Rows.values()) {
                this.Resizer.unobserve(row.E);
                row.E.remove();
            }
            this.Rows.clear();
            this.E.querySelectorAll(":scope > .chat-loading").forEach(x => x.remove());
        }

        /**
         * The message rows (not the preview), in display order.
         * @returns {MessageRow[]} The rows.
         */
        All() {
            return Array.from(this.E.children).map(x => x.SwChatRow).filter(x => x && !x.IsPreview);
        }

        /**
         * Load up to 10 older messages (`GetMessages {ChatId, MaxCount: -10, FromId}`), keeping the scroll position; a spinner shows
         * after 200 ms; stops when none are returned.
         * @returns {Promise<void>}
         */
        async LoadOlder() {
            const t = this;
            if (t.AtTop || t.Loading)
                return;
            const first = t.All()[0];
            if (!first)
                return;
            t.Loading = true;
            const beforeH = t.E.scrollHeight;
            let spinner = null;
            const timer = setTimeout(() => {
                spinner = el("div.chat-loading", null, new Sw.Spinner({ Size: "64px", Title: _TF("Loading more messages", "Tool tip description of a loading indicator shown when the char is loading more messages") }).E);
                t.E.prepend(spinner);
            }, 200);
            try {
                const view = t.View;
                const older = await Sw.Net.Send(view.Api("GetMessages"), { ChatId: view.ChatId, MaxCount: -10, FromId: first.Msg.Id });
                clearTimeout(timer);
                if (spinner)
                    spinner.remove();
                spinner = null;
                if (!older || older.length <= 0) {
                    t.AtTop = true;
                    return;
                }
                for (let i = older.length - 1; i >= 0; --i)
                    if (!t.Get(older[i].Id))
                        await t.Add(older[i], true);
                t.E.scrollTo({ left: t.E.scrollLeft, top: t.E.scrollHeight - beforeH, behavior: "instant" });
            }
            catch (e) {
                console.warn("Chat: failed to load older messages: " + e.message);
            }
            finally {
                clearTimeout(timer);
                if (spinner)
                    spinner.remove();
                t.Loading = false;
            }
        }
    }

    // ------------------------------------------------------------------ composer

    /**
     * Let the user pick files (a temporary file input; must run from a user interaction).
     * @param {boolean} multiple Allow several files.
     * @returns {Promise<?FileList>} The files, or null when cancelled.
     */
    function selectFiles(multiple) {
        return new Promise(resolve => {
            const fi = el("input", { type: "file", multiple: !!multiple });
            fi.addEventListener("change", () => resolve(fi.files && fi.files.length ? fi.files : null), { once: true });
            fi.addEventListener("cancel", () => resolve(null), { once: true });
            fi.click();
        });
    }

    /** The input area (only when the user can post): textarea, send / mic buttons, language flag, preview row, draft, uploads, speech input. */
    class Composer {
        /**
         * @param {ChatView} view The chat.
         */
        constructor(view) {
            const t = this;
            /** @type {ChatView} The chat. */
            t.View = view;
            const data = view.Data;
            const st = view.Settings;
            /** @type {string[]} Sent texts (max 100, not persisted), browsed with Alt+Up / Alt+Down. */
            t.History = [];
            /** History position: 0 = the typed text, -1 = empty, n = the n:th last entry. */
            t.HistoryPos = 0;
            /** The typed text before browsing the history. */
            t.Typed = "";
            /** True while the textarea is changed by history browsing. */
            t.InHistory = false;
            /** True while sending. */
            t.Sending = false;
            /** @type {Set<string>} Uploaded (new) urls of the draft, deleted again when removed from the draft. */
            t.TempFiles = new Set();
            /** Markdown input format. */
            t.UseMarkDown = !!data.AllowMarkDown && st.Bool("MarkDown", false);
            /**
             * Automatic format (the user never picked text or markdown, and the chat allows markdown): a draft that contains markdown
             * syntax (`**bold**`, `*italic*`, `` `code` ``, `~~strike~~`, links, headings, code blocks) is sent as markdown, else as text.
             * Picking a format in the input menu turns it off.
             */
            t.AutoMarkDown = !!data.AllowMarkDown && st.Get("MarkDown") === null;
            const pv = st.Get("Preview");
            /** @type {?boolean} Preview: null = auto (on for markdown), else explicit. */
            t.ShowPreview = pv === "true" ? true : (pv === "false" ? false : null);
            /** @type {?function():Promise<void>} The stop function of the active speech recognition. */
            t.StopRecognition = null;

            /** The input element. */
            t.E = el("div.chat-input");
            /** The textarea. */
            t.Write = el("textarea.chat-write", { maxLength: data.MaxTextLength || 4096, attrs: { "aria-label": _TF("Message", "Accessible name of the text box where a chat message is written"), "aria-keyshortcuts": "Alt+Enter" } });
            /** The send button. */
            t.Send = new Sw.Button({
                Icon: "send", Kind: "ghost", Class: "chat-send",
                Title: _TF("Send the message", "A tool tip description on a button that when clicked will send the entered chat message to the server"),
                OnClick: () => t.DoSend(),
            });
            t.Send.E.style.setProperty("--chat-ics", (view.Options.SendIconSize || 48) + "px");
            addTouchLabel(t.Send.E, _TF("Send", "Short label below the send button of a chat (touch devices), keep it short"));
            /** The input language flag (click selects the language). */
            t.Flag = el("button.chat-input-flag", { type: "button" }, el("img", { alt: "" }));
            attachHint(t.Flag);
            /** The short label of the flag (the language code). */
            t.FlagLabel = addTouchLabel(t.Flag, "");
            t.Flag.addEventListener("click", ev => {
                if (!Sw.BadClick(ev))
                    t.SelectLanguage();
            });
            /** @type {?Sw.Button} The push-to-talk button (speech recognition only). */
            t.Mic = null;
            if (C.SpeechInput.Supported()) {
                t.Mic = new Sw.Button({
                    Icon: "mic", Kind: "ghost", Class: "chat-mic",
                    Title: _TF("Speak: listen for speech input in the selected language", "A tool tip description on a button that when clicked will start listening to speech input (starts with the word shown on the button)"),
                    OnClick: () => {
                        //  A toggle: a click while recording stops it
                        if (t.StopRecognition && t.Mic.E.classList.contains("chat-recording"))
                            t.StopListening();
                        else
                            t.StartListening(true, true);
                    },
                });
                t.Mic.E.style.setProperty("--chat-ics", (view.Options.AudioIconSize || 48) + "px");
                //  A toggle from the start (pressed while recording or listening, SetMicState)
                t.Mic.E.setAttribute("aria-pressed", "false");
                addTouchLabel(t.Mic.E, _TF("Speak", "Short label below the speech input (microphone) button of a chat (touch devices), keep it short"));
            }
            /** @type {?Sw.Button} The attach file button (the same as "Attach file" in the menu; only with an upload repository). */
            t.Attach = null;
            if (view.UploadRepo) {
                t.Attach = new Sw.Button({
                    Icon: "attach", Kind: "ghost", Class: "chat-attach",
                    Title: _TF("Attach a file to the current message", "Tool tip description of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                    OnClick: () => t.AttachFiles(),
                });
                addTouchLabel(t.Attach.E, _TF("Attach", "Short label below the attach file button of a chat input, keep it short"));
            }
            /**
             * The buttons beside the text box (never over the text): a 2 x 2 square at its right (Attach, Language / Speak, Send), then
             * the Menu button (ChatView); a row below the text box when the input is narrow (.chat-pad-row, Composer.WatchSize).
             */
            t.Pad = el("div.chat-input-pad", null, ...[t.Attach && t.Attach.E, t.Flag, t.Mic && t.Mic.E, t.Send.E].filter(x => x));
            t.E.append(el("div.chat-write-box", null, t.Write), t.Pad);
            t.UpdateFlag();

            //  Placeholders: "Write a message...", plus a small second line with the send key on devices with a keyboard and a mouse
            //  (not on phones / tablets); the key is also added to the send button tool tip
            let hint = "";
            if (!Sw.Platform.IsMobileDevice && matchMedia("(any-pointer: fine)").matches) {
                const isMac = Sw.Platform.IsMac;
                hint = "\n" + (isMac
                    ? _TF("Option+Enter to send", "A short hint below the placeholder of a chat input on a Mac: the user can press the Enter key while holding down the Option key to send the chat message")
                    : _TF("Alt+Enter to send", "A short hint below the placeholder of a chat input on Windows / Linux: the user can press the Enter key while holding down the Alt key to send the chat message"));
                t.Send.SetTitle(t.Send.Title + " (" + (isMac ? "Option+Enter" : "Alt+Enter") + ")");
            }
            /** Placeholder in text mode. */
            t.PlaceholderText = _TF("Write a message...", "A placeholder text for an input box where a user can enter a text message for a chat") + hint;
            /** Placeholder in markdown mode. */
            t.PlaceholderMD = _TF("Write a message (Markdown)...", "A placeholder text for an input box where a user can enter a markdown formatted text message for a chat") + hint;

            //  Draft
            const draft = st.Get("Input");
            if (draft)
                t.Write.value = draft;
            /** The preview pseudo message (Id 0). */
            t.PreviewMsg = {
                Id: 0, Time: null, Flags: 0, MenuItems: null,
                Data: st.Get("Data") || null,
                Format: t.UseMarkDown ? Formats.MarkDown : Formats.Text,
                From: data.UserName,
                FromImage: CurrentUserImage,
                Lang: view.CurrentLang,
                Text: null,
            };
            /** The preview row. */
            t.Preview = new MessageRow(view, t.PreviewMsg, true);
            view.List.Insert(t.Preview);
            t.Preview.UpdateData(t.PreviewMsg.Data);

            t.Write.addEventListener("input", () => t.OnInput());
            t.Write.addEventListener("keydown", e => t.OnKey(e));
            //  Keep the text box in view when it gets the focus (a phone keyboard), not inside a frame: there it would scroll the host page too
            t.Write.addEventListener("focus", () => {
                if (window.parent === window)
                    t.Write.scrollIntoView({ behavior: Sw.Motion && Sw.Motion.Reduced() ? "instant" : "smooth", block: "nearest" });
            });
            t.InitDrop();
            t.WatchSize();
        }

        /**
         * Pick the input layout from the chat size, again on every resize (see chat.css):
         * - `.chat-compact` (chat root): a low chat (phone landscape, height below 540 px): less padding.
         * - `.chat-pad-row` (input): a narrow input (below 480 px, ex: phones): the four buttons are a row below the text box
         *   (with the Menu button at its end; the text keeps the whole width), else a 2 x 2 square and the Menu button at its right.
         * - `.chat-tight` (chat root): a very low chat (below TightHeight, ex: a phone with the on screen keyboard open): one row, the
         *   text box (one line, growing) beside Send and Menu (the other buttons and the splitter step aside, a user split is not
         *   applied), the messages get the rest.
         * The buttons always show a short word below the icon (`.chat-labels`). Then sizes the text box (AutoGrow).
         */
        WatchSize() {
            const t = this;
            const c = t.View.Container;
            t.E.classList.add("chat-labels");
            const update = () => {
                const w = c.clientWidth;
                const h = c.clientHeight;
                if (!w || !h)
                    return;
                c.classList.toggle("chat-compact", h < 540);
                const tight = h < TightHeight;
                c.classList.toggle("chat-tight", tight);
                t.E.classList.toggle("chat-pad-row", !tight && ((t.E.clientWidth || w) < 480));
                if (!tight && !c.classList.contains("chat-autosize") && t.View.Split && t.View.Split.isConnected)
                    t.View.RestoreSize();
                t.AutoGrow();
                //  The splitter's value (the list's share of the chat height) also for the automatic split
                const sp = t.View.Split;
                if (sp)
                    sp.setAttribute("aria-valuenow", String(Math.round(100 * t.View.List.E.offsetHeight / Math.max(1, h))));
            };
            if (typeof ResizeObserver === "function")
                new ResizeObserver(update).observe(c);
            update();
        }

        /**
         * Size the text box to its text while the chat uses the automatic split (`.chat-autosize` on the chat root, the user never
         * dragged the splitter): at least three lines (and the height of the buttons beside it), growing while typing up to 40% of the
         * chat height (then it scrolls). With a user split the text box fills the input (no inline height).
         */
        AutoGrow() {
            const t = this;
            const w = t.Write;
            const c = t.View.Container;
            if (!w.isConnected)
                return;
            const tight = c.classList.contains("chat-tight");
            if ((!tight && !c.classList.contains("chat-autosize")) || !c.clientHeight) {
                w.style.height = "";
                return;
            }
            const cs = getComputedStyle(w);
            const line = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.25;
            const borders = parseFloat(cs.borderTopWidth) + parseFloat(cs.borderBottomWidth);
            const pad = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
            //  At least three lines of text (one in a tight chat), and as high as the buttons beside it (the square and the Menu
            //  button; the row is below it)
            let min = Math.ceil((tight ? 1 : 3) * line + pad + borders);
            if (!t.E.classList.contains("chat-pad-row")) {
                min = Math.max(min, t.Pad.offsetHeight);
                const menu = t.View.MenuButton && t.View.MenuButton.E.parentElement === t.E ? t.View.MenuButton.E.offsetHeight : 0;
                min = Math.max(min, menu);
            }
            const max = Math.max(min, Math.round(c.clientHeight * 0.4));
            const top = w.scrollTop;
            w.style.height = "0px";
            const want = w.scrollHeight + borders;
            w.style.height = Math.max(min, Math.min(max, want)) + "px";
            w.scrollTop = top;
        }

        /**
         * @returns {number} The least height of the input (px) for a user split (ChatView.SetListHeight): the buttons beside the text box
         * and two lines of text (the square layout: the higher of the two; the row layout: the text box above the row).
         */
        get MinHeight() {
            const t = this;
            const cs = getComputedStyle(t.E);
            const ws = getComputedStyle(t.Write);
            const line = parseFloat(ws.lineHeight) || parseFloat(ws.fontSize) * 1.25;
            const text = Math.ceil(2 * line + parseFloat(ws.paddingTop) + parseFloat(ws.paddingBottom) + parseFloat(ws.borderTopWidth) + parseFloat(ws.borderBottomWidth));
            const outer = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
            const menu = t.View.MenuButton && t.View.MenuButton.E.parentElement === t.E ? t.View.MenuButton.E.offsetHeight : 0;
            const bar = Math.max(t.Pad.offsetHeight, menu);
            return Math.ceil(outer + (t.E.classList.contains("chat-pad-row") ? text + (parseFloat(cs.rowGap) || 0) + bar : Math.max(text, bar)));
        }

        /** @returns {number} Number of draft attachments. */
        get DataCount() {
            const d = this.PreviewMsg.Data;
            return d ? d.split(";").length : 0;
        }

        /** @returns {boolean} There is something to send. */
        get CanSend() {
            return !!(this.Write.value.trim() || this.PreviewMsg.Data);
        }

        /** Update the input language flag. */
        UpdateFlag() {
            const v = this.View;
            this.Flag.firstElementChild.src = flagUrl(v.CurrentLang);
            this.FlagLabel.textContent = String(v.CurrentLang || "").toUpperCase();
            //  The name starts with the visible code (WCAG 2.5.3 Label in Name)
            setButtonName(this.Flag, _T("{0}: the language of your text is \"{1}\", click to select another language", String(v.CurrentLang || "").toUpperCase(), v.LangText(v.CurrentLang), "Tool tip of a flag button showing the language used to write chat messages.{0} is replaced with the language code shown on the button (ex: EN), {1} with the language"));
        }

        /** Textarea input: update the draft and, unless browsing the history, remember the typed text. */
        OnInput() {
            this.Changed();
            if (!this.InHistory) {
                this.HistoryPos = 0;
                this.Typed = this.Write.value;
            }
        }

        /** The draft changed: enable the send button, persist the draft (keys Input / Data) and update the preview. */
        Changed() {
            const t = this;
            const st = t.View.Settings;
            const text = t.Write.value.trim();
            const d = t.PreviewMsg.Data;
            const can = !!(text || d);
            st.Set("Input", can ? text : null);
            st.Set("Data", can && d ? d : null);
            t.Send.SetDisabled(!can, _TF("Type a message first", "Why the send button of a chat is disabled: the message input is empty"));
            if (t.Attach)
                t.Attach.SetDisabled(t.View.Data.MaxDataCount - t.DataCount <= 0, _TF("No more files can be attached to this message", "Why the attach file button of a chat is disabled: the message has the maximum number of files"));
            t.SetPreviewText(can ? text : null);
            t.AutoGrow();
        }

        /**
         * Keyboard: pure Alt (Option on mac) + Enter sends, + ArrowUp / ArrowDown browse the history; plain Enter is a new line.
         * @param {KeyboardEvent} e The event.
         */
        OnKey(e) {
            const t = this;
            if (!e.altKey || e.ctrlKey || e.metaKey || e.shiftKey || t.Sending)
                return;
            switch (e.key) {
                case "Enter":
                    e.preventDefault();
                    t.DoSend();
                    return;
                case "ArrowUp":
                    e.preventDefault();
                    if (t.HistoryPos < t.History.length) {
                        ++t.HistoryPos;
                        t.FromHistory();
                    }
                    return;
                case "ArrowDown":
                    e.preventDefault();
                    if (t.HistoryPos > -1) {
                        --t.HistoryPos;
                        t.FromHistory();
                    }
                    return;
            }
        }

        /** Put the history entry at HistoryPos into the textarea (0 = the typed text, -1 = empty). */
        FromHistory() {
            const t = this;
            const p = t.HistoryPos;
            t.InHistory = true;
            t.Write.value = p === 0 ? t.Typed : (p < 0 ? "" : t.History[t.History.length - p]);
            t.Write.NextLast = t.Write.value.length;
            t.Changed();
            t.InHistory = false;
        }

        /**
         * Insert text at the cursor (replaces the selection; with `select` the inserted text stays selected so the next insert
         * replaces it, used for interim speech results).
         * @param {string} text The text.
         * @param {boolean} [select] Keep the inserted text selected.
         */
        InsertText(text, select) {
            const w = this.Write;
            const v = w.value;
            let s = w.selectionStart;
            let e = w.selectionEnd;
            if (!s && w.NextLast) {
                s = w.NextLast;
                e = s;
            }
            w.NextLast = null;
            s = s || 0;
            w.value = v.substring(0, s) + text + v.substring(e);
            e = s + text.length;
            if (select)
                w.setSelectionRange(s, e);
            else {
                w.setSelectionRange(e, e);
                w.NextLast = e;
            }
        }

        /**
         * Send the draft (`UserMessage {ChatId, Body: {Format, Data, Text, Lang}}`). While sending the input is read only and shows
         * the text as placeholder; a failure restores the draft and shows a fail toast.
         * @param {boolean} [noFocus] Don't focus the input when done (speech auto send).
         * @returns {Promise<void>}
         */
        async DoSend(noFocus) {
            const t = this;
            const view = t.View;
            const w = t.Write;
            const text = w.value.trim();
            const d = t.PreviewMsg.Data;
            if ((!text && !d) || t.Sending)
                return;
            t.Sending = true;
            t.Send.SetWorking(true);
            t.History.push(text);
            if (t.History.length > 100)
                t.History.splice(0, t.History.length - 100);
            t.HistoryPos = 0;
            t.Typed = "";
            const ph = w.placeholder;
            t.SetData(null);
            w.placeholder = text;
            w.value = "";
            w.readOnly = true;
            w.NextLast = 0;
            t.SetPreviewText(null);
            const failText = _TF("Failed to send message.", "An error message shown when a user tries to send a chat message but the server returns a failure");
            let ok = false;
            try {
                ok = !!await Sw.Net.Send(view.Api("UserMessage"), {
                    ChatId: view.ChatId,
                    Body: { Format: t.FormatOf(text), Data: d, Text: text, Lang: view.CurrentLang },
                });
                if (!ok)
                    Sw.Fail(failText);
            }
            catch (e) {
                Sw.Fail(withError(failText, e));
            }
            if (ok) {
                t.TempFiles.clear();
                view.List.StickForAwhile();
            }
            else if (!w.value) {
                w.value = text;
                t.SetData(d);
            }
            t.Sending = false;
            t.Send.SetWorking(false);
            w.readOnly = false;
            w.placeholder = ph;
            t.Changed();
            if (!noFocus)
                w.focus();
        }

        /**
         * Set the draft attachments (the preview row is updated; call Changed to persist).
         * @param {?string} d The `;` separated urls.
         */
        SetData(d) {
            this.PreviewMsg.Data = d || null;
            this.Preview.UpdateData(this.PreviewMsg.Data);
        }

        /**
         * The preview setting (auto: on for markdown).
         * @returns {boolean} True if the preview is on.
         */
        get PreviewOn() {
            return typeof this.ShowPreview === "boolean" ? this.ShowPreview : this.UseMarkDown;
        }

        /**
         * Show / hide the preview row: visible if it has attachments, or text with the preview on.
         * @returns {boolean} True if visible.
         */
        PreviewChanged() {
            const t = this;
            const show = !!t.PreviewMsg.Data || (t.PreviewOn && !!t.PreviewMsg.Text);
            const was = !t.Preview.E.classList.contains("chat-hide");
            t.Preview.E.classList.toggle("chat-hide", !show);
            if (show && !was)
                t.View.List.ToBottom();
            return show;
        }

        /**
         * Set the preview text (re-renders the preview content).
         * @param {?string} text The draft text (null when there is nothing to send).
         * @returns {Promise<void>}
         */
        async SetPreviewText(text) {
            const t = this;
            if (t.PreviewMsg.Text === text)
                return;
            t.PreviewMsg.Text = text;
            t.PreviewMsg.Format = t.FormatOf(text);
            await t.Preview.UpdateBody(text, t.PreviewMsg.Format, false);
            if (t.PreviewChanged())
                t.View.List.ToBottom();
        }

        /**
         * Apply the input format (text or markdown): placeholder, preview format.
         * @returns {Promise<void>}
         */
        async FormatChanged() {
            const t = this;
            t.PreviewMsg.Format = t.FormatOf(t.PreviewMsg.Text);
            t.Write.placeholder = t.UseMarkDown ? t.PlaceholderMD : t.PlaceholderText;
            await t.Preview.UpdateBody(t.PreviewMsg.Text, t.PreviewMsg.Format, false);
            t.PreviewChanged();
        }

        /**
         * The format a draft text is sent (and previewed) with: markdown if picked, text if picked, else (automatic, see AutoMarkDown)
         * markdown when the text contains markdown syntax.
         * @param {?string} text The draft text.
         * @returns {number} The format (Formats.MarkDown or Formats.Text).
         */
        FormatOf(text) {
            if (this.UseMarkDown)
                return Formats.MarkDown;
            return (this.AutoMarkDown && LooksLikeMarkDown(text)) ? Formats.MarkDown : Formats.Text;
        }

        /**
         * Switch the input format and persist it.
         * @param {boolean} md True for markdown.
         */
        SetMarkDown(md) {
            this.UseMarkDown = md;
            this.AutoMarkDown = false;
            this.View.Settings.Set("MarkDown", md ? "true" : "false");
            this.FormatChanged();
        }

        /** Toggle the preview and persist it ("null" = auto). */
        TogglePreview() {
            const t = this;
            t.ShowPreview = t.PreviewOn ? (t.UseMarkDown ? false : null) : (t.UseMarkDown ? null : true);
            t.View.Settings.Set("Preview", String(t.ShowPreview));
            t.PreviewChanged();
        }

        /**
         * Delete uploaded (temporary) draft files on the server (`DeleteStoredFile` with the server root relative url).
         * @param {string[]} urls The urls (as stored in the draft).
         * @returns {Promise<void>}
         */
        async DeleteTemp(urls) {
            for (const u of urls) {
                if (!this.TempFiles.delete(u))
                    continue;
                try {
                    if (!await Sw.Net.Send(this.View.Api("../Api/UserStorage/DeleteStoredFile"), u.startsWith("../") ? u.substring(3) : u))
                        console.warn('Chat: failed to delete stored file "' + u + '".');
                }
                catch (e) {
                    console.warn('Chat: failed to delete stored file "' + u + '", error: ' + e.message);
                }
            }
        }

        /**
         * Remove one attachment from the draft.
         * @param {string} org The url (as stored).
         * @param {boolean} deleteFile Also delete an uploaded file from the server.
         * @returns {Promise<void>}
         */
        async RemoveAttachment(org, deleteFile) {
            const t = this;
            const rest = (t.PreviewMsg.Data || "").split(";").filter(x => x && x !== org);
            t.SetData(rest.length ? rest.join(";") : null);
            t.Changed();
            t.PreviewChanged();
            if (deleteFile)
                await t.DeleteTemp([org]);
        }

        /**
         * Clear the draft: text and attachments (uploaded ones are deleted on the server).
         * @returns {Promise<void>}
         */
        async Clear() {
            const t = this;
            const d = t.PreviewMsg.Data;
            t.Write.value = "";
            t.Write.NextLast = 0;
            t.SetData(null);
            t.Changed();
            t.PreviewChanged();
            if (d)
                await t.DeleteTemp(d.split(";"));
        }

        /**
         * Pick files and attach them.
         * @returns {Promise<void>}
         */
        async AttachFiles() {
            const multiple = this.View.Data.MaxDataCount - this.DataCount > 1;
            const files = Sw.Upload ? await Sw.Upload.Pick({ Multiple: multiple }) : await selectFiles(multiple);
            if (files)
                await this.Upload(files);
        }

        /**
         * Upload files (`Sw.Upload.Files(UploadRepo, files)`) and attach them to the draft, within MaxDataCount and MaxDataLength.
         * @param {FileList|File[]} files The files.
         * @returns {Promise<void>}
         */
        async Upload(files) {
            const t = this;
            const view = t.View;
            const data = view.Data;
            if (!view.UploadRepo || !files || files.length <= 0)
                return;
            if (t.DataCount + files.length > data.MaxDataCount) {
                Sw.Fail(data.MaxDataCount <= 1
                    ? _TF("Only one file may be attached!", "Error message displayed when the user tries to attach more than 1 file to a chat message")
                    : _T("Only {0} files may be attached!", data.MaxDataCount, "Error message displayed when the user tries to attach more than the allowed number of files to a chat message.{0} is replaced with the maximum allowed number of files"));
                return;
            }
            const U = Sw.Upload;
            const res = U ? await U.Files(view.UploadRepo, files) : await window.fileUploader(view.UploadRepo, files);
            if (res.Error) {
                Sw.Fail(res.Error.message || String(res.Error));
                return;
            }
            const urls = res.Urls || [];
            if (urls.length <= 0) {
                Sw.Fail(_TF("Failed to upload!", "Error message shown when a file upload failed"));
                return;
            }
            const already = U ? U.Status.AlreadyUploaded : 1;
            let d = t.PreviewMsg.Data || "";
            for (let i = 0; i < urls.length; ++i) {
                const s = res.Status[i];
                if (s < 0)
                    Sw.Fail(U ? U.StatusText(s) : (window.fileUploaderStatusText ? window.fileUploaderStatusText(s) : String(s)));
                const u = urls[i];
                if (!u)
                    continue;
                const org = "../" + u;
                const next = d ? d + ";" + org : org;
                if (next.length > data.MaxDataLength) {
                    Sw.Fail(_TF("Can't attach more data!", "Error message shown when no more files can be attached to a chat message"));
                    break;
                }
                if (s !== already)
                    t.TempFiles.add(org);
                d = next;
            }
            t.SetData(d || null);
            t.PreviewChanged();
            t.Changed();
        }

        /**
         * Drag and drop: on the textarea files are uploaded and the first text item is inserted (image urls as markdown images); files
         * dropped anywhere else on the chat are uploaded too.
         */
        InitDrop() {
            const t = this;
            const w = t.Write;
            const mark = on => w.classList.toggle("chat-dragging", on);
            w.addEventListener("dragover", ev => ev.preventDefault());
            w.addEventListener("dragenter", () => mark(true));
            w.addEventListener("dragleave", () => mark(false));
            w.addEventListener("drop", async ev => {
                ev.preventDefault();
                mark(false);
                const dt = ev.dataTransfer;
                const textItem = Array.from(dt.items || []).find(x => x.kind === "string");
                const textPromise = textItem ? new Promise(r => textItem.getAsString(r)) : null;
                if (t.View.UploadRepo && dt.files && dt.files.length)
                    await t.Upload(dt.files);
                if (!textPromise)
                    return;
                let val = await textPromise;
                if (t.UseMarkDown && /^https?:\/\//i.test(val) && Sw.Format.IsImageExt(Sw.Format.FileExtension(val.split("?")[0]).toLowerCase()))
                    val = "![](" + val + ")";
                t.InsertText(val);
                t.OnInput();
            });

            //  Files dropped anywhere on the chat (the list, the splitter, the buttons) are attached to the draft, when the chat has an
            //  upload repository; only drags with files (a text drag inside the list keeps the browser's behaviour). While files are
            //  dragged over the chat, .chat-drop-files on the chat root shows "Drop files to attach" (data-drop), .chat-drop-full when
            //  the message has the maximum number of files. A drop the chat can't take does nothing (the browser would open the file).
            const c = t.View.Container;
            const hasFiles = ev => !!ev.dataTransfer && Array.from(ev.dataTransfer.types || []).includes("Files");
            let timer = 0;
            const show = on => {
                clearTimeout(timer);
                timer = 0;
                c.classList.toggle("chat-drop-files", on);
                if (!on)
                    c.classList.remove("chat-drop-full");
            };
            c.addEventListener("dragover", ev => {
                if (!hasFiles(ev) || !t.View.UploadRepo)
                    return;
                ev.preventDefault();
                const full = t.View.Data.MaxDataCount - t.DataCount <= 0;
                ev.dataTransfer.dropEffect = full ? "none" : "copy";
                c.setAttribute("data-drop", full
                    ? _TF("No more files can be attached to this message", "Why the attach file button of a chat is disabled: the message has the maximum number of files")
                    : _TF("Drop files to attach them to the message", "Shown over a chat while the user drags files over it: dropping them attaches them to the message being written"));
                c.classList.add("chat-drop-files");
                c.classList.toggle("chat-drop-full", full);
                //  (dragover repeats while the files are over the chat: no dragover for a moment = they left, or the drag ended)
                clearTimeout(timer);
                timer = setTimeout(() => show(false), 300);
            });
            c.addEventListener("drop", async ev => {
                if (!hasFiles(ev) || !t.View.UploadRepo)
                    return;
                show(false);
                //  (the text box handles its own drops: files and text)
                if (ev.defaultPrevented)
                    return;
                ev.preventDefault();
                if (ev.dataTransfer.files && ev.dataTransfer.files.length)
                    await t.Upload(ev.dataTransfer.files);
            });
        }

        /**
         * Let the user select the input language (searchable list over Name / LocalName / EnName); persisted only when it differs from
         * the server default; restarts an open mic.
         * @returns {Promise<void>}
         */
        async SelectLanguage() {
            const t = this;
            const view = t.View;
            const speech = C.SpeechInput.Supported();
            const iso = await Sw.Dialog.Select({
                Title: _TF("Select the language to use", "Header text to a list of languages that can be selected"),
                Tip: speech
                    ? _TF("Select the language you're using for the text and voice input", "Tool tip description of a header to a list of languages that can be selected")
                    : _TF("Select the language you're using for the text input", "Tool tip description of a header to a list of languages that can be selected"),
                GetAll: true,
                Items: async search => {
                    const langs = await view.GetLanguages();
                    search = (search || "").toLowerCase();
                    const res = [];
                    for (const l of langs) {
                        if (search && l.Name.toLowerCase().indexOf(search) < 0 && l.LocalName.toLowerCase().indexOf(search) < 0 && l.EnName.toLowerCase().indexOf(search) < 0)
                            continue;
                        let tip = _TF("Name", 'Tool tip prefix indicating what language this is, part of somthing like: "Name: English".') + ": " + l.Name + "\n";
                        if (l.Name !== l.LocalName)
                            tip += _TF("Local name", 'Tool tip prefix indicating what language this is, part of somthing like: "Local name: Svenska".') + ": " + l.LocalName + "\n";
                        if (l.Name !== l.EnName)
                            tip += _TF("English name", 'Tool tip prefix indicating what language this is, part of somthing like: "English name: German".') + ": " + l.EnName + "\n";
                        tip += "Iso639-1: " + l.Iso + "\n\n" + _TF("Select this language", "Tool tip description on a button that when clicked will select tis language");
                        if (l.Comment)
                            tip += "\n\n" + l.Comment;
                        res.push({
                            Text: l.Name !== l.LocalName ? l.Name + " (" + l.LocalName + ")" : l.Name,
                            Hint: l.Iso,
                            Icon: flagUrl(l.Iso) + "*",
                            Title: tip,
                            Value: l.Iso,
                        });
                    }
                    return res;
                },
            });
            if (!iso)
                return;
            view.CurrentLang = iso;
            view.Settings.Set("Lang", iso === view.Data.Lang ? null : iso);
            t.PreviewMsg.Lang = iso;
            t.Preview.UpdateLang();
            t.UpdateFlag();
            if (view.CanOpenMic && view.Settings.Get("Listen") === "true") {
                t.StopListening();
                t.StartListening();
                view.Settings.Set("Listen", "true");
            }
        }

        /** Stop the speech recognition (if any) and reset the mic button. */
        StopListening() {
            const stop = this.StopRecognition;
            this.StopRecognition = null;
            this.SetMicState(false, false);
            if (stop)
                stop();
        }

        /**
         * Show the mic button state.
         * @param {boolean} recording Speech is being recorded (recording icon).
         * @param {boolean} openMic Open mic listening is active (the button is disabled while waiting for the wake word).
         */
        SetMicState(recording, openMic) {
            const m = this.Mic;
            if (!m)
                return;
            m.E.classList.toggle("chat-recording", recording);
            m.E.setAttribute("aria-pressed", recording ? "true" : "false");
            //  The recording / open mic icons animate by themselves (svg): with reduced motion the static mic (the color shows the state)
            const still = matchMedia("(prefers-reduced-motion: reduce)").matches;
            m.SetIcon(still ? "mic" : (recording ? ChatIcons + "ChatRecording.svg" : (openMic ? ChatIcons + "ChatInput.svg" : "mic")));
            m.SetDisabled(openMic && !recording, _TF("Listening for the wake word: say it to start talking", "Why the microphone button of a chat is disabled: open mic listening is waiting for the spoken wake word (the name of the assistant)"));
        }

        /**
         * Start speech input (`Sw.Chat.SpeechInput.Listen` with the SpeechName wake words, in the input language). Interim text is
         * inserted live; a final sentence is sent in open mic mode, or after push-to-talk when auto send is on.
         * @param {boolean} [ignoreKeyword] Any speech activates (push-to-talk).
         * @param {boolean} [noOpenMic] Stop after one sentence (push-to-talk).
         */
        StartListening(ignoreKeyword, noOpenMic) {
            const t = this;
            const view = t.View;
            if (t.StopRecognition)
                t.StopListening();
            const openMic = !noOpenMic;
            t.SetMicState(false, openMic);
            const stop = C.SpeechInput.Listen(view.Data.SpeechName,
                (active, isWorking) => {
                    t.Write.classList.toggle("chat-listening", !!active);
                    if (isWorking) {
                        t.SetMicState(!!active, openMic);
                        return;
                    }
                    if (!ignoreKeyword)
                        view.Settings.Set("Listen", "false");
                    t.StopRecognition = null;
                    t.SetMicState(false, false);
                },
                async (text, isLast) => {
                    t.InsertText(text, !isLast);
                    if (isLast) {
                        t.SetMicState(false, openMic && !!t.StopRecognition);
                        if (text.length > 0 && (openMic || view.Settings.Get("AutoSend") === "true")) {
                            await t.DoSend(true);
                            return;
                        }
                    }
                    t.Changed();
                },
                view.CurrentLang, ignoreKeyword, noOpenMic);
            t.StopRecognition = stop;
            if (!stop)
                t.SetMicState(false, false);
        }

        /** Final start up: apply the format, enable the send button, focus the textarea without forcing the on screen keyboard. */
        Ready() {
            const t = this;
            t.FormatChanged();
            t.Changed();
            t.Focus();
        }

        /** Focus the textarea without forcing the on screen keyboard (read only while focusing). */
        Focus() {
            const w = this.Write;
            w.readOnly = true;
            w.focus({ preventScroll: true });
            w.readOnly = false;
        }
    }

    // ------------------------------------------------------------------ the chat

    /** One chat: Join, message list, composer, menus, push messages, frame commands, speech output. */
    class ChatView {
        /**
         * @param {HTMLElement} container The element the chat is created in.
         * @param {string} chatId The chat id `<provider>.<providerChatId>`.
         * @param {string} [apiBase] Prefix of the api calls (depends on where the page is).
         * @param {Options} [options] Options.
         */
        constructor(container, chatId, apiBase, options) {
            const t = this;
            /** The container element. */
            t.Container = container;
            /** The chat id. */
            t.ChatId = chatId;
            /** The api prefix. */
            t.ApiBase = apiBase || "";
            /** @type {Options} The options. */
            t.Options = Object.assign(new Options(), options || {});
            /** @type {Settings} The persisted settings. */
            t.Settings = new Settings(chatId, t.Options.SettingsName);
            /** @type {Set<string>} Senders whose content is hidden. */
            t.HideFrom = new Set();
            const hidden = Sw.Storage.Local.GetJson(t.Settings.Keys.Hide, null);
            if (Array.isArray(hidden))
                hidden.forEach(x => t.HideFrom.add(x));
            /** True: absolute time stamps, false: live age. */
            t.ShowTimeStamp = t.Settings.Get("TimeStamp") === "true";
            /** @type {?object[]} Input languages (GetInputLanguages). */
            t.Languages = null;
            /** @type {Map<string, object>} Iso -> language. */
            t.LangMap = new Map();
            /** @type {?Composer} The input (CanPost only). */
            t.Composer = null;
            /** @type {Array<function():void>} Removers of the push / frame handlers. */
            t.Offs = [];
        }

        /**
         * An api url.
         * @param {string} method The method (or a relative url).
         * @returns {string} The url.
         */
        Api(method) {
            return this.ApiBase + method;
        }

        /**
         * Load the input languages once.
         * @returns {Promise<object[]>} The languages.
         */
        async GetLanguages() {
            if (!this.Languages) {
                const langs = await Sw.Net.Send(this.Api("GetInputLanguages"));
                this.Languages = langs || [];
                this.Languages.forEach(l => this.LangMap.set(l.Iso, l));
            }
            return this.Languages;
        }

        /**
         * A language as text.
         * @param {string} iso The iso code.
         * @returns {string} "English [en]" or "[xx]" when unknown.
         */
        LangText(iso) {
            const l = this.LangMap.get(iso);
            return l ? l.Name + " [" + iso + "]" : "[" + iso + "]";
        }

        /**
         * Are new messages of a sender hidden (the state the last toggled message of the sender was left in).
         * @param {string} name The sender.
         * @returns {boolean} True if hidden.
         */
        IsHidden(name) {
            return this.HideFrom.has(name);
        }

        /**
         * Set the default for new messages of a sender (persisted); existing rows are not changed.
         * @param {string} name The sender.
         * @param {boolean} hide True to hide.
         */
        SetHidden(name, hide) {
            if (hide === this.HideFrom.has(name))
                return;
            if (hide)
                this.HideFrom.add(name);
            else
                this.HideFrom.delete(name);
            Sw.Storage.Local.SetJson(this.Settings.Keys.Hide, Array.from(this.HideFrom));
        }

        /**
         * Hide / show one message (header click, message menu) and make that the default for new messages of its sender.
         * @param {MessageRow} row The row.
         * @param {boolean} [hide] True to hide, false to show, omitted: toggle.
         */
        ToggleRow(row, hide) {
            row.SetHidden(hide === undefined ? !row.Hidden : hide);
            this.SetHidden(row.Msg.From, row.Hidden);
        }

        /**
         * Hide / show all messages of a sender (message menu), new messages of the sender too.
         * @param {string} name The sender.
         * @param {boolean} hide True to hide.
         */
        SetSenderHidden(name, hide) {
            this.SetHidden(name, hide);
            for (const r of this.List.All())
                if (r.Msg.From === name)
                    r.SetHidden(hide);
        }

        /**
         * Show or hide all content and forget the hidden senders (`chat.showall` / `chat.hideall`, "Show all").
         * @param {boolean} hide True to hide.
         */
        SetAllHidden(hide) {
            this.HideFrom.clear();
            Sw.Storage.Local.Remove(this.Settings.Keys.Hide);
            this.List.All().forEach(r => r.SetHidden(hide));
        }

        /** @returns {boolean} Open mic is possible (speech recognition, not on mobile devices). */
        get CanOpenMic() {
            return C.SpeechInput.Supported() && C.SpeechInput.SupportOpenMic();
        }

        /** @returns {boolean} Speech output is possible (the server sends voices and the browser supports it). */
        get CanSpeak() {
            return !!this.Data.Voices && C.SpeechGen.Supported();
        }

        /**
         * Load the upload library (common/fileUploader.js of the FileUploader service, may be missing) and its md5 dependency.
         * @returns {Promise<void>}
         */
        async LoadUploader() {
            if (!Sw.Upload && !window.fileUploader) {
                await Sw.Net.IncludeJs(null, "md5.js", true);
                await Sw.Net.IncludeJs(null, "fileUploader.js", true);
            }
            try {
                if (Sw.Upload)
                    await Sw.Upload.Ensure();
            }
            catch (e) {
                console.warn("Chat: upload library not ready: " + e.message);
            }
        }

        /**
         * Build the chat: Join, optional dependencies, list, composer, menu, push messages, frame commands.
         * @returns {Promise<HTMLElement>} The message list element.
         */
        async Init() {
            const t = this;
            if (!C.SpeechGen)
                await Sw.Net.IncludeJs(ScriptUrl, "speech.js");
            if (C.SpeechGen.Supported())
                C.SpeechGen.Cancel();
            else
                console.warn("Chat: no speech synthesis supported");
            /** The Join response (ChatJoinResponse). */
            const data = t.Data = await Sw.Net.Send(t.Api("Join"), { ChatId: t.ChatId, MaxCount: 10, FromId: 0 });
            if (!data)
                throw new Error(_TF("Failed to join the chat!", "Error message shown when a chat could not be opened"));
            t.UserName = data.UserName;
            t.CanPost = !!data.CanPost;
            t.CanTranslate = !!data.CanTranslate;
            t.CurrentImage = "../auth/UserImages/" + data.UserGuid + "/small";
            const lang = t.Settings.Get("Lang");
            /** The input language. */
            t.CurrentLang = data.Lang;
            if (lang) {
                if (lang === data.Lang)
                    t.Settings.Set("Lang", null);
                else
                    t.CurrentLang = lang;
            }
            //  The server sends EnableSpeechByDefault (the names read by the old client were never sent)
            t.ListenDefault = data.EnableListenByDefault ?? !!data.EnableSpeechByDefault;
            t.SpeechDefault = data.EnableGenByDefault ?? !!data.EnableSpeechByDefault;
            t.SpeechOn = t.CanSpeak && t.Settings.Bool("Speech", t.SpeechDefault, true);
            const vol = parseInt(t.Settings.Get("Volume"));
            /** Speech volume in percent. */
            t.Volume = vol > 0 ? vol : 50;
            /** @type {Map<?string, object>} Voices by sender (null = default). */
            t.Voices = new Map((data.Voices || []).map(v => [v.Name ?? null, v]));

            //  Optional dependencies
            const deps = [];
            if (t.CanTranslate)
                deps.push(t.GetLanguages().catch(e => console.warn("Chat: failed to load languages: " + e.message)));
            if (data.UploadRepo && t.CanPost)
                deps.push(t.LoadUploader());
            await Promise.all(deps);
            /** @type {?string} The upload repository (uploads enabled). */
            t.UploadRepo = data.UploadRepo && t.CanPost && (Sw.Upload || typeof window.fileUploader === "function") ? data.UploadRepo : null;
            if (t.UploadRepo)
                console.log('Chat: file uploading to repository "' + t.UploadRepo + '" is enabled.');

            //  Elements
            const c = t.Container;
            c.classList.add("chat-root");
            /** @type {MessageList} The message list. */
            t.List = new MessageList(t);
            /** The top right menu button. */
            t.MenuButton = new Sw.Button({
                Icon: "moreV", Kind: "ghost", Class: "chat-menu-button",
                Title: _TF("Show menu option", "Tool tip description of a button that when clicked will show a context menu"),
                Menu: () => t.MainMenuItems(), MenuOptions: { Align: "end" },
            });
            //  Never over the messages: beside the text box (outside it, the other buttons are inside it) when the user can post, else
            //  in a slim bar above the list (sized by the MenuIconSize option)
            c.append(t.List.E);
            if (t.CanPost) {
                //  The automatic split (the text box sized to its text) until the user drags the splitter (RestoreSize, SetListHeight)
                c.classList.add("chat-autosize");
                t.Composer = new Composer(t);
                /** The splitter between the list and the input. */
                t.Split = t.CreateSplit();
                c.append(t.Split, t.Composer.E);
                t.Composer.E.append(t.MenuButton.E);
            }
            else {
                c.classList.add("chat-nopost");
                t.MenuButton.E.style.setProperty("--chat-ics", (t.Options.MenuIconSize || 48) + "px");
                c.prepend(t.MenuButton.E);
            }
            addTouchLabel(t.MenuButton.E, _TF("Menu", "Short label below the menu button of a chat (touch devices), keep it short"));

            for (const m of data.Messages || [])
                await t.List.Add(m);

            t.InitPush();
            t.InitCommands();
            if (t.Composer) {
                t.RestoreSize();
                if (t.CanOpenMic && data.SpeechName && t.Settings.Bool("Listen", t.ListenDefault, true))
                    t.Composer.StartListening();
            }
            t.List.StickForAwhile(true);
            if (t.Composer)
                t.Composer.Ready();
            return t.List.E;
        }

        /**
         * Create the splitter between the list and the input (drag with mouse / touch, or keyboard arrows (Shift = bigger steps),
         * Home / End, Enter / double click resets); the list height fraction is saved in the Size setting.
         * @returns {HTMLElement} The splitter.
         */
        CreateSplit() {
            const t = this;
            const bar = el("div.chat-split", {
                tabIndex: 0,
                title: _TF("Drag to resize the message list", "Tool tip of a bar between the chat messages and the input box that can be dragged to change their sizes"),
                attrs: { role: "separator", "aria-orientation": "horizontal", "aria-valuemin": "0", "aria-valuemax": "100", "aria-label": _TF("Resize the message list", "Accessible name of a bar that changes the size of the chat message list") },
            });
            let startH = 0;
            /** @type {?{Total: number, Split: number, Input: number}} The chat, splitter and least input heights at the drag start. */
            let dims = null;
            /** Reset to the default (automatic) split. */
            const reset = () => {
                t.List.E.style.height = "";
                t.Settings.Set("Size", null);
                t.Container.classList.add("chat-autosize");
                t.Composer.AutoGrow();
                bar.setAttribute("aria-valuenow", String(Math.round(100 * t.List.E.offsetHeight / Math.max(1, t.Container.offsetHeight))));
            };
            Sw.Drag(bar, {
                OnStart: () => {
                    startH = t.List.E.offsetHeight;
                    //  The sizes that do not change during the drag, read once (no layout read per move)
                    dims = { Total: t.Container.offsetHeight, Split: bar.offsetHeight, Input: t.Composer.MinHeight };
                },
                OnMove: (ev, s) => t.SetListHeight(startH + s.DY, dims),
                OnEnd: () => t.SaveSize(),
            });
            bar.addEventListener("dblclick", reset);
            bar.addEventListener("keydown", e => {
                const h = t.List.E.offsetHeight;
                const total = t.Container.offsetHeight;
                const step = total * (e.shiftKey ? 0.2 : 0.05);
                let n;
                switch (e.key) {
                    case "ArrowUp": n = h - step; break;
                    case "ArrowDown": n = h + step; break;
                    case "Home": n = 0; break;
                    case "End": n = total; break;
                    case "Enter":
                        e.preventDefault();
                        reset();
                        return;
                    default: return;
                }
                e.preventDefault();
                t.SetListHeight(n);
                t.SaveSize();
            });
            return bar;
        }

        /**
         * Set the list height (a user split: ends the automatic split; clamped: the list keeps 10% / 80 px, the input keeps 104 px,
         * 56 px in a compact (low) chat, and at least Composer.MinHeight: its buttons and two lines of text).
         * @param {number} h Height in px.
         * @param {?{Total: number, Split: number, Input: number}} [dims] The chat, splitter and least input heights read at the drag start
         * (else read now).
         */
        SetListHeight(h, dims) {
            const c = this.Container;
            const total = dims ? dims.Total : c.offsetHeight;
            const min = Math.min(total * 0.1, 80);
            const input = Math.max(c.classList.contains("chat-compact") ? 56 : 104, dims ? dims.Input : this.Composer.MinHeight);
            const max = Math.max(min, total - (dims ? dims.Split : this.Split.offsetHeight) - input);
            if (c.classList.contains("chat-autosize")) {
                c.classList.remove("chat-autosize");
                this.Composer.AutoGrow();
            }
            //  One layout read per call (the total above): the value is the height just set, not read back (a drag calls this per move)
            const px = Math.round(Math.max(min, Math.min(max, h)));
            this.List.E.style.height = px + "px";
            this.Split.setAttribute("aria-valuemin", String(Math.round(100 * min / Math.max(1, total))));
            this.Split.setAttribute("aria-valuemax", String(Math.round(100 * max / Math.max(1, total))));
            this.Split.setAttribute("aria-valuenow", String(Math.round(100 * px / Math.max(1, total))));
        }

        /** Save the list height fraction (Size setting). */
        SaveSize() {
            const total = this.Container.offsetHeight;
            if (total > 0)
                this.Settings.Set("Size", this.List.E.offsetHeight / total);
        }

        /** Restore the saved list height fraction (a user split, also after the chat is resized), else keep the automatic split. */
        RestoreSize() {
            const f = parseFloat(this.Settings.Get("Size"));
            if (f > 0 && f < 1)
                this.SetListHeight(f * this.Container.offsetHeight);
        }

        /** Subscribe to the server push messages of this chat (post, replace, remove, clear). */
        InitPush() {
            const t = this;
            const id = t.ChatId.toLowerCase();
            /**
             * Subscribe one message type.
             * @param {string} type The operation.
             * @param {function(object):*} fn The handler.
             */
            const on = (type, fn) => {
                const name = "chat." + type + "." + id;
                Sw.Session.Subscribe(name, fn);
                t.Offs.push(() => Sw.Session.Unsubscribe(name, fn));
            };
            on("post", async m => {
                const c = m.Chat;
                if (!c || t.List.Get(c.Id))
                    return;
                t.SpeakNew(await t.List.Add(c));
            });
            on("replace", async m => {
                const c = m.Chat;
                const row = c ? t.List.Get(c.Id) : null;
                if (!row)
                    return;
                await row.Update(c);
                if (t.SpeechOn && row.Voice)
                    C.SpeechGen.SpeakContinue(row.Text, (c.Flags & Flags.IsWorking) === 0);
            });
            on("remove", m => t.List.Remove(m.Value));
            on("clear", () => t.List.Clear());
        }

        /**
         * Speak a new message from another user (speech output on).
         * @param {MessageRow} row The row.
         */
        SpeakNew(row) {
            const t = this;
            const m = row.Msg;
            if (!t.SpeechOn || m.From === t.UserName)
                return;
            const vp = t.Voices.get(m.From) || t.Voices.get(null);
            if (!vp)
                return;
            row.Voice = C.SpeechGen.FindVoice(vp.Language, vp.Male, vp.Voice);
            C.SpeechGen.SpeakNew(row.Text, row.Voice, vp.Pitch, vp.Rate, t.Volume * 0.01, vp.Language, (m.Flags & Flags.IsWorking) === 0);
        }

        /** The commands from the hosting window (`{Type}` postMessage): scroll top / bottom, show / hide all, clear (CanClear only). */
        InitCommands() {
            const t = this;
            const cmds = new Map([
                ["chat.scrolltop", () => t.List.ToTop()],
                ["chat.scrollbottom", () => t.List.ToBottom()],
                ["chat.showall", () => t.SetAllHidden(false)],
                ["chat.hideall", () => t.SetAllHidden(true)],
            ]);
            if (t.CanPost && t.Data.CanClear)
                cmds.set("chat.clear", () => t.Clear());
            /**
             * Window message listener.
             * @param {MessageEvent} ev The event.
             */
            const fn = ev => {
                const d = ev.data;
                const f = d && typeof d.Type === "string" ? cmds.get(d.Type) : null;
                if (!f)
                    return;
                //  Only the hosting windows (or this window) may send commands
                if (ev.source !== window.parent && ev.source !== window.top && ev.source !== window)
                    return;
                console.log("Chat: got command \"" + d.Type + "\" from " + ev.origin);
                f();
            };
            window.addEventListener("message", fn);
            t.Offs.push(() => window.removeEventListener("message", fn));
        }

        /**
         * Remove all messages (`ClearAllMessages`, the chat id as argument).
         * @returns {Promise<void>}
         */
        async Clear() {
            await call(this.Api("ClearAllMessages"), this.ChatId, _TF("Failed to clear all chat messages.", "An error message shown when a user tries to send clear a chat room but the server returns a failure"));
        }

        /**
         * Convert server menu items (ChatMenuItem {Id, Name, Desc, Icon, Value, Checked, Radio, Children}) to menu items; a leaf sends
         * `SetValue {ChatId, Key: Id, Value, Id: messageId}`. Checked (true / false) makes a check box item (with Radio a radio item).
         * @param {?object[]} list The server items.
         * @param {number} [messageId=0] The message id (0 = the chat menu).
         * @returns {object[]} The menu items.
         */
        ServerItems(list, messageId) {
            const t = this;
            return (list || []).map(mi => {
                const sub = mi.Children && mi.Children.length;
                return {
                    Text: mi.Name,
                    Icon: mi.Icon || undefined,
                    Title: mi.Desc,
                    Checked: (mi.Checked === true || mi.Checked === false) ? mi.Checked : undefined,
                    Radio: !!mi.Radio,
                    Items: sub ? t.ServerItems(mi.Children, messageId) : undefined,
                    OnClick: sub ? undefined : () => call(t.Api("SetValue"), { ChatId: t.ChatId, Key: mi.Id, Value: mi.Value, Id: messageId || 0 },
                        _T("Failed to set chat value \"{0}\".", mi.Id, "An error message shown when a user tries to set a server side value but the server returns a failure.{0} is replaced by the value key name.")),
                };
            });
        }

        /**
         * The "Attach file" menu item.
         * @returns {object} The item.
         */
        AttachItem() {
            return {
                Text: _TF("Attach file", "Text of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                Icon: "attach",
                Title: _TF("Attach a file to the current message", "Tool tip description of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                Disabled: this.Data.MaxDataCount - this.Composer.DataCount <= 0,
                OnClick: () => this.Composer.AttachFiles(),
            };
        }

        /**
         * The "Clear" (draft) menu item.
         * @returns {object} The item.
         */
        ClearDraftItem() {
            return {
                Text: _TF("Clear the text box", "Name of a menu item that when clicked will clear the current chat message"),
                Icon: "IconChatClearCurrent",
                Title: _TF("Clear the current message text and any data", "Tool tip description of a menu item that when clicked will clear the current chat message"),
                Disabled: !this.Composer.CanSend,
                OnClick: () => this.Composer.Clear(),
            };
        }

        /**
         * The menu of a message (or of the preview row).
         * @param {MessageRow} row The row.
         * @returns {object[]} The items.
         */
        MessageMenuItems(row) {
            const t = this;
            const m = row.Msg;
            const items = [];
            const isMobile = Sw.Platform.IsMobileDevice;
            if (row.IsPreview) {
                if (t.UploadRepo)
                    items.push(t.AttachItem());
            }
            else if (t.Data.CanShowProfile)
                items.push({
                    Text: _TF("View profile", "Name of a menu item that when clicked will view the profile of a web site member"),
                    Icon: "IconProfile",
                    Title: _T("View the public profile page for \"{0}\"", m.From, "Tool tip description of a menu item that when clicked will view the profile of a web site member.{0} is replaced with the nick name of the member"),
                    OnClick: () => {
                        Sw.Frame.PostParent("ViewProfile", { Name: m.From, Image: m.FromImage });
                        console.log("Chat: sent ViewProfile for \"" + m.From + "\"");
                    },
                });
            const copyText = {
                Text: _TF("Copy text", "Name of a menu item that when clicked will copy the text of chat message to the clipboard"),
                Icon: "copy",
                Title: _TF("Copy the message text to the clipboard", "Tool tip description of a menu item that when clicked will copy the text of chat message to the clipboard"),
                OnClick: () => copy(m.Format === Formats.Text ? row.CurText : row.Text),
            };
            switch (m.Format) {
                case Formats.MarkDown:
                    items.push({
                        Text: _TF("Copy as Markdown", "Name of a menu item that when clicked will copy the original Markdown text of chat message to the clipboard"),
                        Icon: "file",
                        Title: _TF("Copy the message markdown text to the clipboard", "Tool tip description of a menu item that when clicked will copy the original Markdown text of chat message to the clipboard"),
                        OnClick: () => copy(row.CurText),
                    });
                    if (!isMobile)
                        items.push({
                            Text: _TF("Copy as HTML", "Name of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                            Icon: "code",
                            Title: _TF("Copy the formatted HTML to the clipboard", "Tool tip description of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                            OnClick: () => copy(row.Body.innerHTML),
                        });
                    break;
                case Formats.Html:
                    if (!isMobile)
                        items.push({
                            Text: _TF("Copy the original HTML", "Name of a menu item that when clicked will copy the original HTML code of chat message to the clipboard"),
                            Icon: "code",
                            Title: _TF("Copy the formatted HTML to the clipboard", "Tool tip description of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                            OnClick: () => copy(row.CurText),
                        });
                    break;
            }
            items.push(copyText);
            if (row.IsPreview) {
                items.push(t.ClearDraftItem());
                return items;
            }
            items.push({
                Text: _TF("Copy date and time", "Name of a menu item that when clicked will copy the time stamp of chat message to the clipboard"),
                Icon: "clock",
                Title: _T("Copy the time stamp \"{0}\" to the clipboard", m.Time, "Tool tip description of a menu item that when clicked will copy the time stamp of chat message to the clipboard.{0} is replaced with the time stamp"),
                OnClick: () => copy(m.Time),
            });
            items.push({
                Text: _TF("Copy user name", "Name of a menu item that when clicked will copy the user name of chat message to the clipboard"),
                Icon: "user",
                Title: _T("Copy the user name \"{0}\" to the clipboard", m.From, "Tool tip description of a menu item that when clicked will copy the user name of chat message to the clipboard.{0} is replaced with the name of the user that posted the message"),
                OnClick: () => copy(m.From),
            });
            if (row.Hidden)
                items.push({
                    Text: _TF("Show this message", "Name of a menu item that when clicked will show the content of a chat message that is currently hidden"),
                    Icon: "IconChatShow",
                    Title: _T("Show the content of this message, new messages from \"{0}\" are shown too", m.From, "Tool tip description of a menu item that when clicked will show the content of a chat message that is currently hidden (only that message), new messages from that user will also be shown.{0} is replaced with the name of the user"),
                    OnClick: () => t.ToggleRow(row, false),
                });
            else
                items.push({
                    Text: _TF("Hide for me", "Name of a menu item that when clicked will hide the content of a chat message (only for this user, on this device)"),
                    Icon: "IconChatHide",
                    Title: _T("Hide the content of this message, new messages from \"{0}\" are hidden too", m.From, "Tool tip description of a menu item that when clicked will hide the content of a chat message (only that message), new messages from that user will also be hidden.{0} is replaced with the name of the user"),
                    OnClick: () => t.ToggleRow(row, true),
                });
            //  All messages of the sender (and new ones)
            items.push({
                Text: _TF("Hide all messages from this user", "Name of a menu item that when clicked will hide the content of all chat messages from the user that posted this message (only for this user, on this device)"),
                Icon: "IconChatHide",
                Title: _T("Hide the content of all messages from \"{0}\", new messages too", m.From, "Tool tip description of a menu item that when clicked will hide the content of all chat messages from a user, including new messages.{0} is replaced with the name of the user"),
                OnClick: () => t.SetSenderHidden(m.From, true),
            }, {
                Text: _TF("Show all messages from this user", "Name of a menu item that when clicked will show the content of all chat messages from the user that posted this message"),
                Icon: "IconChatShow",
                Title: _T("Show the content of all messages from \"{0}\", new messages too", m.From, "Tool tip description of a menu item that when clicked will show the content of all chat messages from a user, including new messages.{0} is replaced with the name of the user"),
                OnClick: () => t.SetSenderHidden(m.From, false),
            });
            items.push(...t.ServerItems(m.MenuItems, m.Id));
            const canRemove = (m.Flags & Flags.CanRemove) !== 0;
            if (t.Data.CanClear || canRemove)
                items.push("-", {
                    Text: _TF("Remove message", "Name of a menu item that when clicked will remove a chat message"),
                    Icon: "IconChatRemove",
                    Danger: true,
                    Title: _TF("Remove this message from the chat", "Tool tip description of a menu item that when clicked will remove a chat message"),
                    OnClick: async () => {
                        if (!canRemove && !await Sw.Dialog.Confirm(
                            _TF("Remove the message?", "Title of a confirmation dialog that when confirmed will remove a chat message"),
                            _TF("The message is removed for everyone in the chat. This cannot be undone.", "Text of a confirmation dialog that when confirmed will remove a chat message"),
                            _TF("Remove", "Text of a button on a confirmation dialog that when clicked will remove a chat message"), {
                                OkIcon: "IconChatRemove", Danger: true,
                                OkTitle: _TF("Remove the message, this can't be undone", "Tool tip description of a button on a confirmation dialog that when clicked will remove a chat message"),
                                CancelTitle: _TF("Keep the message", "Tool tip description of a button on a confirmation dialog that when clicked will keep a chat message as opposed to remove it"),
                            }))
                            return;
                        await call(t.Api("RemoveMessage"), { ChatId: t.ChatId, MessageId: m.Id },
                            _TF("Failed to remove the chat message.", "An error message shown when a user tries to remove a chat message but the server returns a failure."));
                    },
                });
            return items;
        }

        /**
         * The items of the speech output part of the top right menu.
         * @returns {object[]} The items.
         */
        SpeechItems() {
            const t = this;
            const st = t.Settings;
            /**
             * A volume item.
             * @param {number} v The volume in percent.
             * @param {string} text The text.
             * @param {string} title The tool tip.
             * @param {string} icon The icon class.
             * @returns {object} The item.
             */
            const vol = (v, text, title, icon) => ({
                Text: text, Title: title, Icon: icon, Radio: true, Checked: t.Volume === v, Disabled: !t.SpeechOn,
                OnClick: () => {
                    t.Volume = v;
                    st.Set("Volume", String(v));
                },
            });
            return [
                //  On / off: a check box item (the check mark shows the current state)
                {
                    Text: _TF("Read new messages aloud", "Text of a check box menu item that turns speech generation (text to speech) on or off"),
                    Icon: "IconChatSpeechGenEnable",
                    Checked: !!t.SpeechOn,
                    Title: _TF("Read new messages aloud (text to speech).", "Tool tip description of a check box menu item that turns speech generation (text to speech) on or off") + "\n" +
                        _TF("Uses local services if available to avoid data leakage and bandwidth usage (this may sound worse than online solutions).", "Tool tip description of a menu item that when clicked will enable speech generation (text to speech), explains how the speech is generated"),
                    OnClick: () => t.SetSpeech(!t.SpeechOn),
                },
                {
                    Text: _TF("Reading volume", "Text of a menu item that have a number of speech volume presets as children"),
                    Icon: "IconChatSpeechVolume",
                    Title: _TF("Adjust the volume of the audio from speech synthesis.", "Tool tip description of a menu item that have a number of speech volume presets as children"),
                    Disabled: !t.SpeechOn,
                    Items: () => [
                        vol(25, _TF("25% volume", "Text of a menu item that when clicked will set the speech volume to 25%"), _TF("Use 25% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 25%"), "IconChatSpeechVolume25"),
                        vol(50, _TF("50% volume", "Text of a menu item that when clicked will set the speech volume to 50%"), _TF("Use 50% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 50%"), "IconChatSpeechVolume50"),
                        vol(75, _TF("75% volume", "Text of a menu item that when clicked will set the speech volume to 75%"), _TF("Use 75% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 75%"), "IconChatSpeechVolume75"),
                        vol(100, _TF("Max volume", "Text of a menu item that when clicked will set the speech volume to the maximum (100%)"), _TF("Use 100% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to the maximum (100%)"), "IconChatSpeechVolume100"),
                    ],
                },
            ];
        }

        /**
         * The items of the input part of the top right menu (CanPost only).
         * @returns {object[]} The items.
         */
        InputItems() {
            const t = this;
            const st = t.Settings;
            const cmp = t.Composer;
            const speechRec = C.SpeechInput.Supported();
            const items = [];
            if (t.Data.AllowMarkDown) {
                //  The input format: radio items (the check mark shows the current choice; neither is checked while the format is
                //  picked automatically, see Composer.AutoMarkDown)
                items.push({
                    Text: _TF("Write with formatting (Markdown)", "Text of a radio menu item that selects markdown text input"),
                    Icon: "IconChatMD",
                    Radio: true,
                    Checked: !!cmp.UseMarkDown,
                    Title: _TF("Use Markdown (MD) input instead of regular text", "Tool tip description of a menu item that when clicked will select markdown text input"),
                    OnClick: () => cmp.SetMarkDown(true),
                }, {
                    Text: _TF("Write plain text", "Text of a radio menu item that selects regular text input"),
                    Icon: "IconChatText",
                    Radio: true,
                    Checked: !cmp.UseMarkDown && !cmp.AutoMarkDown,
                    Title: _TF("Use regular text as input instead of Markdown (MD)", "Tool tip description of a menu item that when clicked will select regular text input"),
                    OnClick: () => cmp.SetMarkDown(false),
                });
                if (cmp.UseMarkDown)
                    items.push({
                        Text: _TF("Help on formatting (Markdown)", "Text of a menu item that when clicked will show a help page for MD (Markdown)"),
                        Icon: "IconChatHelp",
                        Title: _TF("Open Markdown syntax help in a new tab", "Tool tip description of a menu item that when clicked will show a help page for MD (Markdown)"),
                        OnClick: () => Sw.Page.Navigate("https://www.markdownguide.org/basic-syntax/", "_blank"),
                    });
            }
            items.push({
                Text: _TF("Show a preview of my message", "Text of a check box menu item that shows or hides the preview of a new chat message"),
                Icon: "IconChatShowPreview",
                Checked: !!cmp.PreviewOn,
                Title: _TF("Show a preview of the message", "Tool tip description of a menu item that when clicked will show a preview of new a chat message"),
                OnClick: () => cmp.TogglePreview(),
            });
            if (t.CanTranslate || speechRec)
                items.push({
                    Text: _TF("Language I write in", "Text of a menu item that when clicked will let the user select what language they used when writing chat messages"),
                    Icon: "IconChatLanguage",
                    Title: speechRec
                        ? _TF("Specify what language you use for your text messages and voice input", "Tool tip description of a menu item that when clicked will let the user select what language they used when writing chat messages")
                        : _TF("Specify what language you use for your text messages", "Tool tip description of a menu item that when clicked will let the user select what language they used when writing chat messages"),
                    OnClick: () => cmp.SelectLanguage(),
                });
            items.push(t.ClearDraftItem());
            if (t.UploadRepo)
                items.push(t.AttachItem());
            if (speechRec) {
                const autoSend = st.Bool("AutoSend", false, true);
                items.push({
                    Text: _TF("Send automatically after speaking", "Text of a check box menu item that turns automatic sending of messages when speech input completes on or off"),
                    Icon: "bolt",
                    Checked: autoSend,
                    Title: _TF("Send the message automatically when speech input completes", "Tool tip description of a check box menu item that turns automatic sending of messages when speech input completes on or off"),
                    OnClick: () => st.Set("AutoSend", autoSend ? "false" : "true"),
                });
            }
            if (t.CanOpenMic && t.Data.SpeechName) {
                const listening = st.Bool("Listen", t.ListenDefault, true);
                items.push({
                    Text: _TF("Listen all the time (open microphone)", "Text of a check box menu item that turns open microphone speech input on or off"),
                    Icon: "mic",
                    Checked: listening,
                    Title: _TF("Enable speech input, listening to: ", "Tool tip description of a menu item that when clicked will enable open microphone speech input, listening to a list of keywords that are listed below this text") +
                        t.Data.SpeechName.map(x => "\n" + x.split("|")[0]).join(""),
                    OnClick: () => {
                        st.Set("Listen", listening ? "false" : "true");
                        if (listening)
                            cmp.StopListening();
                        else
                            cmp.StartListening();
                    },
                });
            }
            return items;
        }

        /**
         * The top right menu.
         * @returns {object[]} The items.
         */
        MainMenuItems() {
            const t = this;
            const list = t.List;
            const items = [
                {
                    Text: _TF("Scroll to top", "Text of a menu item that when clicked will scroll to the first chat message"),
                    Icon: "up",
                    Title: _TF("Scroll to the first loadded message in the chat", "Tool tip description of a menu item that when clicked will scroll to the first chat message"),
                    Disabled: list.IsAtTop,
                    OnClick: () => list.ToTop(),
                },
                {
                    Text: _TF("Scroll to bottom", "Text of a menu item that when clicked will scroll to the last chat message"),
                    Icon: "down",
                    Title: _TF("Scroll to the end of the chat", "Tool tip description of a menu item that when clicked will scroll to the last chat message"),
                    Disabled: list.IsAtBottom,
                    OnClick: () => list.ToBottom(),
                },
                {
                    Text: _TF("Show all hidden messages", "Text of a menu item that when clicked will show and hidden (collapsed) chat messages"),
                    Icon: "IconChatShow",
                    Title: _TF("Show any hidden content in all messages", "Tool tip description of a menu item that when clicked will show and hidden (collapsed) chat messages"),
                    OnClick: () => t.SetAllHidden(false),
                },
                "-",
                //  The time display: radio items (age or time stamp, the check mark shows the current choice)
                {
                    Text: _TF("Show how long ago", "Text of a radio menu item that shows the message age instead of the message time"),
                    Icon: "history",
                    Radio: true,
                    Checked: !t.ShowTimeStamp,
                    Title: _TF("Show the message age instead of the time", "Tool tip description of a menu item that when clicked will show message age instead of message time"),
                    OnClick: () => t.SetTimeStamp(false),
                },
                {
                    Text: _TF("Show date and time", "Text of a radio menu item that shows the message time instead of the message age"),
                    Icon: "clock",
                    Radio: true,
                    Checked: !!t.ShowTimeStamp,
                    Title: _TF("Show the actual time of a message instead of age", "Tool tip description of a menu item that when clicked will show message time instead of message age"),
                    OnClick: () => t.SetTimeStamp(true),
                },
            ];
            if (t.Composer)
                items.push("-", ...t.InputItems());
            if (t.CanSpeak)
                items.push("-", ...t.SpeechItems());
            const server = t.ServerItems(t.Data.Menus, 0);
            if (server.length)
                items.push("-", ...server);
            if (t.CanPost && t.Data.CanClear)
                items.push("-", {
                    Text: _TF("Remove all messages", "Text of a menu item that when clicked will clear the chat (remove all messages)"),
                    Icon: "IconChatClear",
                    Danger: true,
                    Title: _TF("Remove ALL chat messages", "Tool tip description of a menu item that when clicked will clear the chat (remove all messages)"),
                    OnClick: async () => {
                        const title = _TF("Remove all messages?", "Title of a confirmation dialog that when confirmed will remove all chat messages in a room");
                        const text = _TF("Every message in this chat is removed for everyone. This cannot be undone.", "Text of a confirmation dialog that when confirmed will remove all chat messages in a room");
                        const okText = _TF("Remove all", "Text of a button on a confirmation dialog that when clicked will remove all chat messages in a room");
                        //  all messages of a room, for everyone: type to confirm (UX guide SAF-4)
                        const word = _TF("remove all", "The text the user types to confirm removing all chat messages in a room (short, lower case)");
                        if (!t.Data.DoNotConfirmClear && !await ((Sw.Cells && Sw.Cells.ConfirmDanger)
                            ? Sw.Cells.ConfirmDanger({ Title: title, Text: text, OkText: okText, OkIcon: "IconChatRemove", Warning: "", Confirm: word })
                            : Sw.Dialog.Confirm(title, text, okText, {
                                OkIcon: "IconChatRemove", Danger: true,
                                OkTitle: _TF("Remove all chat messages, this can't be undone", "Tool tip description of a button on a confirmation dialog that when clicked will remove all chat messages in a room"),
                                CancelTitle: _TF("Keep the chat messages", "Tool tip description of a button on a confirmation dialog that when clicked will keep all chat messages in a room as opposed to removing them"),
                            })))
                            return;
                        await t.Clear();
                    },
                });
            return items;
        }

        /**
         * Switch between time stamps and ages (persisted).
         * @param {boolean} on True for time stamps.
         */
        SetTimeStamp(on) {
            this.ShowTimeStamp = on;
            this.Settings.Set("TimeStamp", on ? "true" : null);
            this.List.All().forEach(r => r.UpdateFormat());
        }

        /**
         * Enable / disable speech output (persisted).
         * @param {boolean} on True to enable.
         */
        SetSpeech(on) {
            this.SpeechOn = on;
            this.Settings.Set("Speech", on ? "true" : "false");
            if (!on)
                C.SpeechGen.Cancel();
        }

        /** Stop listening, unsubscribe the push messages and the frame commands. */
        Dispose() {
            if (this.Composer)
                this.Composer.StopListening();
            this.Offs.forEach(f => f());
            this.Offs = [];
        }
    }

    /**
     * Create a chat inside a container element.
     * @param {HTMLElement} container The element the chat is created in.
     * @param {string} chatId The chat id `<provider>.<providerChatId>`.
     * @param {string} [apiBase] Prefix of the api calls (where ChatService is relative to the page; "" for pages in chat/).
     * @param {Options} [options] Options.
     * @returns {Promise<HTMLElement>} The message list element (its expando `SwChatView` is the ChatView).
     */
    async function Add(container, chatId, apiBase, options) {
        const v = new ChatView(container, chatId, apiBase, options);
        const e = await v.Init();
        e.SwChatView = v;
        return e;
    }

    /**
     * Page entry of Chat.html: url parameters `id` (required), `options` (json merged into Options) and `nobackground`.
     * @returns {Promise<void>}
     */
    async function Main() {
        const done = Sw.Page.Loading({ Text: _TF("Loading chat messages", "Message shown while waiting for chat messages to be loaded"), Opaque: true });
        const root = el("div.chat-page", { attrs: { role: "main" } });
        if (Sw.Url.Has("nobackground"))
            root.classList.add("chat-nobackground");
        document.body.append(root);
        /**
         * Show a message in the page instead of the chat (missing id, failed to open).
         * @param {string} title The heading.
         * @param {string} text The explanation.
         * @param {boolean} [retry] Add a "Try again" button (reloads the page).
         */
        const message = (title, text, retry) => {
            const box = el("div.chat-message-box", { attrs: { role: retry ? "alert" : "status" } },
                Sw.Icon(retry ? "warning" : "info", "chat-message-icon"),
                el("h1.chat-message-title", { text: title }),
                el("p.chat-message-text", { text }));
            if (retry)
                box.append(new Sw.Button({
                    Text: _TF("Try again", "Text of a button that retries loading a page after an error"),
                    Icon: "refresh",
                    Kind: "primary",
                    OnClick: () => location.reload(),
                }).E);
            root.replaceChildren(box);
            root.classList.add("chat-page-message");
        };
        try {
            const chatId = Sw.Url.Get("id");
            if (!chatId) {
                //  Expected user error: explain it in the page (no console error)
                message(_TF("No chat selected", "Heading of the chat page when it was opened without a chat id"),
                    _TF("This page shows one chat room. Open a chat from the application, or add the id of the chat as the id parameter, example: Chat.html?id=Simple.Generic", "Explanation on the chat page when it was opened without a chat id (the id url parameter is missing)"));
                return;
            }
            const options = new Options();
            const opt = Sw.Url.Get("options");
            if (opt) {
                //  Only known options with the expected value types (the url is not trusted)
                const o = JSON.parse(opt);
                if (o && typeof o === "object")
                    for (const k of Object.keys(o))
                        if (Object.prototype.hasOwnProperty.call(options, k) && (options[k] === null || typeof options[k] === typeof o[k]))
                            options[k] = o[k];
            }
            const list = await Add(root, chatId, null, options);
            done();
            //  The loader kept the focus while loading
            const cmp = list.SwChatView.Composer;
            if (cmp)
                cmp.Focus();
        }
        catch (e) {
            const text = e && e.message ? e.message : String(e);
            console.warn(text);
            message(_TF("Could not open the chat", "Heading of the chat page when the chat could not be opened (followed by the reason)"), text, true);
        }
        finally {
            done();
        }
    }

    Object.assign(C, { Flags, Formats, Options, Settings, Color, MessageRow, Attachments, MessageList, Composer, ChatView, Add, Main });

    /** Legacy global (old chat.js API): `Chat.addChat(container, chatId, apiBase, options)` and friends. */
    window.Chat = {
        addChat: Add,
        chatColor: Color,
        FlagIsWorking: Flags.IsWorking,
        FlagCanRemove: Flags.CanRemove,
        IsMobile: Sw.Platform.IsMobileDevice,
        Platform: Sw.Platform.Name,
    };
    /** Legacy global: the options class. */
    window.ChatOptions = Options;
    /** Legacy global: the page entry of Chat.html. */
    window.chatMain = Main;
})();
