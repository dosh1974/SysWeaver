
/**
 * @file fileUploader.js - browser client of the file upload protocol of FileUploaderService
 * (SysWeaver.MicroService.FileUploader), served as common/fileUploader.js next to the shared scripts of
 * SysWeaver.HttpServer (it only exists when that service is hosted).
 *
 * Loading: classic script, no side effects at load (only declares globals). Included statically with
 * `<script defer src="../common/fileUploader.js">` (upload/Example.html, ServerManager keys.html and
 * serviceInfo.html) or lazily with includeJs (app/home.js; chat/chat.js tolerates a missing file and checks
 * `typeof fileUploader`). Must be loaded after common/common.js, common/md5.js and (optionally) common/UZIP.js.
 *
 * Globals defined: UploadStatus, fileUploaderStatusText, fileUploader, fileUploaderElement, fileUploaderDrop,
 * fileUploaderDrag, fileUploaderDragEnter, fileUploaderDragLeave, selectFiles, fileUploaderClick,
 * fileUploaderSetup, fileUploaderInit.
 *
 * Dependencies: common.js (sendRequest, _T, _TF, GetFunction, badClick, waitEvent2, getFileExtension,
 * isCompressibleExt), md5.js (YaMD5, required), UZIP.js (UZIP; treated as optional with `!!UZIP`, but the bare
 * global must be declared or a ReferenceError is caught as {Error}).
 *
 * Protocol: every file is read fully into memory and MD5-hashed; one JSON POST to <apiBase>CheckStatus with
 * {Repo, Files: FileUploadInfo[]} returns FileUploadResult[] {Result, Url}; only files with status 2 (Upload)
 * are then POSTed in parallel to <apiBase>Upload?repo=&name=&length=&hash=&time= (query values not
 * URL-encoded), deflate compressed when UZIP is available and it helps. Server chunking (Chunks) is not used.
 * Returned Url values are relative to the server root (consumers prefix "../").
 *
 * CSS contract: the script creates no DOM except temporary file inputs; it toggles the classes Dragging,
 * Selecting and Uploading on upload elements, which consumers style. Per-element state: expandos IsUploading,
 * OnUploadStart, OnUploadResults.
 */

/**
 * Contains status codes (named); mirrors the C# FileUploadStatus enum (kept in sync by hand, also with the
 * switch in fileUploaderStatusText). None (0) = uploaded OK, AlreadyUploaded (1) = deduplicated by hash,
 * Upload (2) = may be uploaded (CheckStatus only); negative values are errors / refusals.
 * @type {Object<string, number>}
 */
const UploadStatus =
{
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
    AlreadyUploaded:  1,
    Upload: 2,
}

/**
 * Get a status text from an statuc code
 * Returns a translated (_TF) text for the codes -12..2, otherwise _T("Unknown upload status [{0}]", code).
 * @param {number} code The statuc code (as returned by the server).
 * @returns {string} A human readable status message.
 */
function fileUploaderStatusText(code) {
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
            return _TF("The file data was invalid (some processing went wrong)", "This is the description of a status code for a service that upload file(s) to a repository");
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
 * Uploads file(s) to a repo
 * Reads every file into an ArrayBuffer, MD5-hashes it (YaMD5.hashByteArray, lower-case hex) and builds
 * FileUploadInfo objects {Name, Length, Hash, LastModified}. Posts them once with
 * sendRequest(apiBase + "CheckStatus", {Repo, Files}, true); a falsy reply returns
 * {Error: Error("Couldn't check status!")}. Seeds Status / Urls from the reply (Result, Url), then uploads in
 * parallel (Promise.all, inner doOne) only the files whose status is 2 (Upload); buffers are released
 * (data[i] = null) once queued. Logs every check / upload with console.log. Any exception yields {Error: e}.
 * @param {string} repo Name of the repo (must exist on server)
 * @param {File[]|FileList} files Array of files.
 * @param {string} [apiBase="../upload/"] Base url to the upload Api's, defaults to "../upload/"
 * @returns {Promise<{Status:number[], Urls:(string|null)[], Redirected:(string|null)}|{Error:Error}>} With an array of status codes found in the "Status" property. "Error" is set on an exception. "Urls" is an array of url's to the files (or null if not applicable). "Redirected" is the URL an upload was redirected to (e.g. a login page), or null. Arrays are indexed like `files`.
 */
async function fileUploader(repo, files, apiBase) {
    const l = files.length;
    const hashes = [];
    const data = [];
    for (let i = 0; i < l; ++i) {
        const f = files[i];
        const b = await f.arrayBuffer();
        //const hashBytes = await hashData(b);
        //const hash = await bufferToBase64(hashBytes);
        const hash = YaMD5.hashByteArray(b);
        data.push(b);
        hashes.push(
            {
                Name: f.name,
                Length: b.byteLength,
                Hash: hash,
                LastModified: f.lastModified,
            });
    }
    if (!apiBase)
        apiBase = '../upload/';
    try {

        const csr = await sendRequest(apiBase + "CheckStatus",
            {
                Repo: repo,
                Files: hashes,
            }, true);
        if (!csr)
            return {
                Error: new Error("Couldn't check status!")
            };
        const stats = [];
        const urls = [];
        const len = csr.length;
        for (let i = 0; i < len; ++i) {
            stats[i] = csr[i].Result;
            urls[i] = csr[i].Url;
        }
        const haveZip = !!UZIP;

        /** @type {?string} URL of the last upload that was redirected (reported as Redirected). */
        let redirect = null;
        /**
         * Upload one file: builds a fetch Request POST (mode "cors", "application/octet-stream") to
         * apiBase + "Upload?repo=&name=&length=&hash=&time=" (values not URL-encoded). If UZIP exists and
         * isCompressibleExt(getFileExtension(Name)), the body is UZIP.deflateRaw (level 8) with
         * "Content-Encoding: deflate" when that is smaller than the original. Writes stats[index] / urls[index]
         * from the JSON reply {Result, Url}; a non-200 status, a redirect (also sets the closure `redirect`) or an
         * exception sets status -1. Logs the outcome to the console.
         * @param {{Name:string, Length:number, Hash:string, LastModified:number}} info - FileUploadInfo of the file.
         * @param {ArrayBuffer} buffer - File content.
         * @param {number} index - Index of the file in the result arrays.
         * @returns {Promise<void>}
         */
        async function doOne(info, buffer, index) {

            let r = null;
            if (haveZip) {
                //  Check if file should be zipped
                const ext = getFileExtension(info.Name).toLowerCase();
                if (isCompressibleExt(ext)) {
                    const tb = UZIP.deflateRaw(new Uint8Array(buffer), { level: 8 });
                    if (tb.length < buffer.byteLength) {
                        r = new Request(apiBase + "Upload?repo=" + repo + "&name=" + info.Name + "&length=" + info.Length + "&hash=" + info.Hash + "&time=" + info.LastModified, {
                            method: "POST",
                            mode: "cors",
                            headers: {
                                "Content-Type": "application/octet-stream",
                                "Content-Encoding": "deflate",
                            },
                            body: tb,
                            //redirect: "error",
                        });
                    }
                }
            }
            if (!r) {
                r = new Request(apiBase + "Upload?repo=" + repo + "&name=" + info.Name + "&length=" + info.Length + "&hash=" + info.Hash + "&time=" + info.LastModified, {
                    method: "POST",
                    mode: "cors",
                    headers: {
                        "Content-Type": "application/octet-stream",
                    },
                    body: buffer,
                    //redirect: "error",
                });
            }
            const i = index;
            try {
                urls[i] = null;
                const res = await fetch(r);
                if (res.status != 200) {
                    stats[i] = -1;
                    console.log('Failed: "' + info.Name + '", fetch status: ' + res.status);
                } else {
                    if (res.redirected) {
                        stats[i] = -1;
                        redirect = res.url;
                        console.log('Failed: "' + info.Name + '", fetch redirected to: ' + res.url);
                    } else {
                        const s = await res.json();
                        const status = s.Result;
                        stats[i] = status;
                        urls[i] = s.Url;
                        if (status != 0)
                            console.log('Failed: "' + info.Name + '", status: ' + status);
                        else
                            console.log('Uploaded: "' + info.Name + '", status: ' + status);
                    }
                }
            }
            catch (e) {
                stats[i] = -1;
                console.log('Failed: "' + info.Name + '", fetch exception: ' + e);

            }
        }

        const uploads = [];
        for (let i = 0; i < l; ++i) {

            const info = hashes[i];
            const s = stats[i];
            console.log('Checked: "' + info.Name + '", length: ' + info.Length + ', status: ' + s + ', hash: "' + info.Hash + '"');
            if (s != 2) {
                data[i] = null;
                continue;
            }
            uploads.push(doOne(info, data[i], i));
            data[i] = null;
        }
        if (uploads.length > 0)
            await Promise.all(uploads);
        return {
            Status: stats,
            Urls: urls,
            Redirected: redirect,
        };
    }
    catch (e) {
        return {
            Error: e,
        }
    }
}

/**
 * Called internally when some files are dropped or selected to perform the actual upload
 * Sets element.IsUploading = true, adds class "Uploading" and a temporary translated title. Reads "data-repo";
 * calls the global function named by "data-onstart" (GetFunction) and awaits element.OnUploadStart(element,
 * files, repo) if set; runs fileUploader(repo, files, "data-apibase"); then calls the "data-onresults" function
 * and awaits element.OnUploadResults(element, res, files, repo). Title, class and IsUploading are restored in
 * `finally`.
 * @param {HTMLElement} element The element (that have the attributes etc).
 * @param {File[]|FileList} files The files to upload
 * @returns {Promise<void>}
 */
async function fileUploaderElement(element, files) {

    element.IsUploading = true;
    element.classList.add("Uploading");
    const oldTit = element.title;
    element.title = _TF("Upload in progress, please wait");
    try {

        let repo = element.getAttribute("data-repo");
        const startCb = element.getAttribute("data-onstart");
        if (startCb)
            GetFunction(startCb)(element, files, repo);
        const ons = element.OnUploadStart;
        if (ons)
            await ons(element, files, repo);

        const res = await fileUploader(repo, files, element.getAttribute("data-apibase"));
        const resCb = element.getAttribute("data-onresults");
        if (resCb)
            GetFunction(resCb)(element, res, files, repo);
        const onr = element.OnUploadResults;
        if (onr)
            await onr(element, res, files, repo);
    }
    finally {
        element.title = oldTit;
        element.classList.remove("Uploading");
        element.IsUploading = false;
    }
}

/**
* Used to handle dropping file(s) and file upload onto an element by hooking this to the "drop" even of an element
 * Calls preventDefault, removes "Dragging" (fileUploaderDragLeave), ignores the drop while the element
 * IsUploading. Collects files from dataTransfer.items (kind "file") or dataTransfer.files, keeps only the first
 * file unless "data-multiple" is "true" or "1", then calls fileUploaderElement(currentTarget, files).
 * @param {DragEvent} ev The event object
 * @returns {Promise<void>}
 */
async function fileUploaderDrop(ev) {
    ev.preventDefault();
    await fileUploaderDragLeave(ev);
    const e = ev.currentTarget;
    if (e.IsUploading)
        return;
    const files = [];
    /**
     * Collect one dropped file into the local `files` array.
     * @param {File} file - The dropped file.
     * @param {number} index - Index in the drop list (unused).
     */
    function onFile(file, index) {
        files.push(file);
    }
    if (ev.dataTransfer.items) {
        [...ev.dataTransfer.items].forEach((item, i) => {
            if (item.kind === "file")
                onFile(item.getAsFile(), i);
        });
    } else {
        [...ev.dataTransfer.files].forEach((file, i) => onFile(file, i));
    }
    const ma = e.getAttribute("data-multiple");
    const mu = ma && ((ma == "true") || (ma == "1"));
    if (!mu)
        files.length = files.length > 1 ? 1 : files.length;
    await fileUploaderElement(e, files);
}


/**
 * Used to allow dropping a file onto an element by hooking this to the "dragover" even of an element
 * (only calls preventDefault so the element accepts drops).
 * @param {DragEvent} ev The event object
 * @returns {Promise<void>}
 */
async function fileUploaderDrag(ev) {
    ev.preventDefault();
}

/**
 * Used to add the "Dragging"" css class of an element with a drag target ("dragenter" handler, uses ev.currentTarget)
 * @param {DragEvent} ev The event object
 * @returns {Promise<void>}
 */
async function fileUploaderDragEnter(ev) {
    ev.currentTarget.classList.add("Dragging");
}

/**
 * Used to remove the "Dragging" css class of an element with a drag target ("dragleave" handler, also called
 * by fileUploaderDrop; uses ev.currentTarget)
 * @param {DragEvent} ev The event object
 * @returns {Promise<void>}
 */
async function fileUploaderDragLeave(ev) {
    ev.currentTarget.classList.remove("Dragging");
}

/**
 * Pop-up a file selector box (must be initiated by a user interaction like click)
 * Creates a detached <input type=file> (with "multiple" when requested), clicks it and awaits "change" or
 * "cancel" (waitEvent2). Used directly by chat.js.
 * @param {boolean} allowMultiple If true, multiple files can be selected
 * @returns {Promise<FileList|null>} An array of selected file objects or null if closed without selections.
 */
async function selectFiles(allowMultiple)
{
    const fi = document.createElement("input");
    if (allowMultiple)
        fi.setAttribute("multiple", 1);
    fi.type = "file";
    fi.click();
    await waitEvent2(fi, "change", "cancel");
    const files = fi.files;
    if (files)
        if (files.length > 0)
            return files;
    return null;
}

/**
 * Add this function to a click event listener (or other user interaction) to pop-up a file selection box (that will upload the selected files to the repo specified in the data-repo).
 * Target element can have the following attributes:
 * - "data-repo" specifies what repo to upload to.
 * - "data-onstart" optionally specifies the name of a javascript function that is invoked when file uploading starts, arguments are: (element, files[], repo)
 * - "data-onresults" optionally specifies the name of a javascript function that is invoked when file(s) was uploaded (or not).
 * - "data-multiple" optionally set to "true" or "1" to allow multiple files to be uploaded at once (server repo must suport that).
 * - "data-apibase" optionally specifies the base path of the api calls, defaults to "../upload/".
 * Returns early if badClick(ev) or the element IsUploading. Creates a detached <input type=file> (multiple per
 * "data-multiple"), adds class "Selecting" while the dialog is open (removed on change / cancel) and on change
 * with at least one file calls fileUploaderElement(element, files).
 * @param {MouseEvent} ev The event (currentTarget is used as the element)
 * @returns {Promise<void>}
 */
async function fileUploaderClick(ev) {
    if (badClick(ev))
        return;
    const e = ev.currentTarget;
    if (e.IsUploading)
        return;
    const fi = document.createElement("input");
    const ma = e.getAttribute("data-multiple");
    const mu = ma && ((ma == "true") || (ma == "1"));
    if (mu)
        fi.setAttribute("multiple", 1);
    fi.type = "file";
    fi.addEventListener("change", async ce => {
        ce.preventDefault();
        ce.stopPropagation();
        const files = fi.files;
        e.classList.remove("Selecting");
        if (files) {
            if (files.length > 0) {
                await fileUploaderElement(e, files);
            }
        }
    });
    fi.addEventListener("cancel", () => e.classList.remove("Selecting"));
    e.classList.add("Selecting");
    fi.click();
}


/**
 * Make an element upload files (open dialog on click and accept dropping of files).
 * The following css classes are added to the element:
 * "Dragging" - When a file is dragged over the element.
 * "Selecting" - When a file selection dialog is open.
 * "Uploading" - When a file is being uploaded (and possible processed server side).
 * Adds the click, drop, dragover, dragenter and dragleave listeners and stores the configuration as
 * "data-apibase", "data-repo", "data-multiple"="true" and the expandos el.OnUploadStart / el.OnUploadResults.
 * Falsy arguments leave existing attributes untouched.
 * @param {HTMLElement} el The element to hook up (adding events)
 * @param {string} repo Optional repositiory name (if not set the "data-repo"" attribute of the element is used and should be set).
 * @param {function(HTMLElement, File[], string)} onstart Optional function to invoke when an upload is starting (any function found in the "data-onstart" attribute is also invoked).
 * @param {function(HTMLElement, object, File[], string)} onresults Optional function to invoke when an upload is starting (any function found in the "data-onresults" attribute is also invoked). Actually invoked when the upload has finished, with (element, result of fileUploader, files, repo).
 * @param {string} apiBase Base url to the upload Api's, defaults to "../upload/" (if not set the "data-apibase" attribute of the element is used if it exist).
 * @param {boolean} allowMultiple If true, multiple files may be uploaded at once.
 */
function fileUploaderSetup(el, repo, onstart, onresults, apiBase, allowMultiple) {

    el.addEventListener("click", fileUploaderClick);
    el.addEventListener("drop", fileUploaderDrop);
    el.addEventListener("dragover", fileUploaderDrag);
    el.addEventListener("dragenter", fileUploaderDragEnter);
    el.addEventListener("dragleave", fileUploaderDragLeave);
    if (apiBase)
        el.setAttribute("data-apibase", apiBase);
    if (repo)
        el.setAttribute("data-repo", repo);
    if (allowMultiple)
        el.setAttribute("data-multiple", "true");
    if (onstart)
        el.OnUploadStart = onstart;
    if (onresults)
        el.OnUploadResults = onresults;
}


/**
 * Set up all tags with a certain name (defaults to "sysweaver-drop") for file upload.
 * Elements can have the following attributes:
 * - "data-repo" specifies what repo to upload to.
 * - "data-onstart" optionally specifies the name of a javascript function that is invoked when file uploading starts, arguments are: (element, files[], repo)
 * - "data-onresults" optionally specifies the name of a javascript function that is invoked when file(s) was uploaded (or not).
 * - "data-multiple" optionally set to "true" or "1" to allow multiple files to be uploaded at once (server repo must suport that).
 * - "data-apibase" optionally specifies the base path of the api calls, defaults to "../upload/".
 * The following css classes are added to the elements:
 * "Dragging" - When a file is dragged over the element.
 * "Selecting" - When a file selection dialog is open.
 * "Uploading" - When a file is being uploaded (and possible processed server side).
 * Calls fileUploaderSetup(element) without arguments for every element from
 * document.getElementsByTagName(tagName) (iterating backwards). No in-repo page calls it.
 * @param {string} [tagName="sysweaver-drop"] Name of the tags to setup for file upload (defaults to "sysweaver-drop").
 */
function fileUploaderInit(tagName) {
    if (!tagName)
        tagName = "sysweaver-drop";
    const tags = document.getElementsByTagName(tagName);
    let tl = tags.length;
    while (tl > 0) {
        --tl;
        fileUploaderSetup(tags[tl]);
    }
}

