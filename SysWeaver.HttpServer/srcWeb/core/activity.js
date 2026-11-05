/**
 * @file core/activity.js - background work that stays findable in one place (the model of the app shell's activity list).
 *
 * Purpose: `Sw.Activity` (UX guide FBK-4): every long or background job of the page or the app (an export, an upload, a server job,
 * work that goes on after its dialog closed with "Continue in background") is a job in one list:
 *  - Start(o) -> job: a running job { Key, Title, Text, Progress, Cancel, Retry, EndsWithPage, Data }. A running job with the same Key
 *    is returned instead of starting a second one (job.IsNew false): reopening a dialog or its opener finds the running job.
 *  - job.Update({Progress, Text, Title, DoneTitle}), job.Done({Text, Url, Open, DoneTitle}), job.Fail(error, {Details}),
 *    job.Cancelled(), job.Cancel(), job.Remove() (a running job is cancelled first); job.State "running" | "stopping" | "done" |
 *    "failed" | "cancelled" (the one cancellation model, Sw.Cancel).
 *  - Texts are computed in ONE place (the shell's list and toasts, the stand-alone line and the announcements read them): job.Title
 *    (running: the title; ended: DoneTitle, ex: the file name), job.Label (Title + the time when another item has the same title),
 *    job.Text (the phase: "Stopping…" / "Cancel not sent yet …" > a note that stays ("Could not cancel: …") > "Waiting for the
 *    connection" > the work's phase), job.EndText {Text, Kind} (the result sentence: never "Done" after a Cancel).
 *  - Track(promise, o): Start, then Done / Fail from the promise (o.DoneText, or o.Result(value) -> the Done options; a Result that
 *    throws fails the job). Run(o, fn): the safe form for new code: fn(signal, job) gets a signal of the cancellation model
 *    (Sw.Cancel.Controller): Cancel stops reads, Sw.Sleep and requests not sent yet, but never aborts a write that was sent (the job
 *    then ends "Finished before it could stop", or "Stopped; what was sent before the Cancel was done"). The jobs of Track and Run are
 *    thenable (job.Finished: the value when done; a failure, a cancel or work that finished after a Cancel REJECTS, marked SwSaid: the
 *    list said it, nothing is delivered after a Cancel), so `OnClick: () => Sw.Activity.Run(...)` keeps a Sw.Button busy and puts
 *    the work in the list. Send(o): a server call (Sw.Net.Send, a file download with o.Download) that runs in the window of the list,
 *    so it survives leaving the page it was started on (o.Url required).
 *  - The user's Cancel is state: a cancel that can't reach the server stays pending ("Cancel not sent yet …") and is sent again when
 *    the connection is back (and after a reload: server jobs cancelled on this device are remembered); nothing is saved after it.
 *  - Options are checked in developer mode (Sw.DevOptions: unknown names, wrong types, a running Key reused for other work).
 *  - Find(key), List(), Counts() {Running, Failed (not opened yet), Done}, MarkSeen() (the list was opened: failures are no
 *    longer counted), On(fn) (changes {Type, Job}; a framed page's listener is removed by itself when the page is left), Open() (the
 *    shell shows the list), Supported (a list is shown: "Continue in background" may be offered), EndsWith(win) (the running jobs
 *    that stop when a page is left). Progress must be a finite 0..1 (anything else is "unknown", with a developer warning).
 * Windows: a same-origin page in the app shell uses the shell window's list (one list for the app; the list's own code runs, so a
 * job outlives the page that started it); a page without the shell has its own (no visual list: Supported false) and shows a small
 * activity line at the bottom (44 px items; while it is shown the page reserves its height at the end, so it never covers the page;
 * a developer warning when it still covers a control). Jobs started with
 * EndsWithPage (the default, except Send) stop when the page that started them is left: the shell asks first ("Leave this page?
 * <Title> may not finish." / "stops." for Kind "local", SAF-10) and the job ends as cancelled ("Stopped: the page was left"); "Try again" is only offered while the page
 * that knows the inputs is still open. A page that is left releases its jobs' Cancel / Retry functions and its listeners (no page leak).
 * Finished jobs stay in the list for the session of the window, failed ones until the list was opened and opened again.
 *
 * Load order: after core/page.js. Dependencies: Sw.Events, Sw.Net (Send, ErrorText).
 * Globals defined: none (Sw.Activity).
 */
(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};
    if (Sw.Activity)
        return;

    /** @type {number} Job id counter of this window's list. */
    let nextId = 0;

    /** @type {number} At most this many jobs are kept (the oldest finished ones go first). */
    const MaxJobs = 30;

    /**
     * The local storage key that marks a browser that started a server job (StartServer): only then does the shell ask for the jobs after
     * a reload and follow the "sw.job" push (RestoreServer), so a session that never used one sends nothing the original shell did not.
     * @type {string}
     */
    const UsedKey = "SysWeaver.Activity.Used";

    /**
     * A progress value: a finite number clamped to 0..1, else null (unknown: NaN, Infinity, a text are not shown as progress).
     * @param {*} v The value.
     * @returns {number|null} The progress.
     */
    function progressOf(v) {
        if (v === null || v === undefined)
            return null;
        if (typeof v !== "number" || !Number.isFinite(v)) {
            Sw.DevWarn && Sw.DevWarn("Sw.Activity: Progress must be a number from 0 to 1 (or null when unknown), got " + String(v), "activity.progress");
            return null;
        }
        //  A percent (40 of 100) would show "100 %": unknown, and the author is told (never a wrong number on screen)
        if (v > 1 || v < 0) {
            Sw.DevWarn && Sw.DevWarn("Sw.Activity: Progress must be a fraction from 0 to 1, got " + v + (v > 1 && v <= 100 ? " (a percent? pass " + (v / 100) + ")" : "") + "; it is shown as unknown", "activity.progress.range");
            return null;
        }
        return v;
    }

    /** The options of Sw.Activity.Start (Sw.DevOptions schema; Track / Run / Send / StartServer add theirs). */
    const StartOptions = {
        Key: ["string", "number", "null"], Title: { Type: "string" }, DoneTitle: ["string", "null"], Text: ["string", "null"], Progress: ["number", "null"], Cancel: "function", Retry: "function",
        EndsWithPage: "boolean", Data: "any", Kind: { Type: "string", Values: ["", "server", "export", "download", "upload", "save", "local"] }, Leave: "string",
        CancelMode: { Type: "string", Values: ["work", "self", "fn"] },
    };

    /** The options of Track / Run. */
    const TrackOptions = Object.assign({}, StartOptions, { DoneText: "string", Result: "function" });

    /** A file stamp of an export name ("2026-10-25 07-58 UTC+1"): its spaces and hyphens become no-break ones (it never wraps inside). */
    const StampRx = /\d{4}-\d{2}-\d{2}[ T]\d{2}[-:]\d{2}(?:[-:]\d{2})?(?: ?(?:UTC|GMT)(?:[+-]\d{1,2}(?::?\d{2})?)?)?/g;

    /**
     * Keep the file stamps of a text unbreakable (Sw.Table.KeepStamp when the table library is loaded, else the same rule here).
     * @param {string} s The text.
     * @returns {string} The text.
     */
    function keepStamp(s) {
        s = String(s ?? "");
        try {
            if (Sw.Table && typeof Sw.Table.KeepStamp === "function")
                return Sw.Table.KeepStamp(s);
        }
        catch {
        }
        return s.replace(StampRx, m => m.replace(/-/g, "‑").replace(/ /g, " "));
    }

    /**
     * Is an error a lost connection (the request did not reach the server, or no answer came)?
     * @param {*} e The error.
     * @returns {boolean} True for a lost connection.
     */
    function lostConnection(e) {
        try {
            return !!(Sw.Net && ((Sw.Net.IsNetworkError && Sw.Net.IsNetworkError(e)) || Sw.Net.Online === false));
        }
        catch {
            return false;
        }
    }

    /**
     * An end time: the given one (a server job's Ended: a Date, an ISO text or ms), else now.
     * @param {*} v The time.
     * @returns {Date} The time.
     */
    function endTime(v) {
        if (v !== undefined && v !== null && v !== "") {
            const d = v instanceof Date ? v : new Date(v);
            if (!isNaN(d.getTime()))
                return d;
        }
        return new Date();
    }

    /** Secret-looking parts of a text (SEC-1): PEM blocks, well known api token formats, JWTs, "token / key / secret / password" + value. */
    const SecretParts = [
        /-----BEGIN [A-Z0-9 ]{3,}-----[\s\S]*?(?:-----END [A-Z0-9 ]{3,}-----|$)/g,
        /\b[rsp]k_(?:live|test)_[A-Za-z0-9]{8,}/g,
        /\bgh[pousr]_[A-Za-z0-9]{20,}/g,
        /\bgithub_pat_\w{20,}/g,
        /\bxox[abprs]-[\w-]{10,}/g,
        /\bAKIA[0-9A-Z]{16}\b/g,
        /\beyJ[\w-]{8,}\.[\w-]{8,}\.[\w-]{4,}/g,
    ];

    /**
     * A text of the activity list (a title, a phase, a result, an error) with its secret-looking parts masked (SEC-1: the list and its
     * announcements never show an api key or a token a page put in a message): the first 4 characters stay, the rest is "****".
     * @param {*} s The text.
     * @returns {string} The masked text.
     */
    function mask(s) {
        let t = String(s ?? "");
        for (const r of SecretParts)
            t = t.replace(r, m => m.substring(0, 4) + "****");
        //  "token sk...", "key: abc...", "password=...": the value after the word, when it is a long random string
        t = t.replace(/\b(api[ _-]?key|key|token|secret|password|passwd|pwd|bearer)(\s*[:=]?\s*)([A-Za-z0-9+/=_.-]{16,})/gi, (m, w, sep, v) =>
            /\d/.test(v) && /[A-Za-z]/.test(v) ? w + sep + v.substring(0, 4) + "****" : m);
        return t;
    }

    /**
     * One job of the activity list (see the file header).
     */
    class Job {
        /**
         * @param {ActivityList} list The list.
         * @param {object} o The options of Start.
         * @param {Window} owner The window that started it.
         */
        constructor(list, o, owner) {
            /** @type {ActivityList} */
            this.List = list;
            /** @type {number} Id (unique in the list). */
            this.Id = ++nextId;
            /** @type {string|null} Key (one running job per key). */
            this.Key = o.Key ? String(o.Key) : null;
            /** @type {string} What runs ("Exporting report.xlsx"): Title while it runs. */
            this.RunTitle = mask(o.Title || _TF("Background work", "Name of a job in the activity list when the page gave it no name"));
            /** @type {string} The title once it ended ("report.xlsx"; "" = the same as while it ran). */
            this.DoneTitle = o.DoneTitle ? mask(keepStamp(o.DoneTitle)) : "";
            /** @type {string} The phase of the work ("Waiting for the server"); see Text. */
            this.Phase = o.Text ? mask(o.Text) : "";
            /** @type {string} A note that stays while it runs ("Could not cancel: <reason>"); see Text. */
            this.Note = "";
            /** @type {string} The text while stopping ("" = "Stopping…"); see Text. */
            this.StopText = "";
            /** @type {boolean} The connection is lost while it waits for the server ("Waiting for the connection"); see Text. */
            this.Waiting = false;
            /** @type {number|null} Progress 0..1, null when unknown. */
            this.Progress = progressOf(o.Progress);
            /** @type {string} "running" | "stopping" (Cancel asked, the work did not return yet) | "done" | "failed" | "cancelled". */
            this.State = "running";
            /** @type {Date} Start time. */
            this.Started = new Date();
            /** @type {Date|null} End time. */
            this.Ended = null;
            /** @type {function():*|null} The caller's cancel function (private: CancelFn / Cancel() go through the cancellation model). */
            this.CancelImpl = typeof o.Cancel === "function" ? o.Cancel : null;
            /**
             * @type {string} Who confirms a cancel (Sw.Cancel): "work" (Track / Run / Send: the work's promise ends it: an AbortError =
             * cancelled, a value = done anyway), "self" (a server job: the server's answer), "fn" (Start: the Cancel function's return;
             * false = not cancelled).
             */
            this.CancelMode = o.CancelMode === "work" || o.CancelMode === "self" ? o.CancelMode : "fn";
            /** @type {boolean} Cancel was asked (the end then says how it really ended; nothing is saved / delivered after it). */
            this.CancelAsked = false;
            /** @type {boolean} Cancel was asked, but the work finished anyway. */
            this.AfterCancel = false;
            /** @type {Date|null} When the result is gone (server jobs: kept 30 minutes). */
            this.Expires = null;
            /** @type {*} The value of the work (Track / Run). */
            this.Value = undefined;
            let res = null, rej = null;
            /** @type {Promise<*>} Settles when the job ended (see Finished). */
            this.FinishedP = new Promise((a, b) => {
                res = a;
                rej = b;
            });
            this.FinishedP.catch(() => { });
            this.Resolve = res;
            this.Reject = rej;
            /** @type {function():*|null} Starts the same work again with the original inputs ("Try again" after a failure). */
            this.RetryFn = typeof o.Retry === "function" ? o.Retry : null;
            /** @type {boolean} The work stops when the page that started it is left. */
            this.EndsWithPage = o.EndsWithPage ?? true;
            /**
             * @type {string} What leaving the page means for it (Sw.Page.WorkText kinds: "server", "export", "download", "upload", "save",
             * "local"); "" = not known: the leave question says "<Title> may not finish." (never "stops": it may be on the server).
             */
            this.Kind = o.Kind ? String(o.Kind) : "";
            /** @type {string} The sentence of the leave question (default from Kind and Title). */
            this.Leave = o.Leave ? String(o.Leave) : "";
            /** @type {Window} The window that started it. */
            this.Owner = owner;
            /** @type {*} Free data of the caller. */
            this.Data = o.Data;
            /** @type {{Text: string, Url: string|null, Open: function|null}|null} The result (Done). */
            this.Result = null;
            /** @type {string} The error text (Fail). */
            this.Error = "";
            /** @type {string} The error details (Fail, "Details"). */
            this.Details = "";
            /** @type {boolean} A failure was seen in the opened list (no longer counted). */
            this.Seen = false;
            /** @type {boolean} False when Start returned a running job with the same key. */
            this.IsNew = true;
        }

        /**
         * @type {string} The title, computed in one place: while it runs the title it was started with ("Exporting report.xlsx"), once
         * it ended its DoneTitle when it has one ("report.xlsx"). Setting it sets the running title.
         */
        get Title() {
            return !this.Running && this.DoneTitle ? this.DoneTitle : this.RunTitle;
        }

        set Title(v) {
            this.RunTitle = mask(v);
        }

        /**
         * @type {string} The title as the list shows it: Title, with the time added when another job of the list has the same title
         * ("report.csv (07:58:12)"), so history items can be told apart.
         */
        get Label() {
            const t = this.Title;
            const same = this.List.Jobs.filter(j => j !== this && j.Title === t);
            if (!same.length)
                return t;
            const d = this.Ended || this.Started;
            const fmt = x => x.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
            //  (two in the same second: a counter)
            const n = same.filter(j => fmt(j.Ended || j.Started) === fmt(d) && j.Id < this.Id).length;
            return t + " (" + fmt(d) + (n ? " #" + (n + 1) : "") + ")";
        }

        /**
         * @type {string} The phase line, computed in one place: while stopping "Stopping…" (or why it can't stop yet); while it runs a
         * note that must stay ("Could not cancel: <reason>") before "Waiting for the connection" before the phase of the work; once it
         * ended its end text. Setting it sets the phase (Update({Text}) can't overwrite a note or "Stopping…").
         */
        get Text() {
            if (this.State === "stopping")
                return this.StopText || Sw.Cancel.Texts.Stopping();
            if (this.State === "running") {
                if (this.Note)
                    return this.Note;
                if (this.Waiting)
                    return _TF("Waiting for the connection", "Phase of a job in the activity list while the connection to the server is lost (it goes on by itself)");
            }
            return this.Phase;
        }

        set Text(v) {
            this.Phase = v ? mask(v) : "";
        }

        /**
         * @type {{Text: string, Kind: string}} The result sentence of an ended job, computed in one place for the shell's toast, the
         * stand-alone line and the announcements: "Failed: <title>. <error>" (error), "<title>: finished before it could stop" (+ the
         * result text; Kind "warning": never "Done" after a Cancel), the result text or "Done: <title>" (ok), the cancel text (info).
         */
        get EndText() {
            const t = this.Title;
            if (this.State === "failed")
                return { Text: _T("Failed: {0}. {1}", t, this.Error, "Toast and announcement when background work of the activity list failed.{0} is replaced with the work, {1} with the error"), Kind: "error" };
            if (this.State === "done" && this.AfterCancel) {
                const r = this.Result && this.Result.Text ? this.Result.Text : "";
                return { Text: r && r.indexOf(t) >= 0 ? r : [Sw.Cancel.Texts.FinishedAnywayOf(t), r].filter(Boolean).join(" "), Kind: "warning" };
            }
            if (this.State === "done")
                return { Text: this.Result && this.Result.Text ? this.Result.Text : _T("Done: {0}", t, "Toast and announcement when background work of the activity list finished.{0} is replaced with the work"), Kind: "ok" };
            if (this.State === "cancelled")
                return { Text: this.Text || Sw.Cancel.Texts.Cancelled(), Kind: "info" };
            return { Text: "", Kind: "" };
        }

        /** @returns {boolean} True while the work has not returned (State "running" or "stopping"). */
        get Running() {
            return this.State === "running" || this.State === "stopping";
        }

        /** @returns {boolean} Cancel was asked and the work did not return yet ("Stopping…"). */
        get Stopping() {
            return this.State === "stopping";
        }

        /**
         * @type {function():Promise<boolean>|null} Cancel this job (the same as job.Cancel(): the cancellation model, never a raw call
         * of the caller's function), null when it can't be cancelled now. Setting it replaces the caller's cancel function.
         */
        get CancelFn() {
            return this.CanCancel ? () => this.Cancel() : null;
        }

        set CancelFn(fn) {
            this.CancelImpl = typeof fn === "function" ? fn : null;
        }

        /**
         * @type {Promise<*>} Settles when the job ended: resolves with the work's value when done; rejects with an AbortError when it
         * was cancelled (it stopped) and with the error when it failed. The rejection is marked SwSaid (the activity list shows it), so a
         * Sw.Button that awaits it shows nothing more. Use it to wait for any job (also server jobs): `await job.Finished`.
         * Work that finished although Cancel was asked delivers nothing (the user cancelled it): Finished rejects with an AbortError marked
         * FinishedAnyway (its Value is the work's value; Sw.Cancel.Settle reads it as done + AfterCancel), so the code after an `await`
         * doesn't save or show a result and a button never says "done".
         */
        get Finished() {
            return this.FinishedP;
        }

        /**
         * Settle Finished.
         * @param {*} err Null for done, else the error.
         */
        Settle(err) {
            if (!err && this.AfterCancel) {
                err = new Error(Sw.Cancel.Texts.FinishedAnyway());
                err.name = "AbortError";
                err.FinishedAnyway = true;
                err.Value = this.Value;
            }
            if (err) {
                try {
                    err.SwSaid = true;
                }
                catch {
                }
                this.Reject(err);
            }
            else
                this.Resolve(this.Value);
            if (this.PendingOff) {
                this.PendingOff();
                this.PendingOff = null;
            }
            //  Removed while it ran (Remove = Cancel first): it leaves the list now that it ended
            if (this.RemoveAfter && !this.Running)
                setTimeout(() => this.Remove(), 0);
        }

        /**
         * @returns {string} The sentence of the leave question while it runs (Leave, else Sw.Page.WorkText(Title, Kind)): "<Title> may not
         * finish." (it may be on the server), "<Title> stops." only for Kind "local".
         */
        get LeaveText() {
            if (this.Leave)
                return this.Leave;
            if (Sw.Page && typeof Sw.Page.WorkText === "function")
                return Sw.Page.WorkText(this.Title, this.Kind);
            return this.Kind === "local"
                ? _T("{0} stops.", this.Title, "Text of the question 'Leave this page?' for work that runs in the page and stops when it is left.{0} is replaced with the work, ex: Exporting report.xlsx")
                : _T("{0} may not finish.", this.Title, "Text of the question 'Leave this page?' for work that may not finish when the page is left (it may run on the server).{0} is replaced with the work, ex: Exporting report.xlsx");
        }

        /** @returns {boolean} The window that started the job is still open (its Cancel / Retry functions work). */
        get OwnerAlive() {
            try {
                return !!this.Owner && !this.Owner.closed && !!this.Owner.document;
            }
            catch {
                return false;
            }
        }

        /** @returns {boolean} "Try again" can be offered. */
        get CanRetry() {
            return !!this.RetryFn && !this.Running && this.OwnerAlive;
        }

        /** @returns {boolean} Cancel can be offered. */
        get CanCancel() {
            return !!this.CancelImpl && this.State === "running" && this.OwnerAlive;
        }

        /**
         * Update a running job.
         * @param {{Progress: (number|null), Text: string, Title: string}} o Progress 0..1 (null: unknown), the phase text, a new title.
         * @returns {Job} This job.
         */
        Update(o) {
            if (!this.Running || !o)
                return this;
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Activity job.Update", o, { Progress: ["number", "null"], Text: "string", Title: { Type: "string" }, DoneTitle: "string" });
            if (o.Progress !== undefined)
                this.Progress = progressOf(o.Progress);
            //  (the phase: the Text getter decides what shows: "Stopping…", a note, "Waiting for the connection" come first)
            if (o.Text !== undefined)
                this.Phase = o.Text ? mask(o.Text) : "";
            if (o.Title)
                this.RunTitle = mask(o.Title);
            if (o.DoneTitle)
                this.DoneTitle = mask(keepStamp(o.DoneTitle));
            this.List.Emit("update", this);
            return this;
        }

        /**
         * The job finished.
         * @param {{Text: string, Url: string, Open: function, Quiet: boolean, Ended: *, Value: *}} [o] The result (Quiet: shown in place
         * already or history, no toast): a text ("report.xlsx is ready"), a link (Url) or an action (Open) that shows it; Ended (the
         * server's end time); Value (what Finished resolves with).
         * @returns {Job} This job.
         */
        Done(o) {
            if (!this.Running)
                return this;
            o = o || {};
            //  Cancel was asked but the work finished: say so (never "cancelled" for work that was done)
            this.AfterCancel = this.CancelAsked;
            this.State = "done";
            this.Ended = endTime(o.Ended);
            this.Progress = 1;
            //  (the phase text is over: a done job shows its result)
            this.Note = "";
            this.Phase = this.AfterCancel ? Sw.Cancel.Texts.FinishedAnyway() : "";
            if (o.Value !== undefined)
                this.Value = o.Value;
            if (o.DoneTitle)
                this.DoneTitle = mask(keepStamp(o.DoneTitle));
            this.Result = { Text: o.Text ? mask(keepStamp(o.Text)) : "", Url: o.Url ? String(o.Url) : null, Open: typeof o.Open === "function" ? o.Open : null };
            //  Quiet: the result was shown where it happened (ex: in the table row) or it is history (a job restored after a reload): the
            //  list keeps it, the shell shows no result toast
            if (o.Quiet)
                this.Quiet = true;
            this.List.Emit("done", this);
            this.Settle(null);
            return this;
        }

        /**
         * The job failed.
         * @param {*} error The error (an Error, a Sw.Net.Error or a text).
         * @param {{Details: string, Ended: *, Quiet: boolean}} [o] Details (shown by "Details"; default: the raw error when the text was
         * made readable); Ended (the server's end time); Quiet (no toast: a restored job, or shown in place).
         * @returns {Job} This job.
         */
        Fail(error, o) {
            if (!this.Running)
                return this;
            o = o || {};
            const raw = error && typeof error === "object" ? String(error.message ?? error) : String(error ?? "");
            const et = Sw.Net && Sw.Net.ErrorText ? Sw.Net.ErrorText(error && typeof error === "object" ? error : raw) : { Text: raw, Details: "" };
            this.State = "failed";
            this.Ended = endTime(o.Ended);
            this.Note = "";
            this.Phase = "";
            this.Error = mask(et.Text || raw) || _TF("The work failed.", "Error text of a job in the activity list that failed without a message");
            this.Details = mask(o.Details ? String(o.Details) : (et.Details || ""));
            this.Seen = !!o.Quiet;
            if (o.Quiet)
                this.Quiet = true;
            this.List.Emit("fail", this);
            this.Settle(error && typeof error === "object" ? error : new Error(this.Error));
            return this;
        }

        /**
         * The job was cancelled and the work CONFIRMED that it stopped (it threw an AbortError, the server answered "cancelled", its page
         * was left). Work that only was asked to stop is "stopping" (Cancel), never "cancelled".
         * @param {string} [text] Why ("Stopped: the page was left").
         * @param {{Ended: (Date|string|number), Quiet: boolean}} [o] The server's end time; Quiet (a restored job: history).
         * @returns {Job} This job.
         */
        Cancelled(text, o) {
            if (!this.Running)
                return this;
            o = o || {};
            this.State = "cancelled";
            this.Ended = endTime(o.Ended);
            this.Note = "";
            this.Phase = text ? mask(text) : Sw.Cancel.Texts.Cancelled();
            if (o.Quiet)
                this.Quiet = true;
            this.List.Emit("cancel", this);
            this.Settle(Sw.Cancel.Error());
            return this;
        }

        /**
         * Cancel a running job (the one cancellation model, Sw.Cancel): the state becomes "stopping" ("Stopping…", no second Cancel), the
         * caller's cancel function runs, and the job says "cancelled" only when the work confirmed it: Track / Run / Send jobs when their
         * work ends with an AbortError (a value = "Finished before it could stop"), server jobs by the server's answer, Start jobs when
         * the cancel function returned (false = "Could not cancel", the job runs on). A failing cancel function: "Could not cancel:
         * <reason>" (it stays until the job ends). Nothing is saved or delivered after a cancel (a server file that was ready is offered,
         * not saved). The user's Cancel is STATE, not a text: when it can't reach the server (the connection is lost) the job stays
         * "stopping" ("Cancel not sent yet: …"), the cancel is sent again when the connection is back (a cancel function must be safe to
         * repeat: CancelJob is), and the work that ends meanwhile delivers nothing.
         * @returns {Promise<boolean>} True when the cancel was asked (the job is "stopping" or "cancelled").
         */
        async Cancel() {
            if (!this.CanCancel)
                return false;
            this.CancelAsked = true;
            this.State = "stopping";
            this.Note = "";
            this.StopText = this.WriteOnTheWay ? Sw.Cancel.Texts.StoppingWrite() : "";
            if (typeof this.OnCancelAsked === "function") {
                try {
                    this.OnCancelAsked();
                }
                catch {
                }
            }
            this.List.Emit("update", this);
            return await this.SendCancel();
        }

        /**
         * Send the cancel (the caller's cancel function); a lost connection keeps it pending and sends it again when the connection is back.
         * @returns {Promise<boolean>} True when the cancel was asked.
         */
        async SendCancel() {
            const fn = this.CancelImpl;
            if (!fn || this.State !== "stopping")
                return this.State === "cancelled";
            let r;
            try {
                r = await fn();
            }
            catch (e) {
                if (lostConnection(e) && this.State === "stopping") {
                    this.CancelPending();
                    return true;
                }
                this.Refused(Sw.Net && Sw.Net.ErrorText ? Sw.Net.ErrorText(e).Text : String(e && e.message || e || ""));
                return false;
            }
            //  It ended meanwhile (the work threw its AbortError, the server answered)
            if (this.State !== "stopping")
                return this.State === "cancelled";
            this.StopText = this.StopText === Sw.Cancel.Texts.Pending() ? "" : this.StopText;
            if (r === false) {
                this.Refused("");
                return false;
            }
            if (this.CancelMode === "fn")
                this.Cancelled();
            return true;
        }

        /** The cancel could not reach the server: kept ("Cancel not sent yet: …") and sent again when the connection is back. */
        CancelPending() {
            this.StopText = Sw.Cancel.Texts.Pending();
            this.List.Emit("update", this);
            if (this.PendingOff || !Sw.Events)
                return;
            let once = false;
            const again = () => {
                //  (the connection came back or the retry time is over: whichever comes first sends it once)
                if (once)
                    return;
                once = true;
                if (this.PendingOff) {
                    this.PendingOff();
                    this.PendingOff = null;
                }
                if (this.State === "stopping") {
                    this.StopText = "";
                    this.List.Emit("update", this);
                    this.SendCancel();
                }
            };
            this.PendingOff = Sw.Events.On("net.online", again);
            //  (the server answers other requests, but not this one: again with a growing interval, 2, 4, 8 … 30 s)
            if (Sw.Net && Sw.Net.Online !== false) {
                this.PendingTries = (this.PendingTries || 0) + 1;
                setTimeout(again, Math.min(30000, 1000 * Math.pow(2, this.PendingTries)));
            }
        }

        /**
         * A cancel was refused (the server said no, the cancel function returned false or failed): the job runs on and says so ("Could
         * not cancel: <reason>", kept until the job ends: no later phase text replaces it).
         * @param {string} why The reason ("" when none).
         */
        Refused(why) {
            if (this.State !== "stopping")
                return;
            this.State = "running";
            this.CancelAsked = false;
            this.StopText = "";
            this.Note = mask(Sw.Cancel.Texts.CouldNot(why));
            if (typeof this.OnCancelRefused === "function") {
                try {
                    this.OnCancelRefused();
                }
                catch {
                }
            }
            this.List.Emit("update", this);
        }

        /**
         * Start the same work again ("Try again" of a failed job; the original inputs are kept by the Retry function).
         * @returns {Promise<void>}
         */
        async Retry() {
            if (!this.CanRetry)
                return;
            const fn = this.RetryFn;
            this.Remove();
            await fn();
        }

        /**
         * Remove the job from the list. A running job is CANCELLED first (the one model: never "cancelled" while the work goes on): it
         * leaves the list when it ended; one that can't be cancelled stays (a developer warning).
         * @returns {boolean} True when it was removed now.
         */
        Remove() {
            const l = this.List.Jobs;
            const i = l.indexOf(this);
            if (i < 0)
                return false;
            if (this.Running) {
                if (this.State === "running" && !this.CanCancel) {
                    Sw.DevWarn && Sw.DevWarn("Sw.Activity job.Remove(): \"" + this.Title + "\" is running and has no Cancel: it stays in the list until it ends (removing it would hide work that goes on)", "activity.remove.running");
                    return false;
                }
                this.RemoveAfter = true;
                if (this.State === "running")
                    this.Cancel();
                return false;
            }
            l.splice(i, 1);
            if (this.PendingOff) {
                this.PendingOff();
                this.PendingOff = null;
            }
            //  A server job: the server forgets it too (the user dismissed it)
            if (typeof this.RemoveFn === "function") {
                try {
                    this.RemoveFn();
                }
                catch {
                }
            }
            this.List.Emit("remove", this);
            return true;
        }

        /** @returns {boolean} A write sent with the job's signal is on its way (a Cancel does not abort it: "what was sent may still be done"). */
        get WriteOnTheWay() {
            const s = this.Signal && Sw.Cancel.Sent ? Sw.Cancel.Sent(this.Signal) : null;
            return !!(s && s.Pending > 0);
        }
    }

    /**
     * The list of one window (the shell window's list is used by all its same-origin frames). Its methods run in its own window,
     * so the jobs it creates (and Send) outlive the page that asked for them.
     */
    class ActivityList {
        constructor() {
            /** @type {Array<Job>} The jobs, oldest first. */
            this.Jobs = [];
            /** @type {Array<function({Type: string, Job: Job}):void>} Change listeners. */
            this.Listeners = [];
            /** @type {boolean} A visual list is shown (the app shell sets it). */
            this.IsHost = false;
            /** @type {function():void|null} Shows the list (set by the app shell). */
            this.OnOpen = null;
        }

        /**
         * Tell the listeners about a change.
         * @param {string} type The change ("start", "update", "done", "fail", "cancel", "remove", "seen").
         * @param {Job|null} job The job.
         */
        Emit(type, job) {
            for (const fn of this.Listeners.slice()) {
                //  A listener of a page that is gone (a framed page the shell replaced) is dropped, never called (no page leak)
                if (fn.SwOwner && !alive(fn.SwOwner)) {
                    this.Off(fn);
                    continue;
                }
                try {
                    fn({ Type: type, Job: job });
                }
                catch (e) {
                    console.error("Sw.Activity: a listener failed: " + (e && e.message));
                }
            }
        }

        /**
         * Remove a listener.
         * @param {function} fn The listener (as added).
         */
        Off(fn) {
            const i = this.Listeners.indexOf(fn);
            if (i >= 0)
                this.Listeners.splice(i, 1);
        }

        /**
         * Start a job (a running job with the same Key is returned instead, IsNew false).
         * @param {object} o The options (see Sw.Activity.Start).
         * @param {Window} owner The window that starts it.
         * @returns {Job} The job.
         */
        Start(o, owner) {
            o = o || {};
            if (o.Key) {
                const r = this.Jobs.find(j => j.Running && j.Key === String(o.Key));
                if (r) {
                    r.IsNew = false;
                    //  Other work under a running key never runs: tell the author (one key per kind of work, ex: "export:" + url)
                    if (o.Title && mask(o.Title) !== r.RunTitle)
                        Sw.DevWarn && Sw.DevWarn("Sw.Activity: a job with Key \"" + o.Key + "\" is running (\"" + r.RunTitle + "\"), so \"" + o.Title + "\" was NOT started (the running job is returned, IsNew false). Use another Key for other work (UX guide FBK-4).", "activity.key." + o.Key + "." + o.Title);
                    return r;
                }
            }
            const j = new Job(this, o, owner || window);
            this.Jobs.push(j);
            //  Keep the list short: the oldest finished / cancelled jobs (and failures already seen) go first
            const old = this.Jobs.filter(x => x.State === "done" || x.State === "cancelled" || (x.State === "failed" && x.Seen));
            while (this.Jobs.length > MaxJobs && old.length)
                old.shift().Remove();
            this.Emit("start", j);
            return j;
        }

        /**
         * A server call as a job of this list (see Sw.Activity.Send).
         * @param {object} o The options (Url absolute).
         * @param {Window} owner The window that asked.
         * @returns {Job} The job.
         */
        Send(o, owner) {
            const t = this;
            const run = () => {
                //  (the cancellation model's signal: a write that was sent is never aborted; a download is a read)
                const ac = Sw.Cancel.Controller();
                const j = t.Start({
                    Key: o.Key, Title: o.Title, DoneTitle: o.DoneTitle, Text: o.Text, EndsWithPage: false, Data: o.Data, Kind: o.Kind || (o.Download ? "download" : "server"),
                    Cancel: () => ac.abort(), CancelMode: "work",
                    Retry: () => run(),
                }, window);
                if (!j.IsNew)
                    return j;
                j.Signal = ac.signal;
                //  A download (an export: a read) waits for a lost connection and goes on (FBK-9); other calls only when the caller says
                //  they are safe to repeat (o.Retry)
                const retry = o.Retry !== undefined ? o.Retry : (o.Download ? "online" : 0);
                const offs = retry === "online" && Sw.Events ? [
                    Sw.Events.On("net.offline", () => {
                        j.Waiting = true;
                        t.Emit("update", j);
                    }),
                    Sw.Events.On("net.online", () => {
                        j.Waiting = false;
                        t.Emit("update", j);
                    }),
                ] : [];
                const unhook = () => {
                    j.Waiting = false;
                    offs.forEach(f => typeof f === "function" && f());
                };
                const ro = o.ReadOnly !== undefined ? !!o.ReadOnly : !!o.Download;
                Sw.Net.Send(o.Url, o.Arg, { Blob: !!o.Download, Signal: ac.signal, NoNavigate: true, Retry: retry || undefined, RetryMs: o.RetryMs, ReadOnly: ro }).finally(unhook).then(r => {
                    //  (after a cancel nothing is delivered: the file is offered, not saved)
                    if (o.Download && r instanceof Blob && j.CancelAsked)
                        j.Done({ Open: () => saveBlob(r, o.Download) });
                    else if (o.Download && r instanceof Blob) {
                        saveBlob(r, o.Download);
                        j.Done({ Text: o.DoneText || _T("{0} was downloaded", o.Download, "Result of a download job in the activity list.{0} is replaced with the file name") });
                    }
                    else
                        j.Done({ Text: o.DoneText || "" });
                }, e => {
                    if (e && e.name === "AbortError")
                        j.Cancelled();
                    else
                        j.Fail(e);
                });
                return j;
            };
            return run();
        }

        /**
         * Follow a server job (the server's background job api, Api/jobs/*: its result survives a reload) as a job of this list: the
         * progress by WaitJob (waits for a lost connection: "Waiting for the connection"), the result (a file) saved when it is ready
         * (Save) and again by Open, Remove = RemoveJob. Cancel (job.Cancel(), also job.CancelFn()) sends CancelJob and follows the
         * server's answer: "stopping" until the server says "cancelled"; a job that was done already says so and its file is offered,
         * never saved; a refusal says "Could not cancel: <reason>". A job that is followed already is returned.
         * Restored jobs (o.Restored: after a reload, a new session, another tab or device of the user) are history: a job that had ended
         * is listed quietly with the server's end time; one that ends while it is watched here shows its result only when no other window
         * showed it already (the server's Announced flag, else the ids acknowledged on this device).
         * @param {object} info The server's job ({ Id, Key, Kind, Title, State, Progress, Text, Error, Started, Ended, Expires, ResultName,
         * Announced }).
         * @param {{Save: boolean, DoneText: string, Restored: boolean}} o Save: save the file when it is ready (StartServer); Restored.
         * @returns {Job} The job.
         */
        Server(info, o) {
            const t = this;
            o = o || {};
            const api = (Sw.Url && Sw.Url.Root ? Sw.Url.Root : "/") + "Api/jobs/";
            const id = String(info.Id);
            const known = t.Jobs.find(j => j.ServerId === id);
            if (known) {
                //  Started here after the push listed it (a race): this window saves the file
                if (!o.Restored && known.SrvOpt && known.SrvOpt.Restored)
                    known.SrvOpt = o;
                return known;
            }
            /** Ask the server to cancel; the answer decides (Job.Cancel, CancelMode "self"). @returns {Promise<boolean>} */
            const serverCancel = async () => {
                const x = await Sw.Net.Send(api + "CancelJob", id, { NoNavigate: true, Detached: true });
                if (!x || !x.State)
                    return false;
                await apply(x, true);
                return true;
            };
            const j = new Job(t, {
                Key: info.Key || ("sw.job:" + id), Title: info.Title, DoneTitle: o.DoneTitle || "", Text: info.Text, Progress: info.Progress, Kind: info.Kind || "server", EndsWithPage: false,
                Cancel: serverCancel, CancelMode: "self",
            }, window);
            j.ServerId = id;
            j.SrvOpt = o;
            j.RemoveFn = () => Sw.Net.Send(api + "RemoveJob", id, { NoNavigate: true, Detached: true }).catch(() => null);
            //  The user's Cancel is kept on this device until the job ended (a reload, a lost connection): sent again, and the history
            //  of a job that finished anyway says so ("was ready before it could stop")
            j.OnCancelAsked = () => cancelMark(id, true);
            j.OnCancelRefused = () => cancelMark(id, false);
            if (info.Started)
                j.Started = endTime(info.Started);
            if (info.Expires)
                j.Expires = endTime(info.Expires);
            const ended = x => x && x.Ended ? x.Ended : undefined;
            //  A job that had ended before it was listed here is history: listed quietly (no toast, no announcement), FBK-5 / FBK-8
            const history = !!o.Restored && info.State !== "running" && info.State !== "stopping" && !info.Stopping;
            if (history) {
                j.Quiet = true;
                acknowledge(id, info);
            }
            const cancelledHere = cancelMarked(id);
            if (cancelledHere)
                j.CancelAsked = true;
            t.Jobs.push(j);
            t.Emit("start", j);
            //  Cancelled on this device before a reload / while the connection was lost, and still running: the cancel is sent again
            if (cancelledHere && !history && info.State === "running" && !info.Stopping) {
                j.State = "stopping";
                t.Emit("update", j);
                setTimeout(() => j.SendCancel(), 0);
            }
            /** Fetch the result file and save it. @returns {Promise<void>} */
            const save = async () => {
                const f = await Sw.Net.Send(api + "GetJobResult", id, { NoNavigate: true, Retry: "online", Detached: true });
                if (!f || !f.Data)
                    return;
                const bin = atob(f.Data);
                const bytes = new Uint8Array(bin.length);
                for (let i = 0; i < bin.length; ++i)
                    bytes[i] = bin.charCodeAt(i);
                saveBlob(new Blob([bytes], { type: f.Mime || "application/octet-stream" }), f.Name || info.ResultName || "download");
            };
            /**
             * The end seen here is shown (a toast) only once for the user: not for history, not when another window / device showed it
             * already. A restored job gives the window that started it a moment to show it first.
             * @param {object} x The job info.
             * @param {boolean} fresh The end was seen now (not at the restore).
             * @returns {Promise<boolean>} True to show it.
             */
            const showEnd = async (x, fresh) => {
                if (!fresh || history)
                    return false;
                if (j.SrvOpt && j.SrvOpt.Restored) {
                    await Sw.Util.Delay(1500);
                    if (acknowledged(id, x))
                        return false;
                }
                acknowledge(id, x);
                return true;
            };
            /** Show the server's state. @param {object} x The job info. @param {boolean} fresh The end was seen now. */
            const apply = async (x, fresh) => {
                if (!x || !j.Running)
                    return;
                if (x.Expires)
                    j.Expires = endTime(x.Expires);
                //  The server stops the work (Cancel asked: State "running" + Stopping, or an older "stopping"): "Stopping…" until it
                //  says "cancelled" (the slot is still in use)
                if ((x.State === "running" && x.Stopping) || x.State === "stopping") {
                    if (j.State === "running") {
                        j.CancelAsked = true;
                        j.State = "stopping";
                        j.Note = "";
                        j.List.Emit("update", j);
                    }
                    else if (j.State === "stopping" && j.StopText === Sw.Cancel.Texts.Pending()) {
                        //  The server has the cancel now
                        j.StopText = "";
                        j.List.Emit("update", j);
                    }
                    return;
                }
                if (x.State === "running") {
                    j.Update({ Progress: x.Progress ?? null, Text: x.Text || "" });
                    return;
                }
                //  Cancel was asked anywhere (this device, another session / device: the server keeps CancelRequested): a job that
                //  finished anyway is never a plain "Done … Open" ("… was ready before it could stop; it was not saved.")
                if (x.CancelRequested || x.Cancelled === true || x.CancelAsked === true)
                    j.CancelAsked = true;
                const loud = await showEnd(x, fresh);
                if (!j.Running)
                    return;
                const quiet = !loud;
                if (x.State === "done") {
                    const name = x.ResultName || "";
                    const so = j.SrvOpt || {};
                    const shown = keepStamp(name);
                    //  Nothing is delivered after a cancel: the file is offered (Open), never saved by itself
                    if (loud && so.Save !== false && name && !j.CancelAsked)
                        await save().catch(e => j.Fail(e));
                    if (!j.Running)
                        return;
                    j.Done({
                        //  Done: titled by its file (the title said what ran: "Exporting …"), restored history too
                        DoneTitle: j.DoneTitle ? undefined : shown || undefined,
                        Text: j.CancelAsked
                            ? (name ? Sw.Cancel.Texts.ReadyNotSaved(shown) : (x.ResultText || Sw.Cancel.Texts.FinishedAnyway()))
                            : (so.DoneText || (name ? _T("{0} is ready", shown, "Result of a server job in the activity list.{0} is replaced with the file name") : (x.ResultText || ""))),
                        Open: name ? () => save().catch(e => Sw.Fail ? Sw.Fail(e) : null) : null,
                        Ended: ended(x), Quiet: quiet,
                    });
                }
                else if (x.State === "failed")
                    j.Fail(x.Error || "", { Ended: ended(x), Quiet: quiet });
                else
                    j.Cancelled(undefined, { Ended: ended(x), Quiet: quiet });
            };
            //  The connection is lost: the item says what it waits for (FBK-9, the Text getter: a note or "Stopping…" stay first); back:
            //  the server's phase again
            const offs = Sw.Events ? [
                Sw.Events.On("net.offline", () => {
                    j.Waiting = true;
                    t.Emit("update", j);
                }),
                Sw.Events.On("net.online", () => {
                    j.Waiting = false;
                    t.Emit("update", j);
                }),
            ] : [];
            (async () => {
                let x = info;
                let fresh = !history;
                try {
                    while (j.Running && x && (x.State === "running" || x.State === "stopping")) {
                        await apply(x, false);
                        try {
                            x = await Sw.Net.Send(api + "WaitJob", { Id: id, Ms: 20000 }, { NoNavigate: true, Retry: "online", Detached: true });
                        }
                        catch (e) {
                            //  Gone on the server (expired, a restart): history, quietly
                            if (j.Running)
                                j.Fail(e, { Quiet: !!(e && e.Status === 404) });
                            return;
                        }
                        fresh = true;
                    }
                    await apply(x, fresh);
                }
                finally {
                    j.Waiting = false;
                    offs.forEach(f => typeof f === "function" && f());
                }
            })();
            return j;
        }

        /**
         * The running jobs that stop when a window is left (its nested frames count too).
         * @param {Window} win The window.
         * @returns {Array<Job>} The jobs.
         */
        EndsWith(win) {
            const inside = w => {
                for (let x = w, i = 0; x && i < 12; ++i) {
                    if (x === win)
                        return true;
                    try {
                        if (x === x.parent)
                            return false;
                        x = x.parent;
                    }
                    catch {
                        return false;
                    }
                }
                return false;
            };
            return this.Jobs.filter(j => j.Running && j.EndsWithPage && j.Owner && inside(j.Owner));
        }

        /**
         * A window is left: its running EndsWithPage jobs stop.
         * @param {Window} win The window.
         */
        Left(win) {
            for (const j of this.Jobs.slice()) {
                if (j.Owner !== win)
                    continue;
                if (j.Running && j.EndsWithPage)
                    j.Cancelled(_TF("Stopped: the page was left", "State of a job in the activity list that stopped because the user left the page that ran it"));
                //  The page is gone: its functions can't run any more and must not keep it in memory (the job stays in the list)
                if (!j.Running) {
                    j.CancelFn = null;
                    j.RetryFn = null;
                    j.Owner = null;
                }
            }
            for (const fn of this.Listeners.slice())
                if (fn.SwOwner === win)
                    this.Off(fn);
        }
    }

    /**
     * Save a Blob as a file from this window.
     * @param {Blob} blob The data.
     * @param {string} name The file name.
     */
    function saveBlob(blob, name) {
        const u = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = u;
        a.download = name || "download";
        a.style.display = "none";
        document.body.append(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(u), 60000);
    }

    /**
     * Test if a window is still open with a document (a removed frame's window is closed).
     * @param {Window} w The window.
     * @returns {boolean} True while it lives.
     */
    function alive(w) {
        try {
            return !!w && !w.closed && !!w.document;
        }
        catch {
            return false;
        }
    }

    /**
     * Make a tracked job thenable (Track, Run): it settles when the JOB ends (job.Finished): the work's value when done (also "finished
     * before it could stop"); a rejection with an AbortError when it was cancelled and the work stopped, with the error when it failed,
     * both marked SwSaid (the activity list shows them: one place for the result, FBK-5). So returning the job from a Sw.Button OnClick
     * keeps the button busy while the work runs, and the button never says "done" for cancelled or failed work.
     * @param {Job} j The job.
     */
    function thenable(j) {
        Object.defineProperty(j, "then", { configurable: true, enumerable: false, value: (a, b) => j.Finished.then(a, b) });
        Object.defineProperty(j, "catch", { configurable: true, enumerable: false, value: b => j.Finished.catch(b) });
        Object.defineProperty(j, "finally", { configurable: true, enumerable: false, value: f => j.Finished.finally(f) });
    }

    // ------------------------------------------------------------------ server jobs: an end is shown once per user

    /** Ids of server jobs kept as acknowledged on this device (ms) at most this long (the server keeps a result 30 minutes). */
    const AckMaxAgeMs = 2 * 3600 * 1000;

    /** @type {boolean} The server has no "Api/jobs/AckJob" (an older server): only the device's list is used. */
    let noAnnounceApi = false;

    /**
     * The local storage key of the acknowledged server job ids (per site; job ids are unique per user, so one list serves every user
     * of the browser).
     * @returns {string} The key.
     */
    function ackKey() {
        let p = "/";
        try {
            p = new URL(Sw.Url.Root).pathname || "/";
        }
        catch {
        }
        return "SysWeaver.JobsSeen." + encodeURIComponent(p).replace(/\./g, "%2E");
    }

    /**
     * Read the acknowledged ids of this device (expired ones dropped).
     * @returns {Object<string, number>} id -> when.
     */
    function ackRead() {
        try {
            const v = JSON.parse(window.localStorage.getItem(ackKey()) || "{}");
            const now = Date.now();
            const r = {};
            for (const k of Object.keys(v || {}))
                if (typeof v[k] === "number" && now - v[k] < AckMaxAgeMs)
                    r[k] = v[k];
            return r;
        }
        catch {
            return {};
        }
    }

    /**
     * Was the end of a server job shown to the user already (the server's Announced flag, or this device's list)?
     * @param {string} id The job id.
     * @param {object} [x] The server's job info.
     * @returns {boolean} True when shown.
     */
    function acknowledged(id, x) {
        if (x && x.Announced === true)
            return true;
        return Object.prototype.hasOwnProperty.call(ackRead(), String(id));
    }

    /**
     * Remember that the end of a server job was shown (or is history): this device's list, and the server (Api/jobs/AckJob,
     * feature detected) so the user's other devices and sessions stay quiet.
     * @param {string} id The job id.
     * @param {object} [x] The server's job info (Announced already: nothing to tell the server).
     */
    function acknowledge(id, x) {
        id = String(id);
        try {
            const v = ackRead();
            if (!v[id]) {
                v[id] = Date.now();
                window.localStorage.setItem(ackKey(), JSON.stringify(v));
            }
        }
        catch {
        }
        if (noAnnounceApi || !x || x.Announced === true || x.State === "running" || x.State === "stopping")
            return;
        try {
            Sw.Net.Send((Sw.Url && Sw.Url.Root ? Sw.Url.Root : "/") + "Api/jobs/AckJob", id, { NoNavigate: true, Detached: true }).catch(e => {
                if (e && (e.Status === 404 || e.Status === 405))
                    noAnnounceApi = true;
            });
        }
        catch {
        }
    }

    /**
     * The local storage key of the server jobs the user cancelled on this device (per site).
     * @returns {string} The key.
     */
    function cancelKey() {
        return ackKey().replace("SysWeaver.JobsSeen.", "SysWeaver.JobsCancelled.");
    }

    /**
     * Remember (or forget) that the user cancelled a server job on this device: the cancel survives a reload and a lost connection
     * (sent again), and the history of a job that finished anyway says "was ready before it could stop" (never "is ready").
     * @param {string} id The job id.
     * @param {boolean} on Cancelled (false: the server refused the cancel).
     */
    function cancelMark(id, on) {
        try {
            const k = cancelKey();
            const v = JSON.parse(window.localStorage.getItem(k) || "{}") || {};
            const now = Date.now();
            for (const x of Object.keys(v))
                if (!(typeof v[x] === "number" && now - v[x] < AckMaxAgeMs))
                    delete v[x];
            if (on)
                v[String(id)] = now;
            else
                delete v[String(id)];
            window.localStorage.setItem(k, JSON.stringify(v));
        }
        catch {
        }
    }

    /**
     * Did the user cancel a server job on this device (see cancelMark)?
     * @param {string} id The job id.
     * @returns {boolean} True when cancelled here.
     */
    function cancelMarked(id) {
        try {
            const v = JSON.parse(window.localStorage.getItem(cancelKey()) || "{}") || {};
            const at = v[String(id)];
            return typeof at === "number" && Date.now() - at < AckMaxAgeMs;
        }
        catch {
            return false;
        }
    }

    /** @type {ActivityList} The list of this window. */
    const local = new ActivityList();

    /**
     * The list in use: the app shell window's (same origin, it shows a list), else this window's.
     * @returns {ActivityList} The list.
     */
    function listOf() {
        try {
            if (window.top !== window) {
                const a = window.top.Sw && window.top.Sw.Activity;
                const l = a && a.ListObject;
                if (l && l !== local && l.IsHost)
                    return l;
            }
        }
        catch {
            //  Another origin
        }
        return local;
    }

    // ------------------------------------------------------------------ a page without the app shell: a minimal activity line (FBK-4)

    /** @type {HTMLElement|null} The in-page activity line (a page without the shell's list). */
    let lineE = null;

    /** @type {number} The timer that redraws the line (elapsed time of running jobs). */
    let lineTimer = 0;

    /** The styles of the in-page line (tokens of ui/tokens.css with fallbacks; no stylesheet needed). */
    const LineCss = ".sw-actline{position:fixed;inset-inline-start:calc(16px + env(safe-area-inset-left,0px));inset-block-end:calc(16px + env(safe-area-inset-bottom,0px));"
        + "z-index:2147483000;max-width:min(30rem,calc(100vw - 32px));"
        + "display:flex;flex-direction:column;gap:4px;font:inherit;font-size:var(--sw-text-sm,0.875rem);color:var(--sw-fg,CanvasText)}"
        + ".sw-actline:empty{display:none}"
        + ".sw-actline-item{display:flex;align-items:center;gap:8px;min-height:44px;padding:4px 8px 4px 12px;border-radius:var(--sw-radius-md,8px);"
        + "background:var(--sw-bg-1,Canvas);border:1px solid var(--sw-border,GrayText);box-shadow:var(--sw-elev-2,0 4px 16px rgba(0,0,0,.2))}"
        + ".sw-actline-text{flex:1 1 auto;min-width:0;overflow-wrap:anywhere}"
        + ".sw-actline-item[data-state=failed]{border-color:var(--sw-danger,#c00)}"
        + ".sw-actline-item>button.sw-actline-btn{min-height:44px;min-width:44px;font:inherit;background:none;border:1px solid var(--sw-border,GrayText);"
        + "border-radius:var(--sw-radius-sm,4px);color:inherit;padding:0 10px;cursor:pointer}"
        //  (44 px targets with the ui library's buttons too: touch and mouse)
        + ".sw-actline-item>.sw-button{min-height:44px;min-width:44px}"
        //  The line never covers the page: while it is shown the page reserves its height at the end (and focused fields scroll above it)
        + "html.sw-actline-on{scroll-padding-block-end:calc(var(--sw-actline-h,0px) + 24px)}"
        + "html.sw-actline-on body{padding-block-end:calc(var(--sw-actline-h,0px) + 24px + env(safe-area-inset-bottom,0px))}"
        + "@media print{.sw-actline{display:none}}";

    /** @type {boolean} A geometry check of the line is scheduled. */
    let coverCheck = false;

    /**
     * Reserve the line's height at the end of the page while it is shown (UX guide FBK-8 "never covers actions", LAY-1): the page can
     * always be scrolled so its last button is above the line. In developer mode a geometry check warns when the line still covers a
     * control of the page (a page with its own fixed layout or scroller: give that scroller padding-block-end: var(--sw-actline-h)).
     */
    function reserveLine() {
        const root = document.documentElement;
        const on = !!lineE && lineE.childElementCount > 0;
        root.classList.toggle("sw-actline-on", on);
        if (!on) {
            root.style.removeProperty("--sw-actline-h");
            return;
        }
        const h = Math.ceil(lineE.getBoundingClientRect().height);
        root.style.setProperty("--sw-actline-h", h + "px");
        if (!coverCheck) {
            coverCheck = true;
            requestAnimationFrame(checkCover);
        }
    }

    /** Developer mode: warn once when the stand-alone line covers a control of the page (checked at the end of the page). */
    function checkCover() {
        coverCheck = false;
        try {
            if (!lineE || !lineE.childElementCount || !(typeof Sw.DevMode === "function" ? Sw.DevMode() : !!Sw.DevMode))
                return;
            const r = lineE.getBoundingClientRect();
            const sel = "button, a[href], input, select, textarea, [tabindex]:not([tabindex='-1']), [role=button]";
            for (const c of document.querySelectorAll(sel)) {
                if (lineE.contains(c) || c.closest(".sw-toasts, .sw-toast, .sw-dialog, [hidden], [inert]"))
                    continue;
                const b = c.getBoundingClientRect();
                if (!b.width || !b.height || b.right <= r.left || b.left >= r.right || b.bottom <= r.top || b.top >= r.bottom)
                    continue;
                //  Covered now; would scrolling the page to its end free it? Then the reserved space works (no warning)
                const sc = document.scrollingElement || document.documentElement;
                const room = sc.scrollHeight - sc.clientHeight - sc.scrollTop;
                if (room >= b.bottom - r.top)
                    continue;
                Sw.DevWarn("Sw.Activity: the stand-alone activity line covers \"" + (c.getAttribute("aria-label") || c.textContent || c.tagName).trim().substring(0, 40)
                    + "\" and the page can't be scrolled to free it (a fixed layout or an own scroller?): give the scrolling element padding-block-end: calc(var(--sw-actline-h) + 24px) (UX guide FBK-8, LAY-1)", "activity.line.cover");
                return;
            }
        }
        catch {
        }
    }

    /**
     * A button of the line (Sw.Button when the ui library is loaded).
     * @param {string} text The text.
     * @param {string} title The tool tip.
     * @param {function():*} fn The action.
     * @returns {HTMLElement} The button.
     */
    function lineButton(text, title, fn) {
        if (Sw.Button) {
            //  (the job's own line says how it went: the button adds no "done" / "not done" of its own)
            const b = new Sw.Button({ Text: text, Title: title, Kind: "ghost", Done: false, OnClick: () => Promise.resolve(fn()).then(() => undefined) });
            return b.E;
        }
        const b = document.createElement("button");
        b.type = "button";
        b.className = "sw-actline-btn";
        b.textContent = text;
        b.title = title;
        b.addEventListener("click", () => fn());
        return b;
    }

    /**
     * Draw the in-page activity line: the running jobs (what runs, the time so far or "Stopping…", Cancel), and the results that
     * have no toast (without the ui library). Nothing for a list shown by the app shell.
     */
    function drawLine() {
        if (local.IsHost || listOf() !== local || !document.body)
            return;
        if (!lineE) {
            if (!document.getElementById("sw-actline-css")) {
                const st = document.createElement("style");
                st.id = "sw-actline-css";
                st.textContent = LineCss;
                document.head.append(st);
            }
            lineE = document.createElement("div");
            lineE.className = "sw-actline sw-root";
            lineE.setAttribute("role", "region");
            lineE.setAttribute("aria-label", _TF("Background work", "Accessible name of the line at the bottom of a page that shows running background work (exports, server jobs)"));
            //  (toasts keep clear of it)
            lineE.setAttribute("data-sw-toastavoid", "");
            document.body.append(lineE);
            //  (the framework's overlay geometry self-test in developer mode: a fixed surface that covers a control is reported)
            try {
                if (Sw.Overlay && typeof Sw.Overlay.Register === "function")
                    Sw.Overlay.Register(lineE, { Name: "Sw.Activity stand-alone line" });
            }
            catch {
            }
        }
        const keep = document.activeElement && lineE.contains(document.activeElement) ? document.activeElement.dataset.swAct : null;
        const items = [];
        for (const j of local.Jobs.filter(x => x.Running || x.LineShow).slice(-3)) {
            const it = document.createElement("div");
            it.className = "sw-actline-item";
            it.dataset.state = j.State;
            const tx = document.createElement("span");
            tx.className = "sw-actline-text";
            let s;
            if (j.Stopping)
                s = j.Title + ": " + j.Text;
            else if (j.Running) {
                const secs = Math.max(0, Math.floor((Date.now() - j.Started.getTime()) / 1000));
                s = j.Progress !== null
                    ? _T("{0}: {1} %", j.Title, Math.round(j.Progress * 100), "In-page activity line of a page without the app shell: running work with progress.{0} is the work, {1} the percent")
                    : _T("{0}: running for {1} s", j.Title, secs, "In-page activity line of a page without the app shell: running work.{0} is the work, {1} the seconds so far");
                if (j.Text)
                    s += " · " + j.Text;
            }
            else
                s = j.LineShow;
            tx.textContent = s;
            it.append(tx);
            if (j.CanCancel) {
                const b = lineButton(_TF("Cancel", "Button of the in-page activity line: stops the running work"), _T("Stop {0}", j.Title, "Tool tip of the Cancel button of the in-page activity line.{0} is the work"), () => j.Cancel());
                b.dataset.swAct = "cancel:" + j.Id;
                it.append(b);
            }
            if (!j.Running) {
                if (j.State === "done" && j.Result && j.Result.Open) {
                    const b = lineButton(_TF("Open", "Button of the in-page activity line: shows the result of finished work"), _T("Show the result of {0}", j.Title, "Tool tip of the Open button of the in-page activity line.{0} is the work"), () => j.Result.Open());
                    b.dataset.swAct = "open:" + j.Id;
                    it.append(b);
                }
                const x = lineButton("×", _TF("Close", "Tool tip of the close button of a result in the in-page activity line"), () => {
                    j.LineShow = "";
                    drawLine();
                });
                x.setAttribute("aria-label", _TF("Close", "Tool tip of the close button of a result in the in-page activity line"));
                x.dataset.swAct = "close:" + j.Id;
                it.append(x);
            }
            items.push(it);
        }
        lineE.replaceChildren(...items);
        reserveLine();
        if (keep) {
            const f = lineE.querySelector('[data-sw-act="' + keep + '"]');
            if (f)
                f.focus({ preventScroll: true });
        }
        const running = local.Jobs.some(x => x.Running);
        if (running && !lineTimer)
            lineTimer = setInterval(drawLine, 1000);
        else if (!running && lineTimer) {
            clearInterval(lineTimer);
            lineTimer = 0;
        }
    }

    /**
     * A change of this window's list on a page without the app shell: the line, the announcements and the result (a toast with the ui
     * library, else the line) — the same feedback the shell gives, so work started on a stand-alone page is never silent.
     * @param {{Type: string, Job: Job}} ev The change.
     */
    function onLocal(ev) {
        if (local.IsHost || listOf() !== local)
            return;
        const j = ev.Job;
        const say = (t, o) => Sw.Page && Sw.Page.Announce && Sw.Page.Announce(t, o);
        if (j && ev.Type === "start" && j.IsNew && !j.Quiet)
            say(j.Title);
        if (j && (ev.Type === "done" || ev.Type === "fail") && !j.Quiet) {
            const failed = ev.Type === "fail";
            //  (one text source: the job's end sentence; work that finished after Cancel never says "Done")
            const end = j.EndText;
            const text = end.Text;
            if (Sw.Toast) {
                const action = failed
                    ? (j.CanRetry ? { Text: _TF("Try again", "Button of a failed job in the activity list: starts the same work again"), OnClick: () => j.Retry() } : undefined)
                    : (j.Result && j.Result.Open ? { Text: _TF("Open", "Button of a finished job in the activity list: shows the result"), OnClick: () => j.Result.Open() } : undefined);
                Sw.Toast(text, { Kind: end.Kind, Action: action, Duration: failed ? true : undefined });
            }
            else {
                j.LineShow = text;
                say(text, failed ? { Assertive: true } : undefined);
            }
        }
        if (j && ev.Type === "cancel" && !j.Quiet)
            say(j.Text || Sw.Cancel.Texts.Cancelled());
        drawLine();
    }

    /** @type {boolean} The in-page line listens to this window's list. */
    let lineHooked = false;

    /** Listen to this window's list for the in-page line (once; it draws only on a page without the app shell). */
    function hookLine() {
        if (lineHooked || listOf() !== local)
            return;
        lineHooked = true;
        local.Listeners.push(onLocal);
    }

    /** @type {boolean} The pagehide handler of this window is installed. */
    let leaveHooked = false;

    /** Stop the jobs this window started with EndsWithPage when the page is left (pagehide, not the back / forward cache). */
    function hookLeave() {
        hookLine();
        if (leaveHooked)
            return;
        leaveHooked = true;
        window.addEventListener("pagehide", ev => {
            if (!ev.persisted)
                listOf().Left(window);
        });
    }

    /**
     * @namespace Sw.Activity - the activity list (see the file header).
     */
    Sw.Activity = {
        /** @type {ActivityList} This window's list object (the shell window's is used by its frames). */
        ListObject: local,

        /** @type {boolean} True when a visual list is shown (the app shell): "Continue in background" may be offered. */
        get Supported() {
            return listOf().IsHost;
        },

        /**
         * Start a job (a running job with the same Key is returned instead, IsNew false).
         * @param {{Key: string, Title: string, Text: string, Progress: number, Cancel: function, Retry: function, EndsWithPage: boolean, Data: *}} o
         * Options: Title (what runs, ex: "Exporting report.xlsx"), Text (the phase), Progress (0..1, omit when unknown), Cancel (shows
         * Cancel), Retry (shows "Try again" after a failure; keeps the original inputs), EndsWithPage (default true: the work stops when
         * the page is left, the shell asks first), Key (no double start), Data.
         * @returns {Job} The job.
         */
        Start(o) {
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Activity.Start", o, StartOptions);
            hookLeave();
            return listOf().Start(o, window);
        },

        /**
         * Start a job for a promise: Done when it resolves (o.DoneText, or o.Result(value) returning the Done options), Fail when it
         * rejects (cancelled for an AbortError).
         * @param {Promise<*>} promise The work.
         * @param {object} o The options of Start plus DoneText / Result.
         * @returns {Job} The job.
         */
        Track(promise, o) {
            if (Sw.DevOptions && !(o && o._Run))
                Sw.DevOptions("Sw.Activity.Track", o, TrackOptions);
            o = Object.assign({}, o || {});
            const signal = o._Signal || null;
            delete o._Run;
            delete o._Signal;
            //  The work's promise confirms a cancel (Sw.Cancel): an AbortError = cancelled, a value = finished anyway
            o.CancelMode = "work";
            hookLeave();
            const j = listOf().Start(o, window);
            if (!j.IsNew)
                return j;
            if (signal)
                j.Signal = signal;
            const p = Promise.resolve(promise);
            p.then(v => {
                if (!j.Running)
                    return;
                let r = { Text: o.DoneText || "" };
                if (typeof o.Result === "function") {
                    //  (after a cancel nothing is delivered: Result saves files, so it is not called; the value stays in Finished)
                    //  A Result that throws is a failure of the job (never a job that runs for ever)
                    try {
                        r = j.CancelAsked ? {} : o.Result(v);
                    }
                    catch (e) {
                        j.Fail(e);
                        return;
                    }
                }
                j.Done(Object.assign({}, r || {}, { Value: v }));
            }, e => {
                if (!j.Running)
                    return;
                if (Sw.Cancel.IsAbort(e)) {
                    //  Stopped, but a write it sent before was done on the server: say so (never a plain "Cancelled" then)
                    const s = j.Signal && Sw.Cancel.Sent ? Sw.Cancel.Sent(j.Signal) : null;
                    j.Cancelled(s && s.Writes > 0 ? Sw.Cancel.Texts.CancelledAfterWrite() : undefined);
                }
                else
                    j.Fail(e);
            });
            thenable(j);
            return j;
        },

        /**
         * Run work as a job, the way that can't go wrong (Cancel always works): fn gets an AbortSignal (pass it to Sw.Net.Send / Get as
         * Signal) and the job (Update progress / text); Cancel aborts the signal; the job is Done / Failed / Cancelled from fn's promise.
         * Returns the job, which is thenable: `OnClick: () => Sw.Activity.Run({ Title }, signal => Sw.Net.Send(url, arg, { Signal: signal }))`
         * keeps the button busy until the work ends, and the work is in the activity list. A running job with the same Key is returned
         * instead (no double start).
         * Ex (an export, texts translated by the caller): `Sw.Activity.Run({ Key: "export:" + url, Title: title, Result: blob =>
         * (Sw.Activity.SaveBlob(blob, name), { Text: doneText }) }, s => Sw.Net.Send(api, arg, { Blob: true, Signal: s }))`.
         * @param {object} o The options of Start (Key, Title, Text, Progress, Retry, EndsWithPage, Data) plus DoneText / Result(value).
         * @param {function(AbortSignal, Job):Promise<*>} fn The work.
         * @returns {Job} The job (thenable: resolves with the value; a failure, a cancel or work that finished after a Cancel rejects, marked SwSaid: the list shows it).
         */
        Run(o, fn) {
            if (Sw.DevOptions)
            /* SwWeb:no-tr (a developer warning, English like every Sw.DevWarn) */
                Sw.DevOptions("Sw.Activity.Run", o, Object.assign({}, TrackOptions, { Cancel: { Type: "function", Hint: "Run makes its own Cancel (it aborts the signal fn gets)." } })); /* SwWeb:no-tr (a developer warning) */
            o = Object.assign({}, o || {});
            if (typeof fn !== "function")
                throw new TypeError("Sw.Activity.Run(o, fn): fn(signal, job) must be a function that returns the work's promise");
            if (o.Key) {
                const r = Sw.Activity.Find(o.Key);
                if (r) {
                    r.IsNew = false;
                    if (o.Title && r.RunTitle !== o.Title)
                        Sw.DevWarn && Sw.DevWarn("Sw.Activity.Run: a job with Key \"" + o.Key + "\" is running (\"" + r.RunTitle + "\"), so \"" + o.Title + "\" was NOT started (the running job is returned). Use another Key for other work (UX guide FBK-4).", "activity.key." + o.Key + "." + o.Title);
                    return r;
                }
            }
            //  The cancellation model's signal: Cancel stops reads, Sw.Sleep and requests not sent yet at once, but never aborts a write
            //  that was sent (the server may have it: the job then ends "Finished before it could stop", or "Stopped; what was sent before
            //  the Cancel was done")
            const ac = Sw.Cancel.Controller();
            o.Cancel = () => ac.abort();
            if (!o.Retry)
                o.Retry = () => Sw.Activity.Run(Object.assign({}, o, { Cancel: undefined, Retry: undefined }), fn);
            let job = null;
            const p = new Promise(r => setTimeout(r, 0)).then(() => fn(ac.signal, job));
            job = Sw.Activity.Track(p, Object.assign({}, o, { _Run: true, _Signal: ac.signal }));
            return job;
        },

        /**
         * A server call as a job that runs in the window of the list (the app shell), so it goes on when the page that started it is
         * left: Sw.Net.Send(Url, Arg) there, Cancel aborts it, "Try again" sends it again. With Download (a file name) the reply is
         * saved as that file.
         * @param {{Title: string, Url: string, Arg: *, Download: string, DoneText: string, Key: string, Text: string}} o The options
         * (Url relative to the calling page or absolute).
         * @returns {Job} The job.
         */
        Send(o) {
            o = Object.assign({}, o || {});
            if (!o.Url || typeof o.Url !== "string")
                throw new TypeError("Sw.Activity.Send(o): o.Url (the api url) is required");
            o.Url = new URL(o.Url, window.location.href).href;
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Activity.Send", o);
            return listOf().Send(o, window);
        },

        /**
         * Start a server job (an api that answers a job object, ex: Api/StartExportTableApi) and follow it in the activity list (see
         * ActivityList.Server): the work runs on the server, so it goes on when the page is left and its result comes back after a
         * reload (RestoreServer).
         * @param {string} url The start api (relative to the calling page or absolute).
         * @param {*} arg Its argument.
         * @param {{Save: boolean, DoneText: string, DoneTitle: string, Kind: string}} [o] Options (Save default true: the file is saved when
         * ready; DoneText: the result text; DoneTitle: the title once done, default the file name). A refused start throws (the caller
         * shows it, or falls back when the server has no job api); RunServer shows it.
         * @returns {Promise<Job>} The job.
         */
        async StartServer(url, arg, o) {
            if (Sw.DevOptions)
                Sw.DevOptions("Sw.Activity.StartServer", o, { Save: "boolean", DoneText: "string", DoneTitle: "string", Kind: "string", Title: "string", Quiet: "boolean" });
            o = o || {};
            //  (a refused start throws: the caller shows it, or falls back when the server has no job api (404 / 405); RunServer shows it)
            const info = await Sw.Net.Send(new URL(url, window.location.href).href, arg, { NoNavigate: true });
            if (!info || !info.Id)
                throw new Error(_TF("The service did not start the work.", "Error when a background job could not be started on the service"));
            hookLeave();
            //  (this browser uses server jobs now: the jobs come back after a reload, and the "sw.job" push is followed: RestoreServer)
            if (Sw.Storage && Sw.Storage.Local)
                Sw.Storage.Local.Set(UsedKey, "1");
            if (!listOf().JobPush)
                Sw.Activity.RestoreServer({ Force: true, NoList: true });
            return listOf().Server(info, Object.assign({}, o, { Restored: false }));
        },

        /**
         * Start a server job and wait until it ENDED (StartServer resolves when it started): `OnClick: () => Sw.Activity.RunServer(url,
         * arg)` keeps the button busy until the file is ready, and the job is in the activity list. Resolves with the job when done;
         * rejects (marked SwSaid: the list shows it) when it was cancelled or failed, or with the start error.
         * @param {string} url The start api.
         * @param {*} arg Its argument.
         * @param {{Save: boolean, DoneText: string, DoneTitle: string, Kind: string, Title: string, Quiet: boolean}} [o] Options (see
         * StartServer; Title names the work in the failure sentence of a refused start, which RunServer shows once unless Quiet).
         * @returns {Promise<Job>} The ended job.
         */
        async RunServer(url, arg, o) {
            o = o || {};
            let j;
            try {
                j = await Sw.Activity.StartServer(url, arg, o);
            }
            catch (e) {
                //  RunServer owns the job: a refused start (the server's reason, ex: "The table to export was not found …") or a failure
                //  is shown once here, marked SwSaid (a button that returned it adds nothing); not a lost connection (the shell's offline
                //  line), not with o.Quiet (the caller shows it)
                if (!o.Quiet && !lostConnection(e) && !Sw.Cancel.IsAbort(e) && typeof Sw.Fail === "function") {
                    try {
                        Sw.Fail(o.Title ? Sw.Net.FailText(o.Title, e) : Sw.Net.Message(_TF("The work could not be started.", "Error when a background job could not be started on the server"), e));
                        e.SwSaid = true;
                    }
                    catch {
                    }
                }
                throw e;
            }
            await j.Finished;
            return j;
        },

        /**
         * Put the server jobs of the signed in user back in the list (after a reload or in another tab): running ones are followed;
         * finished ones are history (listed quietly with the server's end time; their file is offered again by Open). Jobs of other tabs,
         * sessions and devices of the user follow by the "sw.job" push; an end is shown (a toast) once for the user. The app shell calls it
         * once when the user is known. Quiet without the job api.
         * @param {{Force: boolean, NoList: boolean}} [o] Force: also when this browser never started a server job (UsedKey); NoList: only
         * follow the "sw.job" push, no GetJobs (StartServer).
         * @returns {Promise<number>} The number of jobs listed.
         */
        async RestoreServer(o) {
            //  Parity with the original shell (no idle request it did not make): only a browser that started a server job asks for the
            //  jobs (and subscribes "sw.job"); StartServer marks it (UsedKey), a list that comes back empty clears the mark
            const store = Sw.Storage && Sw.Storage.Local;
            if (!(o && o.Force) && store && !store.Get(UsedKey, ""))
                return 0;
            let list = [];
            if (!(o && o.NoList)) {
                try {
                    list = await Sw.Net.Send((Sw.Url && Sw.Url.Root ? Sw.Url.Root : "/") + "Api/jobs/GetJobs", null, { NoNavigate: true });
                }
                catch {
                    return 0;
                }
                if (!Array.isArray(list))
                    return 0;
                if (!list.length && store) {
                    store.Remove(UsedKey);
                    return 0;
                }
            }
            const l = listOf();
            for (const info of list.slice().reverse())
                if (info && info.Id)
                    l.Server(info, { Save: false, Restored: true });
            //  Jobs started in another tab / on another device of the user come in by the "sw.job" push (start and end): listed here too,
            //  their end shown once for the user (Server: acknowledged); a removed job leaves the list
            if (!l.JobPush && Sw.Session && typeof Sw.Session.Subscribe === "function") {
                l.JobPush = Sw.Session.Subscribe("sw.job", m => {
                    const info = m && m.Job;
                    if (!info || !info.Id)
                        return;
                    if (info.Removed) {
                        const k = l.Jobs.find(j => j.ServerId === String(info.Id));
                        if (k) {
                            k.RemoveFn = null;
                            k.Remove();
                        }
                        return;
                    }
                    //  Cancel asked elsewhere while this window still follows the job (its end not shown yet): the end says so
                    const live = l.Jobs.find(j => j.ServerId === String(info.Id) && j.Running);
                    if (live && (info.CancelRequested || info.Cancelled === true))
                        live.CancelAsked = true;
                    //  A Cancel that came too late on another device / session (the server sets CancelRequested on the done job and
                    //  pushes it): a job listed here as done says so too, never a plain "Done … Open" (Ops R5-4)
                    const done = l.Jobs.find(j => j.ServerId === String(info.Id) && j.State === "done");
                    if (done) {
                        if ((info.CancelRequested || info.Cancelled === true) && !done.AfterCancel) {
                            done.CancelAsked = true;
                            done.AfterCancel = true;
                            done.Phase = Sw.Cancel.Texts.FinishedAnyway();
                            const name = info.ResultName ? keepStamp(info.ResultName) : "";
                            if (done.Result)
                                done.Result.Text = name ? Sw.Cancel.Texts.ReadyNotSaved(name) : Sw.Cancel.Texts.FinishedAnyway();
                            l.Emit("update", done);
                        }
                        return;
                    }
                    l.Server(info, { Save: false, Restored: true });
                });
            }
            return list.length;
        },

        /**
         * The running job with this key.
         * @param {string} key The key.
         * @returns {Job|null} The job.
         */
        Find(key) {
            return listOf().Jobs.find(j => j.Running && j.Key === String(key)) || null;
        },

        /** @returns {Array<Job>} The jobs, newest first. */
        List() {
            return listOf().Jobs.slice().reverse();
        },

        /** @returns {{Running: number, Failed: number, Done: number}} The counts (Failed: not opened yet). */
        Counts() {
            const l = listOf().Jobs;
            return {
                Running: l.filter(j => j.Running).length,
                Failed: l.filter(j => j.State === "failed" && !j.Seen).length,
                Done: l.filter(j => j.State === "done").length,
            };
        },

        /**
         * The list was opened: failures are no longer counted in the badge; failures seen at an earlier opening are removed (unless
         * keep, ex: when the list closes: failures that happened while it was open count as seen).
         * @param {boolean} [keep] Don't remove failures seen before.
         */
        MarkSeen(keep) {
            const l = listOf();
            let n = 0;
            for (const j of l.Jobs.slice()) {
                if (j.State !== "failed")
                    continue;
                if (j.Seen) {
                    if (!keep)
                        j.Remove();
                }
                else {
                    j.Seen = true;
                    ++n;
                }
            }
            if (n)
                l.Emit("seen", null);
        },

        /**
         * Listen to changes of the list. A listener added by a framed page (the list is the shell window's) is removed by itself when
         * that page is left (pagehide), so a page never stays in memory through it; the returned function removes it earlier.
         * @param {function({Type: string, Job: Job}):void} fn The listener.
         * @returns {function():void} Removes it.
         */
        On(fn) {
            const l = listOf();
            if (l === local) {
                l.Listeners.push(fn);
                return () => l.Off(fn);
            }
            //  The list lives in the shell window: wrap the listener with its owner (Emit drops it when the owner is gone) and remove it
            //  when this page is left
            const w = (e) => fn(e);
            w.SwOwner = window;
            l.Listeners.push(w);
            const off = () => {
                l.Off(w);
                window.removeEventListener("pagehide", onHide);
            };
            const onHide = ev => {
                if (!ev.persisted)
                    off();
            };
            window.addEventListener("pagehide", onHide);
            return off;
        },

        /** Show the activity list (the app shell; nothing without one). */
        Open() {
            const l = listOf();
            if (l.OnOpen)
                l.OnOpen();
        },

        /**
         * The running jobs that stop when a window is left (the app shell asks before it replaces the page, SAF-10).
         * @param {Window} win The window (its nested frames count too).
         * @returns {Array<Job>} The jobs.
         */
        EndsWith(win) {
            return listOf().EndsWith(win);
        },

        /** Save a Blob as a file from this window. */
        SaveBlob: saveBlob,

        /** The job class (for instanceof checks in the window of the list). */
        Job,
    };

    //  The option schemas (Sw.DevOptions registry: checked in developer mode, listed by the walker of core/contracttest.html)
    if (Sw.DevOptions && Sw.DevOptions.Register) {
        const ServerOptions = { Save: "boolean", DoneText: "string", DoneTitle: "string", Kind: "string", Title: "string", Quiet: "boolean" };
        Sw.DevOptions.Register("Sw.Activity.Start", StartOptions);
        Sw.DevOptions.Register("Sw.Activity.Track", TrackOptions);
        Sw.DevOptions.Register("Sw.Activity.Run", Object.assign({}, TrackOptions, { Cancel: { Type: "function", Hint: "Run makes its own Cancel (it aborts the signal fn gets)." } })); /* SwWeb:no-tr (a developer warning) */
        Sw.DevOptions.Register("Sw.Activity.StartServer", ServerOptions);
        Sw.DevOptions.Register("Sw.Activity.RunServer", ServerOptions);
        Sw.DevOptions.Register("Sw.Activity.RestoreServer", { Force: "boolean", NoList: "boolean" });
        Sw.DevOptions.Register("Sw.Activity.Send", Object.assign({}, StartOptions, {
            Url: "string", Arg: "any", Download: "boolean|string", ReadOnly: "boolean", Retry: ["number", "string", "boolean"], RetryMs: { Type: "number", Min: 0 },
            DoneText: "string",
        }));
        Sw.DevOptions.NoOptions("Sw.Activity.Job", "the job class (instanceof checks); jobs are made by Start, Track, Run and Send");
    }
})();
