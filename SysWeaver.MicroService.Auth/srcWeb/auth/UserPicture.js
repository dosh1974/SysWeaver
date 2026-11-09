/**
 * @file auth/UserPicture.js - Sw.UserPicture: the picture of the signed in user, in one dialog.
 * - Generated pictures (AvatarService, "Avatars/[seed].svg"): a grid of choices and "More pictures"; the app stores the chosen seed
 *   (its own api, ex: QuizzWeaver UserSetImageSeed, Hypnos MemberSetImageSeed) through the SetSeed option; seed 0 = the default picture.
 * - Upload a picture (CustomUserImageService, upload repository "UserImage"; loads ../common/fileUploader.js on demand).
 * - Remove the uploaded picture (Api/RemoveUserImage): the generated picture shows again.
 * An uploaded picture wins over the generated one, so choosing a generated picture removes the uploaded one first.
 * After a change the InterOp message "UserImageChanged" is posted: the shell's user button and Sw.SetImageSource(..., { ReloadOn:
 * "UserImageChanged" }) images reload.
 * Usage: `if (await Sw.UserPicture.Choose({ SetSeed: seed => Sw.Net.Send("../myapp/SetImageSeed", seed) })) ...`
 * Options: SetSeed(seed) -> Promise (required for the generated pictures, without it that part is hidden), CanUpload (default true),
 * HasPhoto() -> Promise<boolean> (optional: the user has an uploaded photo; asked before a photo is removed), CanRemove (default true), Root (the site root url, default Sw.Url.Root), Count (pictures per page, default 12).
 * Returns a promise: true if the picture changed.
 */
(function () {
    "use strict";

    const scriptUrl = document.currentScript ? document.currentScript.src : document.baseURI;

    /** A random seed of a generated picture (never 0: that is the default picture). */
    function randomSeed() {
        const a = new Uint32Array(1);
        crypto.getRandomValues(a);
        return (a[0] & 0x7fffffff) || 1;
    }

    function el(tag, cls, text) {
        const e = document.createElement(tag);
        if (cls)
            e.className = cls;
        if (text)
            e.textContent = text;
        return e;
    }

    /**
     * Open the picture dialog.
     * @param {object} [o] Options (see the file header).
     * @returns {Promise<boolean>} True if the picture changed.
     */
    async function Choose(o) {
        o = o || {};
        const root = o.Root || Sw.Url.Root || new URL("../", scriptUrl).href;
        await Sw.Net.IncludeCss(scriptUrl, "UserPicture.css");
        const count = o.Count || 12;
        const canUpload = o.CanUpload !== false;
        const canRemove = o.CanRemove !== false;
        let changed = false;
        let selected = null;
        let dlg = null;

        const body = el("div", "sw-userpic");
        const current = el("img", "sw-userpic-current");
        current.alt = _T("Your picture now", "Alternative text of the current user picture in the picture dialog");
        const showCurrent = () => current.src = root + "auth/UserImages/Current/large?t=" + Date.now();
        showCurrent();
        const head = el("div", "sw-userpic-head");
        const headText = el("p", "sw-dialog-text", _T("Choose a generated picture, or upload a photo of your own. Other users see this picture next to your name.", "Help text at the top of the user picture dialog"));
        head.append(current, headText);
        body.appendChild(head);

        const changedNow = () => {
            changed = true;
            showCurrent();
            Sw.InterOp.Post("UserImageChanged");
        };

        let useButton = null;
        if (o.SetSeed) {
            body.appendChild(el("h3", "sw-userpic-title", _T("Generated pictures", "Heading of the generated pictures in the user picture dialog")));
            const grid = el("div", "sw-userpic-grid");
            grid.setAttribute("role", "radiogroup");
            grid.setAttribute("aria-label", _T("Generated pictures", "Accessible name of the grid of generated pictures"));
            const fill = () => {
                grid.replaceChildren();
                selected = null;
                if (useButton)
                    useButton.SetDisabled(true, _T("Select a picture first", "Reason why the Use this picture button is disabled"));
                for (let i = 0; i < count; ++i) {
                    const seed = randomSeed();
                    const b = el("button", "sw-userpic-pick");
                    b.type = "button";
                    b.setAttribute("role", "radio");
                    b.setAttribute("aria-checked", "false");
                    b.setAttribute("aria-label", _T("Picture {0}", i + 1, "Accessible name of a generated picture in the user picture dialog, {0} is its number"));
                    const img = el("img");
                    img.alt = "";
                    img.loading = "lazy";
                    img.src = root + "Avatars/" + seed + ".svg";
                    b.appendChild(img);
                    b.addEventListener("click", () => {
                        for (const x of grid.children)
                            x.setAttribute("aria-checked", "false");
                        b.setAttribute("aria-checked", "true");
                        selected = seed;
                        if (useButton)
                            useButton.SetDisabled(false);
                    });
                    grid.appendChild(b);
                }
            };
            body.appendChild(grid);
            const row = el("div", "sw-userpic-row");
            row.appendChild(new Sw.Button({
                Text: _T("More pictures", "Button that shows other generated pictures"),
                Icon: "IconReload",
                Kind: "ghost",
                OnClick: () => fill(),
            }).E);
            row.appendChild(new Sw.Button({
                Text: _T("Use the default picture", "Button that goes back to the first generated picture of the user"),
                Kind: "ghost",
                OnClick: async () => {
                    if (canRemove) {
                        if (!await confirmRemove(true))
                            return;
                        await removeUpload(true);
                    }
                    await o.SetSeed(0);
                    changedNow();
                    Sw.Toast(_T("You have your default picture again.", "Toast after choosing the default user picture"), { Kind: "ok" });
                },
            }).E);
            body.appendChild(row);
            useButton = new Sw.Button({
                Text: _T("Use this picture", "Button that sets the selected generated picture as the user picture"),
                Kind: "primary",
                Disabled: _T("Select a picture first", "Reason why the Use this picture button is disabled"),
                OnClick: async () => {
                    if (selected === null)
                        return;
                    if (canRemove) {
                        if (!await confirmRemove(true))
                            return;
                        await removeUpload(true);
                    }
                    await o.SetSeed(selected);
                    changedNow();
                    Sw.Toast(_T("Your picture is changed.", "Toast after choosing a generated user picture"), { Kind: "ok" });
                },
            });
            row.appendChild(useButton.E);
            fill();
        }

        /** True if the user may have an uploaded photo (no HasPhoto option: assume so). */
        const hasPhoto = async () => o.HasPhoto ? !!(await o.HasPhoto()) : true;

        /** Ask before a photo is removed (destructive: the photo is gone). False = keep it. */
        const confirmRemove = async generated => (!await hasPhoto()) || await Sw.Dialog.Confirm(
            _T("Remove your photo?", "Title of the question before the uploaded user picture is removed"),
            generated ? _T("A generated picture replaces your uploaded photo. The photo is deleted.", "Text of the question before a generated picture replaces the uploaded user picture") : _T("Your uploaded photo is deleted and your generated picture shows again.", "Text of the question before the uploaded user picture is removed"),
            _T("Remove photo", "Button that confirms removing the uploaded user picture"), { Danger: true });

        /** Remove the uploaded picture (quiet: no toast, a missing picture is no error). */
        async function removeUpload(quiet) {
            try {
                const r = await Sw.Net.Send(root + "Api/RemoveUserImage", null, { Reload: true });
                if (!quiet) {
                    changedNow();
                    Sw.Toast(r ? _T("Your uploaded picture is removed.", "Toast after removing the uploaded user picture") : _T("You have no uploaded picture.", "Toast when there was no uploaded user picture to remove"), { Kind: r ? "ok" : "info" });
                }
                return r;
            }
            catch (e) {
                if (!quiet)
                    throw e;
                return false;
            }
        }

        const buttons = [];
        if (canUpload)
            buttons.push({
                Text: _T("Upload a photo…", "Button that uploads a photo as the user picture"),
                Icon: "IconUpload",
                Close: false,
                OnClick: async () => {
                    await Sw.Net.IncludeJs(root + "common/fileUploader.js");
                    const files = await Sw.Upload.Pick({ Accept: "image/png,image/jpeg,image/webp,image/avif,image/tiff" });
                    if (!files)
                        return false;
                    const r = await Sw.Upload.Files("UserImage", files, { ApiBase: root + "upload/" });
                    const st = r && r.Status ? r.Status[0] : -1;
                    if (r.Error || (st !== 0 && st !== 1)) {
                        Sw.Fail(r.Error ? r.Error : Sw.Upload.StatusText(st));
                        return false;
                    }
                    changedNow();
                    Sw.Toast(_T("Your photo is your picture now.", "Toast after uploading a user picture"), { Kind: "ok" });
                    return false;
                },
            });
        if (canRemove)
            buttons.push({
                Text: _T("Remove the photo", "Button that removes the uploaded user picture"),
                Kind: "danger",
                Close: false,
                OnClick: async () => {
                    if (await confirmRemove(false))
                        await removeUpload(false);
                    return false;
                },
            });
        buttons.push({ Text: _T("Close", "Button that closes the user picture dialog"), Value: null });

        dlg = Sw.Dialog.Open({
            Title: _T("Your picture", "Title of the user picture dialog"),
            Content: body,
            Buttons: buttons,
            Id: "sw-userpic",
            Wide: true,
        });
        await dlg.Result;
        return changed;
    }

    Sw.UserPicture = { Choose };
})();
