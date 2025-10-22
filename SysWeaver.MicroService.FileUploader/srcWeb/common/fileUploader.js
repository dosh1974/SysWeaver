
/** Contains status codes (named) */
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
 * @param {string} repo Name of the repo (must exist on server)
 * @param {File[]} files Array of files.
 * @param {string} apiBase Base url to the upload Api's, defaults to "../upload/"
 * @returns {object} With an array of status codes found in the "Status" property. "Error" is set on an exception. "Urls" is an array of url's to the files (or null if not applicable)
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

        let redirect = null;
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
 * @param {HTMLElement} element The element (that have the attributes etc).
 * @param {File[]} files The files to upload
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
 * @param {Event} ev The event object
 */
async function fileUploaderDrop(ev) {
    ev.preventDefault();
    await fileUploaderDragLeave(ev);
    const e = ev.currentTarget;
    if (e.IsUploading)
        return;
    const files = [];
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
 * @param {Event} ev The event object
 */
async function fileUploaderDrag(ev) {
    ev.preventDefault();
}

/**
 * Used to add the "Dragging"" css class of an element with a drag target
 * @param {Event} ev The event object
 */
async function fileUploaderDragEnter(ev) {
    ev.currentTarget.classList.add("Dragging");
}

/**
 * Used to remove the "Dragging" css class of an element with a drag target
 * @param {Event} ev The event object
 */
async function fileUploaderDragLeave(ev) {
    ev.currentTarget.classList.remove("Dragging");
}

/**
 * Pop-up a file selector box (must be initiated by a user interaction like click)
 * @param {bool} allowMultiple If true, multiple files can be selected
 * @returns {File[]} An array of selected file objects or null if closed without selections.
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
 * @param {Event} ev The event (target is used as the element)
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
 * @param {HTMLElement} el The element to hook up (adding events)
 * @param {string} repo Optional repositiory name (if not set the "data-repo"" attribute of the element is used and should be set).
 * @param {function(HTMLElement, File[], string)} onstart Optional function to invoke when an upload is starting (any function found in the "data-onstart" attribute is also invoked).
 * @param {function(HTMLElement, object, File[], string)} onresults Optional function to invoke when an upload is starting (any function found in the "data-onresults" attribute is also invoked).
 * @param {string} apiBase Base url to the upload Api's, defaults to "../upload/" (if not set the "data-apibase" attribute of the element is used if it exist).
 */
function fileUploaderSetup(el, repo, onstart, onresults, apiBase) {

    el.addEventListener("click", fileUploaderClick);
    el.addEventListener("drop", fileUploaderDrop);
    el.addEventListener("dragover", fileUploaderDrag);
    el.addEventListener("dragenter", fileUploaderDragEnter);
    el.addEventListener("dragleave", fileUploaderDragLeave);
    if (apiBase)
        el.setAttribute("data-apibase", apiBase);
    if (repo)
        el.setAttribute("data-repo", repo);
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
 * @param {string} tagName Name of the tags to setup for file upload (defaults to "sysweaver-drop").
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

