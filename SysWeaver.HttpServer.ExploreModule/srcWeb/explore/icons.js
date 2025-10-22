

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
            const img = document.createElement("img")
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