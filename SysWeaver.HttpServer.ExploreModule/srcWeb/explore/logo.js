
/**
 * @file logo.js - page script of explore/logo.html (logo seed designer).
 *
 * Loaded with `defer` by logo.html after common/common.js; the page calls `logoMain()` from
 * `<body onload="logoMain()">`. Defines only the global `logoMain`; nothing runs at load time.
 *
 * Dependencies (from common/common.js): AddLoading, Fail, getRequest, createImg, PendingImages,
 * Button (incl. Button.CreateRow), ValueFormat.copyToClipboardInfo, badClick, _TF.
 * Server: `../Api/debug/explore/GetAppSeed` (ExploreModule, Roles.Debug) returns the current app seed
 * (int); preview images are loaded from `../logo_debug.svg?<seed>`.
 * Styled by logo.css (SysWeaver-IconList / SysWeaver-IconItem / SysWeaver-IconSeed, .IconDice).
 */

/**
 * Entry point of logo.html: shows the current logo seed plus 9 random seeds as preview tiles.
 *
 * Gets the current seed from `../Api/debug/explore/GetAppSeed` (kept as string `selected`), appends a
 * `Button.CreateRow()` row with a "Randomize" `Button` (icon class `IconDice`, calls `Create`) and a
 * `SysWeaver-IconList` to `document.body`, renders the tiles with `Create()` and waits for all images.
 * Shows a loading indicator until done; errors are shown with `Fail(e, true)`.
 * @returns {Promise<void>} Resolves when the first set of preview images has loaded.
 */
async function logoMain() {

    const removeLoading = AddLoading();
    try {
        const pending = new PendingImages();

        const target = document.body;
        const list = document.createElement("SysWeaver-IconList");
        const count = 10;
        let selected = "" + await getRequest("../Api/debug/explore/GetAppSeed");

        /**
         * (Re)builds the tile list: clears `list` and adds `count` (10) `SysWeaver-IconItem`s.
         * Item 0 shows the `selected` seed, items 1..9 a random 31-bit seed. Each item holds an `img`
         * loaded from `../logo_debug.svg?<seed>` (through the shared `PendingImages`) and a
         * `SysWeaver-IconSeed` caption with the seed.
         * Item click (ignored when `badClick`): sets `selected` to the item's seed, copies it with
         * `ValueFormat.copyToClipboardInfo`, and for items other than the first swaps image src and caption
         * with the first item (so the chosen seed becomes the highlighted first tile).
         * Also used as the click callback of the "Randomize" button.
         * @returns {void}
         */
        function Create() {
            list.innerText = "";
            for (let i = 0; i < count; ++i) {
                const num = i;
                let seed = i === 0 ? selected : ("" + (Math.floor(Math.random() * 2147483647) | 0));
                const item = document.createElement("SysWeaver-IconItem")
                const img = createImg("img")
                img.draggable = false;
                pending.Start(img, "../logo_debug.svg?" + seed);
                item.appendChild(img);
                const text = document.createElement("SysWeaver-IconSeed");
                text.innerText = seed
                item.appendChild(text);
                list.appendChild(item);
                item.onclick = async ev => {
                    if (badClick(ev))
                        return;
                    const val = text.innerText;
                    selected = val;
                    await ValueFormat.copyToClipboardInfo(val);
                    if (num !== 0) {
                        const fimg = list.firstElementChild.firstElementChild;
                        const ftext = fimg.nextElementSibling;

                        const oimg = fimg.src;
                        const otext = ftext.innerText;

                        fimg.src = img.src;
                        ftext.innerText = val;

                        img.src = oimg;
                        text.innerText = otext;
                    }
                };

            }
        }
        const bs = Button.CreateRow();
        const r = new Button(null,
            _TF("Randomize", "The text of a button that when clicked will show 10 new random logos"),
            _TF("Click to generate new icons", "The tool tip description on a button that when clicked will show 10 new random logos"),
            "IconDice", true, Create);
        bs.appendChild(r.Element);
        target.appendChild(bs);
        target.appendChild(list);
        Create();
        await pending.WaitAll();
    }
    catch (e) {
        Fail(e, true);
    }
    finally {
        removeLoading();
    }
}