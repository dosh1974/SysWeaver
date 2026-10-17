

/**
 * @file chat.js - the web chat client of SysWeaver.Chat (served under `chat/`).
 *
 * Loading: classic (non-module) script, loaded with `<script defer src="chat.js">` by `Chat.html`
 * (whose `<body class="ChatMain" onload="chatMain()">` is empty and built entirely from script).
 * Any other page may include it and call `Chat.addChat` directly (that is what `apiBase` is for).
 *
 * Globals defined:
 * - `SpeechGen`   (class, static only) - text-to-speech on `window.speechSynthesis`, incl. incremental
 *                 speech of a message that is still streaming.
 * - `SpeechInput` (class, static only) - keyword / push-to-talk speech recognition on
 *                 `SpeechRecognition`/`webkitSpeechRecognition` + `SpeechGrammarList`.
 * - `ChatOptions` (class) - options bag for `Chat.addChat`.
 * - `Chat`        (class, static only) - `Chat.addChat` builds the complete chat UI (one ~2500 line closure).
 * - `chatMain`    (async function) - page entry point of `Chat.html`.
 * - `window.ChatColors`, `window.SpeakCurrentText`, `window.SpeakCurrentQueue`, `window.SpeakCurrentInit`
 *   (window globals created lazily at runtime, see `Chat.chatColor` and `SpeechGen`).
 *
 * Dependencies (must be loaded first):
 * - common.js: `sendRequest`, `getUrlParams`, `AddLoading`, `Fail`, `Confirm`, `_T`/`_TF` (i18n), `ColorIcon`,
 *   `includeJs`, `SessionManager`, `InterOp`, `StickToBottom`, `IsAtMaxScrollV`, `ValueFormat`, `legacySha256`,
 *   `isLetter`, `isMobile`, `getPlatform`, `insertAtCursor`, `imageExtensions`, `createImg`, `createIFrame`,
 *   `appendTheme`, `SetImageSource`, `GetAbsolutePath`, `Open`, `downloadFile`, `badClick`, `keyboardClick`
 *   and the `UserInteracted` document event.
 * - application.js: `PopUpMenu`, `PopUpSelection`, `WebMenu`, `WebMenuItem`.
 * - Optional, loaded on demand via `includeJs` when the chat has an `UploadRepo` (FileUploader project):
 *   `md5.js`, `UZIP.js`, `fileUploader.js` (`fileUploader`, `fileUploaderStatusText`, `UploadStatus`, `selectFiles`).
 *
 * Server: all API calls go to `ChatService` (`/chat/<Method>`, page relative) through `sendRequest`;
 * live updates arrive as session push messages `chat.post|replace|remove|clear.<chatid lowercased>`.
 *
 * Conventions: message `Format` (0 text, 1 markdown, 2 HTML) and `Flags` mirror C# enums; `Data` is a
 * `;`-separated list of `../`-prefixed URLs; DOM rows double as the state store (expando members);
 * static field initializers run at load time and call `isMobile()` / `getPlatform()` from common.js.
 */

/**
 * Static-only text-to-speech helper around `window.speechSynthesis`.
 * Supports one-shot speech (`speakOnce`) and incremental speech of a streamed message
 * (`speakNew` followed by `speakContinue`). All state lives in the `window.SpeakCurrent*` globals,
 * so there is only one TTS stream per page. Never instantiated.
 */
class SpeechGen
{

    /**
     * Checks if speech synthesis is available.
     * @returns {boolean} true if `window.speechSynthesis` exists.
     */
    static supportSpeechGen() {
        const ss = window.speechSynthesis;
        if (!ss)
            return false;
        return true;
    }

    /**
     * Substrings of voice names that are replaced by "male" before gender scoring in `findVoice`.
     * @type {string[]}
     */
    static MaleNames = [
        "George",
        "Bengt",
    ];


    /**
     * Substrings of voice names that are replaced by "female" before gender scoring in `findVoice`.
     * @type {string[]}
     */
    static FemaleNames = [
        "Hazel",
        "Susan",
    ];


    /**
     * Case-insensitive containment similarity of two strings.
     * @param {string} s1 - First string.
     * @param {string} s2 - Second string.
     * @returns {number} If the shorter string occurs in the longer one at index f: 1 / (1 + 0.02 * f), else 0.
     */
    static similarity(s1, s2) {
        let longer = s1;
        let shorter = s2;
        if (s1.length < s2.length) {
            longer = s2;
            shorter = s1;
        }
        shorter = shorter.toLowerCase();
        longer = longer.toLowerCase();
        const f = longer.indexOf(shorter);
        if (f < 0)
            return 0;
        return 1.0 / (1.0 + 0.02 * f);
    }


    /**
     * Finds the best matching speech synthesis voice.
     * Each voice from `speechSynthesis.getVoices()` is scored as
     * `similarity(language, voice.lang) * 10 + similarity("male"|"female", fixedName) + (localService ? 0.8 : 0)`,
     * where fixedName is the voice name with `MaleNames`/`FemaleNames` replaced by "male"/"female".
     * A local voice whose name equals `exact` gets score 10000 (wins outright).
     * @param {string} language - Language code to match (e.g. "en-US").
     * @param {boolean} male - True to prefer a male voice, false for a female voice.
     * @param {string} [exact] - Exact voice name to prefer (only if it is a local voice).
     * @returns {SpeechSynthesisVoice|null} The highest scoring voice, or null if none scored above 0.
     */
    static findVoice(language, male, exact) {
        const g = male ? "male" : "female";
        const ss = window.speechSynthesis;
        const voices = ss.getVoices();
        let bestScore = 0;
        let bestVoice = null;
        for (const voice of voices) {

            let nameFix = voice.name;
            for (const fix of SpeechGen.MaleNames)
                nameFix = nameFix.replace(fix, "male");
            for (const fix of SpeechGen.FemaleNames)
                nameFix = nameFix.replace(fix, "female");

            let score = SpeechGen.similarity(language, voice.lang) * 10 + SpeechGen.similarity(g, nameFix) + (voice.localService ? 0.8 : 0);
            if (exact) {
                if (voice.name == exact)
                    if (voice.localService)
                        score = 10000;
            }
            //console.log("Voice: " + voice.lang + " - " + voice.name + " @ " + score);
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestVoice = voice;
        }
        //console.log("Best voice: " + bestVoice.lang + " - " + bestVoice.name + " @ " + bestScore);
        return bestVoice;

    }



    /**
     * Phrase end characters (`! ? . ,`). While a message is incomplete, `speakContinue` only speaks up to the last of these.
     * @type {Map<string, boolean>}
     */
    static EndChars = function () {
        const m = new Map();
        m.set('!', true);
        m.set('?', true);
        m.set('.', true);
        m.set(',', true);
        return m;
    }();


    /**
     * Punctuation kept by `filterText` (`! ? . ,`).
     * @type {Map<string, boolean>}
     */
    static ValidChars = function () {
        const m = new Map();
        m.set('!', true);
        m.set('?', true);
        m.set('.', true);
        m.set(',', true);
        return m;
    }();

    /**
     * White space characters recognized by `filterText` (space, tab, \n, \r).
     * @type {Map<string, boolean>}
     */
    static WhiteSpaces = function () {
        const m = new Map();
        m.set(' ', true);
        m.set('\t', true);
        m.set('\n', true);
        m.set('\r', true);
        return m;
    }();


    /**
     * Math operator characters and their spoken form (`*` " times ", `+` " plus ", `-` " minus ", `/` " divided by ",
     * `%` " modulo ", `^` " to the power of ", `=` " equals ", `≈` " is approximately "), used by `filterText`.
     * @type {Map<string, string>}
     */
    static MathOps = function () {
        const m = new Map();
        m.set('*', " times ");
        m.set('+', " plus ");
        m.set('-', " minus ");
        m.set('/', " divided by ");
        m.set('%', " modulo ");
        m.set('^', " to the power of ");
        m.set('=', " equals ");
        m.set('≈', " is approximately ");
        return m;
    }();



    /**
     * Converts text into speakable text.
     * - Collapses white space runs to one space; white space between two digits is removed (joins digit groups).
     * - Keeps digits and `ValidChars`; a comma directly after a digit is dropped (thousands separators).
     * - A `MathOps` operator followed by a number is replaced by its words if preceded by a number (or always for `-`).
     * - `(` becomes ", ".
     * - Keeps letters (`isLetter` from common.js), drops everything else.
     * @param {string} text - Raw text (plain or markdown).
     * @returns {string} The filtered text ("" for empty input).
     */
    static filterText(text) {
        const tl = text.length;
        if (tl <= 0)
            return "";
        let wasWhite = true;
        let prevIsNumber = false;
        let out = "";
        const white = SpeechGen.WhiteSpaces;
        const mathOps = SpeechGen.MathOps;

        /**
         * Checks if the next non white space character after `pos` in `text` is a digit.
         * @param {number} pos - Index to scan forward from (exclusive).
         * @returns {boolean} true if a digit follows (skipping white space).
         */
        function NextIsNumber(pos) {
            for (; ;) {
                ++pos;
                if (pos >= tl)
                    return false;
                const c = text.charAt(pos);
                if ((c >= '0') && (c <= '9'))
                    return true;
                if (!white.get(c))
                    return false;
            }
            return false;
        }

        /**
         * Checks if the previous non white space character before `pos` in `text` is a digit.
         * @param {number} pos - Index to scan backward from (exclusive).
         * @returns {boolean} true if a digit precedes (skipping white space).
         */
        function PrevIsNumber(pos) {
            for (; ;) {
                --pos;
                if (pos < 0)
                    return false;
                const c = text.charAt(pos);
                if ((c >= '0') && (c <= '9'))
                    return true;
                if (!white.get(c))
                    return false;
            }
            return false;
        }

        const valid = SpeechGen.ValidChars;
        for (let i = 0; i < tl; ++i) {
            const c = text.charAt(i);
            if (white.get(c)) {
                if (PrevIsNumber(i))
                    if (NextIsNumber(i))
                        continue;
                prevIsNumber = false;
                if (wasWhite)
                    continue;
                wasWhite = true;
                out += ' ';
                continue;
            }
            if ((c >= '0') && (c <= '9')) {
                wasWhite = false;
                prevIsNumber = true;
                out += c;
                continue;
            }
            if (valid.get(c)) {
                if (prevIsNumber)
                    if (c === ',')
                        continue;
                wasWhite = false;
                prevIsNumber = false;
                out += c;
                continue;
            }
            const mathOp = mathOps.get(c);
            if (mathOp) {
                if (NextIsNumber(i)) {
                    if ((c === '-') || PrevIsNumber(i)) {
                        wasWhite = false;
                        prevIsNumber = false;
                        out += mathOp;
                        continue;
                    }
                }
            }
            if (c === '('){
                wasWhite = false;
                prevIsNumber = false;
                out += ", ";
                continue;
            }
            if (!isLetter(c))
                continue;
            wasWhite = false;
            prevIsNumber = false;
            out += c;
        }
        return out;
    }



    /**
     * `onend` handler of every utterance: if `window.SpeakCurrentQueue` holds non-empty (trimmed) text,
     * clears the queue and speaks the text as a new utterance initialized by `window.SpeakCurrentInit`.
     */
    static onSpeakEnd() {
        //console.log("SpeachSyntesis: onSpeakEnd");
        const queue = window["SpeakCurrentQueue"];
        if (!queue)
            return;
        if (queue.length <= 0)
            return;
        const text = queue.trim();
        window["SpeakCurrentQueue"] = "";
        if (text.length <= 0)
            return;
        const u = new SpeechSynthesisUtterance(text);
        window["SpeakCurrentInit"](u);
        const ss = window.speechSynthesis;
        //console.log("SpeachSyntesis: Speaking (from queue): " + text);
        ss.speak(u);
    }

    /**
     * Clears `window.SpeakCurrentQueue` and cancels any ongoing speech (`speechSynthesis.cancel()` if speaking).
     */
    static cancelSpeak() {
        const ss = window.speechSynthesis;
        window["SpeakCurrentQueue"] = "";
        if (!ss.speaking)
            return;
        //console.log("SpeachSyntesis: Cancelling speach!");
        ss.cancel();
    }


    /**
     * Speaks a complete text once (not incremental). Cancels any current speech first.
     * Does nothing if the filtered text is empty. Not called inside this file.
     * @param {string} text - Text to speak (filtered with `filterText`).
     * @param {SpeechSynthesisVoice} [voice] - Voice to use (browser default if falsy).
     * @param {number} pitch - Utterance pitch (assigned as is).
     * @param {number} [rate] - Utterance rate (browser default if falsy).
     * @param {number} [volume=0.5] - Volume 0-1 (0.5 if falsy).
     * @param {string} [lang="en"] - Utterance language.
     */
    static speakOnce(text, voice, pitch, rate, volume, lang) {
        text = SpeechGen.filterText(text);
        if (text.length <= 0)
            return;
        const ss = window.speechSynthesis;
        const u = new SpeechSynthesisUtterance(text);
        u.lang = lang ? lang : "en";
        if (voice)
            u.voice = voice;
        u.pitch = pitch;
        if (rate)
            u.rate = rate;
        u.volume = volume ? volume : 0.5;
        u.onend = SpeechGen.onSpeakEnd;
        SpeechGen.cancelSpeak();
        //console.log("SpeachSyntesis: Speaking: " + newText);
        ss.speak(u);
    }

    /**
     * Starts a new incremental speech stream (used for newly posted messages).
     * Resets `window.SpeakCurrentText` to "", stores an utterance initializer with the given settings in
     * `window.SpeakCurrentInit` (lang, voice, pitch, rate, volume, onend = `onSpeakEnd`), then calls `speakContinue(text)`.
     * @param {string} text - The (possibly incomplete) message text.
     * @param {SpeechSynthesisVoice} [voice] - Voice to use (browser default if falsy).
     * @param {number} pitch - Utterance pitch.
     * @param {number} [rate] - Utterance rate (browser default if falsy).
     * @param {number} [volume=0.5] - Volume 0-1 (0.5 if falsy).
     * @param {string} [lang="en"] - Utterance language.
     */
    static speakNew(text, voice, pitch, rate, volume, lang) {
        text = SpeechGen.filterText(text);
        window["SpeakCurrentText"] = "";
        window["SpeakCurrentInit"] = u => {
            u.lang = lang ? lang : "en";
            if (voice)
                u.voice = voice;
            u.pitch = pitch;
            if (rate)
                u.rate = rate;
            u.volume = volume ? volume : 0.5;
            u.onend = SpeechGen.onSpeakEnd;
        };
        SpeechGen.speakContinue(text);
    }

    /**
     * Continues speaking a streamed message whose text has grown (used for `replace` push events).
     * The text is filtered; if it no longer starts with the already spoken prefix (`window.SpeakCurrentText`),
     * the prefix is reset and speech is cancelled. Unless `isCompleted`, returns while already speaking, and only
     * takes the new suffix up to (and including) the last `EndChars` character. The taken part is appended to
     * `SpeakCurrentText` and then either appended to `window.SpeakCurrentQueue` (if speaking, flushed by `onSpeakEnd`)
     * or spoken immediately with `window.SpeakCurrentInit` (cancelling first if it is the first chunk).
     * Requires a prior `speakNew` (uses `window.SpeakCurrentInit`).
     * @param {string} text - The full current message text.
     * @param {boolean} [isCompleted] - True when the message is complete: speak all remaining text.
     */
    static speakContinue(text, isCompleted) {
        //console.log("Continue " + text);
        text = SpeechGen.filterText(text);
        let prev = window["SpeakCurrentText"];
        if (!text.startsWith(prev)) {
            prev = "";
            window["SpeakCurrentText"] = prev;
            SpeechGen.cancelSpeak();
        }
        const ss = window.speechSynthesis;
        if (!isCompleted)
            if (ss.speaking)
                return;
        const next = text.substring(prev.length);
        let goodUntil = next.length;
        if (!isCompleted) {
            const end = SpeechGen.EndChars;
            while (goodUntil > 0) {
                --goodUntil;
                if (end.get(next.charAt(goodUntil))) {
                    ++goodUntil;
                    break;
                }
            }
        }
        if (goodUntil <= 0)
            return;
        let newText = next.substring(0, goodUntil);
        window["SpeakCurrentText"] = prev + newText;
        newText = newText.trim();
        if (newText.length <= 0)
            return;
        if (prev.length <= 0)
            SpeechGen.cancelSpeak();
        if (ss.speaking) {
            let queue = window["SpeakCurrentQueue"];
            if (!queue)
                queue = "";
            //console.log("SpeachSyntesis: Queuing: " + newText);
            window["SpeakCurrentQueue"] = queue + newText;
        } else {
            const u = new SpeechSynthesisUtterance(newText);
            window["SpeakCurrentInit"](u);
            //console.log("SpeachSyntesis: Speaking: " + newText);
            ss.speak(u);

        }
    }


}

/**
 * Static-only speech recognition helper around `SpeechRecognition` / `webkitSpeechRecognition` and
 * `SpeechGrammarList`. `triggerOnSpeech` creates a recognizer for wake words (open mic) or push-to-talk
 * and returns its stop function. Never instantiated.
 */
class SpeechInput {
    /**
     * `isMobile()` (common.js) evaluated at load; open mic and joining of final results are disabled on mobile.
     * @type {boolean}
     */
    static IsMobile = isMobile();

    /**
     * Checks if continuous (open mic, keyword triggered) listening is supported.
     * @returns {boolean} false on mobile devices.
     */
    static supportOpenMic() {
        return !SpeechInput.IsMobile;
    }

    /**
     * Checks if speech recognition is supported.
     * @returns {boolean} true if both a `SpeechRecognition` and a `SpeechGrammarList` constructor (standard or webkit prefixed) exist.
     */
    static supportSpeechRec() {
        const sr = window.SpeechRecognition ?? window.webkitSpeechRecognition;
        const sgl = window.SpeechGrammarList ?? window.webkitSpeechGrammarList;
        if (!sr)
            return false;
        if (!sgl)
            return false;
        return true;
    }


    /**
     * Stop words (abort, stop, cancel, reset, restart): a final result whose last word is one of these aborts the current input.
     * Note: compared case sensitively against `GetLastWord` (which preserves case).
     * @type {Map<string, number>}
     */
    static StopWords = function () {
        const m = new Map();
        m.set('abort', 1);
        m.set('stop', 1);
        m.set('cancel', 1);
        m.set('reset', 1);
        m.set('restart', 1);
        return m;
    }();

    /**
     * Characters that separate words for `GetLastWord`: white space and `! ? . , ¡ ¿`.
     * @type {Map<string, number>}
     */
    static NonWordChars = function () {
        const m = new Map();
        m.set(' ', 1);
        m.set('\t', 1);
        m.set('\n', 1);
        m.set('\r', 1);
        m.set('!', 1);
        m.set('?', 1);
        m.set('.', 1);
        m.set(',', 1);
        m.set('¡', 1);
        m.set('¿', 1);
        return m;
    }();


    /**
     * Question words (who/what/where/when/why/which/whose/how and their "'s" forms, lower case):
     * any occurrence makes a sentence a question (ends with "?").
     * @type {Map<string, number>}
     */
    static QuestionWordMap = function () {
        const m = new Map();
        m.set("who", 1);
        m.set("who's", 1);
        m.set("what", 1);
        m.set("what's", 1);
        m.set("where", 1);
        m.set("where's", 1);
        m.set("when", 1);
        m.set("when's", 1);
        m.set("why", 1);
        m.set("why's", 1);
        m.set("which", 1);
        m.set("which's", 1);
        m.set("whose", 1);
        m.set("whose's", 1);
        m.set("how", 1);
        m.set("how's", 1);
        return m;
    }();

    /**
     * Words (are/aren't/am/is/isn't/do/don't/does/doesn't) that make a sentence a question only when they are its first word.
     * @type {Map<string, number>}
     */
    static QuestionWordStartMap = function () {
        const m = new Map();
        m.set("are", 1);
        m.set("aren't", 1);
        m.set("am", 1);
        m.set("is", 1);
        m.set("isn't", 1);
        m.set("do", 1);
        m.set("don't", 1);
        m.set("does", 1);
        m.set("doesn't", 1);
        return m;
    }();

    /**
     * Gets the last word of a text (trailing `NonWordChars` are skipped).
     * @param {string} text - Text to inspect.
     * @returns {string} The last run of non `NonWordChars` characters (case preserved), or "".
     * Note: because of the `l > 0` loop bound, returns "" when the last word is a single character at index 0.
     */
    static GetLastWord(text) {

        let l = text.length;
        if (l <= 0)
            return "";
        const nc = SpeechInput.NonWordChars;
        while (l > 0) {
            --l;
            const c = text.charAt(l);
            if (!nc.get(c))
                break;
        }
        if (l <= 0)
            return "";
        const e = l + 1;
        while (l > 0) {
            --l;
            const c = text.charAt(l);
            if (nc.get(c)) {
                ++l;
                break;
            }
        }
        return text.substring(l, e);
    }

    /**
     * Creates and starts a speech recognizer that activates on wake words (open mic) or immediately (push-to-talk).
     * - Builds a JSGF grammar `public <keyword> = k1,k2...`, sets `continuous`, `interimResults`, `maxAlternatives = 1`.
     * - If `start()` throws (no user gesture yet), retries on the common.js `UserInteracted` document event.
     * - With `noOpenMic`, calls `onActivate(true, true)` right after starting.
     * - `onend` restarts (open mic) or stops (`noOpenMic`).
     * - `onerror`: "no-speech" ignored; "aborted" stops (`noOpenMic`) or calls `onActivate(null, false)`;
     *   other errors are logged with `console.warn` and stop.
     * - `onresult`: detects the keyword once per sentence (`onActivate(key, true)`), streams `filter(part)` as interim
     *   text via `onSentance(text, false)`. On a final result: if the last word is a `StopWords` entry the input is
     *   aborted (`onSentance("", true)`), else a 1500 ms debounce timer delivers `onSentance(filter(final), true)`;
     *   new results reset the timer. On non mobile, consecutive results of one activation are joined with ". ".
     * @param {string|string[]} keyword - Wake word entries `"wakeword|prefix"`; the wake word is lowercased, the
     *   optional prefix is prepended to every produced sentence.
     * @param {function(?(string|boolean), boolean):(void|Promise<void>)} [onActivate] - Called with the detected keyword
     *   on activation (`true` for push-to-talk start), `(null, true)` after a sentence completes in open mic mode,
     *   `(null, false)` when stopped.
     * @param {function(string, boolean):(void|Promise<void>)} [onSentance] - Receives filtered text; second arg false
     *   for interim text, true for the final sentence.
     * @param {string} [language="en-US"] - Recognition language.
     * @param {boolean} [ignoreKeyword] - If true any speech activates (the first keyword is used for prefix/filtering).
     * @param {boolean} [noOpenMic] - If true, stop after one sentence instead of restarting (push-to-talk).
     * @returns {?function():Promise<void>} The async stop function, or null if speech recognition is unsupported.
     */
    static triggerOnSpeech(keyword, onActivate, onSentance, language, ignoreKeyword, noOpenMic) {

        const keywordMap = new Map();
        if (keyword) {
            if (Array.isArray(keyword)) {
                keyword.forEach(x => {
                    const kv = x.split('|');
                    keywordMap.set(kv[0].toLowerCase(), kv.length > 1 ? kv[1] : "");

                });
            } else {
                const kv = keyword.split('|');
                keywordMap.set(kv[0].toLowerCase(), kv.length > 1 ? kv[1] : "");
            }
        }
        const keywordArray = Array.from(keywordMap.keys());
        const keywords = keywordArray.join(",");
        const keywordCount = keywordArray.length;

        /**
         * Finds the first keyword contained in a transcript.
         * @param {string} str - Transcript (compared lowercased).
         * @returns {?string} The first matching keyword; with `ignoreKeyword` the first keyword when none matches; else null.
         */
        function haveKeyword(str) {
            str = str.toLowerCase();
            for (let i = 0; i < keywordCount; ++i) {
                const x = keywordArray[i];
                if (str.indexOf(x) >= 0)
                    return x;
            }
            if (ignoreKeyword)
                return keywordArray[0];
            return null;
        }


        onActivate = onActivate ?? (x => { });
        onSentance = onSentance ?? (x => { });
        const sr = window.SpeechRecognition ?? window.webkitSpeechRecognition;
        const sgl = window.SpeechGrammarList ?? window.webkitSpeechGrammarList;
        if (!sr)
            return null;
        if (!sgl)
            return null;
        let recognition = new sr();
        const speechRecognitionList = new sgl();
        speechRecognitionList.addFromString("#JSGF V1.0; grammar keywords; public <keyword> = " + keywords, 1);
        recognition.grammars = speechRecognitionList;
        recognition.continuous = true;
        recognition.lang = language ?? "en-US";
        recognition.interimResults = true;
        recognition.maxAlternatives = 1;
        /**
         * Starts the recognizer and removes itself as a `UserInteracted` listener. Also used as `onend` handler in open mic mode (restart).
         * @throws If the browser refuses to start (e.g. no user gesture yet).
         */
        const start = () => {
            recognition.start();
            //console.log("Speech recognition: Started");
            document.removeEventListener("UserInteracted", start);
        };

        /**
         * Stops recognition for good: detaches handlers, stops the recognizer, nulls it and calls `onActivate(null, false)`.
         * No-op if already stopped. This is the function returned by `triggerOnSpeech`.
         * @returns {Promise<void>}
         */
        const stop = async () => {
            if (!recognition)
                return;
            //console.log("Speech recognition: Destroyed");
            recognition.onerror = null;
            recognition.onend = null;
            recognition.onresult = null;
            recognition.stop();
            recognition = null;
            await onActivate(null, false);
        };


        /** Accumulated final text of the current activation (joined with ". " on non mobile). @type {string} */
        let add = "";
        /** Pending debounce timer (setTimeout handle) that delivers the final sentence, or null. */
        let del = null;
        /** The keyword of the current activation, or null when not activated. @type {?string} */
        let isKey = null;

        /**
         * Tests for white space.
         * @param {string} s - Text (usually one character).
         * @returns {boolean} true if `s` contains white space.
         */
        function hasWhiteSpace(s) {
            return RegExp(/\s/g).test(s);
        }

        /**
         * Tests if `s` starts with a Unicode letter. Not used.
         * @param {string} s - Text.
         * @returns {boolean} true if the first character is a letter.
         */
        function isChar(s) {
            return RegExp(/^\p{L}/, 'u').test(s);
        }


        /**
         * Checks if a word makes its sentence a question.
         * @param {string} word - The word (compared lowercased).
         * @param {boolean} isFirstWord - True if it is the first word of the sentence (enables `QuestionWordStartMap`).
         * @returns {boolean} true for a question word.
         */
        function isQuestionWord(word, isFirstWord) {
            word = word.toLowerCase();
            if (isFirstWord && (SpeechInput.QuestionWordStartMap.get(word) === 1))
                return true;
            return SpeechInput.QuestionWordMap.get(word) === 1;
        }

        /**
         * Turns a raw transcript into a sentence: removes text up to and including the keyword, trims, capitalizes the
         * first letter of each sentence, replaces `.` by `?` for question sentences, strips trailing space/`.`/`!`/`?`,
         * appends "? " or ". " and prepends the keyword's prefix (from `keywordMap`).
         * @param {string} text - Transcript.
         * @param {string} keyw - The active keyword.
         * @returns {string} The filtered sentence, or "" for empty text.
         */
        function filter(text, keyw) {
            const ff = text.toLowerCase().indexOf(keyw);
            if (ff >= 0)
                text = text.substring(ff + keyw.length);
            text = text.trim();
            if (text.length <= 0)
                return "";
            let out = "";
            let makeNextUpper = true;
            let isQuestion = false;
            let removeWhiteSpace = true;
            let isFirstWord = true;
            const tl = text.length;
            let wordStart = -1;
            for (let i = 0; i < tl; ++i) {
                let ch = text.charAt(i);
                const isWhiteSpace = hasWhiteSpace(ch) || (ch === '.');
                if (removeWhiteSpace) {
                    if (isWhiteSpace)
                        continue;
                    removeWhiteSpace = false;
                }
                if (makeNextUpper) {
                    makeNextUpper = false;
                    ch = ch.toUpperCase();
                }
                if (wordStart >= 0) {
                    if (isWhiteSpace) {
                        isQuestion |= isQuestionWord(text.substring(wordStart, i), isFirstWord);
                        wordStart = -1;
                        isFirstWord = false;
                    }
                } else {
                    if (isLetter(ch))
                        wordStart = i;
                }
                if (ch === '.') {
                    if (isQuestion) {
                        ch = '?';
                        isQuestion = false;
                    }
                    makeNextUpper = true;
                    isFirstWord = true;
                }
                out += ch;
            }
            if (wordStart >= 0)
                isQuestion |= isQuestionWord(text.substring(wordStart, tl), isFirstWord);
            out = out.trim();
            const ol = out.length;
            if (ol <= 0)
                return "";
            for (; ;) {
                const last = out.charAt(ol - 1);
                switch (last) {
                    case ' ':
                    case '.':
                    case '!':
                    case '?':
                        out = out.substring(0, ol - 1);
                        continue;
                }
                break;
            }
            out += (isQuestion ? "? " : ". ");
            return keywordMap.get(keyw) + out;
        }



        /**
         * Recognition error handler: ignores "no-speech"; "aborted" stops (push-to-talk) or deactivates (open mic);
         * other errors are logged and stop recognition.
         * @param {SpeechRecognitionErrorEvent} ev - The error event.
         * @returns {Promise<void>}
         */
        recognition.onerror = async ev => {
            if (ev.error === "no-speech")
                return;
            if (ev.error === "aborted") {
                if (noOpenMic)
                    await stop();
                else
                    await onActivate(null, false);
                return;
            }
            console.warn("Speech recognition: Error - " + ev.error);
            await stop();
        };
        recognition.onend = noOpenMic ? stop : start;
        /**
         * Recognition result handler: keyword detection, interim streaming, stop word abort and the 1500 ms
         * debounced delivery of the final sentence (see `triggerOnSpeech`).
         * @param {SpeechRecognitionEvent} event - The result event (`event.results[event.resultIndex][0].transcript`).
         * @returns {Promise<void>}
         */
        recognition.onresult = async event => {
            const res = event.results[event.resultIndex];
            const a = res[0];
            if (del) {
                clearTimeout(del);
                del = null;
            }
            const t = a.transcript;
            if (t.length <= 0)
                return;
            //console.log("Speech: " + t);
            const pk = isKey;
            isKey = isKey ?? haveKeyword(t);
            if (pk !== isKey) {
                await onActivate(isKey, true);
                add = "";
            }
            const part = SpeechInput.IsMobile ? t : (add.length > 0 ? (add + ". " + t) : t);
            /*
            const lw = SpeechInput.GetLastWord(part);
            if (SpeechInput.StopWords.get(lw)) {
                console.log("Speech recognition: Input aborted!");
                if (noOpenMic)
                    await stop();
                if (isKey)
                    await onActivate(null, !noOpenMic);
                await onSentance("", false);
                del = null;
                add = "";
                return;
            }
            */

            if (isKey)
                await onSentance(filter(part, isKey), false);
            if (res.isFinal) {
                add = part;
                const final = add.trim();
                const lw = SpeechInput.GetLastWord(final);
                if (SpeechInput.StopWords.get(lw)) {
                    if (isKey) {
                        isKey = null;
                        del = null;
                        add = "";
                        //console.log("Speech recognition: Input aborted!");
                        if (noOpenMic)
                            await stop();
                        else
                            await onActivate(null, true);
                        await onSentance("", true);
                        return;
                    }
                }
                del = setTimeout(async () => {
                    del = null;
                    add = "";
                    if (isKey) {
                        const finalKey = isKey;
                        isKey = null;
                        if (noOpenMic)
                            await stop();
                        else
                            await onActivate(null, true);
                        await onSentance(filter(final, finalKey), true);
                    }
                }, noOpenMic ? 1500 : 1500);
            }
        };
        try {
            //console.log("Speech recognition: Created");
            start();
            if (noOpenMic)
                onActivate(true, true);
        }
        catch
        {
            document.addEventListener("UserInteracted", start);
        }
        return stop;
    }


}


/**
 * Options bag for `Chat.addChat`. Created with `new ChatOptions()` (by `chatMain`, merged with the `options`
 * URL parameter via `Object.assign`, or by `addChat` when no options are given).
 */
class ChatOptions {
    /** The size in pixels of the top right menu icon (scaled with theme) */
    MenuIconSize = 48;
    /** The size in pixels of the microphone icon (scaled with theme) */
    AudioIconSize = 48;
    /** The size in pixels of the send icon (scaled with theme) */
    SendIconSize = 48;
    /** If true use per user color */
    UserColor = true;
    /** Used to determine what local storage keys to use for settings:
     * - '*', use the chat id (per chat settings).
     * - null, use the same settings for all chats.
     * - Some string used in the key (group settings for all chats with same string).
     */
    SettingsName = null;

}

/**
 * Static-only chat UI factory. `Chat.addChat` builds the complete UI of one chat inside a container element
 * (message list, input, preview, menus, speech, uploads, push updates); all logic lives in closures inside it.
 */
class Chat {

    /** If this flag is set on a message it means that it's not completed yet, like streaming chat */
    static FlagIsWorking = 1;

    /** If this flag is set on a message it means that the message can be remvoed by this user */
    static FlagCanRemove = 256;


    /**
     * Converts a text (the sender name, `m.From`) to a stable set of colors.
     * Hashes `text` with `legacySha256` (common.js) into hue/saturation/lightness; results are cached in the
     * lazily created `window.ChatColors` Map.
     * @param {string} text - The sender name.
     * @returns {string[]} `[border, headerBackground, headerText]`: `"hsl(h s l / 0.5)"`, `"hsl(h s l / 0.05)"`, `"hsl(h s l)"`.
     */
    static chatColor(text) {
        const t = window.ChatColors ?? new Map();
        /** Global cache: sender name -> [border, headerBackground, headerText] colors. @type {Map<string, string[]>} */
        window.ChatColors = t;
        let c = t.get(text);
        if (c)
            return c;
        const h = new Uint8Array(legacySha256(new TextEncoder().encode(text)));
        const hue = (h[0] % 90) * 4;
        const sat = (h[1] % 40) + 50;
        const l = (Math.max(0, 40 - Math.abs(hue - 240)) + 40) | 0;
        const bc = "hsl(" + hue + " " + sat + " " + l;
        c = [
            bc + " / 0.5)",
            bc + " / 0.05)",
            bc + ")",
        ];
        t.set(text, c);
        return c;
    }

    /** Cache if we're on mobile or not (`isMobile()` at load); on mobile no "ALT+Enter" hint is added to the placeholder. @type {boolean} */
    static IsMobile = isMobile();

    /** Cache platform name (`getPlatform()` at load); a value starting with "mac" selects the "Option+Enter" hint. @type {string} */
    static Platform = getPlatform();

    /**
     * Builds the complete chat UI for one chat inside `chatBackground`. In order it:
     * cancels ongoing speech synthesis; computes the localStorage key names; creates `<SysWeaver-ChatMain>`;
     * calls `Join {ChatId, MaxCount: 10, FromId: 0}` (`ChatJoinResponse`); loads dynamic deps in parallel
     * (`GetInputLanguages` if `CanTranslate`, `md5.js`/`UZIP.js`/`fileUploader.js` if `UploadRepo`);
     * if `CanPost` builds the input area, preview row, drag and drop, language picker, speech input and uploads and
     * restores the draft; registers postMessage commands (window `message`), server push events
     * (`chat.post|replace|remove|clear.<chatid lowercased>` via `SessionManager.AddServerEvent` / `InterOp`) and speech
     * synthesis; adds the top-right menu; renders the initial messages; installs `StickToBottom` with lazy loading of
     * older messages (`GetMessages`); restores the saved split height and scrolls to the bottom; finally runs `doLast`.
     * localStorage keys (`<s>` = "", "." + chatId or "." + SettingsName): `SysWeaver.Chat.ShowTimeStamp<s>`,
     * `.SpeechGenVol<s>`, `.SpeechGen<s>`, `.SpeechSend<s>`, `.UseMarkDown<s>`, `.Preview<s>`, `.Size<s>`;
     * per chat: `SysWeaver.Chat.SpeechListen<chatId>`, `.Hide.<chatId>`, `.Input.<chatId>`, `.Data.<chatId>`, `.Lang.<chatId>`.
     * @param {HTMLElement} chatBackground - The html element that the chat should be created as a child to (normally `<SysWeaver-Chat>`).
     * @param {string} chatId - The ID of the chat (channel), `<provider>.<providerChatId>`.
     * @param {string} [apiBase=""] - An optional api base prefix for all `sendRequest` calls (depends where the html resides).
     * @param {ChatOptions} [options=new ChatOptions()] - Optional options for more control.
     * @returns {Promise<HTMLElement>} The created `SysWeaver-ChatMain` element (the message list).
     */
    static async addChat(chatBackground, chatId, apiBase, options) {

        if (!apiBase)
            apiBase = "";
        if (!options)
            options = new ChatOptions();
        const ss = window.speechSynthesis;
        if (ss)
            ss.cancel();
        else
            console.warn("No speech synthesis supported");

        /** Input languages from `GetInputLanguages` (null until loaded). @type {?Array<{Iso:string, Name:string, LocalName:string, EnName:string, Comment:string}>} */
        let languages = null;
        /** Iso code to LanguageInfo map built from `languages` (null until loaded). @type {?Map<string, object>} */
        let languageMap = null;


        /**
         * Loads the input languages once: `sendRequest("GetInputLanguages")` into `languages` and builds `languageMap`.
         * Awaited at startup when `CanTranslate`, and lazily by `selectLanguage`.
         * @returns {Promise<void>}
         */
        async function GetLanguages() {
            if (languages)
                return;

            languages = await sendRequest(apiBase + "GetInputLanguages");
            languageMap = new Map();
            const lc = languages.length;
            for (let i = 0; i < lc; ++i) {
                const l = languages[i];
                languageMap.set(l.Iso, l);
            }
        }

        /** Settings key suffix: "" (shared), "." + chatId (SettingsName "*") or "." + SettingsName. @type {string} */
        let sname = options.SettingsName;
        if (!sname) {
            sname = "";
        } else {
            sname = "." + (sname === "*" ? chatId : sname);
        }

        // localStorage key names: settings (scoped by sname) followed by per chat keys (scoped by chatId)
        const timeStampName = "SysWeaver.Chat.ShowTimeStamp" + sname;
        const volumeKey = "SysWeaver.Chat.SpeechGenVol" + sname;
        const genKey = "SysWeaver.Chat.SpeechGen" + sname;

        const autoSendKey = "SysWeaver.Chat.SpeechSend" + sname;


        const useMarkDownKey = "SysWeaver.Chat.UseMarkDown" + sname;
        const previewKey = "SysWeaver.Chat.Preview" + sname;

        const sizeKey = "SysWeaver.Chat.Size" + sname;

        const listenKey = "SysWeaver.Chat.SpeechListen" + chatId;
        const hideName = "SysWeaver.Chat.Hide." + chatId;
        const inputSaveKey = "SysWeaver.Chat.Input." + chatId;
        const dataSaveKey = "SysWeaver.Chat.Data." + chatId;
        const langSaveKey = "SysWeaver.Chat.Lang." + chatId;

        /** True once `GetMessages` returned no older messages (stops history paging). @type {boolean} */
        let isAtTop = false;


        /** The scrolling message list `<SysWeaver-ChatMain>`; its children are the message rows. @type {HTMLElement} */
        const chatE = document.createElement("SysWeaver-ChatMain");

        /** Saved split height fraction (string from `sizeKey`), restored at the end (only when posting). @type {?string} */
        let initSize;

        chatBackground.appendChild(chatE);


        //  Hide show

        const titleVisible = _TF("Click to hide message content and future message content from {0}", "A tool tip message for a buttan that when clicked will hide the content of a specific chat message and all further messages from that user.{0} is replaced with the user name. ");
        const titleHidden = _TF("Click to show message content and future message content from {0}", "A tool tip message for a buttan that when clicked will show the hidden content of a specific chat message and all continue to show all further messages from that user.{0} is replaced with the user name. ");
        /** True to show absolute time stamps, false to show live age ("x ago"). @type {boolean} */
        let showTimeStamp = localStorage.getItem(timeStampName) === "true";
        /** Sender names whose message content is hidden (value 1), persisted in `hideName`. @type {Map<string, number>} */
        const hideFrom = new Map();
        const hideData = localStorage.getItem(hideName);
        if (hideData) {
            const ha = JSON.parse(hideData);
            if (Array.isArray(ha))
                ha.forEach(x => hideFrom.set(x, 1));
        }
        const hiddenClass = "Hidden";

        /**
         * Checks if the content from a sender is hidden.
         * @param {string} name - The sender name.
         * @returns {boolean} true if `name` is in `hideFrom`.
         */
        function isHidden(name) {
            return hideFrom.get(name) === 1;
        }

        /**
         * Hide or show messages from a user; persists the hidden names as a JSON array in `SysWeaver.Chat.Hide.<chatId>`.
         * Does not update existing rows (callers do that with `row.Show` / `row.Hide`).
         * @param {string} name - The sender name (`m.From`).
         * @param {boolean} hide - True to hide (current and future) messages from this user, false to show.
         */
        function setHideFrom(name, hide) {
            if (hide)
                hideFrom.set(name, 1);
            else
                hideFrom.delete(name);
            localStorage.setItem(hideName, JSON.stringify(Array.from(hideFrom.keys())));
        }

        /** Expand all items (none collapsed): clears `hideFrom` and removes the `SysWeaver.Chat.Hide.<chatId>` key. Does not update rows. */
        function setHideNone() {
            hideFrom.clear();
            localStorage.removeItem(hideName);
        }






        /**
         * The `Join` response (`ChatJoinResponse`): session config (UserName, UserGuid, UserStore, Lang, MaxTextLength,
         * MaxDataLength, MaxDataCount, CanClear, CanPost, DoNotConfirmClear, Menus, SpeechName, Voices, AllowMarkDown,
         * CanTranslate, CanShowProfile, UploadRepo, AllowPublicStore, EnableListenByDefault, EnableGenByDefault) and the latest `Messages`.
         * @type {object}
         */
        const data = await sendRequest(apiBase + "Join", {
            ChatId: chatId,
            MaxCount: 10,
            FromId: 0,
        });

        const canPost = data.CanPost;

        if (!canPost)
            chatE.classList.add("NoPost");
        const userName = data.UserName;
        const currentImage = "../auth/UserImages/" + data.UserGuid + "/small";
        const canTranslate = data.CanTranslate;
        const msg = data.Messages;
        const workingAttribute = "Working";
        const sl = localStorage.getItem(langSaveKey);
        let currentLang = data.Lang;
        if (sl) {
            if (sl === data.Lang)
                localStorage.removeItem(langSaveKey);
            else
                currentLang = sl;
        }

        /** postMessage commands: `{Type}` string to handler (chat.scrolltop, chat.scrollbottom, chat.showall, chat.hideall, chat.clear). @type {Map<string, function():any>} */
        const commandMap = new Map();
        /** Uploaded (not already existing) URLs of the draft that are not sent yet; deleted again if removed from the draft. @type {Map<string, *>} */
        const tempFiles = new Map();

        // Draft / input state; the functions below are declared here but only assigned when canPost.
        let useMarkDown = false;
        let canSend = null;
        let previewMessage;
        let uploadFiles;
        let markDownChanged;
        let showPreview;
        let previewChanged;
        let selectLanguage;
        let stopListening;
        let startListening;
        let clearCurrentMessage;
        let cmdClear;
        let previewElement;

        //  Load dynamic dependencies
        const deps = [];
        if (canTranslate)
            deps.push(GetLanguages());
        if (data.UploadRepo) {
            deps.push(includeJs(null, "md5.js", true));
            deps.push(includeJs(null, "UZIP.js", true));
            deps.push(includeJs(null, "fileUploader.js", true));
        }
        await Promise.all(deps);
        /** The upload repository name; only set if `data.UploadRepo` is set and `fileUploader` got defined (upload enabled). @type {?string} */
        let uploadRepo = null;
        if (data.UploadRepo && (typeof fileUploader !== "undefined")) {
            uploadRepo = data.UploadRepo;
            console.log('File uploading to repository "' + uploadRepo + '" is enabled.');
        }


        //  Input
        /**
         * Run at the very end of `addChat`. Default no-op; with `canPost` it is replaced to call `sendEnable()` and focus
         * the textarea (temporarily read-only so no on-screen keyboard is forced).
         */
        let doLast = () => { };
        if (canPost) {
            initSize = localStorage.getItem(sizeKey);
            new ResizeObserver(ev => {
                const f = chatE.offsetHeight / chatBackground.offsetHeight;
                localStorage.setItem(sizeKey, "" + f);
            }).observe(chatE);



            /** The input area `<SysWeaver-ChatInput>` (children: textarea, send button, optional mic button). @type {HTMLElement} */
            const chatInput = document.createElement("SysWeaver-ChatInput");
            /** The message textarea (`NextLast` expando used by `insertAtCursor`). @type {HTMLTextAreaElement} */
            const write = document.createElement("textarea");
            chatBackground.appendChild(chatInput);
            chatInput.appendChild(write);

            /** Sent texts (max 100), browsed with Alt+Up/Down. @type {string[]} */
            const inputHistory = [];
            /** History position: 0 = the text being typed, -1 = empty, n = n-th last history entry. @type {number} */
            let inputHistoryPos = 0;
            /** The text being typed before history browsing started. @type {string} */
            let inputCurrent = "";


            /** True while a `UserMessage` request is in flight. @type {boolean} */
            let isSending = false;
            /**
             * Sends the draft (text and attachments) with `UserMessage {ChatId, Body:{Format, Data, Text, Lang}}`.
             * Returns if both text and data are empty or a send is in progress. Pushes the text into the input history,
             * clears the input (showing the sent text as placeholder, read-only) and the preview. On success clears
             * `tempFiles` and calls `StickForAwhile()`; on false/exception shows `Fail` and restores text/data if the input
             * is still empty. Finally restores the input, calls `sendEnable()` and focuses unless `doFocus === "NoFocus"`.
             * Bound to the send button, Alt+Enter and speech auto-send.
             * @param {*} [doFocus] - Pass "NoFocus" to not focus the input box when done (any other value focuses).
             * @returns {Promise<void>}
             */
            async function Send(doFocus) {
                const t = write.value.trim();
                const d = previewMessage.Data;
                if ((!t) && (!d))
                    return;
                if (isSending)
                    return;
                isSending = true;

                send.StartWorking();

                inputHistory.push(t);
                const hl = inputHistory.length;
                if (hl > 100)
                    inputHistory.splice(0, hl - 100);
                inputHistoryPos = 0;
                inputCurrent = "";

                const wp = write.placeholder;
                if (d) {
                    previewMessage.Data = null;
                    previewElement.UpdateData(null);
                }
                write.placeholder = t;
                write.value = "";
                write.readOnly = true;
                write.NextLast = write.value.length;
                setPreviewText(null);
                try {
                    if (await sendRequest(apiBase + "UserMessage", {
                        ChatId: chatId,
                        Body: {
                            Format: useMarkDown ? 1 : 0,
                            Data: d,
                            Text: t,
                            Lang: currentLang,
                        },
                    })) {
                        tempFiles.clear();
                        StickForAwhile();
                        return;
                    }
                    Fail(_TF("Failed to send message.", "An error message shown when a user tries to send a chat message but the server returns a failure"));
                    if ((!write.value) || (write.value === "")) {
                        previewMessage.Data = d;
                        write.value = t;
                    }
                }
                catch (e) {
                    Fail(_TF("Failed to send message.", "An error message shown when a user tries to send a chat message but the server returns a failure") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                    if ((!write.value) || (write.value === "")) {
                        previewMessage.Data = d;
                        write.value = t;
                    }
                }
                finally {
                    isSending = false;
                    send.StopWorking();
                    sendEnable();
                    write.readOnly = false;
                    write.placeholder = wp;
                    if (doFocus !== "NoFocus")
                        write.focus();
                }
            }

            /** True while the textarea is changed by history browsing (so `onInputChangeFn` keeps the history position). @type {boolean} */
            let isInternalUpdate = false;

            /**
             * Set the input text from a history value: `inputHistoryPos` 0 restores `inputCurrent`, -1 clears, n takes the
             * n-th last history entry. Updates `write.NextLast` and calls `sendEnable()`, flagged as internal update.
             */
            function SetFromHistory() {
                isInternalUpdate = true;
                if (inputHistoryPos === 0) {
                    write.value = inputCurrent;
                    write.NextLast = write.value.length;
                    sendEnable();
                    isInternalUpdate = false;
                    return;
                }
                if (inputHistoryPos === -1) {
                    write.value = "";
                    write.NextLast = write.value.length;
                    sendEnable();
                    isInternalUpdate = false;
                    return;
                }
                write.value = inputHistory[inputHistory.length - inputHistoryPos];
                write.NextLast = write.value.length;
                sendEnable();
                isInternalUpdate = false;
            }

            write.onfocus = function () {
                write.scrollIntoView({ behavior: "smooth" })
            }

            // Pure Alt (no Ctrl/Meta/Shift) shortcuts: Enter sends, ArrowUp/ArrowDown browse the input history.
            write.onkeydown = async e => {
                if (!e.altKey)
                    return;
                if (e.ctrlKey || e.metaKey || e.shiftKey)
                    return;
                if (isSending)
                    return;
                //  Pure alt
                if (e.key === "Enter") {
                    await Send();
                    return;
                }
                if (e.key == "ArrowUp") {
                    if (inputHistoryPos >= inputHistory.length)
                        return;
                    ++inputHistoryPos;
                    SetFromHistory();
                    return;
                }
                if (e.key == "ArrowDown") {
                    if (inputHistoryPos <= -1)
                        return;
                    --inputHistoryPos;
                    SetFromHistory();
                    return;
                }


            };

            /** The send button (`IconSend`). @type {ColorIcon} */
            const send = new ColorIcon("IconSend", "IconColorThemeAcc1", options.SendIconSize, options.SendIconSize, _TF("Click to send the message", "A tool tip description on a button that when clicked will send the entered chat message to the server"), Send);
            chatInput.appendChild(send.Element);
            /** The push-to-talk mic button (`IconChatMicOn`, third child of chatInput), null if speech recognition is unsupported. @type {?ColorIcon} */
            let listen = null;
            if (SpeechInput.supportSpeechRec()) {
                listen = new ColorIcon("IconChatMicOn", "IconColorThemeAcc1", options.AudioIconSize, options.AudioIconSize, _TF("Click to listen for speech input using the selected language", "A tool tip description on a button that when clicked will start listening to speech input"), () => {
                    startListening(true, true);
                });
                chatInput.appendChild(listen.Element);
            }

            /**
             * Called to enable/disable the send button (every time the input text changes).
             * Recomputes `canSend` (trimmed text or data present), saves or removes the draft keys
             * `SysWeaver.Chat.Input.<chatId>` / `SysWeaver.Chat.Data.<chatId>` and updates the preview text.
             */
            function sendEnable() {
                const t = write.value.trim();
                const d = previewMessage.Data;
                canSend = (t && (t.length > 0)) || d;
                if (canSend) {
                    localStorage.setItem(inputSaveKey, t);
                    if (d)
                        localStorage.setItem(dataSaveKey, d);
                    else
                        localStorage.removeItem(dataSaveKey);
                }
                else {
                    localStorage.removeItem(inputSaveKey);
                    localStorage.removeItem(dataSaveKey);
                }
                setPreviewText(canSend ? t : null);
                send.SetEnabled(canSend);
            }

            /** The saved draft text (restored into the textarea). @type {?string} */
            const start = localStorage.getItem(inputSaveKey);
            if (start) {
                write.value = start;
                write.NextLast = write.value.length;
            }

            /**
             * Textarea `oninput` handler (also called after drops): `sendEnable()` and, unless the change came from
             * history browsing, resets the history position and remembers the current text in `inputCurrent`.
             */
            const onInputChangeFn = () => {
                sendEnable();
                if (!isInternalUpdate) {
                    inputHistoryPos = 0;
                    inputCurrent = write.value;
                }
            };


            write.oninput = onInputChangeFn;


            chatInput.style.backgroundImage = "url('../iso_data/language/" + currentLang + ".svg')";
            write.maxLength = data.MaxTextLength;


            // Mark down and preview

            useMarkDown = localStorage.getItem(useMarkDownKey) === "true";
            useMarkDown &= data.AllowMarkDown;

            showPreview = localStorage.getItem(previewKey); // null means auto (on for md and off for text)
            // The preview pseudo message (Id 0), rendered as the last row (class Preview); Data is restored from the draft.
            previewMessage = {
                Data: localStorage.getItem(dataSaveKey) ?? null,
                Time: null,
                Flags: 0,
                Format: useMarkDown ? 1 : 0,
                From: data.UserName,
                FromImage: "../auth/UserImages/Current/small",
                Id: 0,
                Lang: currentLang,
                Text: write.value.trim(),

                MenuItems: null,
            };
            previewElement = (await addMessage(previewMessage, true, true)).parentElement.parentElement;
            previewElement.classList.add("Preview");


            /**
             * Updates the preview row visibility: shown (removes `Hide`, smooth-scrolls to the bottom) if it has data,
             * or has text and preview is on (`showPreview`, or `useMarkDown` when `showPreview` is not a boolean);
             * otherwise class `Hide` is added.
             * @returns {boolean} true if the preview is visible.
             */
            previewChanged = () => {
                const pm = previewElement.Msg;
                const sp = (!!previewMessage.Data) || ((typeof showPreview !== "boolean" ? useMarkDown : showPreview) && (!!pm.Text));
                if (sp) {
                    if (previewElement.classList.contains("Hide")) {
                        previewElement.classList.remove("Hide");
                        chatE.scrollTo({
                            left: chatE.scrollLeft,
                            top: chatE.scrollHeight,
                            behavior: "smooth",
                        });
                    }
                }
                else {
                    if (!previewElement.classList.contains("Hide")) {
                        previewElement.classList.add("Hide");
                    }
                }
                return sp;
            };

            // Placeholders for text / markdown mode; on desktop a send hint (Option+Enter on mac, else ALT+Enter) is appended on a new line.
            let placeholderText = _TF("Write your text here..", "A placeholder text for an input box where a user can enter a text message for a chat");
            let placeholderMD = _TF("Write your Mark Down text here..", "A placeholder text for an input box where a user can enter a markdown formatted text message for a chat");
            if (!Chat.IsMobile) {

                const platform = Chat.Platform;
                const t = "                  \n" +
                    (platform.indexOf("mac") === 0
                        ?
                        _TF("Press Option+Enter to send", "A placeholder instruction on a Mac machine where the user can press the Enter key while holding down the Option key to send a chat message")
                        :
                        _TF("Press ALT+Enter to send", "A placeholder instruction on a Windows machine where the user can press the Enter key while holding down the ALT key to send a chat message")
                    );
                placeholderText += t;
                placeholderMD += t;
            }


            /**
             * Applies `useMarkDown` to the preview message format and the textarea placeholder (text vs markdown, plus
             * the platform send hint), re-renders the preview body and calls `previewChanged()`.
             * @returns {Promise<void>}
             */
            markDownChanged = async () => {
                const pm = previewElement.Msg;
                pm.Format = useMarkDown ? 1 : 0;
                write.placeholder = useMarkDown ? placeholderMD : placeholderText;
                await previewElement.UpdateBody(pm.Text, pm.Format, false);
                previewChanged();
            };

            /**
             * Sets the preview message text: if it changed, stores it on `previewElement.Msg`, re-renders the preview body
             * and, if the preview is visible (`previewChanged()`), smooth-scrolls the list to the bottom.
             * No-op before the preview element exists.
             * @param {?string} newText - The new draft text (null when nothing can be sent).
             * @returns {Promise<void>}
             */
            async function setPreviewText(newText) {
                if (!previewElement)
                    return;
                const pm = previewElement.Msg;
                if (pm.Text === newText)
                    return;
                pm.Text = newText;
                await previewElement.UpdateBody(pm.Text, pm.Format, false);
                if (previewChanged()) {
                    chatE.scrollTo({
                        left: chatE.scrollLeft,
                        top: chatE.scrollHeight,
                        behavior: "smooth",
                    });
                }
            }

            markDownChanged();


            // Drag and drop on text
            /** URL schemas whose dropped image URLs are wrapped as markdown images (`![](url)`). @type {Map<string, number>} */
            const validSchemas = new Map();
            validSchemas.set("http", 1);
            validSchemas.set("https", 1);
            // Drop: uploads dropped files (if uploads are enabled), then inserts the first string item at the cursor
            // (in markdown mode http(s) URLs with an image extension become `![](url)`).
            write.addEventListener("drop", async ev => {
                ev.preventDefault();
                ev.target.classList.remove("Dragging");
                if (uploadRepo)
                    await uploadFiles(ev.dataTransfer.files);
                const items = ev.dataTransfer.items;
                const il = items.length;
                for (let i = 0; i < il; ++i) {
                    const item = items[i];
                    if (item.kind === "string") {
                        let val = await new Promise(res => {
                            item.getAsString(x => res(x));
                        });
                        if (useMarkDown) {
                            const sp = val.indexOf("://");
                            if ((sp > 0) && validSchemas.get(val.substring(0, sp))) {
                                let temp = val;
                                const qstart = val.indexOf('?');
                                if (qstart > 0)
                                    temp = temp.substring(0, qstart);
                                const extStart = temp.lastIndexOf('.');
                                if (extStart >= 0) {
                                    const ext = temp.substring(extStart + 1).toLowerCase();
                                    if (imageExtensions[ext])
                                        val = "![](" + val + ")";
                                }
                            }
                        }
                        insertAtCursor(write, val);
                        onInputChangeFn();
                        return;
                    }

                }
            });
            write.addEventListener("dragover", ev => {
                ev.preventDefault();
            });
            write.addEventListener("dragenter", ev => ev.target.classList.add("Dragging"));
            write.addEventListener("dragleave", ev => ev.target.classList.remove("Dragging"));


            /**
             * Opens a `PopUpSelection` (application.js) with a searchable list (Name/LocalName/EnName) of
             * `<SysWeaver-ChatLangItem>` elements (flag background, tooltip with names, ISO code and comment).
             * Clicking one sets `currentLang`, stores it in `SysWeaver.Chat.Lang.<chatId>` (removed when equal to the server
             * default `data.Lang`), updates the input flag background and the preview flag (`UpdateLang`), restarts open
             * mic listening if it is enabled, and closes the popup.
             * @returns {Promise<void>}
             */
            selectLanguage = async () => {
                const speechRec = SpeechInput.supportSpeechRec();
                await PopUpSelection(
                    _TF("Select the language to use", "Header text to a list of languages that can be selected"),
                    speechRec ?
                        _TF("Select the language you're using for the text and voice input", "Tool tip description of a header to a list of languages that can be selected")
                        :
                        _TF("Select the language you're using for the text input", "Tool tip description of a header to a list of languages that can be selected")
                    ,
                    async (search, closeFn) => {
                        await GetLanguages();
                        const ls = languages;
                        const lc = ls.length;

                        const items = [];
                        search = search?.toLowerCase() ?? "";
                        for (let ii = 0; ii < lc; ++ii) {
                            const lang = ls[ii];
                            if (search) {
                                if (lang.Name.toLowerCase().indexOf(search) < 0)
                                    if (lang.LocalName.toLowerCase().indexOf(search) < 0)
                                        if (lang.EnName.toLowerCase().indexOf(search) < 0)
                                            continue;
                            }
                            const item = document.createElement("SysWeaver-ChatLangItem");
                            keyboardClick(item);
                            items.push(item);
                            const iso = lang.Iso;
                            const name = lang.Name;
                            const lname = lang.LocalName;
                            const ename = lang.EnName;
                            let text = _TF("Name", 'Tool tip prefix indicating what language this is, part of somthing like: "Name: English".') + ": " + name + "\n";
                            if (name !== lname)
                                text += _TF("Local name", 'Tool tip prefix indicating what language this is, part of somthing like: "Local name: Svenska".') + ": " + lname + "\n";
                            if (name !== ename)
                                text += _TF("English name", 'Tool tip prefix indicating what language this is, part of somthing like: "English name: German".') + ": " + ename + "\n";
                            text +=
                                "Iso639-1: " + iso + "\n\n" +
                                _TF("Click to select this language", "Tool tip description on a button that when clicked will select tis language");
                            const com = lang.Comment;
                            if (com)
                                text += "\n\n" + com;
                            item.title = text;
                            item.style.backgroundImage = "url('../iso_data/language/" + iso + ".svg')";
                            text = name;
                            if (text !== lname)
                                text += " (" + lname + ")";
                            item.innerText = text;
                            item.onclick = ev => {
                                if (badClick(ev))
                                    return;
                                currentLang = iso;
                                if (currentLang === data.Lang)
                                    localStorage.removeItem(langSaveKey);
                                else
                                    localStorage.setItem(langSaveKey, currentLang);
                                chatInput.style.backgroundImage = "url('../iso_data/language/" + currentLang + ".svg')";
                                previewMessage.Lang = currentLang;
                                previewElement.UpdateLang();
                                if (canListen()) {
                                    if (localStorage.getItem(listenKey) === "true") {
                                        stopListening();
                                        startListening();
                                        localStorage.setItem(listenKey, "true");
                                    }
                                }
                                closeFn();
                            }

                        }
                        return items;
                    },
                    null,
                    true,
                    true);
            }

            /**
             * Clears the draft. Deletes every `tempFiles` entry referenced by the preview data with
             * `../Api/UserStorage/DeleteStoredFile` (failures are logged with `console.warn`), then clears the preview
             * data, the textarea and the preview text, and calls `previewChanged()` and `sendEnable()`.
             * @returns {Promise<void>}
             */
            clearCurrentMessage = async () => {
                const data = previewMessage.Data;
                if (data) {
                    const parts = data.split(';');
                    const pl = parts.length;
                    for (let ii = 0; ii < pl; ++ii) {
                        const pr = parts[ii];
                        if (tempFiles.get(pr)) {
                            tempFiles.delete(pr);
                            try {
                                if (!await sendRequest(apiBase + "../Api/UserStorage/DeleteStoredFile", pr))
                                    console.warn('Failed to delete stored file "' + pr + '".');
                            }
                            catch (e) {
                                console.warn('Failed to delete stored file "' + pr + '", error: ' + e.message);
                            }
                        }
                    }
                    previewElement.UpdateData(null);
                }
                write.value = "";
                write.NextLast = write.value.length;
                previewMessage.Data = null;
                previewMessage.Text = "";
                previewChanged();
                sendEnable();
            };

            /** Default `stopListening` (nothing is listening): only re-enables the mic button. */
            const defStopListening = () => {
                if (listen)
                    listen.SetEnabled(true);
                //console.log("Chat: No need to stop, wasn't listening!");
            };

            stopListening = defStopListening;

            /**
             * Starts speech input with `SpeechInput.triggerOnSpeech(data.SpeechName, ...)` in `currentLang`.
             * Disables the mic button in open mic mode. Activation toggles class `Listening` on the textarea and the mic
             * button's working state; deactivation stores "false" in the listen key (open mic only) and calls `stopListening()`.
             * Sentences are inserted live with `insertAtCursor(write, text, !isLast)` (interim text is replaceable); a final
             * non empty sentence is sent with `Send("NoFocus")` in open mic mode, or in push-to-talk mode when
             * `SysWeaver.Chat.SpeechSend<s>` is "true"; otherwise `sendEnable()`.
             * On success replaces `stopListening` with a function that stops the recognizer; else resets it to `defStopListening`.
             * The mic button calls `startListening(true, true)` (push-to-talk).
             * @param {boolean} [ignoreKeyword] - If true any speech activates (no wake word needed).
             * @param {boolean} [noOpenMic] - If true stop after one sentence (push-to-talk).
             */
            startListening = (ignoreKeyword, noOpenMic) => {

                if (!noOpenMic) {
                    if (listen)
                        listen.SetEnabled(false);
                }
                const stop = SpeechInput.triggerOnSpeech(data.SpeechName,
                    (activeUser, isWorking) => {
                        if (activeUser) {
                            write.classList.add("Listening");
                        }
                        else {
                            write.classList.remove("Listening");
                        }
                        if (isWorking) {
                            if (listen)
                                listen.StartWorking();
                        }
                        else {
                            if (listen)
                                listen.StopWorking();
                            if (!ignoreKeyword)
                                localStorage.setItem(listenKey, "false");
                            stopListening();
                        }
                    },
                    async (data, isLast) => {
                        insertAtCursor(write, data, !isLast);
                        if (isLast) {
                            if (listen)
                                listen.StopWorking();
                            if (data.length > 0) {
                                if ((!noOpenMic) || (localStorage.getItem(autoSendKey) === "true")) {
                                    await Send("NoFocus");
                                    return;
                                }
                            }
                        }
                        sendEnable();
                    },
                    currentLang, ignoreKeyword, noOpenMic);
                if (stop) {
                    /** Stops the active recognizer: ends the mic button's working state, re-enables it and calls the recognizer's stop function. */
                    stopListening = () => {
                        if (listen) {
                            listen.StopWorking();
                            listen.SetEnabled(true);
                        }
                        stop();
                        //console.log("Chat: Stopped listening");
                    }
                    //console.log("Chat: Started listening");
                } else {
                    stopListening = defStopListening;
                }
            };

            if (canListen()) {
                let l = localStorage.getItem(listenKey);
                if (l === null) {
                    l = data.EnableListenByDefault ? "true" : "false";
                    localStorage.setItem(listenKey, l);
                }
                if (l === "true") {

                    startListening();
                }
            }

            /**
             * Uploads files and attaches them to the draft. Fails if the attachment count would exceed `data.MaxDataCount`;
             * calls the global `fileUploader(uploadRepo, files)` -> `{Error, Urls, Status}` and shows `Fail` on `Error` or no
             * URLs. Per URL: shows `fileUploaderStatusText` for a negative status, adds non `UploadStatus.AlreadyUploaded`
             * URLs to `tempFiles` and appends `"../" + url` to the `;`-separated data unless that exceeds `data.MaxDataLength`
             * (then `Fail` and stop). Updates `previewMessage.Data`, the preview attachments, `previewChanged()` and `sendEnable()`.
             * Used by drop and the "Attach file" menu items. Only assigned when canPost.
             * @param {File[]|FileList} files - The files to upload.
             * @returns {Promise<void>}
             */
            uploadFiles = async (files) => {
                if (files.length <= 0)
                    return;
                let na = previewMessage.Data ?? "";
                const count = na ? na.split(';').length : 0;
                if ((count + files.length) > data.MaxDataCount) {
                    Fail(data.MaxDataCount <= 1
                        ?
                        _TF("Only one file may be attached!", "Error message displayed when the user tries to attach more than 1 file to a chat message")
                        :
                        _T("Only {0} files may be attached!", data.MaxDataCount, "Error message displayed when the user tries to attach more than the allowed number of files to a chat message.{0} is replaced with the maximum allowed number of files")
                    );
                    return;
                }
                const res = await fileUploader(uploadRepo, files);
                const e = res.Error;
                if (e) {
                    Fail(e)
                    return;
                }
                const urls = res.Urls;
                const status = res.Status;
                const ul = urls.length;
                if (ul <= 0) {
                    Fail(_TF("Failed to upload!", "Error message shown when a file upload failed"));
                    return;
                }
                for (let i = 0; i < ul; ++i) {
                    const s = status[i];
                    if (s < 0)
                        Fail(fileUploaderStatusText(s));
                    const u = urls[i];
                    if (!u)
                        continue;
                    if (s !== UploadStatus.AlreadyUploaded)
                        tempFiles.set(u, 1);
                    const prev = na;
                    if (na.length > 0)
                        na += ";";
                    na += ("../" + u);
                    if (na.length > data.MaxDataLength) {
                        Fail(_TF("Can't attach more data!", "Error message shown when no more files can be attached to a chat message"));
                        na = prev;
                        break;
                    }
                }
                previewMessage.Data = na;
                previewElement.UpdateData(na);
                previewChanged();
                sendEnable();
            };

            /**
             * Try to clear all messages from the chat room: `sendRequest("ClearAllMessages", chatId)`; `Fail` on false/exception.
             * Registered as the `chat.clear` postMessage command when `CanClear`, and used by "Remove ALL messages".
             * @returns {Promise<void>}
             */
            cmdClear = async () => {
                try {
                    if (!await sendRequest(apiBase + "ClearAllMessages", chatId))
                        Fail(_TF("Failed to clear all chat messages.", "An error message shown when a user tries to send clear a chat room but the server returns a failure"));
                }
                catch (e) {
                    Fail(_TF("Failed to clear all chat messages.", "An error message shown when a user tries to send clear a chat room but the server returns a failure") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                }
            };

            if (data.CanClear)
                commandMap.set("chat.clear", cmdClear);

            /** Final step with canPost: `sendEnable()` and focus the textarea (read-only during focus so no on-screen keyboard is forced). */
            doLast = () => {
                sendEnable();
                write.readOnly = true;
                write.focus();
                write.readOnly = false;
            };
        }

        //  INputEnd

        // Handling of PostMessage messages (from parent window)
        commandMap.set("chat.scrolltop", () => chatE.scrollTo({
            left: chatE.scrollLeft,
            top: 0,
            behavior: "smooth",
        }));
        commandMap.set("chat.scrollbottom", () => chatE.scrollTo({
            left: chatE.scrollLeft,
            top: chatE.scrollHeight,
            behavior: "smooth",
        }));
        commandMap.set("chat.showall", () => {
            Array.from(chatE.children).forEach(x => x.Show());
            setHideNone();
        });
        commandMap.set("chat.hideall", () => {
            Array.from(chatE.children).forEach(x => x.Hide());
            setHideNone();
        });



        // Dispatches `{Type}` messages from the parent window to commandMap (origin is only logged, not checked).
        window.addEventListener("message", ev => {
            const evd = ev.data;
            if (!evd)
                return;
            const evt = evd.Type;
            if (!evt)
                return;
            const evf = commandMap.get(evt);
            if (evf) {
                console.log("Got command \"" + evt + "\" from " + ev.origin);
                evf();
            }
        });


        /**
        * A function that checks if we can listen to user audio (open mic): speech recognition supported and not mobile.
        * @returns {boolean} True if we can listen to user audio
        */
        function canListen() {
            if (!SpeechInput.supportSpeechRec())
                return false;
            if (!SpeechInput.supportOpenMic())
                return false;
            return true;
        }


        // Set up speech generation
        /** Server voice settings by sender name (`data.Voices`, entry with null Name is the fallback). @type {Map<?string, object>} */
        const userVoices = new Map();
        /** True if the server sent `Voices` (speech synthesis possible). @type {boolean} */
        const userSpeak = !!data.Voices;
        if (userSpeak) {
            for (const voice of data.Voices)
                userVoices.set(voice.Name, voice);
        }

        /**
        * A function that checks if we can use speech generation (server sent `Voices` and speech synthesis is supported)
        * @returns {boolean} True if we can use speech generation
        */
        function canSpeak() {
            return userSpeak && SpeechGen.supportSpeechGen();
        }

        /**
         * Get a language text string from a language iso id
         * @param {string} langId A language id, like: "en"
         * @returns {string} A string like "English [en]" or "[xx]" for unknown language ids
         * Requires `languageMap` (only loaded when `CanTranslate`).
         */
        function getLangText(langId) {
            var mLang = languageMap.get(langId);
            return mLang ? (mLang.Name + " [" + langId + "]") : ("[" + langId + "]");
        }

        /**
         * Add server side menu items: recursively converts server `ChatMenuItem[]` (`Id, Name, Desc, Icon, Value, Children`)
         * into `WebMenuItem`s. Leaf items send `SetValue {ChatId, Key: Id, Value, Id: messageId}` (Fail on false/exception)
         * and then call `close()`.
         * @param {?WebMenuItem[]} dest The destination item array (created if null)
         * @param {?Array<object>} menuList Array of server `ChatMenuItem`s to add (nothing added if null/empty)
         * @param {?number} messageId The message id (if we're adding it to a message menu), defaults to 0
         * @param {function():void} close Function that when invoked closes pop-up menu
         * @returns {?WebMenuItem[]} The destination item array (unchanged `dest` when there is nothing to add)
         */
        function addRec(dest, menuList, messageId, close) {
            if (!menuList)
                return dest;
            const l = menuList.length;
            if (l <= 0)
                return dest;
            messageId = messageId ?? 0;
            if (!dest)
                dest = [];
            for (let mi = 0; mi < l; ++mi) {
                const menu = menuList[mi]
                const key = menu.Id;
                const value = menu.Value;
                const children = menu.Children;
                const item = WebMenuItem.From({
                    Name: menu.Name,
                    IconClass: menu.Icon,
                    Title: menu.Desc,
                    Data: children ? null : async () => {

                        try {
                            if (!await sendRequest(apiBase + "SetValue", {
                                ChatId: chatId,
                                Key: key,
                                Value: value,
                                Id: messageId,

                            }))
                                Fail(_T("Failed to set chat value \"{0}\".", key, "An error message shown when a user tries to set a server side value but the server returns a failure.{0} is replaced by the value key name."));
                        }
                        catch (e) {
                            Fail(_T("Failed to set chat value \"{0}\".", key, "An error message shown when a user tries to set a server side value but the server returns a failure.{0} is replaced by the value key name.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                        }
                        close();
                    },
                });
                dest.push(item);
                item.Children = addRec(item.Children, children, messageId, close);
            }
            return dest;
        }

        /** Attachment extensions shown as sandboxed iframes by `UpdateData`. @type {Readonly<Object<string, number>>} */
        const htmlExtensions = Object.freeze({
            html: 1,
            htm: 1,
        });

        /**
         * Add a message to the chat window. Creates the row structure
         * `SysWeaver-ChatRow > SysWeaver-ChatMsg > (SysWeaver-ChatHeader > [SysWeaver-ChatLanguage], SysWeaver-ChatName,
         * SysWeaver-ChatTime, SysWeaver-ChatIcon), SysWeaver-ChatContent, SysWeaver-ChatData`.
         * The row is inserted first if `onTop`, else before the last child when `canPost` (the preview row stays last),
         * else appended. Adds class `Mine` for own messages, sender colors (if `options.UserColor`), `Hidden` for hidden
         * senders, header click (toggle hide/show + `setHideFrom`, not for the preview) and context menus.
         * Expando members on the row: `Msg`, `Body`, `Translated`, `UpdateLang`, `UpdateBody`, `UpdateData`,
         * `UpdateFormat`, `Show`, `Hide`. Renders time, body and data before returning.
         * @param {object} m The message data to add (`ChatMessage`: Id, From, FromImage, Time, Flags, Text, Data, Format, Lang, MenuItems)
         * @param {boolean} onTop If true, the message is added to the top, else to the bottom
         * @param {boolean} isPreview If true, the message is the preview message
         * @returns {Promise<HTMLElement>} The `SysWeaver-ChatContent` element of the new row
         */
        async function addMessage(m, onTop, isPreview) {
            const msgRow = document.createElement("SysWeaver-ChatRow");
            const f = onTop ? chatE.firstElementChild : (canPost ? chatE.lastElementChild : null);
            if (f)
                chatE.insertBefore(msgRow, f);
            else
                chatE.appendChild(msgRow);
            msgRow.Msg = m;

            const msgE = document.createElement("SysWeaver-ChatMsg");
            msgRow.appendChild(msgE);

            const isMine = m.From === userName;
            if (isMine)
                msgRow.classList.add("Mine");

            const cols = options.UserColor ? Chat.chatColor(m.From) : null;
            //            msgE.style.backgroundColor = cols[1];
            if (cols)
                msgE.style.borderColor = cols[0];

            //  Create elements
            const msgHeader = document.createElement("SysWeaver-ChatHeader");
            const msgName = document.createElement("SysWeaver-ChatName");
            const msgLanguage = canTranslate ? document.createElement("SysWeaver-ChatLanguage") : null;
            const msgTime = document.createElement("SysWeaver-ChatTime");
            const msgIcon = document.createElement("SysWeaver-ChatIcon");
            const msgBody = document.createElement("SysWeaver-ChatContent");
            const msgData = document.createElement("SysWeaver-ChatData");
            msgBody.tabIndex = "0";

            let isH = isPreview ? false : isHidden(m.From);
            msgHeader.IsHidden = isH;
            const tf = isPreview ? _TF("This is a preview of a message, it's not visible to anyone but you", "Tool tip description letting the user know what this is") : ValueFormat.stringFormat(isH ? titleHidden : titleVisible, m.From);
            msgHeader.title = tf;
            msgName.title = tf;
            if (isH) {
                msgBody.classList.add(hiddenClass);
                msgData.classList.add(hiddenClass);
            }

            /**
             * Context menu handler for a message: opens the message `PopUpMenu` at the sender icon (root gets class `Chat`;
             * a `ResizeObserver` on `body` keeps it aligned). Items: preview -> "Attach file" (if uploads), copy items,
             * "Clear"; others -> "View profile" (`parent.postMessage({Type:"ViewProfile", Name, Image}, "*")` if
             * `CanShowProfile`), copy text / markdown / HTML (by `Format`; HTML items hidden on mobile), copy time stamp,
             * copy user name, hide/show content, server `m.MenuItems` via `addRec`, and "Remove message"
             * (`RemoveMessage {ChatId, MessageId}`; confirmation unless the message has `FlagCanRemove`).
             * Bound to the icon click and to `contextmenu` on header and image.
             * @param {Event} ev - The click / contextmenu event (ignored if `badClick`).
             * @returns {Promise<void>}
             */
            const onMenuClick = async ev => {
                if (badClick(ev))
                    return;
                m = msgRow.Msg;
                const chatElement = msgIcon;
                PopUpMenu(msgIcon, (close, backEl) => {

                    const pp = backEl.parentElement;
                    pp.classList.add("Chat");
                    new ResizeObserver(() => {
                        const cr = chatElement.getBoundingClientRect();
                        pp.style.left = cr.right + "px";
                    }).observe(document.body);

                    /**
                     * Copies text to the clipboard with an info toast (`ValueFormat.copyToClipboardInfo`), then closes the menu.
                     * @param {string} text - Text to copy.
                     * @returns {Promise<void>}
                     */
                    async function Copy(text) {
                        await ValueFormat.copyToClipboardInfo(text);
                        close();
                    }

                    const menu = new WebMenu();
                    menu.Name = "ChatMsg";

                    if (isPreview) {
                        if (uploadRepo) {
                            const filesLeft = data.MaxDataCount - GetDataCount();
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Attach file", "Text of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                                Flags: filesLeft <= 0 ? 1 : 0,
                                IconClass: "IconChatAttachFile",
                                Title:
                                    _TF("Attach a file to the current message", "Tool tip description of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                                Data: async () => {
                                    const files = await selectFiles(filesLeft > 1);
                                    if (files)
                                        await uploadFiles(files);
                                    close();
                                },
                            }));
                        }
                    } else {
                        if (data.CanShowProfile) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("View profile", "Name of a menu item that when clicked will view the profile of a web site member"),
                                Flags: 0,
                                IconClass: "IconProfile",
                                Title: _T("View the public profile page for \"{0}\"", m.From, "Tool tip description of a menu item that when clicked will view the profile of a web site member.{0} is replaced with the nick name of the member"),
                                Data: () => {
                                    try {
                                        const smsg = {
                                            Type: "ViewProfile",
                                            Name: m.From,
                                            Image: m.FromImage,
                                        };
                                        parent.postMessage(smsg, "*")
                                        console.log("Send ViewProfile message: " + JSON.stringify(smsg));
                                    }
                                    catch (e) {
                                        console.warn("Failed to send ViewProfile message: " + e.message);
                                    }
                                    close();
                                },
                            }));
                        }
                    }


                    switch (m.Format) {
                        case 0:
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy text", "Name of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Flags: 0,
                                IconClass: "IconCopy",
                                Title: _TF("Copy the message text to the clipboard", "Tool tip description of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Data: async () => await Copy(msgBody.CurrentText),
                            }));
                            break;
                        case 1:
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy markdown (original)", "Name of a menu item that when clicked will copy the original mark down text of chat message to the clipboard"),
                                Flags: 0,
                                IconClass: "IconCopy",
                                Title: _TF("Copy the message markdown text to the clipboard", "Tool tip description of a menu item that when clicked will copy the original mark down text of chat message to the clipboard"),
                                Data: async () => await Copy(msgBody.CurrentText),
                            }));
                            if (!SpeechInput.IsMobile) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy HTML", "Name of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                                    Flags: 0,
                                    IconClass: "IconCopy",
                                    Title: _TF("Copy the formatted HTML to the clipboard", "Tool tip description of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                                    Data: async () => await Copy(msgBody.innerHTML),
                                }));
                            }
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy text", "Name of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Flags: 0,
                                IconClass: "IconCopy",
                                Title: _TF("Copy the message text to the clipboard", "Tool tip description of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Data: async () => await Copy(msgBody.innerText),
                            }));
                            break;
                        case 2:
                            if (!SpeechInput.IsMobile) {
                                menu.Items.push(WebMenuItem.From({
                                    Name: _TF("Copy HTML (original)", "Name of a menu item that when clicked will copy the original HTML code of chat message to the clipboard"),
                                    Flags: 0,
                                    IconClass: "IconCopy",
                                    Title: _TF("Copy the formatted HTML to the clipboard", "Tool tip description of a menu item that when clicked will copy the generated HTML code of chat message to the clipboard"),
                                    Data: async () => await Copy(msgBody.innerHTML),
                                }));
                            }
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Copy text", "Name of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Flags: 0,
                                IconClass: "IconCopy",
                                Title: _TF("Copy the message text to the clipboard", "Tool tip description of a menu item that when clicked will copy the text of chat message to the clipboard"),
                                Data: async () => await Copy(msgBody.innerText),
                            }));
                            break;
                    }
                    if (isPreview) {
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Clear", "Name of a menu item that when clicked will clear the current chat message"),
                            Flags: 0,
                            IconClass: "IconChatClearCurrent",
                            Title: _TF("Clear the current message text and any data", "Tool tip description of a menu item that when clicked will clear the current chat message"),
                            Data: async () => {
                                await clearCurrentMessage();
                                close();
                            },
                        }));
                    } else {
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Copy time stamp", "Name of a menu item that when clicked will copy the time stamp of chat message to the clipboard"),
                            Flags: 0,
                            IconClass: "IconCopy",
                            Title: _T("Copy the time stamp \"{0}\" to the clipboard", m.Time, "Tool tip description of a menu item that when clicked will copy the time stamp of chat message to the clipboard.{0} is replaced with the time stamp"),
                            Data: async () => await Copy(m.Time),
                        }));
                        menu.Items.push(WebMenuItem.From({
                            Name: _TF("Copy user name", "Name of a menu item that when clicked will copy the user name of chat message to the clipboard"),
                            Flags: 0,
                            IconClass: "IconCopy",
                            Title: _T("Copy the user name \"{0}\" to the clipboard", m.From, "Tool tip description of a menu item that when clicked will copy the user name of chat message to the clipboard.{0} is replaced with the name of the user that posted the message"),
                            Data: async () => await Copy(m.From),
                        }));


                        if (msgHeader.IsHidden) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Show content", "Name of a menu item that when clicked will show the content of a chat message that is currently hidden"),
                                Flags: 0,
                                IconClass: "IconChatShow",
                                Title: _T("Show the message content and all new messages from \"{0}\"", m.From, "Tool tip description of a menu item that when clicked will show the content of a chat message that is currently hidden.{0} is replaced with the name of the user"),
                                Data: () => {
                                    msgRow.Show();
                                    setHideFrom(m.From, false);
                                    close();
                                },
                            }));

                        } else {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Hide content", "Name of a menu item that when clicked will hide the content of a chat message"),
                                Flags: 0,
                                IconClass: "IconChatHide",
                                Title: _T("Hide the message content and all new messages from \"{0}\"", m.From, "Tool tip description of a menu item that when clicked will hide the content of a chat message.{0} is replaced with the name of the user"),
                                Data: () => {
                                    msgRow.Hide();
                                    setHideFrom(m.From, true);
                                    close();
                                },
                            }));
                        }

                        addRec(menu.Items, m.MenuItems, m.Id, close);
                        const canClear = data.CanClear
                        const canRemove = ((m.Flags & Chat.FlagCanRemove) !== 0);
                        if (canClear || canRemove) {
                            menu.Items.push(WebMenuItem.From({
                                Name: _TF("Remove message", "Name of a menu item that when clicked will remove a chat message"),
                                Flags: 0,
                                IconClass: "IconChatRemove",
                                Title: _TF("Remove this message from the chat", "Tool tip description of a menu item that when clicked will remove a chat message"),
                                Data: async () => {
                                    if (!canRemove) {
                                        if (!await Confirm(
                                            _TF("Remove message", "Title of a confirmation dialog that when confirmed will remove a chat message"),
                                            _TF("Are you sure that you wan't to remove this message?", "Text of a confirmation dialog that when confirmed will remove a chat message"),
                                            _TF("YES, remove", "Text of a button on a confirmation dialog that when clicked will remove a chat message"),
                                            _TF("NO, keep it", "Text of a button on a confirmation dialog that when clicked will keep a chat message as opposed to remove it"),
                                            "IconChatRemove", "IconChatKeep",
                                            _TF("Click to remove the message, this can't be undone", "Tool tip description of a button on a confirmation dialog that when clicked will remove a chat message"),
                                            _TF("Click to keep the message", "Tool tip description of a button on a confirmation dialog that when clicked will keep a chat message as opposed to remove it"),
                                        )) {
                                            close();
                                            return;
                                        }
                                    }
                                    try {
                                        if (!await sendRequest(apiBase + "RemoveMessage", {
                                            ChatId: chatId,
                                            MessageId: m.Id
                                        }))
                                            Fail(_TF("Failed to remove the chat message.", "An error message shown when a user tries to remove a chat message but the server returns a failure."));
                                    }
                                    catch (e) {
                                        Fail(_TF("Failed to remove the chat message.", "An error message shown when a user tries to remove a chat message but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                    }
                                    close();
                                },
                            }));
                        }

                    }
                    return menu;
                }, isMine);
            };






            //  Header
            msgE.appendChild(msgHeader);
            if (cols) {
                msgHeader.style.backgroundColor = cols[1];
                msgHeader.style.color = cols[2];
            }
            msgHeader.title = isPreview ? _TF("Preview", "Tool tip description indicating that this is a chat preview message") : ("#" + m.Id);
            /**
             * Header context menu: opens the message menu (`onMenuClick`) instead of the browser menu.
             * @param {MouseEvent} ev - The contextmenu event.
             * @returns {boolean} true to allow the default menu (bad click), false to suppress it.
             */
            msgHeader.oncontextmenu = ev => {
                if (badClick(ev, true))
                    return true;
                onMenuClick(ev);
                return false;
            };
            //  Header language
            if (msgLanguage) {
                keyboardClick(msgLanguage);
                const langFlag = createImg("img");
                msgLanguage.appendChild(langFlag);

                /**
                 * Expando (translation enabled): sets the flag image to `m.Lang` and the tooltip, and (re)binds the flag click.
                 * Preview row: the flag click opens `selectLanguage`. Other rows: the flag click toggles between the original
                 * and the translation from `Translate {ChatId, MessageId}` (cached in `msgRow.Translated`), toggling classes
                 * `Translating` (while requesting) and `Translated` and switching the flag to `data.Lang`; `Fail` on error.
                 * Note: uses the `m` captured when the row was created (the preview message object is shared and mutated).
                 */
                msgRow.UpdateLang = () => {
                    langFlag.src = "../iso_data/language/" + m.Lang + ".svg";
                    const mLang = getLangText(m.Lang);
                    const cLang = getLangText(currentLang);
                    const dLang = getLangText(data.Lang);
                    if (isPreview) {
                        msgLanguage.title =
                            _TF("This should be set to the language used in your text.", "Tool tip description indicating that the language specified should match the one used to write the text") +
                            "\n\n" +
                            _T("If the text is not written in \"{0}\", click to specify a different language", cLang, "Tool tip description for a button that when clicked will show a list of languages that the user can select.{0} is the current language")
                            ;
                        /**
                         * Preview flag click: opens the input language selection.
                         * @param {MouseEvent} ev - The click event (ignored if `badClick`).
                         * @returns {Promise<void>}
                         */
                        msgLanguage.onclick = async ev => {
                            if (badClick(ev))
                                return;
                            await selectLanguage();
                        };
                    } else {

                        const msgLangTitle0 =
                            _T("Message was probably written using \"{0}\"", mLang, "A tool tip description indicating what language was used to write a chat message.{0} is replaced by the ISO code of the language") +
                            "\n" +
                            _T("Click to translate the message to \"{0}\".", dLang, "A tool tip description on a button that when pressed will translate a chat message to the specified language.{0} is replaced by the ISO code of the language");

                        const msgLangTitle1 =
                            _T("Message is translated to \"{0}\".", dLang, "A tool tip description indicating that a chat message have been translated.{0} is replaced by the ISO code of the language") +
                            "\n" +
                            _T("Click to show the original message probably written using \"{0}\".", mLang, "A tool tip description on a button that when pressed will show the original message as opposed to a translated message.{0} is replaced by the ISO code of the language");


                        msgLanguage.title = msgLangTitle0;
                        /** True while a translate toggle is running (ignores further clicks). @type {boolean} */
                        let isTranslating = false;
                        /**
                         * Message flag click: toggles between the original text and the (cached or requested) translation.
                         * @param {MouseEvent} ev - The click event (ignored if `badClick`).
                         * @returns {Promise<void>}
                         */
                        msgLanguage.onclick = async ev => {
                            if (badClick(ev))
                                return;
                            if (isTranslating)
                                return;
                            isTranslating = true;
                            try {
                                const mm = msgRow.Msg;
                                if (msgLanguage.IsTranslated) {
                                    msgRow.UpdateBody(mm.Text, mm.Format, (m.Flags & Chat.FlagIsWorking) !== 0);
                                    msgLanguage.title = msgLangTitle0;
                                    msgLanguage.IsTranslated = false;
                                    msgLanguage.classList.remove("Translated");
                                    langFlag.src = "../iso_data/language/" + mm.Lang + ".svg";
                                } else {
                                    let tr = msgRow.Translated;
                                    if (!tr) {
                                        msgLanguage.classList.add("Translating");
                                        tr = await sendRequest(apiBase + "Translate", {
                                            ChatId: chatId,
                                            MessageId: mm.Id,
                                        });
                                        msgRow.Translated = tr;
                                    }
                                    msgRow.UpdateBody(tr, mm.Format, (m.Flags & Chat.FlagIsWorking) !== 0);
                                    msgLanguage.title = msgLangTitle1;
                                    msgLanguage.IsTranslated = true;
                                    msgLanguage.classList.add("Translated");
                                    langFlag.src = "../iso_data/language/" + data.Lang + ".svg";
                                }
                            }
                            catch (e) {
                                Fail(_TF("Failed to translate the message.", "An error message shown when a user tries to translate a chat message but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                            }
                            finally {
                                msgLanguage.classList.remove("Translating");
                                isTranslating = false;
                            }
                        };
                    }
                };
                msgRow.UpdateLang();
            } else {
                /** Expando (no translation): no-op. */
                msgRow.UpdateLang = () => { };
            }

            //  Header name
            msgName.innerText = m.From;

            //  Header time
            msgTime.Value = m.Time;



            //  Header icon: the sender image (own image rewritten to ../auth/UserImages/Current/small), falling back to an IconMenu ColorIcon
            let fi = m.FromImage;
            if (fi === currentImage)
                fi = "../auth/UserImages/Current/small";

            const ttt = _TF("Click to show message menu", "Tool tip description on a button that when clicked will show a menu with options for a chat message");
            if (fi) {
                const img = document.createElement("img"); // Do not use createImg here since we're using SetImageSource.
                msgIcon.appendChild(img);
                SetImageSource(img, fi, null, false, isOk => {
                    if (isOk) {
                        img.title = ttt;
                        img.onclick = onMenuClick;
                        img.oncontextmenu = ev => {
                            if (badClick(ev, true))
                                return true;
                            onMenuClick(ev);
                            return false;
                        };
                        keyboardClick(img);
                    } else {
                        img.remove();
                        const clIcon = new ColorIcon("IconMenu", "IconColorThemeAcc1", 32, 32, ttt, onMenuClick, null, null, true);
                        msgIcon.appendChild(clIcon.Element);
                    }
                });
            } else {
                const clIcon = new ColorIcon("IconMenu", "IconColorThemeAcc1", 32, 32, ttt, onMenuClick, null, null, true);
                msgIcon.appendChild(clIcon.Element);
            }
            if (msgLanguage)
                msgHeader.appendChild(msgLanguage);
            msgHeader.appendChild(msgName);
            msgHeader.appendChild(msgTime);
            msgHeader.appendChild(msgIcon);

            //  Body
            msgE.appendChild(msgBody);
            msgRow.Body = msgBody;
            /**
             * Expando: renders the message body. Toggles class `Working` on the row; if text or format changed, renders into
             * `SysWeaver-ChatContent` by format: 0 -> `innerText`, 1 -> `innerHTML = await ValueFormat.MarkDownToHTML(text)`,
             * 2 -> raw `innerHTML`, with a format tooltip; falls back to `innerText` on error. Stores `CurrentText` /
             * `CurrentFormat` on the content element.
             * @param {?string} newText - The text to show (empty clears the body).
             * @param {number} newFormat - 0 text, 1 markdown, 2 HTML.
             * @param {boolean} isWorking - True while the message is still being produced (`FlagIsWorking`).
             * @returns {Promise<void>}
             */
            msgRow.UpdateBody = async (newText, newFormat, isWorking) => {
                if (msgRow.classList.contains(workingAttribute) !== isWorking) {
                    if (isWorking)
                        msgRow.classList.add(workingAttribute);
                    else
                        msgRow.classList.remove(workingAttribute);
                }
                if (msgBody.CurrentText === newText)
                    if (msgBody.CurrentFormat === newFormat)
                        return;
                msgBody.CurrentText = newText;
                msgBody.CurrentFormat = newFormat;
                if ((!newText) || (newText.length <= 0)) {
                    msgBody.innerText = "";
                    return;
                }
                try {
                    switch (newFormat) {
                        case 0:
                            msgBody.title = _TF("Message is plain text", "A tool tip for a chat message that is written using plain text");
                            msgBody.innerText = newText;
                            break;
                        case 1:
                            msgBody.title = _TF("Message is markdown", "A tool tip for a chat message that is written using Mark Down (MD)");
                            msgBody.innerHTML = await ValueFormat.MarkDownToHTML(newText);
                            break;
                        case 2:
                            msgBody.title = _TF("Message is HTML", "A tool tip for a chat message that is written using HTML");
                            msgBody.innerHTML = newText;
                            break;
                    }
                }
                catch
                {
                    msgBody.innerText = newText;
                }
            };
            //  Data
            msgE.appendChild(msgData);
            /**
             * Expando: rebuilds `SysWeaver-ChatData` from a `;`-separated URL list (skipped if unchanged). Per URL a
             * `<SysWeaver-ChatHolder>` is created: image -> `<img>` (click opens with `Open`); `.html/.htm` -> sandboxed
             * `<iframe>` (`allow-forms allow-presentation allow-scripts allow-same-origin allow-downloads allow-popups`,
             * src through `appendTheme`); other -> `../icons/ext/<ext>.svg` plus a `<SysWeaver-ChatLabel>` (copies the URL on
             * click), class `Extension`, click downloads (`downloadFile`). Clickable holders get class `Clickable`.
             * Each holder has a `<SysWeaver-ChatExpand>` with a menu `ColorIcon` that opens a `PopUpMenu` (root class `ChatData`):
             * preview only "Copy image Mark Down" (markdown mode, images), "Copy url", "Delete file" (removes it from the data
             * and deletes a temp upload via `../Api/UserStorage/DeleteStoredFile`), "Hide file" (removes it from the data and
             * copies a markdown reference); "Open in new tab" (non forced-download items); "Download" (temporary `<a download>`);
             * with `canPost` a "Store file" submenu (`StoreFile {Url, Scope}`: Private 0, Protected 1, Public also sends 1 and
             * is only shown if `AllowPublicStore`; not for HTML or URLs already under `../storage/<UserStore>/`) and a
             * "Create link" submenu (`StoreLink {Url, Scope 0/1/2}`, non absolute URLs only). Store results are made absolute
             * (`GetAbsolutePath`), copied to the clipboard and opened.
             * @param {?string} newData - `;`-separated list of `../`-prefixed URLs, or null/empty to clear.
             */
            msgRow.UpdateData = newData => {
                if (!newData)
                    newData = null;
                if (msgData.CurrentData === newData)
                    return;
                msgData.CurrentData = newData;
                if ((!newData) || (newData.length <= 0)) {
                    msgData.innerText = "";
                    return;
                }
                msgData.innerText = "";
                const parts = newData.split(';');
                const pl = parts.length;
                for (let i = 0; i < pl; ++i) {
                    const orgP = parts[i].trim();
                    const pr = orgP.substring(3);
                    const p = GetAbsolutePath(orgP);
                    const pp = p.indexOf('?');
                    const pbase = pp < 0 ? p : p.substring(0, pp);
                    const pep = pbase.lastIndexOf('.');
                    const ext = pep >= 0 ? pbase.substring(pep + 1).toLowerCase() : "";
                    const baseName = pbase.substring(pbase.lastIndexOf('/') + 1, pep >= 0 ? pep : pbase.length);
                    let element = null;
                    let elements = null;
                    let canDownload = false;
                    let canSave = !orgP.startsWith("../storage/" + data.UserStore + "/");
                    let canLink = orgP.indexOf("://") < 0;
                    let forceDownload = false;
                    let extraClass = null;
                    const isImage = imageExtensions[ext];
                    if (isImage) {
                        element = createImg("img");
                        element.src = p;
                        canDownload = true;
                    } else {
                        if (htmlExtensions[ext]) {
                            element = createIFrame();
                            element.sandbox = "allow-forms allow-presentation allow-scripts allow-same-origin allow-downloads allow-popups";
                            element.src = appendTheme(p);
                            canSave = false;
                        } else {
                            element = createImg("img");
                            extraClass = "Extension";
                            element.src = "../icons/ext/" + ext + ".svg";
                            canDownload = true;
                            forceDownload = true;

                            const label = document.createElement("SysWeaver-ChatLabel");
                            label.innerText = baseName;
                            label.title = p;
                            ValueFormat.copyOnClick(label, p, true, true);
                            elements = [element, label];
                        }
                    }
                    if (element) {
                        const iframeHolder = document.createElement("SysWeaver-ChatHolder");
                        const iframeExpand = document.createElement("SysWeaver-ChatExpand");
                        if (extraClass)
                            iframeHolder.classList.add(extraClass);
                        if (canDownload) {

                            iframeHolder.classList.add("Clickable");
                            element.onclick = ev => {
                                if (badClick(ev))
                                    return;
                                if (forceDownload)
                                    downloadFile(null, p);
                                else
                                    Open(p);
                                //open(p);
                            };
                            if (forceDownload)
                                element.title = _T('Click to download "{0}"', p, "Tool tip description on some content that when clicked will download the content.{0} is replaced with the url");
                            else
                                element.title = _T('Click to open "{0}" in a new tab', p, "Tool tip description on some content that when clicked will open the content in a new browser tab.{0} is replaced with the url");
                            keyboardClick(element);
                        }

                        const iframeIcon = new ColorIcon("IconMenu", "IconColorThemeAcc2", 32, 32, _TF("Click to show menu", "Tool tip description of a button that when clicked will show a context menu with options"), () => {

                            PopUpMenu(iframeExpand, (close, backEl) => {

                                const pp = backEl.parentElement;
                                pp.classList.add("ChatData");

                                const menu = new WebMenu();
                                menu.Name = "ChatData";
                                if (isPreview) {
                                    if (isImage) {
                                        if (useMarkDown) {
                                            menu.Items.push(WebMenuItem.From({
                                                Name: _TF("Copy image Mark Down", "Text of a menu item that when clicked will copy the mark down required to display this image"),
                                                Flags: 0,
                                                IconClass: "IconChatCopyImage",
                                                Title: _TF("Click to copy the Mark Down required to show the file to the clipboard", "Tool tip description of a menu item that when clicked will copy the mark down required to display this image"),
                                                Data: () => {
                                                    ValueFormat.copyToClipboardInfo("![](" + orgP + ")");
                                                    close();
                                                },
                                            }));
                                        }
                                    }
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Copy url", "Text of a menu item that when clicked will copy the url text"),
                                        Flags: 0,
                                        IconClass: "IconChatCopy",
                                        Title: _TF("Click to copy the url to the clipboard", "Tool tip description of a menu item that when clicked will copy the url text"),
                                        Data: () => {
                                            ValueFormat.copyToClipboardInfo(p);
                                            close();
                                        },
                                    }));
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Delete file", "Text of a menu item that when clicked will delete the file from the server"),
                                        Flags: 0,
                                        IconClass: "IconChatDeleteFile",
                                        Title: _TF("Click to delete this file from the server", "Tool tip description of a menu item that when clicked will delete the file from the server"),
                                        Data: async () => {
                                            const np = [];
                                            for (let ii = 0; ii < pl; ++ii) {
                                                if (parts[ii] !== orgP)
                                                    np.push(parts[ii]);
                                            }
                                            const m = np.length > 0 ? np.join(';') : null;
                                            previewMessage.Data = m;
                                            msgRow.UpdateData(m);
                                            if (tempFiles.get(pr)) {
                                                tempFiles.delete(pr);
                                                try {
                                                    if (!await sendRequest(apiBase + "../Api/UserStorage/DeleteStoredFile", pr))
                                                        console.warn('Failed to delete stored file "' + pr + '".');
                                                }
                                                catch (e) {
                                                    console.warn('Failed to delete stored file "' + pr + '", error: ' + e.message);
                                                }
                                            }
                                            previewChanged();
                                            close();
                                        },
                                    }));
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Hide file", "Text of a menu item that when clicked will hide the file"),
                                        Flags: 0,
                                        IconClass: "IconChatHideFile",
                                        Title: _TF("Click to hide this file", "Tool tip description of a menu item that when clicked will hide the file"),
                                        Data: async () => {
                                            parts.splice(i, 1);
                                            let m = parts.join(';');
                                            if (!m)
                                                m = null;
                                            const pr = p.substring(3);
                                            previewMessage.Data = m;
                                            msgRow.UpdateData(m);
                                            previewChanged();
                                            if (isImage)
                                                ValueFormat.copyToClipboardInfo("![](" + orgP + ")");
                                            else
                                                ValueFormat.copyToClipboardInfo("[" + baseName + "](" + p + ")");
                                            close();
                                        },
                                    }));

                                }

                                if (!forceDownload) {
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Open in new tab", "Text of a menu item that when clicked will open some content in a new browser tab"),
                                        Flags: 0,
                                        IconClass: "IconChatExpand",
                                        Title: _TF("Click to show in a new tab", "Tool tip description of a menu item that when clicked will open some content in a new browser tab"),
                                        Data: () => {
                                            open(p, "_blank");
                                            close();
                                        },
                                    }));
                                }
                                if (canDownload) {
                                    menu.Items.push(WebMenuItem.From({
                                        Name: _TF("Download", "Text of menu item that when clicked will download some content to a file"),
                                        Flags: 0,
                                        IconClass: "IconChatSave",
                                        Title: _TF("Save the file to your device", "Tool tip description of menu item that when clicked will download some content to a file"),
                                        Data: () => {
                                            const del = document.createElement('a');
                                            del.setAttribute("href", p);
                                            del.setAttribute('download', '');
                                            del.style.display = 'none';
                                            document.body.appendChild(del);
                                            del.click();
                                            document.body.removeChild(del);
                                            close();
                                        },
                                    }));
                                }
                                if (canPost) {

                                    if (canSave) {
                                        const storeItems = [];
                                        storeItems.push(WebMenuItem.From({
                                            Name: _TF("Private", "Text of menu item that indicates that this is private content, only accessable to the user"),
                                            Flags: 0,
                                            IconClass: "IconChatLink0",
                                            Title: _TF("Store the file on the server for later use, only available to you", "Tool tip description of menu item that when clicked will store some content on the server"),
                                            Data: async () => {
                                                try {
                                                    const stored = await sendRequest(apiBase + "StoreFile", {
                                                        Url: p,
                                                        Scope: 0,
                                                    });
                                                    if (stored) {
                                                        const str = GetAbsolutePath(stored);
                                                        await ValueFormat.copyToClipboardInfo(str);
                                                        Open(str);
                                                    } else
                                                        Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure."));
                                                }
                                                catch (e) {
                                                    Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                }
                                                close();
                                            },
                                        }));
                                        storeItems.push(WebMenuItem.From({
                                            Name: _TF("Protected", "Text of menu item that indicates that this is protected content, only accessable to logged in users"),
                                            Flags: 0,
                                            IconClass: "IconChatLink1",
                                            Title: _TF("Store the file on the server for later use, available to any logged in users", "Tool tip description of menu item that when clicked will store some content on the server"),
                                            Data: async () => {
                                                try {
                                                    const stored = await sendRequest(apiBase + "StoreFile", {
                                                        Url: p,
                                                        Scope: 1,
                                                    });
                                                    if (stored) {
                                                        const str = GetAbsolutePath(stored);
                                                        await ValueFormat.copyToClipboardInfo(str);
                                                        Open(str);
                                                    } else
                                                        Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure."));
                                                }
                                                catch (e) {
                                                    Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                }
                                                close();
                                            },
                                        }));
                                        if (data.AllowPublicStore) {
                                            storeItems.push(WebMenuItem.From({
                                                Name: _TF("Public", "Text of menu item that indicates that this is public content, accessable to everyone"),
                                                Flags: 0,
                                                IconClass: "IconChatLink2",
                                                Title: _TF("Store the file on the server for later use, available to anyone", "Tool tip description of menu item that when clicked will store some content on the server"),
                                                Data: async () => {
                                                    try {
                                                        const stored = await sendRequest(apiBase + "StoreFile", {
                                                            Url: p,
                                                            Scope: 1,
                                                        });
                                                        if (stored) {
                                                            const str = GetAbsolutePath(stored);
                                                            await ValueFormat.copyToClipboardInfo(str);
                                                            Open(str);
                                                        } else
                                                            Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure."));
                                                    }
                                                    catch (e) {
                                                        Fail(_TF("Failed to store the file.", "An error message shown when a user tries to store some content on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                    }
                                                    close();
                                                },
                                            }));
                                        }
                                        if (storeItems.length > 0) {
                                            menu.Items.push(WebMenuItem.From({
                                                Name: _TF("Store file", "Text of a menu folder that contains items that when clicked store this file to the server"),
                                                Flags: 0,
                                                IconClass: "IconChatStore",
                                                Title: _TF("Store the file on the server for later use", "Tool tip description of menu folder that contains items that when clicked store this file to the server"),
                                                Children: storeItems
                                            }));
                                        }
                                    }

                                    if (canLink) {
                                        const linkItems = [];

                                        linkItems.push(WebMenuItem.From({
                                            Name: _TF("Private", "Text of menu item that indicates that this is private content, only accessable to the user"),
                                            Flags: 0,
                                            IconClass: "IconChatLink0",
                                            Title: _TF("Save as a link that only you can view", "Tool tip description of menu item that when clicked will store some content on the server and create a private (only accesible by this user) link to it"),
                                            Data: async () => {
                                                try {
                                                    const stored = await sendRequest(apiBase + "StoreLink", {
                                                        Url: p,
                                                        Scope: 0,
                                                    });
                                                    if (stored) {
                                                        const str = GetAbsolutePath(stored);
                                                        await ValueFormat.copyToClipboardInfo(str);
                                                        Open(str);
                                                    } else
                                                        Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure."));
                                                }
                                                catch (e) {
                                                    Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                }
                                                close();
                                            },
                                        }));
                                        linkItems.push(WebMenuItem.From({
                                            Name: _TF("Protected", "Text of menu item that indicates that this is protected content, only accessable to logged in users"),
                                            Flags: 0,
                                            IconClass: "IconChatLink1",
                                            Title: _TF("Save as a link that all logged in users can view", "Tool tip description of menu item that when clicked will store some content on the server and create a protected (only accesible to logged in users) link to it"),
                                            Data: async () => {
                                                try {
                                                    const stored = await sendRequest(apiBase + "StoreLink", {
                                                        Url: p,
                                                        Scope: 1,
                                                    });
                                                    if (stored) {
                                                        const str = GetAbsolutePath(stored);
                                                        await ValueFormat.copyToClipboardInfo(str);
                                                        Open(str);
                                                    } else
                                                        Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure."));
                                                }
                                                catch (e) {
                                                    Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                }
                                                close();
                                            },
                                        }));
                                        if (data.AllowPublicStore) {
                                            linkItems.push(WebMenuItem.From({
                                                Name: _TF("Public", "Text of menu item that indicates that this is public content, accessable to everyone"),
                                                Flags: 0,
                                                IconClass: "IconChatLink2",
                                                Title: _TF("Save as a link that anyone can view", "Tool tip description of menu item that when clicked will store some content on the server and create a public (accesible to anyone) link to it"),
                                                Data: async () => {
                                                    try {
                                                        const stored = await sendRequest(apiBase + "StoreLink", {
                                                            Url: p,
                                                            Scope: 2,
                                                        });
                                                        if (stored) {
                                                            const str = GetAbsolutePath(stored);
                                                            await ValueFormat.copyToClipboardInfo(str);
                                                            Open(str);
                                                        } else
                                                            Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure."));
                                                    }
                                                    catch (e) {
                                                        Fail(_TF("Failed to store the link.", "An error message shown when a user tries to store some content and create a link to it on the server but the server returns a failure.") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
                                                    }
                                                    close();
                                                },
                                            }));
                                        }
                                        if (linkItems.length > 0) {
                                            menu.Items.push(WebMenuItem.From({
                                                Name: _TF("Create link", "Text of a menu folder that contains items that when clicked will create a link to this file"),
                                                Flags: 0,
                                                IconClass: "IconChatLink",
                                                Title: _TF("Create a link to the file for later use", "Tool tip description of menu folder that contains items that when clicked will create a link to this file"),
                                                Children: linkItems
                                            }));
                                        }
                                    }
                                }
                                return menu;
                            }, true);


                        }, null, null, true);



                        iframeExpand.appendChild(iframeIcon.Element);
                        if (elements)
                            elements.forEach(x => iframeHolder.appendChild(x));
                        else
                            iframeHolder.appendChild(element);
                        iframeHolder.appendChild(iframeExpand);
                        msgData.appendChild(iframeHolder);
                    }
                }
            };

            //  Timestamp
            /**
             * Expando: renders the header time. Clears a pending `msgTime.Updater` timer, then uses
             * `ValueFormat.updateDateTime` (absolute, when `showTimeStamp`) or `ValueFormat.updateDateTimeLive` ("x ago").
             * Uses the `m` captured at row creation (`m.Time`).
             */
            msgRow.UpdateFormat = () => {
                const up = msgTime.Updater;
                if (up) {
                    clearTimeout(up);
                    msgTime.Updater = null;
                }
                if (showTimeStamp)
                    ValueFormat.updateDateTime(msgTime, m.Time, null, null, false, null, null, false, true);
                else
                    ValueFormat.updateDateTimeLive(msgTime, m.Time, ["", "", _TF("{0} ago", "Text that indicates how old a chat message is.{0} is replaced with the elapsed time, examples: \"10 minutes\", \"5.4 seconds\", \"3 days\" or \"1 hour\"")], null, false, null, null, true);
            };
            /**
             * Expando: shows the content: removes class `Hidden` from content and data, clears `msgHeader.IsHidden` and sets
             * the "click to hide" tooltip. Does not persist (callers use `setHideFrom` / `setHideNone`).
             */
            msgRow.Show = () => {
                msgHeader.IsHidden = false;
                const tf = ValueFormat.stringFormat(titleVisible, m.From);
                msgHeader.title = tf;
                msgName.title = tf;
                msgBody.classList.remove(hiddenClass);
                msgData.classList.remove(hiddenClass);
            };
            /**
             * Expando: hides the content: adds class `Hidden` to content and data, sets `msgHeader.IsHidden` and the
             * "click to show" tooltip. Does not persist (callers use `setHideFrom` / `setHideNone`).
             */
            msgRow.Hide = () => {
                msgHeader.IsHidden = true;
                const tf = ValueFormat.stringFormat(titleHidden, m.From);
                msgHeader.title = tf;
                msgName.title = tf;
                msgBody.classList.add(hiddenClass);
                msgData.classList.add(hiddenClass);
            };


            if (!isPreview) {
                /**
                 * Header click (not for the preview): toggles show/hide of this row and persists it for the sender (`setHideFrom`).
                 * Other rows of the same sender are not updated until reloaded.
                 * @param {MouseEvent} ev - The click event (ignored if `badClick`).
                 */
                msgHeader.onclick = ev => {
                    if (badClick(ev))
                        return;
                    if (msgHeader.IsHidden) {
                        msgRow.Show();
                    } else {
                        msgRow.Hide();
                    }
                    setHideFrom(m.From, msgHeader.IsHidden);
                };
            }

            msgRow.UpdateFormat();
            await msgRow.UpdateBody(m.Text, m.Format, (m.Flags & Chat.FlagIsWorking) !== 0);
            msgRow.UpdateData(m.Data);

            return msgBody;
        }

        /**
         * Find the message element from message id (linear scan of `chatE.children` by `Msg.Id`)
         * @param {number} msgId The message id to find
         * @returns {?HTMLElement} The row element of the message or null if not found
         */
        function findMessageE(msgId) {
            const e = chatE.children;
            const el = e.length;
            for (let i = 0; i < el; ++i) {
                if (e[i].Msg.Id === msgId)
                    return e[i];
            }
            return null;
        }

        /**
         * Run a function for each message element
         * @param {function(HTMLElement)} fn The function to execute once per message element
         */
        function onAllMessages(fn) {
            const e = chatE.children;
            const el = e.length;
            for (let i = 0; i < el; ++i) {
                fn(e[i]);
            }
        }

        /** 
         * Update the message format (time or age)
         * by calling `UpdateFormat()` on every row (after toggling `showTimeStamp`).
         * */
        function updateMessageFormat() {
            onAllMessages(e => e.UpdateFormat());
        }


        //  Handling server side messages
        // Push event names (must match ChatService.Controller.MessageName, case folded): chat.<op>.<chatid lowercased>
        const chatIdL = chatId.toLowerCase();
        const msgClear = "chat.clear." + chatIdL;
        const msgPost = "chat.post." + chatIdL;
        const msgRemove = "chat.remove." + chatIdL;
        const msgReplace = "chat.replace." + chatIdL;

        SessionManager.AddServerEvent(msgClear);
        SessionManager.AddServerEvent(msgPost);
        SessionManager.AddServerEvent(msgRemove);
        SessionManager.AddServerEvent(msgReplace);

        // Push handler: post (append unless the id exists; speak if enabled and from another user), replace (update Msg,
        // body and data; speech continues incrementally), remove (remove the row), clear (empty the list, keep the preview).
        InterOp.AddListener(async ev => {
            const m = InterOp.GetMessage(ev);
            if (!m)
                return;
            const mt = m.Type;
            if (mt === msgPost) {
                const c = m.Chat;
                const me = findMessageE(c.Id);
                if (me)
                    return;
                const msgBdy = await addMessage(c);
                if (enableSpeech) {
                    if (c.From !== userName) {
                        let vp = userVoices.get(c.From);
                        if (!vp)
                            vp = userVoices.get(null);
                        if (vp) {
                            const voice = SpeechGen.findVoice(vp.Language, vp.Male, vp.Voice);
                            msgBdy.Voice = voice;
                            SpeechGen.speakNew(msgBdy.innerText, voice, vp.Pitch, vp.Rate, speechVolume * 0.01);
                        }
                    }
                }
                return;
            }
            if (mt === msgReplace) {
                const c = m.Chat;
                const me = findMessageE(c.Id);
                if (!me)
                    return;
                me.Msg = c;
                const isWorking = (c.Flags & Chat.FlagIsWorking) !== 0;
                await me.UpdateBody(c.Text, c.Format, isWorking);
                me.UpdateData(c.Data);
                if (enableSpeech) {
                    const msgBdy = me.Body;
                    if (msgBdy.Voice)
                        SpeechGen.speakContinue(msgBdy.innerText, !isWorking);
                }
                return;
            }
            if (mt === msgClear) {
                chatE.innerText = "";
                if (previewElement)
                    chatE.appendChild(previewElement);
                return;
            }
            if (mt === msgRemove) {
                const me = findMessageE(m.Value);
                if (me)
                    me.remove();
                return;
            }
        });






        /** True if incoming messages from other users are spoken (`SysWeaver.Chat.SpeechGen<s>` === "true"). @type {boolean} */
        let enableSpeech = localStorage.getItem(genKey) === "true";
        /** Speech volume in percent (25/50/75/100, default 50) from `SysWeaver.Chat.SpeechGenVol<s>`. @type {number} */
        let speechVolume = localStorage.getItem(volumeKey);
        if (!speechVolume)
            speechVolume = 50;
        else {
            try {
                speechVolume = parseInt(speechVolume);
            }
            catch {
                speechVolume = 50;
            }
        }

        /**
         * Counts the attachments of the draft.
         * @returns {number} Number of `;`-separated entries in `previewMessage.Data` (0 if none).
         * Note: only valid when `canPost` (otherwise `previewMessage` is undefined); callers check `uploadRepo`/preview first.
         */
        function GetDataCount() {
            const data = previewMessage.Data;
            if (!data)
                return 0;
            return data.split(';').length;
        }

        /**
         * The top-right menu button (`IconMenu`, `<SysWeaver-Chat> > .IconButton`). Its click opens a `PopUpMenu` (root class
         * `Chat`) with: scroll top/bottom (disabled when already there), show all, time vs age toggle; with `canPost`: text /
         * markdown toggle (if `AllowMarkDown`) with a markdown help link, show/hide preview, set input language, clear draft,
         * attach file, auto-send toggle, open mic toggle (lists the keywords); speech synthesis toggle and a volume submenu
         * (25/50/75/100, Flags 3 = disabled+checked marks the selected one); server `data.Menus` via `addRec`; and
         * "Remove ALL messages" (if `CanClear`, confirmed unless `DoNotConfirmClear`).
         * @type {ColorIcon}
         */
        const menu = new ColorIcon("IconMenu", "IconColorThemeAcc2", options.MenuIconSize, options.MenuIconSize,
            _TF("Click to show menu option", "Tool tip description of a button that when clicked will show a context menu")
            , ev => {

            const chatElement = menu.Element;
            PopUpMenu(chatElement, (close, backEl) => {

                const pp = backEl.parentElement;
                pp.classList.add("Chat");
                new ResizeObserver(() => {
                    const cr = chatElement.getBoundingClientRect();
                    pp.style.left = cr.right + "px";
                }).observe(document.body);

                const menu = new WebMenu();
                menu.Name = "Chat";

                // Navigation


                menu.Items.push(WebMenuItem.From({
                    Name:
                        _TF("Scroll to top", "Text of a menu item that when clicked will scroll to the first chat message"),
                    IconClass: "IconChatTop",
                    Title:
                        _TF("Scroll to the first loadded message in the chat", "Tool tip description of a menu item that when clicked will scroll to the first chat message"),
                    Data: () => {
                        chatE.scrollTo(
                            {
                                left: chatE.scrollLeft,
                                top: 0,
                                behavior: "smooth",
                            });
                        close();
                    },
                    Flags: chatE.scrollTop <= 0 ? 1 : 0,
                }));
                menu.Items.push(WebMenuItem.From({
                    Name:
                        _TF("Scroll to bottom", "Text of a menu item that when clicked will scroll to the last chat message"),
                    IconClass: "IconChatBottom",
                    Title:
                        _TF("Scroll to the end of the chat", "Tool tip description of a menu item that when clicked will scroll to the last chat message"),
                    Data: () => {
                        chatE.scrollTo(
                            {
                                left: chatE.scrollLeft,
                                top: chatE.scrollHeight,
                                behavior: "smooth",
                            });
                        close();
                    },
                    Flags: IsAtMaxScrollV(chatE) ? 1 : 0,
                }));

                menu.Items.push(WebMenuItem.From({
                    Name:
                        _TF("Show all", "Text of a menu item that when clicked will show and hidden (collapsed) chat messages"),
                    Flags: 0,
                    IconClass: "IconChatShow",
                    Title:
                        _TF("Show any hidden content in all messages", "Tool tip description of a menu item that when clicked will show and hidden (collapsed) chat messages"),
                    Data: () => {
                        Array.from(chatE.children).forEach(x => x.Show());
                        setHideNone();
                        close();
                    },
                }));



                if (showTimeStamp) {
                    menu.Items.push(WebMenuItem.From({
                        Name:
                            _TF("Show message age", "Text of a menu item that when clicked will show message age instead of message time"),
                        Flags: 0,
                        IconClass: "IconChatAge",
                        Title:
                            _TF("Show the message age instead of the time", "Tool tip description of a menu item that when clicked will show message age instead of message time"),
                        Data: () => {
                            showTimeStamp = false;
                            localStorage.removeItem(timeStampName);
                            updateMessageFormat();
                            close();
                        },
                    }));

                } else {
                    menu.Items.push(WebMenuItem.From({
                        Name:
                            _TF("Show message time", "Text of a menu item that when clicked will show message time instead of message age"),
                        Flags: 0,
                        IconClass: "IconChatTime",
                        Title:
                            _TF("Show the actual time of a message instead of age", "Tool tip description of a menu item that when clicked will show message time instead of message age"),
                        Data: () => {
                            showTimeStamp = true;
                            localStorage.setItem(timeStampName, "true");
                            updateMessageFormat();
                            close();
                        },
                    }));
                }

                //  Message type
                if (canPost) {


                    if (data.AllowMarkDown) {
                        if (useMarkDown) {
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Use regular text", "Text of a menu item that when clicked will select regular text input"),
                                Flags: 0,
                                IconClass: "IconChatText",
                                Title:
                                    _TF("Use regular text as input instead of mark down (MD)", "Tool tip description of a menu item that when clicked will select regular text input"),
                                Data: () => {
                                    useMarkDown = false;
                                    localStorage.setItem(useMarkDownKey, useMarkDown);
                                    markDownChanged();
                                    close();
                                },
                            }));
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Mark Down syntax", "Text of a menu item that when clicked will show a help page for MD (Mark Down)"),
                                Flags: 0,
                                IconClass: "IconChatHelp",
                                Title:
                                    _TF("Open mark down syntax help in a new tab", "Tool tip description of a menu item that when clicked will show a help page for MD (Mark Down)"),
                                Data: () => {
                                    Open("https://www.markdownguide.org/basic-syntax/", "_blank");
                                    close();
                                },
                            }));
                        } else {
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Use Mark Down text", "Text of a menu item that when clicked will select markdown text input"),
                                Flags: 0,
                                IconClass: "IconChatMD",
                                Title:
                                    _TF("Use Mark Down (MD) input instead of regular text", "Tool tip description of a menu item that when clicked will select markdown text input"),
                                Data: () => {
                                    useMarkDown = true;
                                    localStorage.setItem(useMarkDownKey, useMarkDown);
                                    markDownChanged();
                                    close();
                                },
                            }));
                        }
                    }
                    const sp = typeof showPreview !== "boolean" ? useMarkDown : showPreview;
                    if (sp) {
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Hide preview", "Text of a menu item that when clicked will hide the chat message preview"),
                            Flags: 0,
                            IconClass: "IconChatHidePreview",
                            Title:
                                _TF("Hide the preview of a new chat message", "Tool tip description of a menu item that when clicked will hide the chat message preview"),
                            Data: () => {
                                showPreview = useMarkDown ? false : null;
                                localStorage.setItem(previewKey, showPreview);
                                previewChanged();
                                close();
                            },
                        }));
                    } else {
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Show preview", "Text of a menu item that when clicked will show a preview of new a chat message"),
                            Flags: 0,
                            IconClass: "IconChatShowPreview",
                            Title:
                                _TF("Show a preview of the message", "Tool tip description of a menu item that when clicked will show a preview of new a chat message"),
                            Data: () => {
                                showPreview = useMarkDown ? null : true;
                                localStorage.setItem(previewKey, showPreview);
                                previewChanged();
                                close();
                            },
                        }));
                    }
                    const speechRec = SpeechInput.supportSpeechRec();
                    if (canTranslate || speechRec) {
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Set input language", "Text of a menu item that when clicked will let the user select what language they used when writing chat messages"),
                            Flags: 0,
                            IconClass: "IconChatLanguage",
                            Title:
                                speechRec
                                    ?
                                    _TF("Specify what language you use for your text messages and voice input", "Tool tip description of a menu item that when clicked will let the user select what language they used when writing chat messages")
                                    :
                                    _TF("Specify what language you use for your text messages", "Tool tip description of a menu item that when clicked will let the user select what language they used when writing chat messages")
                            ,
                            Data: async () => {
                                await selectLanguage();
                                close();
                            },
                        }));
                    }

                    menu.Items.push(WebMenuItem.From({
                        Name: _TF("Clear", "Name of a menu item that when clicked will clear the current chat message"),
                        Flags: canSend ? 0 : 1,
                        IconClass: "IconChatClearCurrent",
                        Title: _TF("Clear the current message text and any data", "Tool tip description of a menu item that when clicked will clear the current chat message"),
                        Data: async () => {
                            await clearCurrentMessage();
                            close();
                        },
                    }));



                    //  Upload

                    if (uploadRepo) {
                        const filesLeft = data.MaxDataCount - GetDataCount();
                        // The handler below only calls `uploadFiles(files)` when files were selected.
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Attach file", "Text of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                            Flags: filesLeft <= 0 ? 1 : 0,
                            IconClass: "IconChatAttachFile",
                            Title:
                                _TF("Attach a file to the current message", "Tool tip description of a menu item that when clicked will enable the user to upload a file to the server and attach it to the current chat message"),
                            Data: async () => {
                                const files = await selectFiles(filesLeft > 1);
                                if (files)
                                    await uploadFiles(files);
                                close();
                            },
                        }));
                    }


                    //  Audio input
                    if (speechRec) {
                        let l = localStorage.getItem(autoSendKey);
                        if (l === null) {
                            l = false;
                            localStorage.setItem(autoSendKey, l);
                        }
                        if (l === "true") {
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Disable auto send", "Text of a menu item that when clicked will disable the function to automatically send messages when speech input completes"),
                                Flags: 0,
                                IconClass: "IconChatAutoSendOff",
                                Title:
                                    _TF("Disable automatic send of message when speech input completes", "Tool tip description of a menu item that when clicked will disable the function to automatically send messages when speech input completes"),
                                Data: () => {
                                    localStorage.setItem(autoSendKey, "false");
                                    close();
                                },
                            }));
                        } else {
                            menu.Items.push(WebMenuItem.From({
                                Name:
                                    _TF("Enable auto send", "Text of a menu item that when clicked will enable the function to automatically send messages when speech input completes"),
                                Flags: 0,
                                IconClass: "IconChatAutoSendOn",
                                Title:
                                    _TF("Enable automatic send of message when speech input completes", "Tool tip description of a menu item that when clicked will enable the function to automatically send messages when speech input completes"),
                                Data: () => {
                                    localStorage.setItem(autoSendKey, "true");
                                    close();
                                },
                            }));
                        }


                    }


                    if (canListen()) {

                        if (data.SpeechName) {
                            let l = localStorage.getItem(listenKey);
                            if (l === null) {
                                l = data.EnableListenByDefault ? "true" : "false";
                                localStorage.setItem(listenKey, l);
                            }
                            if (l === "true") {
                                menu.Items.push(WebMenuItem.From({
                                    Name:
                                        _TF("Disable speech input (open mic)", "Text of a menu item that when clicked will disable open microphone speech input"),
                                    Flags: 0,
                                    IconClass: "IconChatMicOff",
                                    Title:
                                        _TF("Disable open mic speech input", "Tool tip description of a menu item that when clicked will disable open microphone speech input"),
                                    Data: () => {
                                        localStorage.setItem(listenKey, "false");
                                        stopListening();
                                        close();
                                    },
                                }));
                            } else {
                                let t = _TF("Enable speech input, listening to: ", "Tool tip description of a menu item that when clicked will enable open microphone speech input, listening to a list of keywords that are listed below this text");
                                data.SpeechName.forEach(x => t = t + "\n" + x.split('|')[0]);
                                menu.Items.push(WebMenuItem.From({
                                    Name:
                                        _TF("Enable speech input (open mic)", "Text of a menu item that when clicked will enable open microphone speech input"),
                                    Flags: 0,
                                    IconClass: "IconChatMicOn",
                                    Title: t,
                                    Data: () => {
                                        localStorage.setItem(listenKey, "true");
                                        stopListening();
                                        startListening();
                                        close();
                                    },
                                }));

                            }
                        }
                    }


                }

                if (canSpeak()) {
                    let l = localStorage.getItem(genKey);
                    if (l === null) {
                        l = data.EnableGenByDefault ? "true" : "false";
                        localStorage.setItem(genKey, l);
                    }
                    if (l === "true") {
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Disable speech synthesis", "Text of a menu item that when clicked will disable speech generation (text to speech)"),
                            Flags: 0,
                            IconClass: "IconChatSpeechGenDisable",
                            Title:
                                _TF("Disable text to speech synthesis", "Tool tip description of a menu item that when clicked will disable speech generation (text to speech)"),
                            Data: () => {
                                localStorage.setItem(genKey, "false");
                                SpeechGen.cancelSpeak();
                                enableSpeech = false;
                                close();
                            },
                        }));
                    } else {
                        menu.Items.push(WebMenuItem.From({
                            Name:
                                _TF("Enable speech synthesis", "Text of a menu item that when clicked will enable speech generation (text to speech)"),
                            Flags: 0,
                            IconClass: "IconChatSpeechGenEnable",
                            Title:
                                _TF("Enable text to speech synthesis.\nUses local services if available to avoid data leakage and bandwidth usage (this may sound worse than online solutions).",
                                    "Tool tip description of a menu item that when clicked will enable speech generation (text to speech)"),
                            Data: () => {
                                localStorage.setItem(genKey, "true");
                                enableSpeech = true;
                                close();
                            },
                        }));
                    }

                    const flags = enableSpeech ? 0 : 1;
                    menu.Items.push(WebMenuItem.From({
                        Name:
                            _TF("Speech volume", "Text of a menu item that have a number of speech volume presets as children"),
                        Flags: flags,
                        IconClass: "IconChatSpeechVolume",
                        Title:
                            _TF("Adjust the volume of the audio from speech synthesis.", "Tool tip description of a menu item that have a number of speech volume presets as children"),
                        Children: [
                            {
                                Name:
                                    _TF("25% volume", "Text of a menu item that when clicked will set the speech volume to 25%"),
                                Flags: (speechVolume === 25 ? 3 : flags),
                                IconClass: "IconChatSpeechVolume25",
                                Title:
                                    _TF("Use 25% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 25%"),
                                Data: () => {
                                    localStorage.setItem(volumeKey, "25");
                                    speechVolume = 25;
                                    close();
                                },
                            },
                            {
                                Name:
                                    _TF("50% volume", "Text of a menu item that when clicked will set the speech volume to 50%"),
                                Flags: (speechVolume === 50 ? 3 : flags),
                                IconClass: "IconChatSpeechVolume50",
                                Title:
                                    _TF("Use 50% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 50%"),
                                Data: () => {
                                    localStorage.setItem(volumeKey, "50");
                                    speechVolume = 50;
                                    close();
                                },
                            },
                            {
                                Name:
                                    _TF("75% volume", "Text of a menu item that when clicked will set the speech volume to 75%"),
                                Flags: (speechVolume === 75 ? 3 : flags),
                                IconClass: "IconChatSpeechVolume75",
                                Title:
                                    _TF("Use 75% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 75%"),
                                Data: () => {
                                    localStorage.setItem(volumeKey, "75");
                                    speechVolume = 75;
                                    close();
                                },
                            },
                            {
                                Name:
                                    _TF("Max volume", "Text of a menu item that when clicked will set the speech volume to the maximum (100%)"),
                                Flags: (speechVolume === 100 ? 3 : flags),
                                IconClass: "IconChatSpeechVolume100",
                                Title:
                                    _TF("Use 100% volume for speech.", "Tool tip description of a menu item that when clicked will set the speech volume to 25%"),
                                Data: () => {
                                    localStorage.setItem(volumeKey, "100");
                                    speechVolume = 100;
                                    close();
                                },
                            },
                        ]
                    }));

                }


                //  Audio




                addRec(menu.Items, data.Menus, null, close);
                if (data.CanClear)
                    menu.Items.push(WebMenuItem.From({
                        Name:
                            _TF("Remove ALL messages", "Text of a menu item that when clicked will clear the chat (remove all messages)"),
                        Flags: 0,
                        IconClass: "IconChatClear",
                        Title:
                            _TF("Remove ALL chat messages", "Tool tip description of a menu item that when clicked will clear the chat (remove all messages)"),
                        Data: async () => {
                            if (!data.DoNotConfirmClear) {
                                if (!await Confirm(
                                    _TF("Remove all messages", "Title of a confirmation dialog that when confirmed will remove all chat messages in a room"),
                                    _TF("Are you sure that you wan't to remove all chat messages?", "Text of a confirmation dialog that when confirmed will remove all chat messages in a room"),
                                    _TF("YES, clear", "Text of a button on a confirmation dialog that when clicked will remove all chat messages in a room"),
                                    _TF("NO, keep them", "Text of a button on a confirmation dialog that when clicked will keep all chat messages in a room as opposed to removing them"),
                                    "IconChatRemove", "IconChatKeep",
                                    _TF("Click to remove all chat messages, this can't be undone", "Tool tip description of a button on a confirmation dialog that when clicked will remove all chat messages in a room"),
                                    _TF("Click to keep the chat messages", "Tool tip description of a button on a confirmation dialog that when clicked will keep all chat messages in a room as opposed to removing them"),
                                )) {
                                    close();
                                    return;
                                }
                            }
                            await cmdClear();
                            close();
                        },
                    }));
                return menu;
            }, true);

        }, null, null, true);
        chatBackground.appendChild(menu.Element);



        if (msg) {
            const ml = msg.length;
            for (let i = 0; i < ml; ++i)
                await addMessage(msg[i]);
        }





        /** True while older messages are being fetched by the StickToBottom top callback. @type {boolean} */
        let isLoading = false;
        // StickToBottom (common.js): toggles class Stick while stuck to the bottom; when scrolled to the top, loads older
        // messages with GetMessages {ChatId, MaxCount: -10, FromId: <first row id>}, prepends them keeping the scroll
        // position, shows a 64x64 IconWorking spinner after 200 ms, and sets isAtTop when none are returned.
        StickToBottom(chatE, true,
            () => chatE.classList.add("Stick"),
            () => chatE.classList.remove("Stick"),
            async () =>
            {
                if (isAtTop)
                    return;
                if (isLoading)
                    return;
                const beforeH = chatE.scrollHeight;
                isLoading = true;
                let loadingElement = null;
                const showLoading = setTimeout(() => {
                    loadingElement = new ColorIcon("IconWorking", "IconColorThemeMain", 64, 64, _TF("Loading more messages", "Tool tip description of a loading indicator shown when the char is loading more messages")).Element;
                    const f = chatE.firstElementChild;
                    if (f)
                        chatE.insertBefore(loadingElement, f);
                    else
                        chatE.appendChild(loadingElement);
                }, 200);
                console.log("Loading more messages..");
                try {
                    const first = chatE.firstElementChild;
                    if (!first)
                        return;
                    const lastId = first.Msg.Id;
                    const newMsg = await sendRequest(apiBase + "GetMessages", {
                        ChatId: chatId,
                        MaxCount: -10,
                        FromId: lastId,
                    });
                    clearTimeout(showLoading);
                    if (loadingElement)
                        loadingElement.remove();
                    loadingElement = null;
                    if (newMsg) {
                        let ml = newMsg.length;
                        if (ml > 0) {
                            while (ml > 0) {
                                --ml;
                                await addMessage(newMsg[ml], true);
                                const added = chatE.scrollHeight - beforeH;
                                chatE.scrollTo({
                                    left: chatE.scrollLeft,
                                    top: added,
                                    behavior: "instant",
                                });
                            }
                            return;
                        }
                    }
                    isAtTop = true;
                    console.log("All messages loaded!");
                }
                finally
                {
                    isLoading = false;
                    clearTimeout(showLoading);
                    if (loadingElement)
                        loadingElement.remove();
                }
            }
        );

        /**
         * Forces the list to stay scrolled to the bottom for 500 ms (every animation frame), then scrolls once more.
         * Covers async layout (images, markdown). Called after sending and at startup.
         * @param {boolean} [direct] - True for `behavior: "instant"`, else "smooth".
         */
        function StickForAwhile(direct) {
            let stick = true;
            /** Per frame scroll-to-bottom callback; re-queues itself with `requestAnimationFrame` while `stick` is true. */
            const stickFn = () => {
                if (!stick)
                    return;
                chatE.scrollTo({
                    left: chatE.scrollLeft,
                    top: chatE.scrollHeight,
                    behavior: direct ? "instant" : "smooth",
                    });
                requestAnimationFrame(stickFn);
            };
            requestAnimationFrame(stickFn);

            setTimeout(() => {
                chatE.scrollTo({
                    left: chatE.scrollLeft,
                    top: chatE.scrollHeight,
                    behavior: direct ? "instant" : "smooth",
                });
                stick = false;
            }, 500);
        }

        // Restore the saved split height (fraction of the container height)
        if (initSize) {
            initSize = parseFloat(initSize) * chatBackground.offsetHeight;
            chatE.style.height = initSize + "px";
        }


        StickForAwhile(true);
        doLast();
        return chatE;

    }

}


/**
 * Page entry point of `Chat.html` (`<body class="ChatMain" onload="chatMain()">`).
 * Shows a loader (`AddLoading`), reads URL params `id` (required, else throws "No id supplied!"), `options` (JSON merged
 * into a new `ChatOptions` with `Object.assign`) and `nobackground` (adds class `NoBackground`), creates
 * `<SysWeaver-Chat>` in `document.body` and awaits `Chat.addChat(chatBackground, chatId, null, options)`.
 * Errors are shown with `Fail(e.message)`; the loader is always removed.
 * @returns {Promise<void>}
 */
async function chatMain() {
    const stopLoading = AddLoading(null, _TF("Loading chat messages", "Message shown while waiting for chat messages to be loaded"), true);
    try {
        const ps = getUrlParams();
        const chatId = ps.get('id');
        if (!chatId)
            throw new Error(_TF("No id supplied!", "Error message thrown when no 'id' parameter is supplied"));
        const options = new ChatOptions();
        const opt = ps.get('options');
        if (opt)
            Object.assign(options, JSON.parse(opt));

        const page = document.body;

        const chatBackground = document.createElement("SysWeaver-Chat");
        if (ps.has("nobackground"))
            chatBackground.classList.add("NoBackground");
        page.appendChild(chatBackground);
        await Chat.addChat(chatBackground, chatId, null, options);
    }

    catch (e) {
        Fail(e.message);
    }
    finally {
        stopLoading();
    }


}
