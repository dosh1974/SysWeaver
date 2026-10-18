

/**
 * @file icons.js - page script of explore/icons.html (icon gallery).
 *
 * Loaded with `defer` by icons.html after common/common.js; the page calls `iconsMain()` from
 * `<body onload="iconsMain()">`. Defines only the global `iconsMain`; nothing runs at load time.
 *
 * Dependencies (from common/common.js): AddLoading, Fail, getUrlParams, getRequest, sendRequest,
 * createImg, PendingImages, ValueFormat.copyOnClick, _T.
 * Server: `../Api/debug/explore/GetIcons` (ExploreModule, Roles.Dev) returning IconInfo[]
 * (`{Url:string, Name:string, Size:number, Location:string}`).
 * Styled by icons.css (SysWeaver-IconList / SysWeaver-IconItem / SysWeaver-IconName).
 */

/**
 * Entry point of icons.html: fetches the icon list and renders one tile per icon.
 *
 * Reads the optional URL parameter `path`; when set, POSTs `{Param: path}` (a TableDataRequest) to
 * `../Api/debug/explore/GetIcons`, otherwise GETs it (server default folder `icons/`).
 * Appends a `SysWeaver-IconList` to `document.body` with one `SysWeaver-IconItem` per icon, holding a
 * non-draggable `img` loaded via `PendingImages.Start(img, "../" + Url)` and a `SysWeaver-IconName`
 * caption. Clicking a tile copies the relative `Url` to the clipboard (`ValueFormat.copyOnClick`);
 * the tooltip shows the Url, the size in bytes and the server `Location`.
 * Shows a loading indicator (AddLoading) until all images are loaded; errors are shown with `Fail(e, true)`.
 * @returns {Promise<void>} Resolves when all icon images have loaded (or failed).
 */
async function iconsMain() {

    const removeLoading = AddLoading();
    try {
        const pending = new PendingImages();
        const target = document.body;   
        const ps = getUrlParams();
        const url = ps.get('path');

        const icons = url ?
            await sendRequest("../Api/debug/explore/GetIcons",
                {
                    Param: url,
                })
            :
                await getRequest("../Api/debug/explore/GetIcons")
            ;
        const count = icons.length;
        const list = document.createElement("SysWeaver-IconList");
        for (let i = 0; i < count; ++i) {
            const icon = icons[i];
            const item = document.createElement("SysWeaver-IconItem")
            const img = createImg("img")
            const relName = icon.Url;
            ValueFormat.copyOnClick(item, relName, true);
            item.title = relName + '\n' + 
                _T('Size: {0} bytes.', icon.Size, "A tool tip describing the size of a file in bytes.{0} is replaced with the numeric size") +
                '\n\n' + icon.Location + '\n\n' +
                _T('Click to copy "{0}" to the clipboard.', relName, "A tool tip describing an item that when clicked will copy some text to the clipboard.{0} is replaced with the text that will be copied on click");

            img.draggable = false;
            pending.Start(img, "../" + relName);
            item.appendChild(img);

            const name = document.createElement("SysWeaver-IconName");
            name.innerText = icon.Name;
            item.appendChild(name);

            list.appendChild(item);
        }
        target.appendChild(list);
        await pending.WaitAll();
    }
    catch (e) {
        Fail(e, true);
    }
    finally {
        removeLoading();
    }
}