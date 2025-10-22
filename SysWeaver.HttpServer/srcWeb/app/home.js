
const CS = document.currentScript.src;

async function homeMain() {

    if (await sendRequest("../Api/HaveService", "SysWeaver.MicroService.CustomUserImageService")) {

        const current = CS;
        await Promise.all([
            await includeJs(current, "../common/md5.js"),
            await includeJs(current, "../common/UZIP.js"),
            await includeJs(current, "../common/fileUploader.js")
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
                        break;
                    case UploadStatus.None:
                        Info(_T("Uploaded user image \"{0}\"", files[0].name, "Text displayed when a user image file was succesfully uploaded to a server.{0} is replaced with the name of the user image file"));
                        const cs = el.src;
                        if (cs.endsWith("/small"))
                            el.src = cs.substring(0, cs.length - 5) + "large";
                        else
                            SetImageSource(el, cs, null, true);
                        break;
                    default:
                        Fail(_T("{0}, when uploading user image \"{1}\"", fileUploaderStatusText(res0), files[0].name, "Text displayed when uploading of a user image file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the user image file"));
                        break;
                }
            }
        );
    }
    PageLoaded();
}