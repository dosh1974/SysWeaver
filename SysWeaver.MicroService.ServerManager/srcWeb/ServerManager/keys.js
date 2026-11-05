/**
 * @file keys.js - the ServerManager "Key files" page (keys.html, entry `keysMain`; menu item restricted to Roles.Admin).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js, common/md5.js, common/UZIP.js,
 * common/fileUploader.js, ../explore/table.js and smShared.js; started from `<body onload>`.
 *
 * A Design v2 fill page (Sw.PageLayout): the page header with the action "Upload key files" (SmShared.UploadButton: a button that
 * opens the file picker and takes dropped files, multiple files, server repo "Keys") and the live key file table
 * (SmShared.FillTable("../ServerManager/KeysTable")), refreshed after a successful upload.
 *
 * Globals defined: `keysMain`.
 */

/**
 * Entry point of keys.html.
 * @returns {Promise<void>}
 */
function keysMain() {
    return Sw.Page.Main(async () => {
        let table = null;
        const page = Sw.PageLayout({
            Main: ".kf-page",
            Type: "fill",
            Compact: false,
            Title: _TF("Key files", "Title of the key files page"),
            Description: _TF("Keys and certificates the managed services read. Upload files with the button or drop them on it.", "Description of the key files page"),
            Actions: [SmShared.UploadButton({
                Text: _TF("Upload key files", "Text of the button on the key files page that opens a file picker to upload key files (files can also be dropped on it)"),
                Title: _TF("Select or drag key files to add them", "Tool tip of the key file upload button"),
                Repo: "Keys",
                OnUploaded: () => table && table.UpdateNow(),
                //  No role check up front (no extra request, as the original page): the button is disabled after a refused request
            })],
        });
        table = await SmShared.FillTable(page, "../ServerManager/KeysTable");
    }, { Loading: true });
}
