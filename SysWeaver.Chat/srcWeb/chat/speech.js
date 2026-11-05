/**
 * @file speech.js - speech output (text to speech) and speech input (recognition) for the SysWeaver chat (served as `chat/speech.js`).
 *
 * Loading: classic script, loaded by `Chat.html` before `chat.js` (`chat.js` also loads it on demand when a page only includes chat.js).
 * Needs the core (`sw/core.js`: `Sw.Format.IsLetter`, `Sw.Platform`, `Sw.Events`). No DOM, no visible UI.
 *
 * Defines:
 * - `Sw.Chat.SpeechGen` (static class): text to speech on `window.speechSynthesis`, incl. incremental speech of a message that is
 *   still streaming (`SpeakNew` + `SpeakContinue`).
 * - `Sw.Chat.SpeechInput` (static class): wake word ("open mic") or push-to-talk speech recognition on
 *   `SpeechRecognition` / `webkitSpeechRecognition` + `SpeechGrammarList`.
 * - Legacy globals `SpeechGen` and `SpeechInput` (the same classes; the old lower case member names are kept as aliases).
 */
(() => {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    /** @type {object} The chat namespace (shared with chat.js). */
    const C = Sw.Chat = Sw.Chat || {};

    /**
     * Build a lookup set from characters / words.
     * @param {...string} items The members.
     * @returns {Set<string>} The set.
     */
    const set = (...items) => new Set(items);

    /**
     * Static-only text to speech helper around `window.speechSynthesis` (one speech stream per page).
     * One-shot speech (`SpeakOnce`) and incremental speech of a streamed message (`SpeakNew` followed by `SpeakContinue`).
     */
    class SpeechGen {

        /** Substrings of voice names that count as "male" when scoring voices (`FindVoice`). @type {string[]} */
        static MaleNames = ["George", "Bengt"];

        /** Substrings of voice names that count as "female" when scoring voices (`FindVoice`). @type {string[]} */
        static FemaleNames = ["Hazel", "Susan"];

        /** Phrase end characters: while a message streams only the text up to the last of these is spoken. @type {Set<string>} */
        static EndChars = set("!", "?", ".", ",");

        /** Punctuation kept by `FilterText`. @type {Set<string>} */
        static ValidChars = set("!", "?", ".", ",");

        /** White space characters recognized by `FilterText`. @type {Set<string>} */
        static WhiteSpaces = set(" ", "\t", "\n", "\r");

        /** Math operators and their spoken form (used by `FilterText` when next to numbers). @type {Map<string, string>} */
        static MathOps = new Map([
            ["*", " times "],
            ["+", " plus "],
            ["-", " minus "],
            ["/", " divided by "],
            ["%", " modulo "],
            ["^", " to the power of "],
            ["=", " equals "],
            ["≈", " is approximately "],
        ]);

        /** The (filtered) text of the current stream that is spoken or queued. @type {string} */
        static CurrentText = "";

        /** Text waiting for the current utterance to end. @type {string} */
        static Queue = "";

        /** Initializes a new utterance of the current stream (voice, pitch, rate, volume, language). @type {?function(SpeechSynthesisUtterance):void} */
        static Init = null;

        /**
         * Test if speech synthesis is available.
         * @returns {boolean} True if `window.speechSynthesis` exists.
         */
        static Supported() {
            return !!window.speechSynthesis;
        }

        /**
         * Case insensitive containment similarity of two strings.
         * @param {string} s1 First string.
         * @param {string} s2 Second string.
         * @returns {number} If the shorter string occurs in the longer one at index f: 1 / (1 + 0.02 * f), else 0.
         */
        static Similarity(s1, s2) {
            let longer = (s1 || "").toLowerCase();
            let shorter = (s2 || "").toLowerCase();
            if (longer.length < shorter.length)
                [longer, shorter] = [shorter, longer];
            const f = longer.indexOf(shorter);
            return f < 0 ? 0 : 1.0 / (1.0 + 0.02 * f);
        }

        /**
         * Find the best matching voice: score = similarity(language, voice.lang) * 10 + similarity(gender, name) + (local ? 0.8 : 0);
         * a local voice named `exact` wins outright.
         * @param {string} language Language code to match (ex: "en-US").
         * @param {boolean} male True to prefer a male voice.
         * @param {string} [exact] Exact (local) voice name to prefer.
         * @returns {SpeechSynthesisVoice|null} The best voice, or null if none scored above 0.
         */
        static FindVoice(language, male, exact) {
            const g = male ? "male" : "female";
            let bestScore = 0;
            let bestVoice = null;
            for (const voice of window.speechSynthesis.getVoices()) {
                let name = voice.name;
                for (const fix of SpeechGen.MaleNames)
                    name = name.replace(fix, "male");
                for (const fix of SpeechGen.FemaleNames)
                    name = name.replace(fix, "female");
                let score = SpeechGen.Similarity(language, voice.lang) * 10 + SpeechGen.Similarity(g, name) + (voice.localService ? 0.8 : 0);
                if (exact && voice.name === exact && voice.localService)
                    score = 10000;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestVoice = voice;
            }
            return bestVoice;
        }

        /**
         * Convert text (plain or markdown) to speakable text: white space runs collapse (removed between digits, joining digit groups),
         * digits, letters and `! ? . ,` are kept (a comma right after a digit is dropped), math operators next to numbers become words,
         * `(` becomes ", ", everything else is dropped.
         * @param {string} text The text.
         * @returns {string} The filtered text ("" for empty input).
         */
        static FilterText(text) {
            text = text || "";
            const tl = text.length;
            const white = SpeechGen.WhiteSpaces;
            const isDigit = c => c >= "0" && c <= "9";
            /**
             * Test if the next (dir 1) or previous (dir -1) non white space character is a digit.
             * @param {number} pos Start index (exclusive).
             * @param {number} dir Direction.
             * @returns {boolean} True for a digit.
             */
            const numberAt = (pos, dir) => {
                for (pos += dir; pos >= 0 && pos < tl; pos += dir) {
                    const c = text.charAt(pos);
                    if (isDigit(c))
                        return true;
                    if (!white.has(c))
                        return false;
                }
                return false;
            };
            let wasWhite = true;
            let prevIsNumber = false;
            let out = "";
            for (let i = 0; i < tl; ++i) {
                const c = text.charAt(i);
                if (white.has(c)) {
                    if (numberAt(i, -1) && numberAt(i, 1))
                        continue;
                    prevIsNumber = false;
                    if (wasWhite)
                        continue;
                    wasWhite = true;
                    out += " ";
                    continue;
                }
                if (isDigit(c)) {
                    wasWhite = false;
                    prevIsNumber = true;
                    out += c;
                    continue;
                }
                if (SpeechGen.ValidChars.has(c)) {
                    if (prevIsNumber && c === ",")
                        continue;
                    wasWhite = false;
                    prevIsNumber = false;
                    out += c;
                    continue;
                }
                const op = SpeechGen.MathOps.get(c);
                if (op && numberAt(i, 1) && (c === "-" || numberAt(i, -1))) {
                    wasWhite = false;
                    prevIsNumber = false;
                    out += op;
                    continue;
                }
                if (c === "(") {
                    wasWhite = false;
                    prevIsNumber = false;
                    out += ", ";
                    continue;
                }
                if (!Sw.Format.IsLetter(c))
                    continue;
                wasWhite = false;
                prevIsNumber = false;
                out += c;
            }
            return out;
        }

        /**
         * Create an utterance initializer.
         * @param {SpeechSynthesisVoice} [voice] Voice (browser default if falsy).
         * @param {number} [pitch] Pitch.
         * @param {number} [rate] Rate (browser default if falsy).
         * @param {number} [volume=0.5] Volume 0-1.
         * @param {string} [lang="en"] Language.
         * @returns {function(SpeechSynthesisUtterance):void} The initializer.
         */
        static MakeInit(voice, pitch, rate, volume, lang) {
            return u => {
                u.lang = lang || "en";
                if (voice)
                    u.voice = voice;
                if (typeof pitch === "number")
                    u.pitch = pitch;
                if (rate)
                    u.rate = rate;
                u.volume = volume || 0.5;
                u.onend = SpeechGen.OnSpeakEnd;
            };
        }

        /**
         * Speak a text with the current initializer.
         * @param {string} text The text.
         */
        static Speak(text) {
            const u = new SpeechSynthesisUtterance(text);
            (SpeechGen.Init || SpeechGen.MakeInit())(u);
            window.speechSynthesis.speak(u);
        }

        /** `onend` of every utterance: speaks the queued text (if any). */
        static OnSpeakEnd() {
            const text = SpeechGen.Queue.trim();
            SpeechGen.Queue = "";
            if (text.length > 0)
                SpeechGen.Speak(text);
        }

        /** Clear the queue and cancel any ongoing speech. */
        static Cancel() {
            SpeechGen.Queue = "";
            const ss = window.speechSynthesis;
            if (ss && ss.speaking)
                ss.cancel();
        }

        /**
         * Speak a complete text once (cancels the current speech).
         * @param {string} text Text (filtered with `FilterText`).
         * @param {SpeechSynthesisVoice} [voice] Voice.
         * @param {number} [pitch] Pitch.
         * @param {number} [rate] Rate.
         * @param {number} [volume=0.5] Volume 0-1.
         * @param {string} [lang="en"] Language.
         */
        static SpeakOnce(text, voice, pitch, rate, volume, lang) {
            text = SpeechGen.FilterText(text);
            if (text.length <= 0)
                return;
            SpeechGen.Cancel();
            SpeechGen.Init = SpeechGen.MakeInit(voice, pitch, rate, volume, lang);
            SpeechGen.CurrentText = text;
            SpeechGen.Speak(text);
        }

        /**
         * Start a new incremental speech stream (a newly posted message), then `SpeakContinue(text)`.
         * @param {string} text The (possibly incomplete) message text.
         * @param {SpeechSynthesisVoice} [voice] Voice.
         * @param {number} [pitch] Pitch.
         * @param {number} [rate] Rate.
         * @param {number} [volume=0.5] Volume 0-1.
         * @param {string} [lang="en"] Language.
         * @param {boolean} [isCompleted] The text is complete (speak all of it).
         */
        static SpeakNew(text, voice, pitch, rate, volume, lang, isCompleted) {
            SpeechGen.CurrentText = "";
            SpeechGen.Init = SpeechGen.MakeInit(voice, pitch, rate, volume, lang);
            SpeechGen.SpeakContinue(text, isCompleted);
        }

        /**
         * Continue a streamed message whose text has grown (`replace` push messages). If the text no longer starts with the spoken
         * part, speech restarts. Unless completed only text up to the last phrase end is taken; while speaking it is queued.
         * @param {string} text The full current message text.
         * @param {boolean} [isCompleted] True when the message is complete: speak all remaining text.
         */
        static SpeakContinue(text, isCompleted) {
            text = SpeechGen.FilterText(text);
            let prev = SpeechGen.CurrentText;
            if (!text.startsWith(prev)) {
                prev = "";
                SpeechGen.CurrentText = "";
                SpeechGen.Cancel();
            }
            const ss = window.speechSynthesis;
            if (!isCompleted && ss.speaking)
                return;
            const next = text.substring(prev.length);
            let goodUntil = next.length;
            if (!isCompleted) {
                while (goodUntil > 0 && !SpeechGen.EndChars.has(next.charAt(goodUntil - 1)))
                    --goodUntil;
            }
            if (goodUntil <= 0)
                return;
            let newText = next.substring(0, goodUntil);
            SpeechGen.CurrentText = prev + newText;
            newText = newText.trim();
            if (newText.length <= 0)
                return;
            if (prev.length <= 0)
                SpeechGen.Cancel();
            if (ss.speaking)
                SpeechGen.Queue += newText;
            else
                SpeechGen.Speak(newText);
        }
    }

    /**
     * Static-only speech recognition helper. `Listen` creates a recognizer for wake words (open mic) or push-to-talk and returns
     * its stop function.
     */
    class SpeechInput {

        /** Stop words: a final result ending with one of these (any case) aborts the current input. @type {Set<string>} */
        static StopWords = set("abort", "stop", "cancel", "reset", "restart");

        /** Characters that separate words (white space and `! ? . , ¡ ¿`). @type {Set<string>} */
        static NonWordChars = set(" ", "\t", "\n", "\r", "!", "?", ".", ",", "¡", "¿");

        /** Question words: any occurrence makes a sentence a question. @type {Set<string>} */
        static QuestionWords = set("who", "who's", "what", "what's", "where", "where's", "when", "when's", "why", "why's",
            "which", "which's", "whose", "whose's", "how", "how's");

        /** Words that make a sentence a question when they are its first word. @type {Set<string>} */
        static QuestionStartWords = set("are", "aren't", "am", "is", "isn't", "do", "don't", "does", "doesn't");

        /**
         * Test if open mic (continuous wake word listening) is supported (not on mobile devices).
         * @returns {boolean} True if supported.
         */
        static SupportOpenMic() {
            return !Sw.Platform.IsMobileDevice;
        }

        /**
         * Test if speech recognition is supported (a SpeechRecognition and a SpeechGrammarList, standard or webkit prefixed).
         * @returns {boolean} True if supported.
         */
        static Supported() {
            return !!((window.SpeechRecognition || window.webkitSpeechRecognition) && (window.SpeechGrammarList || window.webkitSpeechGrammarList));
        }

        /**
         * Get the last word of a text (trailing non word characters skipped).
         * @param {string} text The text.
         * @returns {string} The last word (case preserved), or "".
         */
        static GetLastWord(text) {
            const nc = SpeechInput.NonWordChars;
            let e = (text || "").length;
            while (e > 0 && nc.has(text.charAt(e - 1)))
                --e;
            let s = e;
            while (s > 0 && !nc.has(text.charAt(s - 1)))
                --s;
            return text.substring(s, e);
        }

        /**
         * Test if a word makes its sentence a question.
         * @param {string} word The word.
         * @param {boolean} isFirstWord True for the first word of the sentence.
         * @returns {boolean} True for a question word.
         */
        static IsQuestionWord(word, isFirstWord) {
            word = word.toLowerCase();
            return (isFirstWord && SpeechInput.QuestionStartWords.has(word)) || SpeechInput.QuestionWords.has(word);
        }

        /**
         * Turn a raw transcript into a sentence: text up to the wake word removed, sentences capitalized, "." replaced by "?" for
         * questions, trailing punctuation replaced by ". " or "? ", the wake word prefix prepended.
         * @param {string} text The transcript.
         * @param {string} keyword The active wake word (lower case).
         * @param {string} prefix The prefix of the wake word.
         * @returns {string} The sentence, or "" for empty text.
         */
        static FilterSentence(text, keyword, prefix) {
            const ff = text.toLowerCase().indexOf(keyword);
            if (ff >= 0)
                text = text.substring(ff + keyword.length);
            text = text.trim();
            if (text.length <= 0)
                return "";
            let out = "";
            let makeUpper = true;
            let isQuestion = false;
            let skipWhite = true;
            let isFirstWord = true;
            let wordStart = -1;
            const tl = text.length;
            for (let i = 0; i < tl; ++i) {
                let ch = text.charAt(i);
                const isWhite = /\s/.test(ch) || ch === ".";
                if (skipWhite) {
                    if (isWhite)
                        continue;
                    skipWhite = false;
                }
                if (makeUpper && !isWhite) {
                    makeUpper = false;
                    ch = ch.toUpperCase();
                }
                if (wordStart >= 0) {
                    if (isWhite) {
                        isQuestion = SpeechInput.IsQuestionWord(text.substring(wordStart, i), isFirstWord) || isQuestion;
                        wordStart = -1;
                        isFirstWord = false;
                    }
                }
                else if (Sw.Format.IsLetter(ch))
                    wordStart = i;
                if (ch === ".") {
                    if (isQuestion) {
                        ch = "?";
                        isQuestion = false;
                    }
                    makeUpper = true;
                    isFirstWord = true;
                }
                out += ch;
            }
            if (wordStart >= 0)
                isQuestion = SpeechInput.IsQuestionWord(text.substring(wordStart), isFirstWord) || isQuestion;
            out = out.trim().replace(/[ .!?]+$/, "");
            if (out.length <= 0)
                return "";
            return (prefix || "") + out + (isQuestion ? "? " : ". ");
        }

        /**
         * Create and start a recognizer that activates on wake words (open mic) or at once (push-to-talk).
         * Interim text is delivered with `onSentence(text, false)`, the final sentence 1.5 s after the last final result with
         * `onSentence(text, true)`; a final result ending with a stop word aborts (`onSentence("", true)`).
         * If starting needs a user gesture it is retried on the first interaction.
         * @param {string|string[]} keyword Wake word entries `"wakeword|prefix"`.
         * @param {function(?(string|boolean), boolean):*} [onActivate] `(keyword, true)` on activation (`true` for push-to-talk),
         * `(null, true)` after a sentence in open mic mode, `(null, false)` when stopped.
         * @param {function(string, boolean):*} [onSentence] Receives the filtered text (false = interim, true = final).
         * @param {string} [language="en-US"] Recognition language.
         * @param {boolean} [ignoreKeyword] Any speech activates (the first wake word gives the prefix).
         * @param {boolean} [noOpenMic] Stop after one sentence (push-to-talk).
         * @returns {?function():Promise<void>} The stop function, or null if speech recognition is unsupported.
         */
        static Listen(keyword, onActivate, onSentence, language, ignoreKeyword, noOpenMic) {
            const SR = window.SpeechRecognition || window.webkitSpeechRecognition;
            const SGL = window.SpeechGrammarList || window.webkitSpeechGrammarList;
            if (!SR || !SGL)
                return null;
            const keywordMap = new Map();
            for (const x of (Array.isArray(keyword) ? keyword : (keyword ? [keyword] : []))) {
                const kv = x.split("|");
                keywordMap.set(kv[0].toLowerCase(), kv.length > 1 ? kv[1] : "");
            }
            const keywords = Array.from(keywordMap.keys());
            onActivate = onActivate || (() => { });
            onSentence = onSentence || (() => { });
            /**
             * Find the first wake word in a transcript.
             * @param {string} str The transcript.
             * @returns {?string} The wake word (the first one with ignoreKeyword), or null.
             */
            const findKeyword = str => {
                str = str.toLowerCase();
                for (const k of keywords)
                    if (str.indexOf(k) >= 0)
                        return k;
                return ignoreKeyword ? (keywords[0] ?? "") : null;
            };
            const filter = (text, k) => SpeechInput.FilterSentence(text, k, keywordMap.get(k));

            let recognition = new SR();
            const grammar = new SGL();
            grammar.addFromString("#JSGF V1.0; grammar keywords; public <keyword> = " + keywords.join(","), 1);
            recognition.grammars = grammar;
            recognition.continuous = true;
            recognition.lang = language || "en-US";
            recognition.interimResults = true;
            recognition.maxAlternatives = 1;

            let offInteract = null;
            /** Start the recognizer (also the open mic restart on end). */
            const start = () => {
                if (!recognition)
                    return;
                recognition.start();
                if (offInteract) {
                    offInteract();
                    offInteract = null;
                }
            };
            /** Stop for good (detaches the handlers) and report `(null, false)`. */
            const stop = async () => {
                if (!recognition)
                    return;
                if (offInteract) {
                    offInteract();
                    offInteract = null;
                }
                const r = recognition;
                recognition = null;
                r.onerror = null;
                r.onend = null;
                r.onresult = null;
                try {
                    r.stop();
                }
                catch {
                }
                await onActivate(null, false);
            };

            /** Accumulated final text of the current activation. */
            let add = "";
            /** Pending timer that delivers the final sentence. */
            let timer = null;
            /** The wake word of the current activation, or null. @type {?string} */
            let active = null;

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
            recognition.onresult = async event => {
                const res = event.results[event.resultIndex];
                if (timer) {
                    clearTimeout(timer);
                    timer = null;
                }
                const t = res[0].transcript;
                if (t.length <= 0)
                    return;
                const prevActive = active;
                active = active ?? findKeyword(t);
                if (prevActive !== active) {
                    await onActivate(active === null ? null : (active || true), true);
                    add = "";
                }
                const part = Sw.Platform.IsMobileDevice ? t : (add.length > 0 ? (add + ". " + t) : t);
                if (active !== null)
                    await onSentence(filter(part, active), false);
                if (!res.isFinal)
                    return;
                add = part;
                const final = add.trim();
                if (active !== null && SpeechInput.StopWords.has(SpeechInput.GetLastWord(final).toLowerCase())) {
                    active = null;
                    add = "";
                    if (noOpenMic)
                        await stop();
                    else
                        await onActivate(null, true);
                    await onSentence("", true);
                    return;
                }
                timer = setTimeout(async () => {
                    timer = null;
                    add = "";
                    if (active === null)
                        return;
                    const k = active;
                    active = null;
                    if (noOpenMic)
                        await stop();
                    else
                        await onActivate(null, true);
                    await onSentence(filter(final, k), true);
                }, 1500);
            };
            try {
                start();
                if (noOpenMic)
                    onActivate(true, true);
            }
            catch {
                //  No user gesture yet: retry on the first interaction
                offInteract = Sw.Events.On("page.interacted", () => {
                    try {
                        start();
                    }
                    catch (e) {
                        console.warn("Speech recognition: Failed to start - " + e.message);
                    }
                });
            }
            return stop;
        }
    }

    //  Old (lower case) member names, kept for compatibility
    Object.assign(SpeechGen, {
        supportSpeechGen: SpeechGen.Supported,
        similarity: SpeechGen.Similarity,
        findVoice: SpeechGen.FindVoice,
        filterText: SpeechGen.FilterText,
        onSpeakEnd: SpeechGen.OnSpeakEnd,
        cancelSpeak: SpeechGen.Cancel,
        speakOnce: SpeechGen.SpeakOnce,
        speakNew: SpeechGen.SpeakNew,
        speakContinue: SpeechGen.SpeakContinue,
    });
    Object.assign(SpeechInput, {
        IsMobile: Sw.Platform ? Sw.Platform.IsMobileDevice : false,
        supportOpenMic: SpeechInput.SupportOpenMic,
        supportSpeechRec: SpeechInput.Supported,
        triggerOnSpeech: SpeechInput.Listen,
    });

    C.SpeechGen = SpeechGen;
    C.SpeechInput = SpeechInput;
    /** Legacy global (old chat.js): the same class. */
    window.SpeechGen = SpeechGen;
    /** Legacy global (old chat.js): the same class. */
    window.SpeechInput = SpeechInput;
})();
