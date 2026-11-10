/**
 * @file The text / code editor page edit/text.html (`Sw.TextPage`, legacy entry `textMain()`), built on the vendored Ace
 * 1.44 editor (external/ace.js, language coverage of ~200 modes, search / replace, folding, multi cursor, autocompletion)
 * and Prettier 3 (external/prettier, Beautify), themed with the Sw tokens (text.css, Ace theme class "ace-sysweaver").
 *
 * Url parameters (raw, as inserted by the server's extension editor format `edit/text.html?r={0}&u={1}&d={2}`; names are
 * case insensitive):
 * - `r` read url (required; may contain "?" and quotes), fetched with GET.
 * - `u` save url: enables editing and Save (POST `{Url, Name, Content}`); without it the text is read only.
 * - `d` delete url: Delete (confirm, POST `{Url, Name}`).
 * - `n` display / download / save name (default: the last path segment of r, uri decoded); sets the document title.
 * - `t` Ace mode (default from the file extension); `sw_log` is the SysWeaver log mode defined here (level colors, see
 *   DefineLogMode). Without `t`: `.log` files use it, and so does a plain text (ex: LogFile.txt of logFile/logfile.html) whose lines
 *   are SysWeaver log lines (LooksLikeLog), so every log shows its Debug / Info / Warning / Error levels; `t=plain_text` shows a
 *   log as plain text.
 * - `b=true` beautify on load and reload (when a formatter exists for the mode).
 * - `download=false` hides Download.
 * - `scrolltoend=true` scrolls to the last line on load and reload.
 * - `wrap=true|false` soft wraps long lines (default: the remembered choice of the "Wrap lines" toggle, localStorage
 *   `SysWeaver.TextWrap`, else on for narrow screens (<= 700 px) and logs).
 * - `follow=true` turns Follow on at load.
 * Read only logs (`sw_log`) are shown compact (CompactLog): header lines without the dot leader, continuation lines (stack
 * frames) at a fixed indent of one tab, the repeated source dropped; a copy and Download give the raw text. Wrapped log
 * rows get a hanging indent and long paths / urls wrap after "/" and "\" (LogBreakPoints). Date lines and "== File log
 * started ==" lines are separator bands (UpdateSeparators).
 * Layout (Design v2 fill page): Sw.PageLayout "fill" = the shared compact page header (in the app shell: the
 * menu name as title, the file name as description; stand alone: the file name as title, the file type as description) with
 * the file actions Reload, Beautify, Download, Save and Delete (Delete always in the "More actions" menu, class sw-ph-in-menu;
 * actions that do not fit move there too; phones keep the action row), then one card (.text-card.sw-fill) = a tool bar of
 * ghost icon tools (find, problems, the status, Wrap lines, Follow, Scroll to the end) above the Ace editor. The status
 * shows the line count of read only logs, else the file type and the cursor position. Empty read only texts show an empty
 * state, failed loads / bad links an error state (Sw.StateView), a deleted file a "deleted" state in the card.
 * Toolbar find field (all texts): case insensitive literal search in the editor (Ace find, all matches marked, "3 / 12" match
 * count in the field), Enter / F3 next, Shift+Enter / Shift+F3 previous, Escape back to the text; Ctrl+F focuses it (with the
 * selected text), Ctrl+F in the field opens Ace's full search box (case, whole word, regular expression).
 * Follow (read only logs and texts opened at their end, off by default): reloads every 5 s (FollowMs; only while the page is
 * visible) and adds the new lines (Append; a replaced / shorter file is shown anew), keeping the end in view; scrolled away
 * from the end it pauses (pause icon, dashed) and resumes at the end.
 * Logs: Previous / Next problem (Shift+F8 / F8) jump to Warning / Error / Fatal / Critical entries (wrapping; disabled with
 * the reason when there are none), "Problems only" folds every other line ("··· 120 lines ···", click a fold to open it) and keeps
 * the date line of each day with problems. "Local time" (remembered, localStorage SysWeaver.Log.LocalTime) shows each entry's time in the
 * viewer's zone in the left margin (Sw.Format.ServerToLocalText; the lines keep the server's clock). A new search typed while
 * the log is at its end (Follow, or opened at the end) starts at the newest match.
 * Phones: the header text goes (the top bar names the page), the action row and the one row tool bar stay; the card is
 * edge to edge in portrait.
 * View (the first header action): the file in its registered extension viewer (FindViewer: Api/GetExtensionViewer, else
 * Api/GetExtensionViewerFormat for the extension of the name), only when there is one and it is not this editor.
 * InterOp messages: `FileSaved {Url, Name, Content}`, `FileDeleted {Url, Name}`.
 * Keys: Ace defaults (Ctrl+H replace, Ctrl+Z / Y, Ctrl+Space, Ctrl+/ ...) plus Ctrl+F (toolbar find), F3 / Shift+F3, F8 / Shift+F8 (logs)
 * and Ctrl+S = Save (when saving is possible). Unsaved changes are marked in the title bar and the page asks before it is left.
 */
(function () {
    "use strict";

    const el = Sw.el;

    /** Ace mode by file extension (keep in sync with TextExtensions in TypeService.cs). .log files use the log mode sw_log (level
     * colors); a plain text that looks like a SysWeaver log does too (LooksLikeLog). */
    const Modes = {
        err: "plain_text", txt: "plain_text", log: "sw_log", cfg: "plain_text", nfo: "plain_text", sln: "plain_text", cmake: "plain_text",
        bat: "batchfile", cmd: "batchfile", ps1: "powershell", psm1: "powershell", psd1: "powershell", sh: "sh", bash: "sh", zsh: "sh",
        py: "python", rb: "ruby", php: "php", pl: "perl", pm: "perl", lua: "lua", r: "r", tcl: "tcl",
        c: "c_cpp", "c++": "c_cpp", cc: "c_cpp", cpp: "c_cpp", cxx: "c_cpp", h: "c_cpp", hh: "c_cpp", hpp: "c_cpp", hxx: "c_cpp", inl: "c_cpp", ipp: "c_cpp", ino: "c_cpp",
        cs: "csharp", csx: "csharp", cshtml: "razor", razor: "razor", vb: "vbscript", vbs: "vbscript", fs: "fsharp", fsx: "fsharp", fsi: "fsharp",
        go: "golang", rs: "rust", java: "java", kt: "kotlin", kts: "kotlin", swift: "swift", dart: "dart", scala: "scala", groovy: "groovy", gradle: "groovy", asm: "assembly_x86",
        glsl: "glsl", vert: "glsl", frag: "glsl", geom: "glsl", comp: "glsl", hlsl: "c_cpp", hlsli: "c_cpp", fx: "c_cpp",
        htm: "html", html: "html", xhtml: "html", css: "css", less: "less", scss: "scss", sass: "sass", js: "javascript", mjs: "javascript", cjs: "javascript", jsx: "jsx",
        ts: "typescript", mts: "typescript", cts: "typescript", tsx: "tsx", vue: "vue", svg: "svg",
        csv: "csv", tsv: "tsv", json: "json", jsonc: "json5", json5: "json5", yaml: "yaml", yml: "yaml", toml: "toml", properties: "properties", sql: "mysql", proto: "protobuf", gql: "graphqlschema",
        ini: "ini", inf: "ini", conf: "ini", reg: "ini", editorconfig: "ini",
        xml: "xml", xsd: "xml", xsl: "xml", xslt: "xml", config: "xml", slnx: "xml", csproj: "xml", vbproj: "xml", fsproj: "xml", props: "xml", targets: "xml", nuspec: "xml", resx: "xml", xaml: "xml", axaml: "xml",
        md: "markdown", rst: "rst", tex: "latex", adoc: "asciidoc", diff: "diff", patch: "diff",
        make: "makefile", mk: "makefile", dockerfile: "dockerfile", tf: "terraform", gitignore: "gitignore", gitattributes: "gitignore",
    };

    /** Display names of Ace modes whose name is not just the capitalized mode id. */
    const ModeNames = {
        plain_text: _TF("Plain text", "Name of a file type in the status line of the text editor: a text file without a language"), sw_log: _TF("Log file", "Name of a file type in the status line of the text editor: a log file of the server"), c_cpp: "C/C++", csharp: "C#", javascript: "JavaScript", typescript: "TypeScript",
        json: "JSON", json5: "JSON5", html: "HTML", css: "CSS", xml: "XML", svg: "SVG", csv: "CSV", glsl: "GLSL", mysql: "SQL",
        sql: "SQL", batchfile: _TF("Batch file", "Name of a file type in the status line of the text editor: a Windows command script"), markdown: "Markdown", yaml: "YAML", php: "PHP", powershell: "PowerShell", sh: _TF("Shell", "Name of a file type in the status line of the text editor: a Unix shell script"),
        vbscript: "Visual Basic", fsharp: "F#", golang: "Go", tsx: "TSX", jsx: "JSX", scss: "SCSS", toml: "TOML", ini: "INI", tsv: "TSV",
        protobuf: "Protocol Buffers", graphqlschema: "GraphQL", latex: "LaTeX", rst: "reStructuredText", asciidoc: "AsciiDoc", assembly_x86: _TF("Assembly", "Name of a file type in the status line of the text editor: source code in assembly language"), gitignore: _TF("Git ignore", "Name of a file type in the status line of the text editor: the file that lists what Git must ignore"),
    };

    /**
     * A human readable name of an Ace mode id ("plain_text" -> "Plain text"), shown in the toolbar.
     * @param {string} mode - Ace mode id.
     * @returns {string} The name.
     */
    function ModeName(mode) {
        const n = ModeNames[mode];
        if (n)
            return n;
        const s = (mode || "").replace(/_/g, " ");
        return s.charAt(0).toUpperCase() + s.substring(1);
    }

    /**
     * Define the Ace mode "ace/mode/sw_log" (once): SysWeaver log files (FileLogMessageHandler lines
     * "HH:mm:ss Level   . . .[Source] text", indented continuation lines, date lines "yyyy-MM-dd").
     * The level word of a header line is a badge (token classes ace_swlog ace_badge ace_lv*), and the header with its
     * continuation lines (stack traces) gets the level color (ace_swlog ace_lvdebug / lvinfo / lvwarn / lverror, text.css).
     * The level word stays in the text, so the color is never the only cue.
     */
    function DefineLogMode() {
        if (DefineLogMode.Done || !window.ace || !ace.define)
            return;
        DefineLogMode.Done = true;
        ace.define("ace/mode/sw_log", ["require", "exports", "module", "ace/lib/oop", "ace/mode/text", "ace/mode/text_highlight_rules"], (require, exports) => {
            const oop = require("ace/lib/oop");
            const TextMode = require("ace/mode/text").Mode;
            const TextHighlightRules = require("ace/mode/text_highlight_rules").TextHighlightRules;
            const levels = { Debug: "lvdebug", Info: "lvinfo", Warning: "lvwarn", Warn: "lvwarn", Error: "lverror", Fatal: "lverror", Critical: "lverror" };
            const heads = Object.keys(levels).map(lv => ({
                regex: "^(\\s*\\d{1,2}:\\d\\d:\\d\\d(?:[.,]\\d+)?\\s+)(" + lv + ")(\\b.*)$",
                token: ["swlog.time", "swlog.badge." + levels[lv], "swlog." + levels[lv]],
                next: levels[lv],
            }));
            //  (date lines and "== File log started ==" lines: separators, see UpdateSeparators)
            heads.push({ regex: "^(?:\\d{4}-\\d\\d-\\d\\d|==.*==)\\s*$", token: "swlog.sep", next: "start" });
            /** Highlight rules: one state per level, so continuation lines keep the level of their header line. */
            function LogHighlightRules() {
                this.$rules = { start: heads.concat([{ defaultToken: "text" }]) };
                for (const cls of new Set(Object.values(levels)))
                    this.$rules[cls] = heads.concat([{ regex: ".+", token: "swlog." + cls }]);
            }
            oop.inherits(LogHighlightRules, TextHighlightRules);
            /** The mode. */
            function Mode() {
                this.HighlightRules = LogHighlightRules;
            }
            oop.inherits(Mode, TextMode);
            Mode.prototype.$id = "ace/mode/sw_log";
            exports.Mode = Mode;
        });
    }

    /**
     * Give the wrapped rows of an Ace session a fixed hanging indent of one tab width (logs: each entry stands out, its
     * wrapped rows are indented while every new line starts at its own column).
     * Ace's "code" wrap method with indented soft wrap indents wrapped rows by the line's leading white space plus one tab;
     * log continuation lines (stack traces) start with a long run of spaces, which would leave a narrow column on phones.
     * So the wrap splits are computed on display tokens whose leading spaces / tabs count as plain characters (same
     * width, same split positions), which makes the indent exactly one tab width.
     * @param {object} session - Ace EditSession (soft wrap may be toggled later).
     */
    function HangingIndent(session) {
        const orig = session.$computeWrapSplits;
        if (typeof orig !== "function")
            return;
        //  Ace display tokens: 10 = space, 11 = tab, 12 = tab padding (kept: it is not a document column), 1 = a character
        session.$computeWrapSplits = function (tokens, wrapLimit, tabSize) {
            let i = 0;
            while (i < tokens.length && tokens[i] >= 10 && tokens[i] <= 12)
                ++i;
            if (i > 0) {
                tokens = tokens.slice();
                for (let k = 0; k < i; ++k)
                    if (tokens[k] !== 12)
                        tokens[k] = 1;
            }
            return orig.call(this, tokens, wrapLimit, tabSize);
        };
        session.setOption("wrapMethod", "code");
    }

    /** Fixed indent of log continuation lines (message lines, stack frames) in the compact log view: one tab width. */
    const LogIndent = "    ";

    /** A log header line: (time and level with its padding) (dot leader) ([Source]) (gap) (message). */
    const LogHeadRx = /^(\s*\d{1,2}:\d\d:\d\d(?:[.,]\d+)?\s+[A-Za-z]+\s*)((?:\.\s?)*)(\[[^\]]*\])?(\s*)(.*)$/;

    /** A log continuation line: (leading white space) (dot leader) ([Source]) (gap) (text). */
    const LogContRx = /^([ \t]+)((?:\.\s?)*)(\[[^\]]*\])?([ \t]*)(.*)$/;

    /**
     * The compact display text of a SysWeaver log (read only logs), so every line uses the full width, also on phones:
     * - Header lines ("HH:mm:ss Level   . . . .[Source]     message"): the dot leader is dropped and the gap before the
     *   message is one space, so the first row shows time, level, source and the start of the message.
     * - Continuation lines (more message lines and stack frames; raw: 17 to 52 leading spaces, often the entry's source
     *   again): the leading white space is collapsed to a fixed indent (LogIndent) and a repeated source is dropped.
     * - Other lines (date lines, "== File log started ==") are kept.
     * The line count is unchanged (line numbers match the file). Maps maps display columns back to raw columns, so a copy
     * gives the raw text (RawText); Download uses Raw.
     * @param {string} text - The raw log text.
     * @returns {{Raw: string, Lines: string[], Text: string, Maps: Array<?number[]>}} Lines = the raw lines; Maps[row] = null
     *   (unchanged) or [displayColumn, rawColumn, ...] pairs, one pair per kept piece.
     */
    function CompactLog(text) {
        const lines = text.split(/\r\n|\r|\n/);
        const out = new Array(lines.length);
        const maps = new Array(lines.length);
        let source = null;
        //  Only the continuation lines of a SysWeaver log entry are compacted: an indented line before the first header line (or
        //  after a line that ends the entry) keeps its indent, so other .log files (no SysWeaver header lines) are shown as written
        let inEntry = false;
        for (let i = 0; i < lines.length; ++i) {
            const s = lines[i];
            //  Pieces of the display line: [raw start column, display text]
            const pieces = [];
            let m = LogHeadRx.exec(s);
            if (m) {
                inEntry = true;
                const src = m[3] || "";
                source = src || null;
                pieces.push([0, m[1]]);
                if (src)
                    pieces.push([m[1].length + m[2].length, src + (m[5] ? " " : "")]);
                pieces.push([s.length - m[5].length, m[5]]);
            }
            else if (inEntry && (m = LogContRx.exec(s))) {
                const src = m[3] || "";
                //  A bracket right after the white space is the source only after a dot leader or when it repeats the entry's source
                const isSrc = !!src && (!!m[2] || src === source);
                const rest = isSrc ? m[5] : s.substring(m[1].length);
                if (rest) {
                    pieces.push([0, LogIndent]);
                    if (isSrc && src !== source)
                        pieces.push([m[1].length + m[2].length, src + " "]);
                    pieces.push([s.length - rest.length, rest]);
                }
            }
            else {
                //  A line that is not indented and not empty (a date line, "== File log started ==", other text) ends the entry
                if (s.trim())
                    inEntry = false;
                out[i] = s;
                maps[i] = null;
                continue;
            }
            let d = "";
            //  A line of white space only becomes empty (its column 0 maps to raw column 0)
            const map = pieces.length ? [] : [0, 0];
            for (const [r, t] of pieces) {
                map.push(d.length, r);
                d += t;
            }
            out[i] = d;
            maps[i] = d === s ? null : map;
        }
        return { Raw: text, Lines: lines, Text: out.join("\n"), Maps: maps };
    }

    /**
     * Map a display column of a compact log line (CompactLog) to its raw column.
     * @param {{Lines: string[], Maps: Array<?number[]>}} view - The compact log.
     * @param {number} row - Line index.
     * @param {number} col - Display column.
     * @returns {number} The raw column.
     */
    function RawColumn(view, row, col) {
        const map = view.Maps[row];
        if (!map)
            return col;
        let k = 0;
        while (k + 2 < map.length && map[k + 2] <= col)
            k += 2;
        return Math.min(map[k + 1] + (col - map[k]), view.Lines[row].length);
    }

    /**
     * The raw text of the selection of an editor showing a compact log, so a copy gives the file's text (Ace "copy" event).
     * @param {object} editor - Ace editor.
     * @param {{Lines: string[], Maps: Array<?number[]>}} view - The compact log.
     * @param {string} text - The text Ace copies (display text).
     * @returns {string} The raw text.
     */
    function RawText(editor, view, text) {
        const nl = editor.session.doc.getNewLineCharacter();
        const ranges = editor.selection.getAllRanges();
        const lines = view.Lines;
        if (ranges.every(r => r.isEmpty())) {
            //  A copy without a selection copies the lines of the cursors (Ace "copyWithEmptySelection")
            if (!text)
                return text;
            const rows = [...new Set(ranges.map(r => r.start.row))];
            return rows.map(row => (lines[row] || "") + nl).join("");
        }
        return ranges.filter(r => !r.isEmpty()).map(r => {
            const s = r.start, e = r.end;
            const sc = RawColumn(view, s.row, s.column), ec = RawColumn(view, e.row, e.column);
            if (s.row === e.row)
                return (lines[s.row] || "").substring(sc, ec);
            const parts = [(lines[s.row] || "").substring(sc)];
            for (let row = s.row + 1; row < e.row; ++row)
                parts.push(lines[row]);
            parts.push((lines[e.row] || "").substring(0, ec));
            return parts.join(nl);
        }).join(nl);
    }

    /**
     * Wrap long log tokens (paths, urls) after "/" and "\" (Ace's code wrap breaks at spaces and before punctuation, which
     * split urls as "http" / "://host" and Windows paths inside names). The display tokens of "/" and "\" become break points
     * (space tokens, same width); a ":" before "/" stays with its word. Leading white space is untouched (it is the wrap
     * indent). Lines with tabs or wide characters keep Ace's tokens. Ace still splits a token longer than the row.
     * @param {object} session - Ace EditSession.
     */
    function LogBreakPoints(session) {
        const orig = session.$getDisplayTokens;
        if (typeof orig !== "function")
            return;
        //  Ace display tokens: 1 = a character, 9 = punctuation, 10 = space
        session.$getDisplayTokens = function (str, offset) {
            const tokens = orig.call(this, str, offset);
            if (tokens.length !== str.length)
                return tokens;
            let i = 0;
            while (i < str.length && (str.charCodeAt(i) === 32 || str.charCodeAt(i) === 9))
                ++i;
            for (; i < str.length; ++i) {
                const c = str.charCodeAt(i);
                if (c === 47 || c === 92)
                    tokens[i] = 10;
                else if (c === 58 && str.charCodeAt(i + 1) === 47)
                    tokens[i] = 1;
            }
            return tokens;
        };
    }

    if (Sw.Icons && !Sw.Icons.Has("wrapLines"))
        Sw.Icons.Add("wrapLines", '<path d="M4 6h16M4 12h13a3 3 0 0 1 0 6h-4M4 18h5"/><path d="M15 15.5l-2.5 2.5 2.5 2.5"/>');
    if (Sw.Icons && !Sw.Icons.Has("followEnd"))
        Sw.Icons.Add("followEnd", '<path d="M12 4v11M7 10.5l5 5 5-5M5 20h14"/>');
    if (Sw.Icons && !Sw.Icons.Has("toEnd"))
        Sw.Icons.Add("toEnd", '<path d="M7 5l5 5 5-5M7 11l5 5 5-5M6 20h12"/>');
    if (Sw.Icons && !Sw.Icons.Has("problemNext"))
        Sw.Icons.Add("problemNext", '<path d="M9 3 2 15h14z"/><path d="M9 7.5v3.5M9 13h.01"/><path d="M19.5 9v11M16.5 17l3 3 3-3"/>');
    if (Sw.Icons && !Sw.Icons.Has("problemPrev"))
        Sw.Icons.Add("problemPrev", '<path d="M9 9 2 21h14z"/><path d="M9 13.5V17M9 19h.01"/><path d="M19.5 15V4M16.5 7l3-3 3 3"/>');

    /** Follow (live tail): the poll interval in ms. The log apis are cached for 4 to 5 s on the server, so polling faster gains nothing. */
    const FollowMs = 5000;

    /** A log header line of a problem: level Warning or worse. */
    const ProblemRx = /^\s*\d{1,2}:\d\d:\d\d(?:[.,]\d+)?\s+(?:Warning|Warn|Error|Fatal|Critical)\b/;

    /** A log line that starts a new entry (any header line, a date line or a "== ... ==" line); other lines continue the entry above. */
    const EntryStartRx = /^(?:\s*\d{1,2}:\d\d:\d\d(?:[.,]\d+)?\s+[A-Za-z]+\b|\d{4}-\d\d-\d\d\s*$|==.*==\s*$)/;

    /** A log separator line: a date line ("2026-10-19") or a "== File log started ... ==" line (TextPage.UpdateSeparators). */
    const SeparatorRx = /^(?:\d{4}-\d\d-\d\d|==.*==)\s*$/;

    /** A date line of a log ("2026-10-19": the server writes one at the start and at each change of day). */
    const DateLineRx = /^\d{4}-\d\d-\d\d\s*$/;

    /** A SysWeaver log header line with a known level ("HH:mm:ss Level ..."), to recognize a log by its content (LooksLikeLog). */
    const LevelLineRx = /^\s*\d{1,2}:\d\d:\d\d(?:[.,]\d+)?\s+(?:Debug|Info|Warning|Warn|Error|Fatal|Critical)\b/;

    /**
     * True when a text is a SysWeaver log (FileLogMessageHandler format), so a log served under another name (ex: LogFile.txt)
     * gets the log mode: of the first 200 lines that are not empty, indented or separators, at least one and at least half are
     * log header lines with a known level.
     * @param {string} text - The text.
     * @returns {boolean} True for a log.
     */
    function LooksLikeLog(text) {
        if (typeof text !== "string" || !text)
            return false;
        let heads = 0, other = 0;
        for (const s of text.split(/\r?\n/, 1000)) {
            if (!s.trim() || /^[ \t]/.test(s) || SeparatorRx.test(s))
                continue;
            if (LevelLineRx.test(s))
                ++heads;
            else
                ++other;
            if (heads + other >= 200)
                break;
        }
        return heads > 0 && heads >= other;
    }

    /**
     * The problem entries of a SysWeaver log: Warning, Error, Fatal and Critical header lines with their continuation lines.
     * @param {string[]} lines - The lines (raw or compact display lines: the row count and the line starts are the same).
     * @returns {Array<number[]>} [firstRow, lastRow] per entry, in order.
     */
    function ProblemEntries(lines) {
        const out = [];
        for (let i = 0; i < lines.length; ++i) {
            if (!ProblemRx.test(lines[i]))
                continue;
            let e = i;
            while (e + 1 < lines.length && !EntryStartRx.test(lines[e + 1]))
                ++e;
            out.push([i, e]);
            i = e;
        }
        return out;
    }

    /**
     * A case insensitive, literal search expression (the toolbar find field).
     * @param {string} q - The text to find.
     * @returns {?RegExp} The expression ("gi"), null for an empty text.
     */
    function FindRegExp(q) {
        return q ? new RegExp(q.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "gi") : null;
    }

    /**
     * Quote unquoted html attribute values with "/" or ":" (Prettier's html parser rejects them).
     * @param {string} html - The html.
     * @returns {string} The html.
     */
    function SanitizeHtml(html) {
        return html.replace(/(\s[a-zA-Z0-9:-]+)=([^"'\s>]+)/g, (m, attr, value) => (value.includes("/") || value.includes(":")) ? (attr + '="' + value + '"') : m);
    }

    /**
     * The Prettier formatter of an Ace mode (no xml: the xml plugin is not vendored).
     * @param {string} mode - Ace mode.
     * @returns {?function(string):Promise<string>} The formatter.
     */
    function formatter(mode) {
        if (!window.prettier)
            return null;
        const plugins = window.prettierPlugins;
        const json = x => prettier.format(x, { parser: "json5", plugins, useTabs: true, quoteProps: "preserve", singleQuote: false, trailingComma: "all", bracketSpacing: true });
        const map = {
            json: json,
            json5: json,
            css: x => prettier.format(x, { parser: "css", plugins, useTabs: true }),
            html: x => prettier.format(SanitizeHtml(x), { parser: "html", plugins, useTabs: true }),
            markdown: x => prettier.format(x, { parser: "markdown", plugins, useTabs: true }),
            javascript: x => prettier.format(x, { parser: "typescript", plugins, useTabs: true }),
        };
        return map[mode] || null;
    }

    /**
     * Test if a viewer url from the server is another page than this editor (the server falls back to the extension editor, text.html,
     * when an extension has no viewer).
     * @param {*} v - The url (absolute, or relative to the site root).
     * @returns {boolean} True for a viewer to offer.
     */
    function IsOtherViewer(v) {
        if (typeof v !== "string" || !v)
            return false;
        try {
            return !/\/edit\/text\.html$/i.test(new URL(v, Sw.Url.Root).pathname);
        }
        catch {
            return false;
        }
    }

    /**
     * The registered viewer page of a file (the server's extension viewers, Api/GetExtensionViewer, as used by Sw.Page.View: ex: markdown
     * rendered, csv / tsv as a table, an html page or an svg image itself), when it is another page than this editor. A read url without
     * an extension (an api, ex: ReadTextFile?"x.md") asks for the viewer of the name's extension (Api/GetExtensionViewerFormat).
     * @param {string} url - The read url (r).
     * @param {string} name - The file name (n, else from r).
     * @returns {Promise<?string>} The viewer url (absolute, or relative to the site root), null when there is none or the lookup failed.
     */
    async function FindViewer(url, name) {
        const root = Sw.Url.Root;
        const read = Sw.Url.Absolute(url);
        try {
            const v = await Sw.Net.Send(root + "Api/GetExtensionViewer", read);
            if (IsOtherViewer(v))
                return v;
            const i = name.lastIndexOf(".");
            const ext = i > 0 ? name.substring(i + 1).replace(/["']/g, "").toLowerCase() : "";
            if (!/^[a-z0-9+_]{1,16}$/.test(ext))
                return null;
            const f = await Sw.Net.Send(root + "Api/GetExtensionViewerFormat", ext);
            const u = typeof f === "string" && f.includes("{0}") ? f.split("{0}").join(read) : null;
            return IsOtherViewer(u) ? u : null;
        }
        catch {
            return null;
        }
    }

    /**
     * The page.
     */
    const TextPage = {
        /**
         * Page main.
         * @returns {Promise<void>}
         */
        async Main() {
            const done = Sw.Page.Loading();
            try {
                await this.Init();
            }
            catch (e) {
                if (Sw.Access && Sw.Access.IsDenied(e)) {
                    //  Not allowed to read the file (403): the shared "not allowed" state (who, the role needed, Home), no toast,
                    //  no tool bar (Reload / Download / Delete can't work)
                    document.body.replaceChildren();
                    document.body.classList.remove("text-page");
                    Sw.Page.ShowNotAllowed({ Error: e, Url: e.Url || Sw.Url.Get("r") });
                }
                else {
                    //  The shared error state in the page (what failed, Try again, the technical text under Details), no toast
                    const msg = e && e.message ? e.message : String(e);
                    this.ShowState({
                        Kind: "error",
                        Title: _TF("The file could not be loaded", "Title of the error state of the text viewer when the file failed to load"),
                        Text: _T("Loading \"{0}\" failed. The server may be busy or restarting; try again in a moment.", this.Name || Sw.Url.Get("n") || Sw.Url.Get("r") || "", "Text of the error state of the text viewer when the file failed to load. {0} is replaced by the file name"),
                        Details: msg,
                        Actions: [new Sw.Button({ Icon: "refresh", Text: _TF("Try again", "Text of a button that loads a page again after an error"), OnClick: () => location.reload() })],
                    });
                }
            }
            finally {
                done();
                Sw.Page.Loaded();
            }
        },

        /**
         * Show only a state (an error before or instead of the text): the fill page with its header and the state view in the card.
         * @param {object} o Sw.StateView options (Kind, Title, Text, Details, Actions).
         */
        ShowState(o) {
            const body = document.body;
            body.replaceChildren();
            body.classList.add("text-page");
            const page = Sw.PageLayout({ Type: "fill", Title: this.Name || Sw.Url.Get("n") || document.title, Compact: true, Class: "text-main" });
            page.Add(el("section.sw-card.sw-fill.text-card.text-card-state", null, Sw.StateView(o).E));
        },

        /**
         * Build the page.
         * @returns {Promise<void>}
         */
        async Init() {
            const U = Sw.Url;
            const url = U.Get("r");
            const body = document.body;
            body.classList.add("text-page");
            if (!url) {
                const msg = _TF("No read url supplied!", "Error message displayed when a required url parameter wasn't supplied") + " (r=..)";
                this.ShowState({ Kind: "error", Title: _TF("No file to show", "Title of the error state of the text viewer when the link names no file (no r url parameter)"), Text: msg });
                return;
            }
            const save = U.Get("u");
            const del = U.Get("d");
            //  Security: the read, save and delete urls must be on this site (relative or the same origin, http(s)). A crafted link must
            //  not make the page show another site's text as ours, or send the user's text to another site; nothing is requested.
            if (![url, save, del].every(x => !x || U.SafeSameOrigin(x))) {
                const msg = _TF("This file cannot be shown: its address (the r, u and d parameters) must be on this site.", "Error message on the text editor page when an url parameter points to another web site (not allowed)");
                this.ShowState({ Kind: "error", Title: _TF("This file cannot be shown", "Title of the error state of the text viewer when an url parameter points to another web site"), Text: msg });
                return;
            }
            const canDownload = (U.Get("download") || "").toLowerCase() !== "false";
            const scrollToEnd = (U.Get("scrolltoend") || "").toLowerCase() === "true";
            const autoBeautify = (U.Get("b") || "").toLowerCase() === "true";
            const pathName = url.split("?")[0];
            const lastSeg = url.substring(url.lastIndexOf("/") + 1);
            //  (the default name is for showing and saving: uri decoded, "My%20log.txt" -> "My log.txt")
            let name = U.Get("n");
            if (!name)
                try {
                    name = decodeURIComponent(lastSeg);
                }
                catch {
                    name = lastSeg;
                }
            document.title = name;
            //  (asked at once, in parallel with the text; the View action waits for it, so the header is built once)
            const viewerP = FindViewer(url, name);
            let mode = U.Get("t");
            if (!mode) {
                const seg = pathName.substring(pathName.lastIndexOf("/") + 1) || lastSeg;
                const e = Sw.Format.FileExtension ? Sw.Format.FileExtension(seg.replace(/["']/g, "")) : seg.split(".").pop();
                //  (else the extension of the name: n, or the last segment of r, ex: ReadTextFile?"x.json"; quotes dropped)
                mode = Modes[(e || "").toLowerCase()] || Modes[(name.split(".").pop() || "").replace(/["']/g, "").toLowerCase()] || "plain_text";
            }
            //  A plain text without an explicit type that is a SysWeaver log (ex: Debug > Log file reads Api/logFile/LogFile.txt) gets the
            //  log mode: the text is fetched first (still the one request) and used below, where a failed fetch is reported as before.
            let firstText = null;
            if (mode === "plain_text" && !U.Get("t")) {
                firstText = Sw.Net.Get(url, { Text: true, Reload: true });
                try {
                    if (LooksLikeLog(await firstText))
                        mode = "sw_log";
                }
                catch (e) {
                    //  (reported when the text is shown)
                }
            }
            const beautify = formatter(mode);
            const t = this;
            t.Url = url;
            t.Name = name;
            t.Saved = null;
            //  Read only logs are shown compact (CompactLog: collapsed indents, full width rows); Raw keeps the file's text
            t.CompactLog = mode === "sw_log" && !save;
            body.classList.toggle("text-log", mode === "sw_log");
            t.Raw = null;

            const isLog = mode === "sw_log";
            const modeName = ModeName(mode);
            t.ModeName = modeName;
            t.Mode = mode;
            t.IsLog = isLog;

            //  Page (Design v2 fill page): the shared compact page header (Sw.PageLayout / Sw.PageHeader) and one card
            //  that takes the rest of the height, with the view tools (find, problems, wrap, follow, to the end) above the text.
            //  In the app shell (a menu page, ex: Monitoring > Log file) the header title is the menu name and the file name is the
            //  description (the h1, the breadcrumb and the menu say the same); stand alone the file name is the title and the file type
            //  the description. The file actions (Reload, Beautify, Download, Save) are the header actions; Delete always sits in the
            //  "More actions" menu (sw-ph-in-menu), and actions that do not fit move there too (phones keep the action row).
            const page = Sw.PageLayout({ Type: "fill", Title: name, Compact: true, Class: "text-main" });
            const header = page.Header;
            const fromMenu = !!header.FromMenu;
            header.E.classList.add("text-ph");
            header.SetDescription(fromMenu ? name : modeName);
            const nameE = fromMenu ? header.DescE : header.TitleE;
            nameE.title = url;
            nameE.classList.add("text-name");
            const typeTitle = _T("File type: {0}", modeName, "Tool tip of the file type label in the toolbar of the text viewer. {0} is replaced by the type name (ex: Log file, JavaScript)");
            //  The status at the right of the tool bar: the line count of read only texts (logs), else the file type and the cursor
            //  position (the type name is not repeated for logs: the header says "Log file")
            const info = el("span.text-info", { title: typeTitle + " (" + mode + ")", attrs: { "aria-label": typeTitle } });
            t.InfoE = info;
            const bar = el("div.text-bar", { attrs: { role: "toolbar", "aria-label": _TF("Text tools", "Accessible name of the tool bar of the text viewer (find, wrap lines, follow ...)") } });
            const host = el("div.text-editor", { attrs: { role: "region", "aria-label": name } });
            const card = el("section.sw-card.sw-fill.text-card", { attrs: { "aria-label": name } }, bar, host);
            page.Add(card);
            t.Card = card;
            /** A tool bar button: ghost, icon only (the text is the accessible name, the tool tip tells the action). */
            const tool = o => new Sw.Button(Object.assign({ Kind: "ghost", Class: "text-tool" }, o));

            //  Find: a visible field wired to the editor's search (Ctrl+F focuses it; Ctrl+F again opens Ace's full search box)
            const findInput = el("input.sw-input.text-find-input", {
                type: "search",
                placeholder: _TF("Find", "Placeholder of the find field in the toolbar of the text viewer"),
                attrs: {
                    "aria-label": _TF("Find in the text", "Accessible name of the find field in the toolbar of the text viewer"),
                    title: _TF("Find in the text (Ctrl+F). Enter: next match, Shift+Enter: previous match", "Tool tip of the find field in the toolbar of the text viewer"),
                    spellcheck: "false", autocomplete: "off", enterkeyhint: "search",
                },
            });
            const findCount = el("span.text-find-count", { attrs: { "aria-live": "polite" } });
            const findPrev = tool({
                Icon: "up", Title: _TF("Previous match (Shift+Enter, Shift+F3)", "Tool tip of a button in the toolbar of the text viewer that selects the previous match of the find text"),
                Label: _TF("Previous match", "Accessible name of a button in the toolbar of the text viewer that selects the previous match of the find text"),
                OnClick: () => t.Find(-1),
            });
            const findNext = tool({
                Icon: "down", Title: _TF("Next match (Enter, F3)", "Tool tip of a button in the toolbar of the text viewer that selects the next match of the find text"),
                Label: _TF("Next match", "Accessible name of a button in the toolbar of the text viewer that selects the next match of the find text"),
                OnClick: () => t.Find(1),
            });
            bar.append(el("div.text-find", { attrs: { role: "search" } }, el("span.text-find-field", null, findInput, findCount), findPrev.E, findNext.E));
            t.FindInput = findInput;
            t.FindCount = findCount;
            let findTimer = 0;
            findInput.addEventListener("input", () => {
                clearTimeout(findTimer);
                findTimer = setTimeout(() => t.Find(0), 120);
            });
            findInput.addEventListener("keydown", e => {
                if (e.key === "Enter" || e.key === "F3") {
                    e.preventDefault();
                    clearTimeout(findTimer);
                    t.Find(e.shiftKey ? -1 : 1);
                }
                else if (e.key === "Escape") {
                    e.preventDefault();
                    e.stopPropagation();
                    if (t.Editor)
                        t.Editor.focus();
                }
                else if ((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === "f" || e.key === "F")) {
                    //  Ctrl+F in the field: Ace's full search box (match case, whole word, regular expression)
                    e.preventDefault();
                    if (t.Editor) {
                        t.Editor.focus();
                        t.Editor.execCommand("find");
                    }
                }
            });

            //  Logs: jump to the previous / next warning or error (F8 / Shift+F8), and a "problems only" view (folds the other lines)
            let problemPrev = null, problemNext = null, problemsOnly = null;
            if (isLog) {
                problemPrev = tool({
                    Icon: "problemPrev", Label: _TF("Previous problem", "Text of a button in the log viewer that jumps to the previous warning or error line"),
                    Title: _TF("Go to the previous warning or error (Shift+F8)", "Tool tip of a button in the log viewer that jumps to the previous warning or error line"),
                    OnClick: () => t.GotoProblem(-1),
                });
                problemNext = tool({
                    Icon: "problemNext", Label: _TF("Next problem", "Text of a button in the log viewer that jumps to the next warning or error line"),
                    Title: _TF("Go to the next warning or error (F8)", "Tool tip of a button in the log viewer that jumps to the next warning or error line"),
                    OnClick: () => t.GotoProblem(1),
                });
                problemsOnly = tool({
                    Icon: "filter", Label: _TF("Problems only", "Text of a toggle button in the log viewer that hides all lines except warnings and errors"),
                    Title: _TF("Show only warnings and errors (the other lines are folded; click a fold to open it)", "Tool tip of a toggle button in the log viewer that hides all lines except warnings and errors") + "\n" +
                        _TF("The date line of each day with problems stays visible. Failures logged at the Info or Debug level are not shown: use Find for them.", "Tool tip part of the Problems only toggle of the log viewer"),
                    Pressable: true,
                    OnClick: () => t.ApplyProblemsOnly(),
                });
                bar.append(el("div.text-problems", { attrs: { role: "group", "aria-label": _TF("Problems", "Accessible name of the group of buttons in the log viewer that find warnings and errors") } }, problemPrev.E, problemNext.E, problemsOnly.E));
            }
            //  Logs: the times in the viewer's own time zone (in the left margin; the lines stay as the server wrote them)
            let localBtn = null;
            if (isLog) {
                const localKey = "SysWeaver.Log.LocalTime";
                localBtn = tool({
                    Icon: "clock", Label: _TF("Local time", "Text of a toggle button in the log viewer that shows the time of each line in the viewer's time zone"),
                    Title: _TF("Show the time of each entry in your time zone, in the left margin (the lines keep the server's time)", "Tool tip of a toggle button in the log viewer that shows the times in the viewer's time zone"),
                    Pressable: true, Pressed: Sw.Storage.Local.Get(localKey) === "1",
                    OnClick: b => {
                        Sw.Storage.Local.Set(localKey, b.Pressed ? "1" : "0");
                        t.ApplyLocalTime();
                    },
                });
            }
            bar.append(el("span.text-spacer"), info);

            const fetchText = () => Sw.Net.Get(url, { Text: true, Reload: true });
            const doBeautify = async text => {
                if (!beautify || !autoBeautify)
                    return text;
                try {
                    return await beautify(text);
                }
                catch (e) {
                    console.warn("Failed to beautify: " + (e && e.message));
                }
                return text;
            };
            const reloadBtn = new Sw.Button({
                Icon: "IconReload", Text: _TF("Reload", "Text on a button that when clicked will reload a file"),
                Title: _TF("Reload the file", "Tool tip description on a button that when clicked will reload the file"),
                //  Not async: the button shows its spinner only for the reload itself, never while the confirmation is open
                OnClick: b => {
                    if (!t.IsDirty())
                        return reload();
                    Sw.Cells.ConfirmDanger({
                        Title: _TF("Reload the file?", "Title of the dialog that confirms reloading a modified file (the changes are lost)"),
                        ItemLabel: _TF("File:", "Label before the file name in a confirmation dialog of the text editor (ex: File: LogFile.txt)"),
                        Item: t.Name,
                        Warning: _TF("Your changes will be lost.", "Text of a dialog that confirms reloading a modified file"),
                        OkText: _TF("Reload", "Text on a button that when clicked will reload a file"),
                    }).then(async ok => {
                        if (!ok)
                            return;
                        b.SetWorking(true);
                        await reload();
                        b.SetWorking(false);
                    });
                },
            });
            /**
             * Load the file again (replaces the text; the caller confirmed when there were changes).
             * @returns {Promise<void>}
             */
            const reload = async () => {
                try {
                    const text = await fetchText();
                    if (text === null || text === undefined)
                        return;
                    const nt = await doBeautify(text);
                    if (nt !== t.Text())
                        t.SetText(nt, scrollToEnd);
                    t.Saved = t.Editor.session.getValue();
                    t.UpdateDirty();
                }
                catch (e) {
                    Sw.Fail(_TF("Failed to reload the file", "Error message displayed when a file reload failed") + ":\n" + (e.message || e));
                }
            };
            const actions = [];
            //  View: the file in its registered viewer (markdown rendered, a table, the page or image itself); only when there is one
            const viewer = await viewerP;
            t.ViewerUrl = viewer;
            if (viewer) {
                actions.push(new Sw.Button({
                    Icon: "eye", Text: _TF("View", "Text on a button of the text editor that opens the file in its viewer (ex: markdown shown as a document, csv as a table, an svg as an image)"),
                    Title: _TF("Show the file in its viewer (ex: markdown as a document, a table, a page or an image). With unsaved changes it opens in a new tab and shows the saved file.", "Tool tip of the View button of the text editor"),
                    OnClick: () => t.View(),
                }));
            }
            actions.push(reloadBtn);
            t.FetchText = fetchText;
            t.DoBeautify = doBeautify;
            if (beautify) {
                actions.push(new Sw.Button({
                    Icon: "wand", Text: _TF("Beautify", "Text on a button that when clicked will beatify some source code text"),
                    Title: _TF("Beautify the text", "Tool tip description on a button that when clicked will beatify some source code text"),
                    OnClick: async () => {
                        try {
                            const s = t.Editor.session.getValue();
                            const f = await beautify(s);
                            if (f !== s) {
                                //  Replace through the document so Ace's undo can revert the formatting.
                                t.Editor.session.doc.setValue(f);
                            }
                        }
                        catch (e) {
                            Sw.Fail(e && e.message ? e.message : String(e));
                        }
                    },
                }));
            }
            let downloadBtn = null, saveBtn = null, deleteBtn = null;
            if (canDownload) {
                downloadBtn = new Sw.Button({
                    Icon: "download", Text: _TF("Download", "Text on a button that when clicked will download the file"),
                    Title: _TF("Download the file", "Tool tip description on a button that when clicked will download the file"),
                    OnClick: () => Sw.Net.DownloadText(name, t.Text()),
                });
                actions.push(downloadBtn);
            }
            if (save) {
                saveBtn = new Sw.Button({
                    Icon: "save", Text: _TF("Save", "Text on a button that when clicked will save the data"),
                    Title: _TF("Save your changes", "Tool tip description on a button that when clicked will save the data") + " (" + Sw.Keys.Format("Mod+S") + ")",
                    OnClick: () => t.Save(save),
                });
                actions.push(saveBtn);
            }
            if (del) {
                deleteBtn = new Sw.Button({
                    Icon: "trash", Kind: "danger", Text: _TF("Delete", "Text on a button that when clicked will delete the file from the server"),
                    Title: _TF("Delete the file", "Tool tip description on a button that when clicked will delete the file from the server"),
                    //  Always in the "More actions" menu of the page header (a destructive action is not a neighbour of Download)
                    Class: "sw-ph-in-menu",
                    //  SwWeb:allow ux-fbk-2 (no promise on purpose: the button is busy while Sw.Cells.ConfirmDanger prepares the token, not while the user reads the confirmation; Delete shows its own busy state after the confirmation)
                    OnClick: () => {
                        t.Delete(del); /* SwWeb:allow ux-fbk-2 (see above) */
                    },
                });
                actions.push(deleteBtn);
            }
            header.SetActions(actions);
            //  (Save / Delete are enabled like the original viewer, no role lookup first: a refusal disables the button with the reason,
            //  Sw.Access.ActionDenied)
            //  Soft wrap: url "wrap", else the remembered choice, else on for narrow screens and logs
            const wrapKey = "SysWeaver.TextWrap";
            const wrapParam = (U.Get("wrap") || "").toLowerCase();
            const wrapStored = Sw.Storage.Local.Get(wrapKey);
            const wrapOn = wrapParam ? wrapParam === "true" : wrapStored ? wrapStored === "1" : (mode === "sw_log" || window.matchMedia("(max-width: 700px)").matches);
            const wrapBtn = tool({
                Icon: "wrapLines", Label: _TF("Wrap lines", "Text on a toggle button that when pressed wraps long lines of a text so that they fit the screen"),
                Title: _TF("Wrap long lines so that they fit the screen (or to show each line on one row)", "Tool tip description of a toggle button that wraps long lines of a text"),
                Pressable: true, Pressed: wrapOn,
                OnClick: b => {
                    Sw.Storage.Local.Set(wrapKey, b.Pressed ? "1" : "0");
                    if (t.Editor)
                        t.Editor.session.setUseWrapMode(b.Pressed);
                },
            });
            //  Follow (live tail) of read only texts that are logs or open at their end: off by default (url "follow=true" turns it on)
            let followBtn = null;
            if (!save && (isLog || scrollToEnd)) {
                followBtn = tool({
                    Icon: "followEnd", Label: _TF("Follow", "Text of a toggle button in the text viewer that keeps loading new lines of a file (like a live log) and keeps the end in view"),
                    Pressable: true,
                    OnClick: b => t.SetFollow(b.Pressed),
                });
            }
            const endBtn = tool({
                Icon: "toEnd", Label: _TF("Scroll to the end", "Accessible name of a button in the toolbar of the text viewer that scrolls to the last line"),
                Title: _TF("Scroll to the last line (Ctrl+End)", "Tool tip of a button in the toolbar of the text viewer that scrolls to the last line"),
                OnClick: () => {
                    t.ToEnd();
                    t.UpdateFollowState();
                },
            });
            bar.append(el("div.text-view", { attrs: { role: "group", "aria-label": _TF("View", "Accessible name of the group of view buttons in the toolbar of the text viewer (wrap lines, follow, scroll to the end)") } }, wrapBtn.E, localBtn ? localBtn.E : null, followBtn ? followBtn.E : null, endBtn.E));
            t.Buttons = {
                Save: saveBtn, Delete: deleteBtn, Download: downloadBtn, Reload: reloadBtn, Wrap: wrapBtn, Follow: followBtn, End: endBtn,
                FindPrev: findPrev, FindNext: findNext, ProblemPrev: problemPrev, ProblemNext: problemNext, ProblemsOnly: problemsOnly, LocalTime: localBtn,
            };
            t.NameE = nameE;
            /** @type {?HTMLElement} The file name in the page header (kept for callers of the old toolbar name; = NameE). */
            t.PHDesc = null;
            t.Header = header;
            //  Low screens (text.css): the header actions lie over the end of the tool bar, which keeps room for them (--text-ph-w)
            if (window.ResizeObserver)
                new ResizeObserver(() => page.E.style.setProperty("--text-ph-w", header.E.offsetWidth + "px")).observe(header.E);

            //  Editor. The card already has its final size (it fills the page); until the text is in and laid out (scrolled to its end,
            //  gutter width known) the editor area shows a skeleton and the editor is hidden, so nothing moves when the text arrives
            //  (LAY-1 / LAY-2: Ace moves its layers on the first render and on the scroll to the end; FBK-6 loading state).
            host.classList.add("text-loading");
            const loadingE = el("div.text-loadstate", null, Sw.StateView({
                Kind: "loading", Lines: 8,
                Title: isLog
                    ? _TF("Loading the log...", "Loading state of the log viewer while the log text is fetched")
                    : _TF("Loading the file...", "Loading state of the text viewer while the file text is fetched"),
            }).E);
            loadingE.style.setProperty("--text-bar-h", bar.offsetHeight + "px");
            card.append(loadingE);
            //  Until the text is shown the file actions and tools have nothing to work on: disabled with the reason (INP-12); a button
            //  the role doesn't allow (after a refusal, Sw.Access.ActionDenied: "Requires Admin") stays disabled with its own reason afterwards
            const waitWhy = isLog
                ? _TF("Wait until the log is loaded", "Why a button of the log viewer is disabled while the log text is loading")
                : _TF("Wait until the file is loaded", "Why a button of the text viewer is disabled while the file text is loading");
            const locked = Array.from(new Set(actions.concat(Object.values(t.Buttons)).filter(x => x && x.SetDisabled && !x.E.classList.contains("sw-denied-action"))));
            locked.forEach(x => x.SetDisabled(true, waitWhy));
            if (Sw.Disabled)
                Sw.Disabled.Set(findInput, true, waitWhy);
            const unlock = () => {
                locked.forEach(x => {
                    if (!x.E.classList.contains("sw-denied-action"))
                        x.SetDisabled(false);
                });
                if (Sw.Disabled)
                    Sw.Disabled.Set(findInput, false);
            };
            let text = await (firstText || fetchText());
            firstText = null;
            if (text === null || text === undefined)
                text = "";
            text = await doBeautify(text);
            const readOnly = !save;
            const editor = ace.edit(host, {
                readOnly: readOnly,
                animatedScroll: true,
                displayIndentGuides: mode !== "sw_log",
                enableAutoIndent: true,
                firstLineNumber: 1,
                highlightGutterLine: true,
                //  Logs have no folds: no fold widgets, so the gutter is narrower (phones)
                showFoldWidgets: mode !== "sw_log",
                showFoldedAnnotations: true,
                showLineNumbers: true,
                enableBasicAutocompletion: true,
                enableLiveAutocompletion: true,
                useSvgGutterIcons: true,
                useWorker: !readOnly,
                showPrintMargin: false,
                //  Logs are read like the rest of the page: the normal text size (--sw-size; the smaller code size made them harder to
                //  read than everything around them); code keeps the code size
                fontSize: mode === "sw_log" ? "var(--sw-text-md)" : "var(--sw-mono-size)",
                wrap: wrapOn,
                //  Logs: wrapped rows are indented, so each entry stands out (compact logs: a header's rows by one tab, a
                //  continuation line's rows by one tab more than the line; editable logs: HangingIndent)
                indentedSoftWrap: mode === "sw_log",
            });
            t.Editor = editor;
            if (mode === "sw_log") {
                if (!t.CompactLog)
                    HangingIndent(editor.session);
                LogBreakPoints(editor.session);
            }
            //  A copy of a compact log gives the file's raw text
            if (t.CompactLog)
                editor.on("copy", e => {
                    if (t.Raw && e)
                        e.text = RawText(editor, t.Raw, e.text);
                });
            //  The shared Sw theme (edit/ace.css), also on the popups Ace appends to body (autocomplete, tooltips)
            Sw.AceEditor.Theme(editor, host);
            //  Whole lines at the top: the bottom scroll margin makes the end position a multiple of the line height (2 px + the
            //  remainder of the view height), so a view scrolled to the end (Follow, the end of a log) starts with a whole line
            //  instead of one cut under the toolbar. Re-checked after every render (resize, font, zoom).
            const alignEnd = () => {
                const r = editor.renderer;
                const h = r.lineHeight;
                const H = r.$size && r.$size.scrollerHeight;
                if (!h || !H)
                    return;
                const b = 2 + ((H - 2) % h + h) % h;
                if (!(Math.abs(b - (t.EndMargin ?? -9)) < 0.01)) {
                    //  A view at the end stays at the (new) end; a text shorter than the view has no end to keep (scrolling to it showed only
                    //  its last, often empty, line: a JSON, SVG or Markdown file looked empty, P3-A-13)
                    const total = editor.session.getScreenLength() * h;
                    const end = total > H && r.getScrollTop() + H >= total - h;
                    t.EndMargin = b;
                    r.setScrollMargin(2, b);
                    if (end)
                        r.scrollToY(editor.session.getScreenLength() * h + b);
                }
            };
            editor.renderer.setScrollMargin(2, 2);
            editor.renderer.on("afterRender", alignEnd);
            if (mode === "sw_log")
                DefineLogMode();
            editor.session.setMode("ace/mode/" + mode);
            t.SetText(text, scrollToEnd);
            t.Saved = editor.session.getValue();
            editor.session.on("change", () => t.UpdateDirty());
            t.TypeTitle = typeTitle;
            editor.selection.on("changeCursor", () => t.UpdateInfo());
            t.UpdateInfo();
            if (save) {
                editor.commands.addCommand({ name: "swSave", bindKey: { win: "Ctrl-S", mac: "Command-S" }, exec: () => t.Save(save) });
                //  Unsaved edits: UpdateDirty marks the name and Save; it asks before leaving only with LeaveGuard (the original never asked)
            }
            new ResizeObserver(() => editor.resize()).observe(host);

            //  Find: all matches marked (Ace's search highlight marker), Ctrl+F focuses the toolbar field, F3 / Shift+F3 next / previous
            const SearchHighlight = ace.require("ace/search_highlight").SearchHighlight;
            t.FindMarker = new SearchHighlight(null, "ace_selected-word text-find-hit", "text");
            editor.session.addDynamicMarker(t.FindMarker);
            editor.commands.bindKey({ win: "Ctrl-F", mac: "Command-F" }, { name: "swFind", exec: () => t.FocusFind() });
            editor.commands.addCommand({ name: "swFindNext", bindKey: { win: "F3", mac: "F3|Command-G" }, exec: () => t.Find(1) });
            editor.commands.addCommand({ name: "swFindPrev", bindKey: { win: "Shift-F3", mac: "Shift-F3|Command-Shift-G" }, exec: () => t.Find(-1) });
            if (isLog) {
                editor.commands.addCommand({ name: "swNextProblem", bindKey: { win: "F8", mac: "F8" }, exec: () => t.GotoProblem(1) });
                editor.commands.addCommand({ name: "swPrevProblem", bindKey: { win: "Shift-F8", mac: "Shift-F8" }, exec: () => t.GotoProblem(-1) });
            }
            //  The same keys when the focus is outside the editor (toolbar, page)
            document.addEventListener("keydown", e => {
                if (e.defaultPrevented || editor.container.contains(e.target))
                    return;
                const mod = (e.ctrlKey || e.metaKey) && !e.altKey;
                if (mod && (e.key === "f" || e.key === "F") && e.target !== findInput) {
                    e.preventDefault();
                    t.FocusFind();
                }
                else if (e.key === "F3" && e.target !== findInput) {
                    e.preventDefault();
                    t.Find(e.shiftKey ? -1 : 1);
                }
                else if (e.key === "F8" && isLog) {
                    e.preventDefault();
                    t.GotoProblem(e.shiftKey ? -1 : 1);
                }
            });
            unlock();
            t.UpdateProblems();

            //  Follow: pauses while the user looks at earlier lines (scrolled away from the end), resumes at the end
            if (followBtn) {
                editor.session.on("changeScrollTop", () => t.UpdateFollowState());
                document.addEventListener("visibilitychange", () => {
                    if (!document.hidden && t.Following)
                        t.Poll();
                });
                t.UpdateFollowState();
                if ((U.Get("follow") || "").toLowerCase() === "true") {
                    followBtn.SetPressed(true);
                    t.SetFollow(true);
                }
            }
            //  Show the editor once Ace has drawn the text at its final place (two frames after a forced full render: the end
            //  margin of alignEnd and the scroll to the end are applied by then), in place of the skeleton
            editor.resize(true);
            editor.renderer.updateFull(true);
            //  (a tab in the background gets no animation frames: the time-out shows it anyway)
            await new Promise(r => {
                requestAnimationFrame(() => requestAnimationFrame(r));
                setTimeout(r, 400);
            });
            loadingE.remove();
            host.classList.remove("text-loading");
            if (!scrollToEnd)
                editor.focus();
        },

        /**
         * Open the file in its viewer (ViewerUrl, FindViewer), like Sw.Page.View: a root relative viewer in this window, an absolute one
         * (an html page or svg image itself) in a new tab; with unsaved changes always a new tab, so the edits stay.
         */
        View() {
            const v = this.ViewerUrl;
            if (!v)
                return;
            const abs = Sw.Url.IsAbsolute(v);
            Sw.Page.Navigate(abs ? v : Sw.Url.Root + v, abs || this.IsDirty() ? "_blank" : "_self");
        },

        /**
         * The status at the right of the tool bar: read only logs show the line count ("1 138 lines"), other texts the file type and the
         * cursor position ("JavaScript · 12:4"); the tool tip has the type, the Ace mode and the cursor position.
         */
        UpdateInfo() {
            const t = this;
            const ed = t.Editor;
            if (!ed || !t.InfoE)
                return;
            const c = ed.getCursorPosition();
            const n = ed.session.getLength();
            const empty = n <= 1 && !ed.session.getLine(0);
            const lines = _T("{0} lines", empty ? 0 : n, "Status in the toolbar of the log viewer: the number of lines of the log. {0} is replaced by the number");
            //  The time rule (core/README "Times"): a log's lines stay raw (the server's wall clock, no zone in the file);
            //  the status says which clock they are: "Server time (UTC+2)", "Server time" while the server's zone is unknown
            let zone = "", zoneTip = "";
            if (t.IsLog) {
                const F = Sw.Format || {};
                const sz = typeof F.ServerZoneText === "function" ? F.ServerZoneText() : "";
                zone = sz
                    ? _T("Server time ({0})", sz, "Status in the toolbar of the log viewer: the times in the log are the server's local time.{0} is replaced with the server's UTC offset, ex: UTC+2")
                    : _TF("Server time", "Status in the toolbar of the log viewer when the server's time zone is not known: the times in the log are the server's local time");
                zoneTip = (sz
                    ? _T("The times in this file are the server's local time ({0}), as written by the server.", sz, "Tool tip of the time zone status of the log viewer.{0} is replaced with the server's UTC offset, ex: UTC+2")
                    : _TF("The times in this file are the server's local time, as written by the server (the file has no time zone).", "Tool tip of the time zone status of the log viewer when the server's zone is not known"))
                    + (typeof F.ZoneText === "function" ? "\n" + _T("Your time zone: {0}", F.ZoneText(), "Tool tip part of the time zone status of the log viewer.{0} is replaced with the viewer's zone, ex: WEST, UTC+1") : "");
                //  Local time on: the margin shows the viewer's time, the lines keep the server's
                const lb = t.Buttons && t.Buttons.LocalTime;
                if (lb && lb.Pressed && t.LocalCache && t.LocalCache.Ok && typeof F.ZoneText === "function")
                    zone = _T("Left: your time ({0}) · lines: server time ({1})", F.ZoneText(), sz, "Status in the toolbar of the log viewer with Local time on: the times in the left margin are in the viewer's zone, the lines keep the server's time.{0} is the viewer's zone, ex: WEST, UTC+1.{1} is the server's UTC offset");
            }
            t.InfoE.textContent = t.IsLog && ed.getReadOnly() ? lines + (zone ? " · " + zone : "") : t.ModeName + " · " + (c.row + 1) + ":" + (c.column + 1);
            t.InfoE.title = (zoneTip ? zoneTip + "\n\n" : "") + t.TypeTitle + " (" + t.Mode + ")\n" + lines + "\n" + _T("Line {0}, column {1}", c.row + 1, c.column + 1, "Tool tip part of the cursor position in the toolbar of the text viewer. {0} is replaced by the line number, {1} by the column");
            //  (the server's zone may become known a moment later: the serverTime reply)
            if (t.IsLog && !t.ZoneTimer && !(Sw.Format && Sw.Format.ServerZoneText && Sw.Format.ServerZoneText()))
                t.ZoneTimer = setTimeout(() => t.UpdateInfo(), 3000);
        },

        /**
         * The empty state of a read only text without content (ex: a log that was just deleted or not written yet): a state view over
         * the editor area with Reload (and Follow for logs); editable texts stay an empty editor (the user types).
         */
        UpdateEmpty() {
            const t = this;
            const ed = t.Editor;
            if (!ed || !t.Card)
                return;
            const empty = ed.getReadOnly() && !ed.session.getValue();
            if (empty && !t.EmptyE) {
                const sv = Sw.StateView({
                    Kind: "empty", Icon: t.IsLog ? "log" : "file",
                    Title: t.IsLog
                        ? _TF("The log is empty", "Title of the empty state of the log viewer when the log file has no lines")
                        : _TF("The file is empty", "Title of the empty state of the text viewer when the file has no text"),
                    Text: t.Buttons.Follow
                        ? _TF("New lines show up after Reload, or right away with Follow on.", "Text of the empty state of the log viewer when the log file has no lines")
                        : _TF("There is no text in this file.", "Text of the empty state of the text viewer when the file has no text"),
                });
                t.EmptyE = el("div.text-empty", null, sv.E);
                t.Card.append(t.EmptyE);
            }
            if (t.EmptyE)
                t.EmptyE.hidden = !empty;
            t.Card.classList.toggle("text-is-empty", empty);
        },

        /**
         * Logs: the date lines ("2026-10-19") and the "== File log started ... ==" lines are separators, a band across the text (Ace
         * full line markers, text.css .text-log-sep), so each day and each start of the server stands out.
         */
        UpdateSeparators() {
            const t = this;
            const ed = t.Editor;
            if (!ed || !t.IsLog)
                return;
            const s = ed.session;
            for (const id of t.SepMarkers || [])
                s.removeMarker(id);
            t.SepMarkers = [];
            const Range = ace.require("ace/range").Range;
            const lines = s.doc.getAllLines();
            for (let i = 0; i < lines.length; ++i)
                if (SeparatorRx.test(lines[i]))
                    t.SepMarkers.push(s.addMarker(new Range(i, 0, i, 1), /^==/.test(lines[i]) ? "text-log-sep text-log-start" : "text-log-sep", "fullLine", false));
        },

        /**
         * Focus the toolbar find field (Ctrl+F), filled with the selected text when it is a part of one line.
         */
        FocusFind() {
            const t = this;
            const inp = t.FindInput;
            if (t.Editor) {
                const r = t.Editor.getSelectionRange();
                if (!r.isEmpty() && !r.isMultiLine()) {
                    inp.value = t.Editor.session.getTextRange(r);
                    t.Find(0, true);
                }
            }
            inp.focus();
            inp.select();
        },

        /**
         * Find the text of the toolbar field: select a match and update the match count ("3 / 12") and the marks of all matches.
         * @param {number} dir - 1 = next match, -1 = previous match, 0 = the first match from the cursor on (typing).
         * @param {boolean} [noJump] - Only update the marks and the count (the selection stays).
         */
        Find(dir, noJump) {
            const t = this;
            const ed = t.Editor;
            if (!ed)
                return;
            const q = t.FindInput.value;
            const rx = FindRegExp(q);
            t.FindMarker.setRegexp(rx);
            ed.renderer.updateBackMarkers();
            t.FindInput.parentElement.classList.toggle("text-find-none", false);
            if (!rx) {
                t.FindCount.textContent = "";
                t.FindInput.style.paddingRight = "";
                t.FindFromEnd = null;
                return;
            }
            //  Typing a new search in a log viewed at its end (following, or opened at the end) starts at the newest match, not at
            //  yesterday's first line; elsewhere it starts at the cursor. Enter / F3 then step as usual.
            if (dir === 0 && !noJump && t.FindFromEnd == null)
                t.FindFromEnd = !!(t.Following || (t.IsLog && t.AtEnd()));
            if (!noJump && dir === 0 && t.FindFromEnd) {
                const s0 = ed.session;
                const lr = s0.getLength() - 1;
                ed.selection.moveTo(lr, s0.getLine(lr).length);
                ed.find(q, { caseSensitive: false, regExp: false, wholeWord: false, wrap: true, backwards: true, skipCurrent: false }, true);
            }
            else if (!noJump)
                ed.find(q, { caseSensitive: false, regExp: false, wholeWord: false, wrap: true, backwards: dir < 0, skipCurrent: dir !== 0 }, true);
            //  Count the matches and the index of the selected one (the text is the display text, the same Ace searches)
            const s = ed.session;
            const sel = ed.getSelectionRange();
            const lines = s.doc.getAllLines();
            let n = 0, cur = 0;
            for (let row = 0; row < lines.length; ++row) {
                const line = lines[row];
                rx.lastIndex = 0;
                let m;
                while ((m = rx.exec(line))) {
                    ++n;
                    if (row === sel.start.row && m.index === sel.start.column && sel.end.row === row && sel.end.column === m.index + m[0].length)
                        cur = n;
                    if (!m[0].length)
                        ++rx.lastIndex;
                }
            }
            t.FindInput.parentElement.classList.toggle("text-find-none", n === 0);
            t.FindCount.textContent = n === 0
                ? _TF("No matches", "Text in the find field of the text viewer when the text was not found")
                : (cur ? cur + " / " + n : String(n));
            t.FindCount.title = _T("{0} matches", n, "Tool tip of the match count in the find field of the text viewer. {0} is replaced by the number of matches");
            //  The typed text ends before the count (the count is inside the field, on the right)
            const c = t.FindCount;
            t.FindInput.style.paddingRight = (c.offsetWidth + (t.FindInput.offsetWidth - c.offsetLeft - c.offsetWidth) + 6) + "px";
        },

        /**
         * The problem entries of a log (cached until the text changes): see ProblemEntries.
         * @returns {Array<number[]>} [firstRow, lastRow] per Warning / Error entry.
         */
        Problems() {
            const t = this;
            if (!t.ProblemCache)
                t.ProblemCache = t.IsLog && t.Editor ? ProblemEntries(t.Editor.session.doc.getAllLines()) : [];
            return t.ProblemCache;
        },

        /** After a text change: the problem buttons (disabled without problems, count in the tool tip) and the "problems only" folds. */
        UpdateProblems() {
            const t = this;
            t.ProblemCache = null;
            t.LocalCache = null;
            t.ApplyLocalTime();
            const b = t.Buttons;
            if (!b || !b.ProblemNext)
                return;
            const n = t.Problems().length;
            const none = n === 0;
            const count = none
                ? _TF("This log has no warnings or errors.", "Tool tip part of the problem buttons in the log viewer when the log has no warning or error lines")
                : _T("This log has {0} warnings or errors.", n, "Tool tip part of the problem buttons in the log viewer. {0} is replaced by the number of warning and error entries");
            b.ProblemPrev.SetTitle(_TF("Go to the previous warning or error (Shift+F8)", "Tool tip of a button in the log viewer that jumps to the previous warning or error line") + "\n" + count);
            b.ProblemNext.SetTitle(_TF("Go to the next warning or error (F8)", "Tool tip of a button in the log viewer that jumps to the next warning or error line") + "\n" + count);
            const why = t.Gone
                ? _TF("The file was deleted", "Why a problem button in the log viewer is disabled: the log file was deleted")
                : _TF("This log has no warnings or errors", "Why a problem button in the log viewer is disabled: there are no warning or error lines to go to");
            for (const x of [b.ProblemPrev, b.ProblemNext])
                x.SetDisabled(none || t.Gone, why);
            //  "Problems only" stays usable when it is on (to turn it off)
            b.ProblemsOnly.SetDisabled((none && !b.ProblemsOnly.Pressed) || t.Gone, why);
            t.ApplyProblemsOnly();
        },

        /**
         * Go to the next / previous warning or error entry from the cursor (wraps around at the end / start).
         * @param {number} dir - 1 = next, -1 = previous.
         */
        GotoProblem(dir) {
            const t = this;
            const ed = t.Editor;
            if (!ed || t.Gone)
                return;
            const p = t.Problems();
            if (!p.length) {
                Sw.Info(_TF("This log has no warnings or errors.", "Tool tip part of the problem buttons in the log viewer when the log has no warning or error lines"));
                return;
            }
            const row = ed.getCursorPosition().row;
            let i = dir > 0 ? p.findIndex(x => x[0] > row) : p.findLastIndex(x => x[0] < row);
            if (i < 0)
                i = dir > 0 ? 0 : p.length - 1;
            const r = p[i][0];
            ed.selection.clearSelection();
            ed.gotoLine(r + 1, 0, false);
            ed.scrollToLine(r, true, false);
            const text = _T("Problem {0} of {1}", i + 1, p.length, "Status in the log viewer after jumping to a warning or error. {0} is replaced by the index of the problem, {1} by the number of problems");
            t.FindCount.title = text;
            if (Sw.Page.Announce)
                Sw.Page.Announce(text);
        },

        /** Fold every line that is not a part of a warning / error entry ("Problems only" toggle), or remove the folds. */
        ApplyProblemsOnly() {
            const t = this;
            const ed = t.Editor;
            const b = t.Buttons && t.Buttons.ProblemsOnly;
            if (!ed || !b || t.Gone)
                return;
            const s = ed.session;
            s.removeFolds(s.getAllFolds());
            if (!b.Pressed)
                return;
            const Range = ace.require("ace/range").Range;
            const last = s.getLength() - 1;
            let from = 0;
            const fold = (a, e) => {
                if (e < a)
                    return;
                const n = e - a + 1;
                s.addFold(_T("··· {0} lines ···", n, "Placeholder of folded lines in the log viewer when only warnings and errors are shown. {0} is replaced by the number of hidden lines"), new Range(a, 0, e, s.getLine(e).length));
            };
            //  The date line ("2026-10-19", a separator band) before the problems of each day stays visible, so a problem of an
            //  earlier day never shows only its time
            const lines = s.doc.getAllLines();
            const dates = [];
            for (let i = 0; i < lines.length; ++i)
                if (DateLineRx.test(lines[i]))
                    dates.push(i);
            let di = -1;
            for (const [a, e] of t.Problems()) {
                while (di + 1 < dates.length && dates[di + 1] < a)
                    ++di;
                const d = di >= 0 ? dates[di] : -1;
                if (d >= from) {
                    fold(from, d - 1);
                    from = d + 1;
                }
                fold(from, a - 1);
                from = e + 1;
            }
            fold(from, last);
        },

        /**
         * The viewer's local time of each entry header (Local time toggle): row -> text, from the time at the start of the line and
         * the date of the date line above it (Sw.Format.ServerToLocalText; the server's zone must be known). Cached until the text
         * changes. A local date that differs from the server's date shows the date too ("10-19 23:03:53").
         * @returns {{Rows: Map<number, string>, Width: number}} The texts and the longest text length.
         */
        LocalTimes() {
            const t = this;
            if (t.LocalCache)
                return t.LocalCache;
            const F = Sw.Format || {};
            const rows = new Map();
            let width = 0;
            const ok = t.Editor && typeof F.ServerToLocalText === "function" && F.ServerZoneText && F.ServerZoneText();
            if (ok) {
                const lines = t.Editor.session.doc.getAllLines();
                let date = null;
                for (let i = 0; i < lines.length; ++i) {
                    const l = lines[i];
                    if (DateLineRx.test(l)) {
                        date = l.trim();
                        continue;
                    }
                    const m = /^\s*(\d{1,2}:\d\d:\d\d)(?:[.,]\d+)?\s+[A-Za-z]+\b/.exec(l);
                    if (!m)
                        continue;
                    const time = m[1].length === 7 ? "0" + m[1] : m[1];
                    const loc = F.ServerToLocalText(date ? date + " " + time : time);
                    if (!loc)
                        continue;
                    const sameDay = !date || loc.substring(0, 10) === date || loc.length <= 8;
                    const text = loc.length <= 8 ? loc : (sameDay ? loc.substring(11) : loc.substring(5));
                    rows.set(i, text);
                    width = Math.max(width, text.length);
                }
            }
            return (t.LocalCache = { Rows: rows, Width: width, Ok: !!ok });
        },

        /** Show the local times in the left margin instead of the line numbers (Local time toggle on), or the line numbers. */
        ApplyLocalTime() {
            const t = this;
            const ed = t.Editor;
            const b = t.Buttons && t.Buttons.LocalTime;
            if (!ed || !b)
                return;
            const s = ed.session;
            const on = !!b.Pressed;
            if (on) {
                const lt = t.LocalTimes();
                s.gutterRenderer = {
                    getWidth: (session, lastLineNumber, config) => Math.max(lt.Width, 2) * config.characterWidth,
                    //  a fold ("··· 46 lines ···", Problems only) shows no time: its first row is hidden
                    getText: (session, row) => {
                        const f = session.getFoldAt(row, 0, 1);
                        return f && f.start.row === row && f.start.column === 0 && f.end.row > row ? "" : (lt.Rows.get(row) || "");
                    },
                };
                if (!lt.Ok && !t.LocalZoneTimer)
                    //  the server's zone is not known yet (the serverTime reply): try again
                    t.LocalZoneTimer = setTimeout(() => {
                        t.LocalZoneTimer = 0;
                        t.LocalCache = null;
                        t.ApplyLocalTime();
                    }, 3000);
            }
            else
                s.gutterRenderer = null;
            ed.renderer.$gutterLayer && (ed.renderer.$gutterLayer.gutterWidth = 0);
            ed.renderer.updateFull(true);
            t.UpdateInfo();
        },

        /**
         * Test if the view shows the end of the text.
         * @returns {boolean} True when scrolled to the end (within a line).
         */
        AtEnd() {
            const ed = this.Editor;
            const r = ed.renderer;
            const h = r.lineHeight || 16;
            return r.getScrollTop() + r.$size.scrollerHeight >= ed.session.getScreenLength() * h - 1.5 * h;
        },

        /** Scroll to the last line (Follow). */
        ToEnd() {
            const ed = this.Editor;
            const last = ed.session.getLength();
            ed.selection.clearSelection();
            ed.selection.moveTo(last - 1, 0);
            ed.renderer.scrollToLine(last, false, false);
            ed.renderer.scrollToY(ed.session.getScreenLength() * (ed.renderer.lineHeight || 16));
        },

        /**
         * Turn Follow on or off: on loads new lines every FollowMs (only while the page is visible) and keeps the end in view.
         * @param {boolean} on - Follow.
         */
        SetFollow(on) {
            const t = this;
            clearInterval(t.FollowTimer);
            t.FollowTimer = 0;
            t.Following = !!on && !t.Gone;
            if (t.Following) {
                t.ToEnd();
                t.FollowTimer = setInterval(() => t.Poll(), FollowMs);
                t.Poll();
            }
            t.UpdateFollowState();
        },

        /** Show the Follow state: following, paused (scrolled away from the end) or off (tool tip and icon). */
        UpdateFollowState() {
            const t = this;
            const b = t.Buttons && t.Buttons.Follow;
            if (!b || !t.Editor)
                return;
            const paused = !!t.Following && !t.AtEnd();
            if (paused !== !!t.FollowPaused || !b.Title) {
                t.FollowPaused = paused;
                b.SetIcon(paused ? "pause" : "followEnd");
                b.E.classList.toggle("text-follow-paused", paused);
                b.SetTitle(paused
                    ? _TF("Following is paused while you look at earlier lines: scroll to the end to continue (click to stop following)", "Tool tip of the Follow toggle button of the text viewer while following is paused because the user scrolled up")
                    : _TF("Follow the file: load new lines every 5 seconds and keep the end in view (scroll up to pause)", "Tool tip of the Follow toggle button of the text viewer"));
            }
        },

        /**
         * Follow: load the file and add the new lines (the whole text is replaced when the file was replaced, ex: rotated or deleted).
         * @returns {Promise<void>}
         */
        async Poll() {
            const t = this;
            if (t.Polling || document.hidden || !t.Following || t.Gone)
                return;
            t.Polling = true;
            try {
                const text = await t.FetchText();
                if (text === null || text === undefined || !t.Following || t.Gone)
                    return;
                t.PollFailed = false;
                t.Append(await t.DoBeautify(text));
            }
            catch (e) {
                //  One message per failure streak (the server may be restarting), polling goes on
                if (!t.PollFailed)
                    Sw.Fail(_TF("Failed to reload the file", "Error message displayed when a file reload failed") + ":\n" + (e.message || e));
                t.PollFailed = true;
            }
            finally {
                t.Polling = false;
            }
        },

        /**
         * Show a newer version of the text: when it only grew, the new lines are added (the view, selection and folds stay),
         * else the text is replaced. Following at the end keeps the end in view.
         * @param {string} text - The new text.
         */
        Append(text) {
            const t = this;
            const ed = t.Editor;
            const old = t.Text();
            if (text === old)
                return;
            const atEnd = t.Following && t.AtEnd();
            const s = ed.session;
            if (old && text.startsWith(old)) {
                const view = t.CompactLog ? CompactLog(text) : null;
                const lines = view ? view.Text.split("\n") : text.split(/\r\n|\r|\n/);
                //  The last old line may have been incomplete: replace from it on (the lines above are unchanged)
                const from = Math.max(0, s.getLength() - 1);
                const Range = ace.require("ace/range").Range;
                const lastRow = s.getLength() - 1;
                s.doc.replace(new Range(from, 0, lastRow, s.getLine(lastRow).length), lines.slice(from).join(s.doc.getNewLineCharacter()));
                if (view)
                    t.Raw = view;
                s.getUndoManager().reset();
                t.UpdateProblems();
                t.UpdateSeparators();
                t.UpdateEmpty();
                t.UpdateInfo();
                if (t.FindInput.value)
                    t.Find(0, true);
            }
            else
                t.SetText(text, false);
            t.Saved = s.getValue();
            t.UpdateDirty();
            if (atEnd)
                t.ToEnd();
            t.UpdateFollowState();
        },

        /**
         * Replace the text (optionally scrolling to the end). A compact log (CompactLog) shows its display text, Raw keeps the file's text.
         * @param {string} text - The text.
         * @param {boolean} toEnd - Move to the last line.
         */
        SetText(text, toEnd) {
            const ed = this.Editor;
            if (this.CompactLog) {
                this.Raw = CompactLog(text);
                ed.session.setValue(this.Raw.Text);
            }
            else
                ed.session.setValue(text);
            //  The problem buttons / "problems only" folds and the find count follow the text
            if (this.Buttons)
                this.UpdateProblems();
            this.UpdateSeparators();
            this.UpdateEmpty();
            this.UpdateInfo();
            if (this.FindInput && this.FindInput.value && this.FindMarker)
                this.Find(0, true);
            if (toEnd) {
                const last = ed.session.getLength();
                ed.resize(true);
                ed.scrollToLine(last, false, false);
                ed.gotoLine(last, 0, false);
                //  The soft wrap rows and font metrics of a long wrapped text settle during the first renders, which left the view
                //  hundreds of lines above the end: keep the end in view for a moment, unless the user scrolls or types
                const until = Date.now() + 2000;
                const host = ed.container;
                const stop = () => {
                    ed.renderer.off("afterRender", again);
                    for (const n of ["wheel", "touchstart", "mousedown", "keydown"])
                        host.removeEventListener(n, stop, true);
                };
                const again = () => {
                    if (Date.now() > until)
                        return stop();
                    if (ed.renderer.getLastVisibleRow() < last - 1)
                        ed.scrollToLine(last, false, false);
                };
                ed.renderer.on("afterRender", again);
                for (const n of ["wheel", "touchstart", "mousedown", "keydown"])
                    host.addEventListener(n, stop, true);
            }
        },

        /**
         * The file's text: the raw text of a compact log, else the editor's text.
         * @returns {string} The text.
         */
        Text() {
            return this.Raw ? this.Raw.Raw : this.Editor.session.getValue();
        },

        /**
         * Test for unsaved changes.
         * @returns {boolean} True when modified.
         */
        IsDirty() {
            return !!this.Editor && this.Saved !== null && !this.Editor.getReadOnly() && this.Editor.session.getValue() !== this.Saved;
        },

        /** Show the modified state. */
        UpdateDirty() {
            const d = this.IsDirty();
            this.NameE.classList.toggle("text-dirty", d);
            if (this.PHDesc)
                this.PHDesc.classList.toggle("text-dirty", d);
            if (this.Buttons.Save)
                this.Buttons.Save.SetKind(d ? "primary" : null);
            //  Not by default: the original text editor never asked "Leave site?" (P3-A-4); the "●" name mark and the primary Save stay
            if (this.LeaveGuard && Sw.Page && Sw.Page.SetDirty) {
                const name = (this.NameE && this.NameE.textContent || "").trim();
                Sw.Page.SetDirty("edit.text", d ? (name
                    ? _T("Your changes to {0} are lost.", name, "Text of the question 'Leave without saving?' of the text editor.{0} is replaced with the file name")
                    : _TF("Your changes to the text are lost.", "Text of the question 'Leave without saving?' of the text editor when the file has no name")) : null);
            }
        },

        /**
         * Save the text (POST {Url, Name, Content} to the save url).
         * @param {string} saveUrl - The save url.
         * @returns {Promise<void>}
         */
        async Save(saveUrl) {
            const b = this.Buttons;
            //  Not allowed for this role (a refused Save, Sw.Access.ActionDenied): also not with Ctrl+S
            if (b.Save.E.classList.contains("sw-denied-action"))
                return;
            if (b.Delete)
                b.Delete.SetDisabled(true, _TF("Wait until the file is saved", "Why the delete button of the text editor is disabled while the file is being saved"));
            b.Save.SetWorking(true);
            try {
                const m = { Url: this.Url, Name: this.Name, Content: this.Editor.session.getValue() };
                await Sw.Net.Send(saveUrl, m, { Post: true });
                this.Saved = m.Content;
                this.UpdateDirty();
                Sw.InterOp.Post("FileSaved", m);
                Sw.Info(_T("File \"{0}\" saved!", this.Name, "An information message that is displayed whan a file was saved. {0} is replaced by the name of the file that was saved"));
            }
            catch (e) {
                //  Refused (403): name the role and disable Save
                if (Sw.Access && Sw.Access.IsDenied(e))
                    Sw.Access.ActionDenied(e, b.Save);
                else
                    Sw.Fail(_TF("Failed to save the file", "Error message displayed when the file failed to be saved") + ":\n" + (e.message || e));
            }
            finally {
                b.Save.SetWorking(false);
                //  A Delete the role doesn't allow stays disabled
                if (b.Delete && !b.Delete.E.classList.contains("sw-denied-action"))
                    b.Delete.SetDisabled(false);
            }
        },

        /**
         * Delete the file (after a confirmation; POST {Url, Name} to the delete url).
         * @param {string} delUrl - The delete url.
         * @returns {Promise<void>}
         */
        async Delete(delUrl) {
            const b = this.Buttons;
            //  The server log (Api/logFile/DeleteLogFile) is the history of the server (sign ins, user changes, errors): its service
            //  hands out a one time delete token with the facts and what is lost (GetDeleteLogFileToken); the confirmation shows them
            //  and asks (a plain confirmation, like the original). The confirmation fetches the token itself (Prepare: the wait is shown at once,
            //  a refused or failed request stops with the reason; never a weaker question). Other files: the plain confirmation below.
            const tokenUrl = /\/logFile\/DeleteLogFile$/i.test(delUrl.split("?")[0]) ? delUrl.split("?")[0].replace(/DeleteLogFile$/i, "GetDeleteLogFileToken") : null;
            const prepare = tokenUrl ? async signal => {
                const info = await Sw.Net.Send(tokenUrl, null, { Post: true, Signal: signal });
                if (!info || !info.Token)
                    throw new Error(_TF("the service gave no delete token", "Reason when the log file delete could not be prepared: the service answered without a one time delete token"));
                const facts = [];
                if (info.Size >= 0 && Sw.Format.Bytes)
                    facts.push({ Label: _TF("Size", "Label of the file size in the confirmation that deletes the log file"), Value: Sw.Format.Bytes(info.Size) });
                if (this.Editor && !this.Gone)
                    facts.push({ Label: _TF("Lines", "Label of the number of lines in the confirmation that deletes the log file"), Value: String(this.Editor.session.getLength()) });
                if (info.LastWrite)
                    facts.push({ Label: _TF("Last written", "Label of the time of the last write in the confirmation that deletes the log file"), Value: new Date(info.LastWrite).toLocaleString() });
                return { Token: info.Token, Item: info.Name || this.Name, Facts: facts, Text: info.Warning || undefined };
            } : null;
            //  The shared confirmation of destructive actions (same look and wording as Kill in the tables)
            const ok = await Sw.Cells.ConfirmDanger(prepare ? {
                Title: _TF("Delete the log file?", "Title of the dialog that confirms deleting the server's log file"),
                ItemLabel: _TF("File:", "Label before the file name in a confirmation dialog of the text editor (ex: File: LogFile.txt)"),
                Item: this.Name,
                Prepare: prepare,
                Button: b.Delete,
                Text: _TF("The log is the history of what happened on this server (sign ins, user changes, errors); it can't be restored.", "Text of the dialog that confirms deleting the server's log file"),
                //  (a plain confirmation, like the original viewer: no word to type)
                OkText: _TF("Delete", "Text of a button that when clicked will delete a file"),
                OkIcon: "trash",
                OkTitle: _TF("Delete the log file permanently", "Tool tip of the button that deletes the server's log file"),
                CancelTitle: _TF("Keep the file as is", "Tool tip description of a button that when clicked will leave a file as is, as opposed to deleting it"),
            } : {
                Title: _TF("Delete the file?", "Title of a pop-up dialog that confirms that the user wants to delete a file"),
                ItemLabel: _TF("File:", "Label before the file name in a confirmation dialog of the text editor (ex: File: LogFile.txt)"),
                Item: this.Name,
                OkText: _TF("Delete", "Text of a button that when clicked will delete a file"),
                OkIcon: "trash",
                OkTitle: _TF("Delete the file permanently", "Tool tip description of a button that when clicked will delete a file"),
                CancelTitle: _TF("Keep the file as is", "Tool tip description of a button that when clicked will leave a file as is, as opposed to deleting it"),
            });
            if (!ok)
                return;
            if (b.Save)
                b.Save.SetDisabled(true, _TF("Wait until the file is deleted", "Why the save button of the text editor is disabled while the file is being deleted"));
            b.Delete.SetWorking(true);
            try {
                const m = { Url: this.Url, Name: this.Name };
                if (ok && ok.Token)
                    m.Token = ok.Token;
                await Sw.Net.Send(delUrl, m, { Post: true });
                Sw.InterOp.Post("FileDeleted", { Url: m.Url, Name: m.Name });
                this.SetFollow(false);
                this.Gone = true;
                for (const k in b)
                    if (b[k] && k !== "Reload")
                        b[k].SetDisabled(true, _TF("The file was deleted: reload the page to see a new file once it is written", "Why the buttons of the text editor are disabled after the file was deleted"));
                this.Saved = null;
                this.UpdateDirty();
                this.Editor.destroy();
                const goneWhy = _TF("The file was deleted: reload the page to see a new file once it is written", "Why the buttons of the text editor are disabled after the file was deleted");
                if (Sw.Disabled)
                    Sw.Disabled.Set(this.FindInput, true, goneWhy);
                else
                    this.FindInput.disabled = true;
                const host = document.querySelector(".text-editor");
                //  The empty state in the card (the tool bar stays, disabled; Reload shows a new file once the server writes one)
                host.replaceChildren(Sw.StateView({
                    Kind: "empty", Icon: "trash",
                    Title: _T("File \"{0}\" deleted!", this.Name, "An information message that is displayed whan a file was deleted. {0} is replaced by the name of the file that was deleted"),
                    Text: _TF("Reload the page to see a new file once it is written.", "Text of the state of the text viewer after the file was deleted"),
                }).E);
                host.classList.remove("ace-sysweaver", "ace_editor");
                host.classList.add("text-gone");
                if (this.EmptyE)
                    this.EmptyE.hidden = true;
                this.Card.classList.remove("text-is-empty");
                //  The result is shown once, in place (the state in the card), and announced: no toast besides it (FBK-5); the focus
                //  goes to Reload, the one action left (FOC-1)
                Sw.Page.Announce(_T("File \"{0}\" deleted!", this.Name, "An information message that is displayed whan a file was deleted. {0} is replaced by the name of the file that was deleted"));
                if (b.Reload && b.Reload.E.isConnected)
                    b.Reload.E.focus();
            }
            catch (e) {
                //  Refused (403): name the role and keep Delete disabled
                if (Sw.Access && Sw.Access.IsDenied(e))
                    Sw.Access.ActionDenied(e, b.Delete);
                else
                    Sw.Fail(_TF("Failed to delete the file", "Error message displayed when the file failed to be deleted") + ":\n" + (e.message || e));
                if (b.Save && !b.Save.E.classList.contains("sw-denied-action"))
                    b.Save.SetDisabled(false);
            }
            finally {
                b.Delete.SetWorking(false);
            }
        },
    };

    Sw.TextPage = TextPage;
    Sw.TextPage.SanitizeHtml = SanitizeHtml;
    //  The original text.js defined the globals SanitizeHtml and textMain: both are kept for callers of the old page
    if (!window.SanitizeHtml)
        window.SanitizeHtml = SanitizeHtml;
    if (!window.textMain)
        window.textMain = () => TextPage.Main();
})();