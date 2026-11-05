/**
 * @file Script of the upload example / debug page (upload/Example.html, menu "Debug/FileUploader/Upload file").
 * Loaded with a static `<script defer>` after ../sw/core.js, ../sw/ui.js and ../common/fileUploader.js; started by
 * `<body onload="exampleMain()">`.
 *
 * Url parameters: `repo` (initial repository, default "UserImage"), `multiple` (any value: allow multiple files).
 * Globals defined: `exampleMain`.
 * Dependencies: Sw core / ui (`Sw.Page`, `Sw.Url`, `Sw.Info`, `Sw.Fail`, `Sw.SetImageSource`) and `Sw.Upload`
 * (common/fileUploader.js, which loads common/md5.js on demand).
 */
"use strict";

/**
 * Show the result of an upload (only the first file is reported, like before): the url of the first file as a link,
 * a toast per status and a cache flushing reload of the current user image after a successful upload.
 * @param {{Status:number[], Urls:string[], Error:Error}} res - Result of Sw.Upload.Files.
 * @param {File[]|FileList} files - The uploaded files.
 * @returns {void}
 */
function exampleShowResult(res, files) {
    const a = document.getElementById("link");
    if (res.Error) {
        a.classList.remove("Show");
        Sw.Fail(res.Error.message || String(res.Error));
        return;
    }
    const status = res.Status[0];
    const url = res.Urls[0];
    a.classList.toggle("Show", !!url);
    if (url) {
        a.textContent = url;
        a.href = "../" + url;
    }
    const name = files[0].name;
    const S = Sw.Upload.Status;
    switch (status) {
        case S.AlreadyUploaded:
            Sw.Info(_T("File \"{0}\" was already uploaded", name, "Text displayed when a file have already been uploaded to a server.{0} is replaced with the name of the file"));
            break;
        case S.None:
            Sw.Info(_T("Uploaded \"{0}\"", name, "Text displayed when a file was successfully uploaded to a server.{0} is replaced with the name of the file"));
            {
                const img = document.getElementById("userimage");
                Sw.SetImageSource(img, img.src, { FlushCache: true });
            }
            break;
        default:
            Sw.Fail(_T("{0}, when uploading \"{1}\"", Sw.Upload.StatusText(status), name, "Text displayed when uploading of a file to a server failed.{0} is replaced with a message as to why the file failed.{1} is replaced with the name of the file"));
            break;
    }
}

/**
 * Page entry point: reads the url parameters, sets up the repository input and the drop zone (Sw.Upload.Target).
 * @returns {Promise<void>}
 */
async function exampleMain() {
    const done = Sw.Page.Loading();
    try {
        //  Design v2: inside the app shell the h1 says what the menu says (Sw.MenuTitle, ex: "File upload test")
        const menuTitle = typeof Sw.MenuTitle === "function" ? Sw.MenuTitle() : null;
        const h1 = document.querySelector(".sw-ph-title");
        if (menuTitle && h1) {
            h1.textContent = menuTitle;
            document.title = menuTitle;
        }
        const repo = Sw.Url.Get("repo") || "UserImage";
        const drop = document.getElementById("drop");
        const input = document.getElementById("repo");
        input.value = repo;
        input.addEventListener("change", () => drop.setAttribute("data-repo", input.value.trim()));
        //  Touch only devices (no mouse): files can't be dropped there, so the words say "tap"
        if (window.matchMedia && window.matchMedia("(pointer: coarse)").matches && !window.matchMedia("(any-pointer: fine)").matches) {
            const words = drop.querySelector(".DropText");
            if (words)
                words.textContent = _TF("Tap here to choose files", "Text inside the upload area on a touch device (phone, tablet): tapping it opens the file picker");
        }
        Sw.Upload.Target(drop, {
            Repo: repo,
            Multiple: Sw.Url.Has("multiple"),
            Title: drop.title,
            OnStart: (el, files) => Sw.Info(_T("Uploading \"{0}\"", files[0].name, "Text displayed when a file is about to be uploaded to a server.{0} is replaced with the name of the file")),
            OnResults: (el, res, files) => exampleShowResult(res, files),
        });
        drop.setAttribute("aria-label", drop.title);
        await Sw.Upload.Ensure();
    }
    catch (e) {
        Sw.Fail(e.message || String(e));
    }
    finally {
        done();
    }
}
