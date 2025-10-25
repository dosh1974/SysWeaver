

class SpeechGen
{

    static supportSpeechGen() {
        const ss = window.speechSynthesis;
        if (!ss)
            return false;
        return true;
    }

    static MaleNames = [
        "George",
        "Bengt",
    ];


    static FemaleNames = [
        "Hazel",
        "Susan",
    ];


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



    static EndChars = function () {
        const m = new Map();
        m.set('!', true);
        m.set('?', true);
        m.set('.', true);
        m.set(',', true);
        return m;
    }();


    static ValidChars = function () {
        const m = new Map();
        m.set('!', true);
        m.set('?', true);
        m.set('.', true);
        m.set(',', true);
        return m;
    }();

    static WhiteSpaces = function () {
        const m = new Map();
        m.set(' ', true);
        m.set('\t', true);
        m.set('\n', true);
        m.set('\r', true);
        return m;
    }();


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



    static filterText(text) {
        const tl = text.length;
        if (tl <= 0)
            return "";
        let wasWhite = true;
        let prevIsNumber = false;
        let out = "";
        const white = SpeechGen.WhiteSpaces;
        const mathOps = SpeechGen.MathOps;

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

    static cancelSpeak() {
        const ss = window.speechSynthesis;
        window["SpeakCurrentQueue"] = "";
        if (!ss.speaking)
            return;
        //console.log("SpeachSyntesis: Cancelling speach!");
        ss.cancel();
    }


    static speakOnce(text, voice, pitch, rate, volume, lang) {
        text = SpeechGen.filterText(text);
        if (text.lentg <= 0)
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

class SpeechInput {
    static IsMobile = isMobile();

    static supportOpenMic() {
        return !SpeechInput.IsMobile;
    }

    static supportSpeechRec() {
        const sr = window.SpeechRecognition ?? window.webkitSpeechRecognition;
        const sgl = window.SpeechGrammarList ?? window.webkitSpeechGrammarList;
        if (!sr)
            return false;
        if (!sgl)
            return false;
        return true;
    }


    static StopWords = function () {
        const m = new Map();
        m.set('abort', 1);
        m.set('stop', 1);
        m.set('cancel', 1);
        m.set('reset', 1);
        m.set('restart', 1);
        return m;
    }();

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

    static triggerOnSpeech(keyword, onActivate, onSentance, language, ignoreKeyword, noOpenMic) {

        const keywordMap = new Map();
        if (Array.isArray(keyword)) {
            keyword.forEach(x => {
                const kv = x.split('|');
                keywordMap.set(kv[0].toLowerCase(), kv.length > 1 ? kv[1] : "");

            });
        } else {
            const kv = keyword.split('|');
            keywordMap.set(kv[0].toLowerCase(), kv.length > 1 ? kv[1] : "");
        }
        const keywordArray = Array.from(keywordMap.keys());
        const keywords = keywordArray.join(",");
        const keywordCount = keywordArray.length;

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
        const start = () => {
            recognition.start();
            //console.log("Speech recognition: Started");
            document.removeEventListener("UserInteracted", start);
        };

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


        let add = "";
        let del = null;
        let isKey = null;

        function hasWhiteSpace(s) {
            return RegExp(/\s/g).test(s);
        }

        function isChar(s) {
            return RegExp(/^\p{L}/, 'u').test(s);
        }


        function isQuestionWord(word, isFirstWord) {
            word = word.toLowerCase();
            if (isFirstWord && (SpeechInput.QuestionWordStartMap.get(word) === 1))
                return true;
            return SpeechInput.QuestionWordMap.get(word) === 1;
        }

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

class Chat {

    /** If this flag is set on a message it means that it's not completed yet, like streaming chat */
    static FlagIsWorking = 1;

    /** If this flag is set on a message it means that the message can be remvoed by this user */
    static FlagCanRemove = 256;


    /**
     * Convert a text (user guid) to a color
     * @param {string} text Guid
     * @returns {string} html color
     */
    static chatColor(text) {
        const t = window.ChatColors ?? new Map();
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

    /** Cache if we're on mobile or not */
    static IsMobile = isMobile();

    /** Cache platform name */
    static Platform = getPlatform();

    /**
     * @param {HTMLElement} chatBackground The html element that the chat should be created as a child to
     * @param {string} chatId The ID of the chat (cahnnel)
     * @param {string} apiBase An optional api base (depends where the html resides)
     * @param {ChatOptions} options Optional options for more control
     * @returns {HTMLElement} The html element that was created
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

        let languages = null;
        let languageMap = null;


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

        let sname = options.SettingsName;
        if (!sname) {
            sname = "";
        } else {
            sname = "." + (sname === "*" ? chatId : sname);
        }

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

        let isAtTop = false;


        const chatE = document.createElement("SysWeaver-ChatMain");

        let initSize = localStorage.getItem(sizeKey);
        new ResizeObserver(ev => {
            const f = chatE.offsetHeight / chatBackground.offsetHeight;
            localStorage.setItem(sizeKey, "" + f);
        }).observe(chatE);


        chatBackground.appendChild(chatE);

        const chatInput = document.createElement("SysWeaver-ChatInput");
        chatBackground.appendChild(chatInput);



        const write = document.createElement("textarea");
        chatInput.appendChild(write);

//        write.placeholder = _TF("Write your text here..", "A placeholder text for an input box where a user can enter a text message for a chat");

        const titleVisible = _TF("Click to hide message content and future message content from {0}", "A tool tip message for a buttan that when clicked will hide the content of a specific chat message and all further messages from that user.{0} is replaced with the user name. ");
        const titleHidden = _TF("Click to show message content and future message content from {0}", "A tool tip message for a buttan that when clicked will show the hidden content of a specific chat message and all continue to show all further messages from that user.{0} is replaced with the user name. ");

        let showTimeStamp = localStorage.getItem(timeStampName) === "true";

        const hideFrom = new Map();
        const hideData = localStorage.getItem(hideName);
        if (hideData) {
            const ha = JSON.parse(hideData);
            if (Array.isArray(ha))
                ha.forEach(x => hideFrom.set(x, 1));
        }

        const hiddenClass = "Hidden";

        function isHidden(name) {
            return hideFrom.get(name) === 1;
        }

        const inputHistory = [];
        let inputHistoryPos = 0;
        let inputCurrent = "";

        /**
         * Hide or show a message from a user
         * @param {string} name The user guid
         * @param {boolean} hide True to hide future messages from this user
         */
        function setHideFrom(name, hide) {
            if (hide)
                hideFrom.set(name, 1);
            else
                hideFrom.delete(name);
            localStorage.setItem(hideName, JSON.stringify(Array.from(hideFrom.keys())));
        }

        /** Expand all items (none collapsed) */
        function setHideNone() {
            hideFrom.clear();
            localStorage.removeItem(hideName);
        }


        let isSending = false;
        /**
         * Send the message
         * @param {boolean} doFocus If true, the input box will be focused when done
         * @returns
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

        let isInternalUpdate = false;

        /** Set the input text from a history value */
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

        const send = new ColorIcon("IconSend", "IconColorThemeAcc1", options.SendIconSize, options.SendIconSize, _TF("Click to send the message", "A tool tip description on a button that when clicked will send the entered chat message to the server"), Send);
        chatInput.appendChild(send.Element);
        let listen = null;
        if (SpeechInput.supportSpeechRec()) {
            listen = new ColorIcon("IconChatMicOn", "IconColorThemeAcc1", options.AudioIconSize, options.AudioIconSize, _TF("Click to listen for speech input using the selected language", "A tool tip description on a button that when clicked will start listening to speech input"), () => {
                startListening(true, true);
            });
            chatInput.appendChild(listen.Element);
        }

        let previewElement = null;
        let canSend = null;
        /** Called to enable/disable the send button (every time the input text changes) */
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

        const start = localStorage.getItem(inputSaveKey);
        if (start) {
            write.value = start;
            write.NextLast = write.value.length;
        }

        const onInputChangeFn = () => {
            sendEnable();
            if (!isInternalUpdate) {
                inputHistoryPos = 0;
                inputCurrent = write.value;
            }
        };


        write.oninput = onInputChangeFn;



        const data = await sendRequest(apiBase + "Join", {
            ChatId: chatId,
            MaxCount: 10,
            FromId: 0,
        });
        const currentImage = "../auth/UserImages/" + data.UserGuid + "/small";

        const canTranslate = data.CanTranslate;

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
        let uploadRepo = null;
        if (data.UploadRepo && (typeof fileUploader !== "undefined")) {
            uploadRepo = data.UploadRepo;
            console.log('File uploading to repository "' + uploadRepo + '" is enabled.');
        }



        const sl = localStorage.getItem(langSaveKey);
        let currentLang = data.Lang;
        if (sl) {
            if (sl === data.Lang)
                localStorage.removeItem(langSaveKey);
            else
                currentLang = sl;
        }
        chatInput.style.backgroundImage = "url('../iso_data/language/" + currentLang + ".svg')";
        write.maxLength = data.MaxTextLength;


        /** Try to clear all messages from the chat room */
        async function cmdClear() {
            try {
                if (!await sendRequest(apiBase + "ClearAllMessages", chatId))
                    Fail(_TF("Failed to clear all chat messages.", "An error message shown when a user tries to send clear a chat room but the server returns a failure"));
            }
            catch (e) {
                Fail(_TF("Failed to clear all chat messages.", "An error message shown when a user tries to send clear a chat room but the server returns a failure") + "\n" + _TF("Error:", "The header to a techincal error message, typically an exception, the error text is shown on a new line after this header") + "\n" + e.message);
            }
        }


        // Handling of PostMessage messages (from parent window)
        const commandMap = new Map();
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




        if (data.CanClear)
            commandMap.set("chat.clear", cmdClear);


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
        * A function that checks if we can listen to user audio (open mic)
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
        const userVoices = new Map();
        const userSpeak = !!data.Voices;
        if (userSpeak) {
            for (const voice of data.Voices)
                userVoices.set(voice.Name, voice);
        }

        /**
        * A function that checks if we can use speech generation
        * @returns {boolean} True if we can use speech generation
        */
        function canSpeak() {
            return userSpeak && SpeechGen.supportSpeechGen();
        }

        const msg = data.Messages;

        const userName = data.UserName;


        const workingAttribute = "Working";


        /**
         * Get a language text string from a language iso id
         * @param {string} langId A language id, like: "en"
         * @returns {string} A string like "English [en]" or "[xx]" for unknown language ids
         */
        function getLangText(langId) {
            var mLang = languageMap.get(langId);
            return mLang ? (mLang.Name + " [" + langId + "]") : ("[" + langId + "]");
        }

        /**
         * Add server side menu items
         * @param {WebMenuItem} dest The destination menu object
         * @param {array} menuList Array of items to add
         * @param {number} messageId The message id (if we're adding it to a message menu)
         * @param {function(void)} close Function that when invoked closes pop-up menu
         * @returns {WebMenuItem} The destination menu object
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

        const htmlExtensions = Object.freeze({
            html: 1,
            htm: 1,
        });

        async function selectLanguage() {
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

        async function clearCurrentMessage() {
            const data = previewMessage.Data;
            if (data) {
                const parts = data.split(';');
                const pl = parts.lengths;
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
        }

        /**
         * Add a message to the chat window
         * @param {any} m The message data to add
         * @param {boolean} onTop If true, the message is added to the top, else to the bottom
         * @param {boolean} isPreview If true, the message is the preview message
         */
        async function addMessage(m, onTop, isPreview) {
            const msgRow = document.createElement("SysWeaver-ChatRow");
            const f = onTop ? chatE.firstElementChild : chatE.lastElementChild;
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

            /** Context menu handler for a message */
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
            msgHeader.oncontextmenu = ev => {
                if (badClick(ev, true))
                    return true;
                onMenuClick(ev);
                return false;
            };
            //  Header language
            if (msgLanguage) {
                keyboardClick(msgLanguage);
                const langFlag = document.createElement("img");
                msgLanguage.appendChild(langFlag);

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
                        let isTranslating = false;
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
                msgRow.UpdateLang = () => { };
            }

            //  Header name
            msgName.innerText = m.From;

            //  Header time
            msgTime.Value = m.Time;



            //  Header icon
            let fi = m.FromImage;
            if (fi === currentImage)
                fi = "../auth/UserImages/Current/small";

            const ttt = _TF("Click to show message menu", "Tool tip description on a button that when clicked will show a menu with options for a chat message");
            if (fi) {
                const img = document.createElement("img");
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
                        element = document.createElement("img");
                        element.src = p;
                        canDownload = true;
                    } else {
                        if (htmlExtensions[ext]) {
                            element = document.createElement("iframe");
                            element.sandbox = "allow-forms allow-presentation allow-scripts allow-same-origin allow-downloads allow-popups";
                            element.src = appendTheme(p);
                            canSave = false;
                        } else {
                            element = document.createElement("img");
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
            msgRow.Show = () => {
                msgHeader.IsHidden = false;
                const tf = ValueFormat.stringFormat(titleVisible, m.From);
                msgHeader.title = tf;
                msgName.title = tf;
                msgBody.classList.remove(hiddenClass);
                msgData.classList.remove(hiddenClass);
            };
            msgRow.Hide = () => {
                msgHeader.IsHidden = true;
                const tf = ValueFormat.stringFormat(titleHidden, m.From);
                msgHeader.title = tf;
                msgName.title = tf;
                msgBody.classList.add(hiddenClass);
                msgData.classList.add(hiddenClass);
            };


            if (!isPreview) {
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
         * Find the message element from message id
         * @param {number} msgId The message element to find
         * @returns {HTMLElement} The element of the message or null if not found
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
         * */
        function updateMessageFormat() {
            onAllMessages(e => e.UpdateFormat());
        }


        //  Handling server side messages
        const chatIdL = chatId.toLowerCase();
        const msgClear = "chat.clear." + chatIdL;
        const msgPost = "chat.post." + chatIdL;
        const msgRemove = "chat.remove." + chatIdL;
        const msgReplace = "chat.replace." + chatIdL;

        SessionManager.AddServerEvent(msgClear);
        SessionManager.AddServerEvent(msgPost);
        SessionManager.AddServerEvent(msgRemove);
        SessionManager.AddServerEvent(msgReplace);

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
                        const vp = userVoices.get(c.From);
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


        const defStopListening = () => {
            if (listen)
                listen.SetEnabled(true);
            //console.log("Chat: No need to stop, wasn't listening!");
        };

        let stopListening = defStopListening;

        const startListening = (ignoreKeyword, noOpenMic) => {

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


        // Mark down and preview

        let useMarkDown = !!localStorage.getItem(useMarkDownKey);
        useMarkDown &= data.AllowMarkDown;

        let showPreview = localStorage.getItem(previewKey); // null means auto (on for md and off for text)
        const previewMessage = {
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
        const tempFiles = new Map();
        previewElement = (await addMessage(previewMessage, true, true)).parentElement.parentElement;
        previewElement.classList.add("Preview");


        function previewChanged() {
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
        }

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


        async function markDownChanged() {
            const pm = previewElement.Msg;
            pm.Format = useMarkDown ? 1 : 0;
            write.placeholder = useMarkDown ? placeholderText : placeholderMD;
            await previewElement.UpdateBody(pm.Text, pm.Format, false);
            previewChanged();
        }

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




        let enableSpeech = localStorage.getItem(genKey) === "true";
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

        function GetDataCount() {
            const data = previewMessage.Data;
            if (!data)
                return 0;
            return data.split(';').length;
        }

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

        async function uploadFiles(files) {
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
        }

        // Drag and drop on text
        const validSchemas = new Map();
        validSchemas.set("http", 1);
        validSchemas.set("https", 1);
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






        let isLoading = false;
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

        function StickForAwhile(direct) {
            let stick = true;
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

        if (initSize) {
            initSize = parseFloat(initSize) * chatBackground.offsetHeight;
            chatE.style.height = initSize + "px";
        }

        sendEnable();

        StickForAwhile(true);
        write.readOnly = true;
        write.focus();
        write.readOnly = false;

        return chatE;

    }

}


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
