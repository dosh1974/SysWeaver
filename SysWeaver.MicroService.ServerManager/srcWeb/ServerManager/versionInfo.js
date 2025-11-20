
async function versionInfoMain() {
    const p = getUrlParams();
    const version = p.get("p");
    if (!version) {
        Fail("No version paramater supplied!");
        return;
    }
    if (window.IsTop)
        document.body.classList.add("TopWindow");

    document.title = "Version - " + version;
    document.body.getElementsByTagName("si-name")[0].innerText = version;
    const abortWait = new AbortHandler();

    let data = null;
    function update(first) {

        if (first)
            PageLoaded();
    }
    for (; ;) {
        try {
            const noOld = !data;
            data = await sendRequest("GetVersion", version);
            update(noOld && data);
        }
        catch (e)
        {
            Fail(e);
        }
        await delayWithAbort(5000, abortWait);
    }


}