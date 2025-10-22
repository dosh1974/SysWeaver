
async function logoMain() {

    const removeLoading = AddLoading();
    try {
        const pending = new PendingImages();

        const target = document.body;
        const list = document.createElement("SysWeaver-IconList");
        const count = 10;
        let selected = "" + await getRequest("../Api/debug/explore/GetAppSeed");

        function Create() {
            list.innerText = "";
            for (let i = 0; i < count; ++i) {
                const num = i;
                let seed = i === 0 ? selected : ("" + (Math.floor(Math.random() * 2147483647) | 0));
                const item = document.createElement("SysWeaver-IconItem")
                const img = document.createElement("img")
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