/**
 * @file Script of the ServerManager "Key files" page (keys.html, menu item restricted to Roles.Admin).
 * Loaded with a static `<script defer>` tag by keys.html and started from `<body onload="keysMain()">`.
 * Wires a file-upload drop target (`<kf-upload>`) to the server side `Keys` BackupFileRepo and renders the
 * key file table (`<kf-table>`) through the explore table protocol.
 *
 * Globals defined: `keysMain` (no other globals, no immediately executed code).
 *
 * Dependencies (globals from other files):
 * - common/fileUploader.js (+ md5.js, UZIP.js): `fileUploaderSetup`, `UploadStatus`, `fileUploaderStatusText`.
 * - explore/table.js: `Table.addTable`.
 * - common/common.js: `Fail`, `Info`, `_T`, `PageLoaded`.
 *
 * Conventions: the `UploadCompleted` handler is a copy of the one in serviceInfo.js.
 */

/**
 * Entry point of keys.html (called from `<body onload>`).
 * Sets up the `<kf-upload>` element as an upload target for the repo "Keys" (fileUploaderSetup with a no-op
 * third callback, `UploadCompleted` as completion callback and `true` as last argument), then renders
 * `../ServerManager/KeysTable` into `<kf-table>` and finally calls `PageLoaded()`.
 * @returns {Promise<void>} Resolves when the key table has been created.
 */
async function keysMain() {

    /**
     * The handle returned by `Table.addTable` (has an `UpdateNow()` function); null until the table exists.
     * @type {?{UpdateNow:function():void}}
     */
    let table = null;

    /**
     * Refreshes the key file table immediately (calls `table.UpdateNow()`); does nothing until the table exists.
     * @returns {void}
     */
    function updateNow() {
        if (table)
            table.UpdateNow();
    }

    /**
     * Upload-finished callback passed to `fileUploaderSetup`.
     * If `res.Error` is set it shows `Fail(err)`. Otherwise it scans `res.Status` in three passes and reports
     * only one message: the first status that is neither `UploadStatus.AlreadyUploaded` nor `UploadStatus.None`
     * shows a `Fail` with `fileUploaderStatusText(status)` and the file name; else the first `AlreadyUploaded`
     * shows an `Info` ("already uploaded"); else the first `None` (success) shows an `Info` ("Uploaded") and
     * calls `updateNow()` to refresh the key table. All texts are localized with `_T`.
     * @param {HTMLElement} e - The upload element (`<kf-upload>`).
     * @param {{Error:?string, Status:Array<number>}} res - Upload result; `Status` holds one `UploadStatus` value per file.
     * @param {File[]|FileList} files - The uploaded files (same order as `res.Status`).
     * @returns {void}
     */
    function UploadCompleted(e, res, files) {
        const err = res.Error;
        if (err) {
            Fail(err);
            return;
        }
        const ss = res.Status;
        const sl = ss.length;
        for (let i = 0; i < sl; ++i) {
            const res0 = ss[i];
            switch (res0) {
                case UploadStatus.AlreadyUploaded:
                case UploadStatus.None:
                    break;
                default:
                    Fail(_T("{0}, when uploading \"{1}\"", fileUploaderStatusText(res0), files[i].name, "Text displayed when uploading of a file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the file"));
                    return;
            }
        }
        for (let i = 0; i < sl; ++i) {
            const res0 = ss[i];
            if (res0 === UploadStatus.AlreadyUploaded) {
                Info(_T("File \"{0}\" was already uploaded", files[i].name, "Text displayed when a file have already been uploaded to a server.{0} is replaced with the name of the file"));
                return;
            }
        }
        for (let i = 0; i < sl; ++i) {
            const res0 = ss[i];
            if (res0 === UploadStatus.None) {
                Info(_T("Uploaded \"{0}\"", files[i].name, "Text displayed when a file was succesfully uploaded to a server.{0} is replaced with the name of the file"));
                updateNow();
                return;
            }
        }
    }


    // Make <kf-upload> an upload target for the "Keys" repo (third argument is a no-op callback).
    fileUploaderSetup(
        document.body.getElementsByTagName("kf-upload")[0],
        "Keys",
        f => {
        },
        UploadCompleted,
        null,
        true);
    table = await Table.addTable("../ServerManager/KeysTable", document.body.getElementsByTagName("kf-table")[0]);
    PageLoaded();
}