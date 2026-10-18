
/**
 * @file Script of the Home page (app/Home.html), the first main-menu item for logged in users.
 * Loaded with a static `<script defer>` tag by Home.html after ../common/common.js; started by
 * `<body onload="homeMain()">`. Home.html is a server template (session nick name, app description,
 * the current user image `../auth/UserImages/Current/small` and the logo) embedded in the shell's iframe.
 *
 * Globals defined: `CS`, `homeMain`.
 * Dependencies (common.js): `AddLoading`, `sendRequest`, `includeJs`, `_T`/`_TF`, `Info`, `Fail`,
 * `SetImageSource`, `InterOp`. Dynamically loaded (fileUploader.js from SysWeaver.MicroService.FileUploader):
 * `fileUploaderSetup`, `fileUploaderStatusText`, `UploadStatus`.
 * Convention: the user image only becomes an upload target when the server has the
 * `SysWeaver.MicroService.CustomUserImageService`; a successful upload is broadcast as the InterOp message
 * `UserImageChanged`, which the shell's user button (application.js `App.Init`) listens for.
 */

/**
 * URL of this script (captured while the script executes, since `document.currentScript` is null later).
 * Used as the base URL for the `includeJs` calls in `homeMain`.
 * @type {string}
 */
const CS = document.currentScript.src;

/**
 * Page entry point (body onload). Shows the loading overlay, asks the server whether the custom user image
 * service exists (`../Api/HaveService` with "SysWeaver.MicroService.CustomUserImageService") and, if so,
 * loads ../common/md5.js, ../common/UZIP.js and ../common/fileUploader.js in parallel and turns
 * `img.UserImage` into an upload target: adds class `CanUpload`, sets a tool tip and calls
 * `fileUploaderSetup(el, "UserImage", onSelect, onDone)`.
 * - onSelect shows an Info toast with the first file name.
 * - onDone: on `res.Error` calls Fail; otherwise inspects only `res.Status[0]`:
 *   `UploadStatus.AlreadyUploaded` -> Info + `InterOp.Post("UserImageChanged")`;
 *   `UploadStatus.None` (success) -> Info, switches the image src from ".../small" to ".../large"
 *   (or reloads it with `SetImageSource(el, src, null, true)` when it does not end in "/small"), then posts
 *   `UserImageChanged`; any other status -> Fail with `fileUploaderStatusText(status)`.
 * Errors are reported with Fail(e.message); the loading overlay is always removed (which also posts
 * `LoaderRemoved` to the shell via common.js).
 * @returns {Promise<void>}
 */
async function homeMain() {

    const onLoaded = AddLoading();
    try {
        if (await sendRequest("../Api/HaveService", "SysWeaver.MicroService.CustomUserImageService")) {

            const current = CS;
            await Promise.all([
                includeJs(current, "../common/md5.js"),
                includeJs(current, "../common/UZIP.js"),
                includeJs(current, "../common/fileUploader.js")
            ]);
            const el = document.body.getElementsByClassName("UserImage")[0];
            el.classList.add("CanUpload");
            el.title = _TF("Click to select a new user image or drag an image here", "A tool tip description of a html element that will let the user modify their user image");
            fileUploaderSetup(el, "UserImage",
                (el, files, rr) => {
                    //  Called when file(s) are selected for uploading
                    Info(_T("Uploading user image \"{0}\"", files[0].name, "Text displayed when a user image file is about to be uploaded to a server.{0} is replaced with the name of the user image file"));
                },
                (el, res, files, rr) => {
                    const err = res.Error;
                    if (err) {
                        Fail(err);
                        return;
                    }
                    //  Only showing status of first file, todo: when multiple is allowed show all?
                    const res0 = res.Status[0];
                    switch (res0) {
                        case UploadStatus.AlreadyUploaded:
                            Info(_T("User image \"{0}\" was already uploaded", files[0].name, "Text displayed when the user image that was uploaded to a server is the same as the current user image.{0} is replaced with the name of the user image file"));
                            InterOp.Post("UserImageChanged");
                            break;
                        case UploadStatus.None:
                            Info(_T("Uploaded user image \"{0}\"", files[0].name, "Text displayed when a user image file was succesfully uploaded to a server.{0} is replaced with the name of the user image file"));
                            const cs = el.src;
                            if (cs.endsWith("/small"))
                                el.src = cs.substring(0, cs.length - 5) + "large";
                            else
                                SetImageSource(el, cs, null, true);
                            InterOp.Post("UserImageChanged");
                            break;
                        default:
                            Fail(_T("{0}, when uploading user image \"{1}\"", fileUploaderStatusText(res0), files[0].name, "Text displayed when uploading of a user image file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the user image file"));
                            break;
                    }
                }
            );
        }
    }
    catch (e) {
        Fail(e.message);
    }
    finally {
        onLoaded();
    }

}