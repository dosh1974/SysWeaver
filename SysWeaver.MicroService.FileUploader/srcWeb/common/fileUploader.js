/**
 * @file fileUploader.js - browser client of the file upload protocol of FileUploaderService
 * (SysWeaver.MicroService.FileUploader), served as common/fileUploader.js next to the shared scripts of
 * SysWeaver.HttpServer (it only exists when that service is hosted).
 *
 * New API: `Sw.Upload` (needs the Sw core, `sw/core.js` or the legacy `common/common.js`; the visual helpers use the
 * Sw ui library when it is loaded):
 *  - `Sw.Upload.Status` (the C# FileUploadStatus codes), `Sw.Upload.StatusText(code)` (translated text).
 *  - `await Sw.Upload.Ensure()`: loads `common/md5.js` (required, MD5 hashes) when the page did not load it.
 *  - `await Sw.Upload.Files(repo, files, {ApiBase, OnProgress})` -> `{Status[], Urls[], Redirected}` or `{Error}`.
 *  - `await Sw.Upload.Pick({Multiple, Accept})` -> FileList or null (must run in a user gesture).
 *  - `Sw.Upload.Target(el, {Repo, Multiple, ApiBase, Accept, Title, OnStart, OnResults})` -> `{Remove()}`: makes an
 *    element an upload target (click / Enter / Space opens the file picker, files can be dropped on it; classes
 *    `Dragging`, `Selecting`, `Uploading`; `aria-busy` while uploading).
 *
 * Legacy globals (kept for old pages and other projects: app/home.js before the rewrite, chat/chat.js,
 * ServerManager keys.js / serviceInfo.js, upload/Example.html): `UploadStatus`, `fileUploaderStatusText`,
 * `fileUploader`, `fileUploaderElement`, `fileUploaderDrop`, `fileUploaderDrag`, `fileUploaderDragEnter`,
 * `fileUploaderDragLeave`, `selectFiles`, `fileUploaderClick`, `fileUploaderSetup`, `fileUploaderInit`. They are thin
 * wrappers over Sw.Upload with the old signatures and results.
 *
 * Loading: classic script, no side effects at load (only declares globals and `Sw.Upload`). Statically with
 * `<script defer src="../common/fileUploader.js">` after the core, or lazily with `Sw.Net.IncludeJs(null, "fileUploader.js")`.
 * `common/md5.js` is loaded on demand (Ensure); `common/UZIP.js` is no longer needed (deflate uses the browser's
 * CompressionStream through `Sw.Util.Deflate`; UZIP is still used when present and CompressionStream is not).
 *
 * Protocol: every file is read fully into memory and MD5-hashed; one JSON POST to <apiBase>CheckStatus with
 * {Repo, Files: FileUploadInfo[]} returns FileUploadResult[] {Result, Url}; only files with status 2 (Upload) are then
 * POSTed in parallel to <apiBase>Upload?repo=&name=&length=&hash=&time= (values URL-encoded), raw-deflate compressed
 * (`Content-Encoding: deflate`) for compressible extensions when that is smaller. Server chunking is not used.
 * Returned Url values are relative to the server root (consumers prefix "../").
 *
 * Element configuration (legacy and Target): `data-repo`, `data-multiple` ("true" / "1"), `data-apibase`,
 * `data-onstart` / `data-onresults` (names of global functions), expandos `OnUploadStart` / `OnUploadResults`,
 * `IsUploading`.
 */

/**
 * Status codes of the upload protocol; mirrors the C# FileUploadStatus enum. None (0) = uploaded OK,
 * AlreadyUploaded (1) = deduplicated by hash, Upload (2) = may be uploaded (CheckStatus only); negative values are
 * errors / refusals. (Legacy global, also `Sw.Upload.Status`.)
 * @type {Readonly<Object<string, number>>}
 */
const UploadStatus = Object.freeze({
    MultipleFilesNotAllowed: -12,
    UnknownRepo: -11,
    InvalidFileName: -10,
    OperationInProgress: -9,
    InvalidFile: -8,
    DiscQuotaExceeded: -7,
    NotAuthorized: -6,
    InvalidParams: -5,
    RefuseExtension: -4,
    RefuseSize: -3,
    Refuse: -2,
    UploadFailed: -1,
    None: 0,
    AlreadyUploaded: 1,
    Upload: 2,
});

(function () {
    "use strict";

    const Sw = window.Sw = window.Sw || {};

    /** Default api base (relative to the pages of a first level folder). */
    const DefaultApiBase = "../upload/";

    /**
     * Get a translated status text for a status code.
     * @param {number} code - The status code (as returned by the server).
     * @returns {string} A human readable status message ("Unknown upload status [n]" for unknown codes).
     */
    function statusText(code) {
        switch (code) {
            case -12:
                return _TF("The server only accepts a single file upload", "This is the description of a status code for a service that upload file(s) to a repository");
            case -11:
                return _TF("The repo is unknown", "This is the description of a status code for a service that upload file(s) to a repository");
            case -10:
                return _TF("The filename was invalid (too long, using invalid chars or similar)", "This is the description of a status code for a service that upload file(s) to a repository");
            case -9:
                return _TF("Another upload is in progress and concurrent uploads are not allowed", "This is the description of a status code for a service that upload file(s) to a repository");
            case -8:
                return _TF("The file can not be used: it is empty, damaged or its content is not allowed", "This is the description of a status code for a service that upload file(s) to a repository");
            case -7:
                return _TF("Disc quota exceeded", "This is the description of a status code for a service that upload file(s) to a repository");
            case -6:
                return _TF("Not authorized", "This is the description of a status code for a service that upload file(s) to a repository");
            case -5:
                return _TF("Invalid params", "This is the description of a status code for a service that upload file(s) to a repository");
            case -4:
                return _TF("Invalid file extension", "This is the description of a status code for a service that upload file(s) to a repository");
            case -3:
                return _TF("File too large", "This is the description of a status code for a service that upload file(s) to a repository");
            case -2:
                return _TF("File refused", "This is the description of a status code for a service that upload file(s) to a repository");
            case -1:
                return _TF("Upload failed", "This is the description of a status code for a service that upload file(s) to a repository");
            case 0:
                return _TF("Ok", "This is the description of a status code for a service that upload file(s) to a repository");
            case 1:
                return _TF("Already uploaded", "This is the description of a status code for a service that upload file(s) to a repository");
            case 2:
                return _TF("May be uploaded", "This is the description of a status code for a service that upload file(s) to a repository");
        }
        return _T("Unknown upload status [{0}]", code, "This is the description of a status code for a service that upload file(s) to a repository.{0} is replaced with the status code");
    }

    /**
     * Test if a "data-multiple" style value allows multiple files.
     * @param {?string} v - Attribute value.
     * @returns {boolean} True for "true" or "1".
     */
    function isMultiple(v) {
        return (v === "true") || (v === "1");
    }

    /**
     * Load md5.js (YaMD5, required for hashing) unless the page already has it.
     * @returns {Promise<void>}
     * @throws {Error} If md5.js can't be loaded.
     */
    async function ensure() {
        if (typeof YaMD5 !== "undefined")
            return;
        await Sw.Net.IncludeJs(null, "md5.js");
        if (typeof YaMD5 === "undefined")
            throw new Error("md5.js (YaMD5) is not available");
    }

    /**
     * Raw deflate compress (native CompressionStream via Sw.Util.Deflate, else UZIP when loaded).
     * @param {ArrayBuffer} buffer - Data.
     * @returns {Promise<Uint8Array|null>} The compressed data, or null if no compressor is available / it failed.
     */
    async function deflate(buffer) {
        try {
            if ((typeof CompressionStream !== "undefined") && Sw.Util && Sw.Util.Deflate)
                return await Sw.Util.Deflate(new Uint8Array(buffer));
            if (typeof UZIP !== "undefined")
                return UZIP.deflateRaw(new Uint8Array(buffer), { level: 8 });
        }
        catch (e) {
            console.warn("Upload: deflate failed: " + e);
        }
        return null;
    }

    /**
     * Test if a file name has a compressible extension.
     * @param {string} name - File name.
     * @returns {boolean} True if compressing the content is worth trying.
     */
    function isCompressible(name) {
        const f = Sw.Format;
        if (!f)
            return false;
        const ext = (f.FileExtension(name) || "").toLowerCase();
        return f.IsCompressibleExt(ext);
    }

    /**
     * Upload one file (status Upload) and write its result.
     * @param {string} apiBase - Api base url.
     * @param {string} repo - Repo name.
     * @param {{Name:string, Length:number, Hash:string, LastModified:number}} info - FileUploadInfo.
     * @param {ArrayBuffer} buffer - File content.
     * @returns {Promise<{Status:number, Url:(string|null), Redirected:(string|null)}>} The result of this file.
     */
    async function uploadOne(apiBase, repo, info, buffer) {
        const e = encodeURIComponent;
        const url = apiBase + "Upload?repo=" + e(repo) + "&name=" + e(info.Name) + "&length=" + info.Length + "&hash=" + e(info.Hash) + "&time=" + info.LastModified;
        const headers = { "Content-Type": "application/octet-stream" };
        let body = buffer;
        if (isCompressible(info.Name)) {
            const comp = await deflate(buffer);
            if (comp && (comp.length < buffer.byteLength)) {
                body = comp;
                headers["Content-Encoding"] = "deflate";
            }
        }
        try {
            const res = await fetch(url, { method: "POST", mode: "cors", headers: headers, body: body });
            if (res.status !== 200) {
                console.warn('Upload failed: "' + info.Name + '", fetch status: ' + res.status);
                return { Status: -1, Url: null, Redirected: null };
            }
            if (res.redirected) {
                console.warn('Upload failed: "' + info.Name + '", fetch redirected to: ' + res.url);
                return { Status: -1, Url: null, Redirected: res.url };
            }
            const s = await res.json();
            if (s.Result !== 0)
                console.warn('Upload failed: "' + info.Name + '", status: ' + s.Result);
            return { Status: s.Result, Url: s.Url ?? null, Redirected: null };
        }
        catch (ex) {
            console.warn('Upload failed: "' + info.Name + '", fetch exception: ' + ex);
            return { Status: -1, Url: null, Redirected: null };
        }
    }

    /**
     * Upload file(s) to a repo (see the file header for the protocol).
     * @param {string} repo - Name of the repo (must exist on the server).
     * @param {File[]|FileList} files - The files.
     * @param {{ApiBase:string, OnProgress:function({Phase:string, Done:number, Count:number}):void}} [opts] - ApiBase
     * (default "../upload/"); OnProgress is called with Phase "hash", "check", "upload" and "done".
     * @returns {Promise<{Status:number[], Urls:(string|null)[], Redirected:(string|null)}|{Error:Error}>} Results
     * indexed like files, or {Error} on an exception.
     */
    async function upload(repo, files, opts) {
        opts = opts || {};
        const apiBase = opts.ApiBase || DefaultApiBase;
        const progress = (phase, done, count) => {
            if (opts.OnProgress) {
                try {
                    opts.OnProgress({ Phase: phase, Done: done, Count: count });
                }
                catch (e) {
                    console.error(e);
                }
            }
        };
        try {
            await ensure();
            const l = files.length;
            const infos = [];
            const data = [];
            for (let i = 0; i < l; ++i) {
                progress("hash", i, l);
                const f = files[i];
                const b = await f.arrayBuffer();
                data.push(b);
                infos.push({ Name: f.name, Length: b.byteLength, Hash: YaMD5.hashByteArray(b), LastModified: f.lastModified });
            }
            progress("check", 0, l);
            const csr = await Sw.Net.Send(apiBase + "CheckStatus", { Repo: repo, Files: infos }, { Reload: true, Post: true });
            if (!csr)
                return { Error: new Error(_TF("Couldn't check status!", "Error of a file upload: the service did not answer when asked which of the files it already has")) };
            const stats = csr.map(x => x.Result);
            const urls = csr.map(x => x.Url ?? null);
            let redirect = null;
            const uploads = [];
            let done = 0;
            for (let i = 0; i < l; ++i) {
                if (stats[i] !== UploadStatus.Upload) {
                    data[i] = null;
                    continue;
                }
                const buffer = data[i];
                data[i] = null;
                uploads.push(uploadOne(apiBase, repo, infos[i], buffer).then(r => {
                    stats[i] = r.Status;
                    urls[i] = r.Url;
                    if (r.Redirected)
                        redirect = r.Redirected;
                    progress("upload", ++done, uploads.length);
                }));
            }
            if (uploads.length > 0) {
                progress("upload", 0, uploads.length);
                await Promise.all(uploads);
            }
            progress("done", l, l);
            return { Status: stats, Urls: urls, Redirected: redirect };
        }
        catch (e) {
            return { Error: e };
        }
    }

    /**
     * Show a file picker (must be called from a user gesture).
     * @param {{Multiple:boolean, Accept:string}} [opts] - Allow multiple files, accepted types (input accept attribute).
     * @returns {Promise<FileList|null>} The selected files or null when cancelled.
     */
    async function pick(opts) {
        opts = opts || {};
        const fi = document.createElement("input");
        fi.type = "file";
        if (opts.Multiple)
            fi.multiple = true;
        if (opts.Accept)
            fi.accept = opts.Accept;
        const wait = new Promise(resolve => {
            fi.addEventListener("change", () => resolve(true), { once: true });
            fi.addEventListener("cancel", () => resolve(false), { once: true });
        });
        fi.click();
        await wait;
        const files = fi.files;
        return (files && files.length > 0) ? files : null;
    }

    /**
     * Run an upload for an upload target element: busy state, callbacks (data-onstart / OnUploadStart, then
     * data-onresults / OnUploadResults, called as (element, files, repo) and (element, result, files, repo)).
     * @param {HTMLElement} element - The target element.
     * @param {File[]|FileList} files - Files.
     * @returns {Promise<void>}
     */
    async function uploadFor(element, files) {
        if (element.IsUploading)
            return;
        element.IsUploading = true;
        element.classList.add("Uploading");
        element.setAttribute("aria-busy", "true");
        const oldTitle = element.title;
        let denied = false;
        element.title = _TF("Upload in progress, please wait", "Tool tip of an element that is used to upload files while an upload is running");
        try {
            const repo = element.getAttribute("data-repo");
            const resolve = name => (name && Sw.Util) ? Sw.Util.Resolve(name) : null;
            const startCb = resolve(element.getAttribute("data-onstart"));
            if (typeof startCb === "function")
                startCb(element, files, repo);
            if (element.OnUploadStart)
                await element.OnUploadStart(element, files, repo);
            const res = await upload(repo, files, { ApiBase: element.getAttribute("data-apibase") });
            //  Refused (403, or every file "not authorized"): the role doesn't allow uploads here, the target is disabled (below)
            denied = !!res && ((!!res.Error && !!Sw.Access && Sw.Access.IsDenied(res.Error)) ||
                (Array.isArray(res.Status) && res.Status.length > 0 && res.Status.every(s => s === UploadStatus.NotAuthorized)));
            const resCb = resolve(element.getAttribute("data-onresults"));
            if (typeof resCb === "function")
                resCb(element, res, files, repo);
            if (element.OnUploadResults)
                await element.OnUploadResults(element, res, files, repo);
        }
        finally {
            element.title = oldTitle;
            element.classList.remove("Uploading");
            element.removeAttribute("aria-busy");
            element.IsUploading = false;
        }
        if (denied && Sw.Access) {
            const info = await Sw.Access.ApiInfo((element.getAttribute("data-apibase") || DefaultApiBase) + "CheckStatus");
            Sw.Access.Disable(element, Sw.Access.Requires(info ? info.Auth : null));
        }
    }

    /**
     * True if an upload target is disabled (disabled, or aria-disabled: ex: Sw.Access.Guard, a refused upload).
     * @param {HTMLElement} e - The target element.
     * @returns {boolean}
     */
    function isOff(e) {
        return !!e.disabled || e.getAttribute("aria-disabled") === "true";
    }

    /**
     * Collect the files of a drop event (only the first one unless the element allows multiple files).
     * @param {DragEvent} ev - The drop event.
     * @param {HTMLElement} element - The target element.
     * @returns {File[]} Files.
     */
    function dropFiles(ev, element) {
        const files = [];
        const dt = ev.dataTransfer;
        if (dt) {
            if (dt.items && dt.items.length) {
                for (const item of dt.items)
                    if (item.kind === "file") {
                        const f = item.getAsFile();
                        if (f)
                            files.push(f);
                    }
            }
            else if (dt.files)
                files.push(...dt.files);
        }
        if (!isMultiple(element.getAttribute("data-multiple")) && (files.length > 1))
            files.length = 1;
        return files;
    }

    /**
     * True if a drag event carries files (other drags, like text, are not highlighted).
     * @param {DragEvent} ev - Event.
     * @returns {boolean} True for file drags.
     */
    function hasFiles(ev) {
        const t = ev.dataTransfer && ev.dataTransfer.types;
        return !t || Array.prototype.indexOf.call(t, "Files") >= 0;
    }

    /** "dragenter": count nested enters, add class Dragging. @param {DragEvent} ev - Event. */
    function onDragEnter(ev) {
        if (!hasFiles(ev))
            return;
        const e = ev.currentTarget;
        e.SwDragDepth = (e.SwDragDepth || 0) + 1;
        e.classList.add("Dragging");
    }

    /** "dragleave": remove class Dragging when the pointer left the element. @param {DragEvent} ev - Event. */
    function onDragLeave(ev) {
        const e = ev.currentTarget;
        e.SwDragDepth = Math.max(0, (e.SwDragDepth || 0) - 1);
        if (e.SwDragDepth === 0)
            e.classList.remove("Dragging");
    }

    /** "dragover": accept the drop. @param {DragEvent} ev - Event. */
    function onDragOver(ev) {
        ev.preventDefault();
        if (ev.dataTransfer)
            ev.dataTransfer.dropEffect = (ev.currentTarget.IsUploading || isOff(ev.currentTarget)) ? "none" : "copy";
    }

    /**
     * "drop": upload the dropped files.
     * @param {DragEvent} ev - Event.
     * @returns {Promise<void>}
     */
    async function onDrop(ev) {
        ev.preventDefault();
        const e = ev.currentTarget;
        e.SwDragDepth = 0;
        e.classList.remove("Dragging");
        if (e.IsUploading || isOff(e))
            return;
        const files = dropFiles(ev, e);
        if (files.length > 0)
            await uploadFor(e, files);
    }

    /**
     * "click" (also Enter / Space through Sw.KeyClick): pick files and upload them.
     * @param {MouseEvent} ev - Event (plain clicks only).
     * @returns {Promise<void>}
     */
    async function onClick(ev) {
        ev.preventDefault();
        ev.stopPropagation();
        if (ev.shiftKey || ev.altKey || ev.ctrlKey || ev.metaKey)
            return;
        const e = ev.currentTarget;
        if (e.IsUploading || e.classList.contains("Selecting") || isOff(e))
            return;
        e.classList.add("Selecting");
        let files;
        try {
            files = await pick({ Multiple: isMultiple(e.getAttribute("data-multiple")), Accept: e.getAttribute("data-accept") });
        }
        finally {
            e.classList.remove("Selecting");
        }
        if (files)
            await uploadFor(e, files);
    }

    /**
     * Make an element an upload target.
     * @param {HTMLElement} el - The element.
     * @param {object} [o] - Options (falsy values leave existing data-* attributes untouched):
     *  Repo {string} (data-repo), Multiple {boolean} (data-multiple), ApiBase {string} (data-apibase),
     *  Accept {string} (data-accept, file picker filter), Title {string} (tool tip, also shown on long press / focus),
     *  OnStart(element, files, repo), OnResults(element, result, files, repo), NoKeyboard {boolean},
     *  Guard {{Url:string, Auth:*}|boolean}: disable the target for users without the role (Sw.Access.Guard: Url = a server method
     *  of the page with the same auth, ex: its table; true = disable after the first refused request of the page).
     * A target is disabled (with the role needed as tool tip) after an upload was refused (403 or "not authorized").
     * @returns {{Remove:function():void}} Remove the listeners.
     */
    function target(el, o) {
        o = o || {};
        if (o.Guard && Sw.Access)
            Sw.Access.Guard(el, o.Guard === true ? {} : o.Guard);
        if (o.ApiBase)
            el.setAttribute("data-apibase", o.ApiBase);
        if (o.Repo)
            el.setAttribute("data-repo", o.Repo);
        if (o.Multiple)
            el.setAttribute("data-multiple", "true");
        if (o.Accept)
            el.setAttribute("data-accept", o.Accept);
        if (o.OnStart)
            el.OnUploadStart = o.OnStart;
        if (o.OnResults)
            el.OnUploadResults = o.OnResults;
        if (o.Title) {
            el.title = o.Title;
            if (Sw.Hint)
                Sw.Hint.Attach(el);
        }
        if (el.SwUploadTarget)
            return el.SwUploadTarget;
        el.addEventListener("click", onClick);
        el.addEventListener("drop", onDrop);
        el.addEventListener("dragover", onDragOver);
        el.addEventListener("dragenter", onDragEnter);
        el.addEventListener("dragleave", onDragLeave);
        let keyed = false;
        if (!o.NoKeyboard && Sw.KeyClick && !el.matches("button, a[href], input, select, textarea")) {
            Sw.KeyClick(el);
            if (!el.hasAttribute("role"))
                el.setAttribute("role", "button");
            keyed = true;
        }
        const handle = {
            /** Remove the listeners (the data-* attributes stay). */
            Remove() {
                el.removeEventListener("click", onClick);
                el.removeEventListener("drop", onDrop);
                el.removeEventListener("dragover", onDragOver);
                el.removeEventListener("dragenter", onDragEnter);
                el.removeEventListener("dragleave", onDragLeave);
                if (keyed && Sw.RemoveKeyClick)
                    Sw.RemoveKeyClick(el);
                el.SwUploadTarget = null;
            },
        };
        el.SwUploadTarget = handle;
        return handle;
    }

    /**
     * @namespace Sw.Upload - client of the FileUploaderService upload protocol (see the file header).
     */
    Sw.Upload = {
        /** Status codes (C# FileUploadStatus). */
        Status: UploadStatus,
        /** Default api base ("../upload/"). */
        DefaultApiBase: DefaultApiBase,
        StatusText: statusText,
        Ensure: ensure,
        Files: upload,
        Pick: pick,
        Target: target,
        /** Run an upload for a target element (busy state and callbacks), used by the drop / click handlers. */
        ForElement: uploadFor,
        /** Event handlers (used by the legacy globals). */
        Handlers: Object.freeze({ Click: onClick, Drop: onDrop, DragOver: onDragOver, DragEnter: onDragEnter, DragLeave: onDragLeave }),
    };
})();

// ------------------------------------------------------------------ legacy globals (old API, thin wrappers)

/**
 * LEGACY: get a translated status text (use Sw.Upload.StatusText).
 * @param {number} code - The status code.
 * @returns {string} Text.
 */
function fileUploaderStatusText(code) {
    return Sw.Upload.StatusText(code);
}

/**
 * LEGACY: upload file(s) to a repo (use Sw.Upload.Files).
 * @param {string} repo - Repo name.
 * @param {File[]|FileList} files - Files.
 * @param {string} [apiBase="../upload/"] - Api base url.
 * @returns {Promise<{Status:number[], Urls:(string|null)[], Redirected:(string|null)}|{Error:Error}>} Results.
 */
function fileUploader(repo, files, apiBase) {
    return Sw.Upload.Files(repo, files, { ApiBase: apiBase });
}

/**
 * LEGACY: upload files for a configured element (use Sw.Upload.ForElement).
 * @param {HTMLElement} element - The element (data-* attributes, expandos).
 * @param {File[]|FileList} files - Files.
 * @returns {Promise<void>}
 */
function fileUploaderElement(element, files) {
    return Sw.Upload.ForElement(element, files);
}

/**
 * LEGACY: "drop" handler of an upload element.
 * @param {DragEvent} ev - Event.
 * @returns {Promise<void>}
 */
function fileUploaderDrop(ev) {
    return Sw.Upload.Handlers.Drop(ev);
}

/**
 * LEGACY: "dragover" handler of an upload element (accepts the drop).
 * @param {DragEvent} ev - Event.
 * @returns {Promise<void>}
 */
async function fileUploaderDrag(ev) {
    Sw.Upload.Handlers.DragOver(ev);
}

/**
 * LEGACY: "dragenter" handler of an upload element (adds class Dragging).
 * @param {DragEvent} ev - Event.
 * @returns {Promise<void>}
 */
async function fileUploaderDragEnter(ev) {
    Sw.Upload.Handlers.DragEnter(ev);
}

/**
 * LEGACY: "dragleave" handler of an upload element (removes class Dragging).
 * @param {DragEvent} ev - Event.
 * @returns {Promise<void>}
 */
async function fileUploaderDragLeave(ev) {
    Sw.Upload.Handlers.DragLeave(ev);
}

/**
 * LEGACY: show a file picker (use Sw.Upload.Pick); must be called from a user gesture.
 * @param {boolean} allowMultiple - Allow multiple files.
 * @returns {Promise<FileList|null>} Files or null when cancelled.
 */
function selectFiles(allowMultiple) {
    return Sw.Upload.Pick({ Multiple: allowMultiple });
}

/**
 * LEGACY: "click" handler of an upload element (picks files and uploads them).
 * @param {MouseEvent} ev - Event.
 * @returns {Promise<void>}
 */
function fileUploaderClick(ev) {
    return Sw.Upload.Handlers.Click(ev);
}

/**
 * LEGACY: make an element upload files (use Sw.Upload.Target). Falsy arguments leave existing attributes untouched.
 * The element is now also keyboard operable (Enter / Space open the picker).
 * @param {HTMLElement} el - The element.
 * @param {string} [repo] - Repo name (data-repo).
 * @param {function(HTMLElement, File[], string)} [onstart] - Called when an upload starts.
 * @param {function(HTMLElement, object, File[], string)} [onresults] - Called with the result when it finished.
 * @param {string} [apiBase] - Api base url (data-apibase).
 * @param {boolean} [allowMultiple] - Allow multiple files (data-multiple).
 */
function fileUploaderSetup(el, repo, onstart, onresults, apiBase, allowMultiple) {
    Sw.Upload.Target(el, { Repo: repo, OnStart: onstart, OnResults: onresults, ApiBase: apiBase, Multiple: allowMultiple });
}

/**
 * LEGACY: set up all elements with a tag name (default "sysweaver-drop") as upload targets configured by their
 * data-* attributes.
 * @param {string} [tagName="sysweaver-drop"] - Tag name.
 */
function fileUploaderInit(tagName) {
    for (const e of Array.from(document.getElementsByTagName(tagName || "sysweaver-drop")))
        Sw.Upload.Target(e);
}
