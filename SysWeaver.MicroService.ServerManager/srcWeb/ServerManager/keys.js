
async function keysMain() {

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


    fileUploaderSetup(
        document.body.getElementsByTagName("kf-upload")[0],
        "Keys",
        f => {
        },
        UploadCompleted,
        null,
        true);
    await Table.addTable("../ServerManager/KeysTable", document.body.getElementsByTagName("kf-table")[0]);
    PageLoaded();
}