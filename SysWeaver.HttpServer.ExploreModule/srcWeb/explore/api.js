







async function apiMain() {

    const removeLoading = AddLoading();
    try {

        const ps = getUrlParams();
        const url = ps.get('q');
        if (!url) {
            Fail(_TF("No query parameter specified!", "An error message that is shown when a required parameter isn't present"), true);
            return;
        }
        document.title = _TF("API", "The page title prefix, is followed by the url to the API") + ": " + url;

        let data;
        try {
            data = await sendRequest('../Api/debug/GetApiInfo', url, false);
        }
        catch (e) {
            Fail(_T('Failed to get information about the api "{0}".\n{1}', url, e, "An error message that is shown when the server failed to get information about an API.{0} is replaced with the url of the API.{1} is replaced with the java script excetion text"), true);
            return;
        }
        if (!data) {
            Fail(_T('Failed to get information about the api "{0}"', url, "An error message that is shown when the server failed to get information about an API.{0} is replaced with the url of the API."), true);
            return;
        }

        const key = "SysWeaver.Api." + url;


        const target = document.createElement("SysWeaver-Page");
        document.body.appendChild(target);

        const haveArg = !!data.Arg;

        let section = document.createElement("SysWeaver-ApiSection");
        target.appendChild(section);
        let tag = document.createElement("SysWeaver-ApiTag");
        const method = haveArg ? "POST" : "GET";
        tag.innerText = method;
        tag.title = _TF("All API's can be accessed using GET or POST.\nWhen an API takes argument and GET is used, the argument json is appended directly after the query paramaters separator, ex: api?json\n", "A tool tip description of how to access a REST API end point") + "\n\n" + ValueFormat.copyOnClick(tag, method);
        section.appendChild(tag);
        tag = document.createElement("SysWeaver-ApiUrl");
        section.appendChild(tag);
        tag.innerText = data.Uri;
        let fullUri = window.location.href.split('?')[0].split('/');
        fullUri.splice(fullUri.length - 2, 2);
        fullUri = fullUri.join('/') + "/" + data.Uri;
        tag.title = _TF("This is the path relative to the site's base address.\nThe full path is:", "A tool tip description of the relative URL to an API end point") + "\n\"" + fullUri + "\"\n\n" + ValueFormat.copyOnClick(tag, fullUri, true);
        const auth = data.Auth;
        if (auth != null) {
            tag = new ColorIcon("IconSecured", "IconColorThemeMain", 24, 24,
                _TF("Only authenticated users can use this API.", "A tool tip description on an icon that explains that only autheticated users may access this API")
            );
            section.appendChild(tag.Element);
            if (auth) {
                const tokens = auth.split(',');
                const tl = tokens.length;
                for (let i = 0; i < tl; ++i) {
                    tag = document.createElement("SysWeaver-ApiTag");
                    const tagName = tokens[i].trim().toUpperCase();
                    tag.innerText = tagName;
                    tag.title = _T("The user must have the \"{0}\" security token to use this API.", tagName, "A tool tip description on a security token that is required to use this API") + "\n\n" + ValueFormat.copyOnClick(tag, tagName);
                    section.appendChild(tag);
                }
            }
        }
        const desc = data.Desc;
        if (desc) {
            section = document.createElement("SysWeaver-ApiSection");
            target.appendChild(section);
            tag = document.createElement("SysWeaver-ApiDesc");
            tag.title = ValueFormat.copyOnClick(tag, desc);
            tag.innerText = desc;
            section.appendChild(tag);
        }
        const cd = data.ClientCacheDuration;
        const rd = data.RequestCacheDuration;
        const cp = data.CompPreference ?? "";
        if ((cd > 0) || (rd > 0) || (cp.length > 0)) {
            section = document.createElement("SysWeaver-ApiSection");
            target.appendChild(section);
            if (cd > 0) {
                tag = document.createElement("SysWeaver-ApiTag");
                tag.innerText = _T("CLIENT {0}s", cd, "The text on a tag that explains that the API response will instruct the web client to cache the result for the specified duration.{0} is replaced with the number of seconds that the result should be cached");
                tag.title = (cd === 1
                    ?
                    _TF("Client's are instructed to cache the response of the API call for one second.", "A tool tip description on a tag explains that the API response will instruct the web client to cache the result for one second")
                    :
                    _T("Client's are instructed to cache the response of the API call for {0} seconds.", cd, "A tool tip description on a tag explains that the API response will instruct the web client to cache the result for the specified duration.{0} is replaced with the number of seconds that the result should be cached and will never be 1")
                    ) + "\n\n" + ValueFormat.copyOnClick(tag, cd);
                section.appendChild(tag);
            }
            if (rd > 0) {
                tag = document.createElement("SysWeaver-ApiTag");
                tag.innerText = _T("SERVER {0}s", cd, "The text on a tag that explains that the API response will be cached for the specified duration.{0} is replaced with the number of seconds that the result should be cached");
                tag.title = (cd === 1
                    ?
                    _TF("The server will cache the response of the API call for one second.", "A tool tip description on a tag that explains that the API response will be cached on the server one second")
                    :
                    _T("The server will cache the response of the API call for {0} seconds.", cd, "A tool tip description on a tag that explains that the API response will cached on the server for the specified duration.{0} is replaced with the number of seconds that the result should be cached and will never be 1")
                ) + "\n\n" + ValueFormat.copyOnClick(tag, cd);
                section.appendChild(tag);
            }
            if (cp.length > 0) {
                const methods = cp.split(',');
                const ml = methods.length;
                for (let i = 0; i < ml; ++i) {
                    const kv = methods[i].split(':');
                    const method = kv[0].trim();
                    const effort = kv[1].trim();

                    tag = document.createElement("SysWeaver-ApiTag");
                    tag.classList.add("right");
                    const text = method.toUpperCase();
                    tag.innerText = text;
                    tag.title =
                        _T("The server compress the repsonse using {0}-compression if the client accept it.", text, "A tool tip description on a tag that explains the compression method used on an API.{0} is repladced with the compression method name (Brotli, Deflate, Gzip)") +
                        "\n" + 
                        _T("The compression quality used is: {0}.", effort, "A tool tip description on a tag that explains the compression quality used on an API.{0} is repladced with the compression quality: fast, normal, best etc") +
                        "\n" +
                        _TF("If the client accepts multiple compression methods, the first supported method will be used.", "A tool tip description on a tag that explains that the first client supported compression method is used") + 
                        "\n\n" + ValueFormat.copyOnClick(tag, method + ": " + effort);
                    section.appendChild(tag);
                    didAdd = true;
                }

            }

        }

        //  Signature
        section = document.createElement("SysWeaver-ApiSection");
        section.classList.add("ApiSignature");
        section.title = _TF("C# function signature", "A tool tip description of block of code that contains a C# function signature");
        target.appendChild(section);
        const isRaw = !!data.Mime;
        if (data.Return) {
            if (isRaw) {
                const t = ValueFormat.createLink(
                    'https://www.google.com/search?q=Information about the "' + data.Mime + '" mime type',
                    "[" + data.Mime.split(';')[0] + "]",
                    "_blank",
                    _T('This API returns raw data in the "{0}" format.', data.Mime, "A tool tip description on a tag that explains that a API returns data using the specified MIME type.{0} is replaced with the MIME type") +
                    "\n\n" +
                    _TF("Click to search google for information about this mime type.", "A tool tip description on a button that when pressed does a google search for a MIME type"));
                section.appendChild(t);
            } else {
                AddTypeName(section, data.Return.TypeName, true);
            }
        } else {
            AddTextElement(section, "void");
        }
        const uparts = url.split('/');
        AddTextElement(section, uparts[uparts.length - 1].split('.')[0], url + ((data.Return && data.Return.Summary) ? ("\n" + _TF("Returns", "A prefix to a tool tip description of what an API returns") + ": " + data.Return.Summary) : ""), "Signature"); 
        AddTextElement(section, "(", null, "Control");
        if (haveArg) {
            AddTypeName(section, data.Arg.TypeName, true);
            AddTextElement(section, data.ArgName, data.ArgSummary, "Signature");
        }
        AddTextElement(section, ");", null, "Control");

        const button = document.createElement("SysWeaver-ApiSendButton");
        //  Input
        let results = null;
        function RemoveResults() {
            if (!results)
                return;
            let e = results.Element;
            for (; ;) {
                const s = button.nextElementSibling;
                if (!s)
                    break;
                s.remove();
            }
            e.remove();
            results = null;
        }

        let input = null;
        if (haveArg) {

            const tab = new Tab("Api.Args");
            target.appendChild(tab.Element);

            const tc0 = document.createElement("SysWeaver-TabContent");

            let obj = await Edit.CreateDefault(data.Arg, null, true);
            const local = localStorage.getItem(key);
            if (local)
            {
                try {
                    if (Edit.IsPrimitive(data.Arg)) {
                        obj = JSON.parse(local);
                    } else {
                        const lobj = JSON.parse(local);
                        Object.assign(obj, lobj);
                    }
                    await Edit.CleanUp(data.Arg, obj);
                }
                catch (e) {
                    console.warn("Failed to parse previous object, error: " + e);
                }
            }   
            input = new Edit(data.Arg);
            await input.SetObject(obj);
            input.Element.addEventListener("EditChange", ev => {
                RemoveResults();

                let mp = ev.detail.Member;
                let p = mp ? mp.Name : "*";
                let val = ev.detail.Value;
                for (; ;) {
                    ev = ev.detail.Next;
                    if (!ev)
                        break;
                    mp = ev.detail.Member;
                    const key = ev.detail.KeyName;
                    if (key)
                        p += key;
                    p = p + "." + (mp ? mp.Name : "*");
                    val = ev.detail.Value;
                }
                /*if (typeof val === "object")
                    console.log(p + " = " + JSON.stringify(val));
                else
                    console.log(p + " = " + val);*/

            });
            tc0.appendChild(input.Element);
            target.appendChild(tc0);
            tab.AddTab(
                _TF("Parameters", "The text of a tab header that will display an explaination of all paramaters for an API"),
                null,
                _TF("Click to edit parameters for this API", "The tool tip description of a clickable tab header that when clicked will display an explaination of all paramaters for an API")
                , null, tc0,
                _TF("Parameters for this API", "The tool tip description of a tab header that is displaying an explaination of all paramaters for an API")
            );

            const tc1 = document.createElement("SysWeaver-TabContent");
            tc1.classList.add("CODE");
            tc1.title = _TF("Click to copy to clipboard.", "A tool tip description on a block of text containing a JSON object that when clicked will copy the json text to the clipboard");
            tc1.onclick = async ev => {
                if (!isPureClick(ev))
                    return;
                await ValueFormat.copyToClipboardInfo(tc1.textContent, true);
            };
            target.appendChild(tc1);
            tab.AddTab(
                _TF("JSON", "The text of a tab header that will display all parameters of an API as formatted JSON text"),
                header => {
                    try {

                        const o = input.GetObject();
                        const json = JSON.stringify(o, null, 4);
                        tc1.classList.add("SysWeaverCode");
                        tc1.innerHTML = ValueFormat.jsonToHtml(json);
                    }
                    catch (e) {
                        console.warn("Failed to set json! Error: " + e);
                    }
                },
                _TF("Click to show the json for the parameters", "The tool tip description of a clickable tab header that when clicked will display all parameters of an API as formatted JSON text"),
                null, tc1,
                _TF("Parameters as JSON", "The tool tip description of a tab header that is displaying all parameters of an API as formatted JSON text")
            );
        }


        const abutton = new Button(null,
            _TF("Send API call", "The text of a button that when clicked will make a request to and API"),
            _TF("Click to call this API and display any results below.", "The tool tip text of a button that when clicked will make a request to and API"),
            "IconSendIt", true, async ev => {
            abutton.StartWorking();
            try {
                RemoveResults();
                let res = null;
                let error = null;
                try {
                    if (haveArg) {
                        const obj = input.GetObject();
                        localStorage.setItem(key, JSON.stringify(obj));
                        res = await sendRequest(fullUri, obj, true, null, isRaw);
                    } else {
                        res = await getRequest(fullUri, true, isRaw);
                    }
                }
                catch (e) {
                    error = e;
                }
                results = new Tab("Api.Results");
                target.appendChild(results.Element);



                const tc0 = document.createElement("SysWeaver-TabContent");
                tc0.classList.add("CODE");
                tc0.title = _TF("Click to copy to clipboard.", "A tool tip description on a block of text containing a JSON object that when clicked will copy the json text to the clipboard");
                tc0.onclick = async ev => {
                    if (!isPureClick(ev))
                        return;
                    await ValueFormat.copyToClipboardInfo(tc0.textContent, true);
                };
                const titleJson = _TF("JSON", "The text of a tab header that will display the result of an API call as formatted JSON text");
                let title = titleJson;
                if (error != null) {
                    tc0.innerText = _TF("Error", "The prefix of a server side error as the result of an API call") + ":\n" + error;
                    tc0.classList.add("Error");
                    title = _TF("ERROR", "The text of a tab header that will display the server error as a result of an API call");
                } else {
                    if (isRaw) {
                        title = _TF("PREVIEW", "The text of a tab header that will display the API result of a specific MIME type (as an embedded iframe)");
                        const url = URL.createObjectURL(res);
                        const frm = document.createElement("iframe");
                        tc0.appendChild(frm);
                        frm.src = url;
                    } else {
                        if ((res == null) && (!data.Return)) {
                            tc0.classList.add("NoResult");
                            tc0.innerText = _TF("The API doesn't return a result.", "The text to display when a successful call to an API that doesn't return any result was made");
                            title = _TF("VOID", "The text of a tab header that will is the result of a successful call to an API that doesn't return any result");
                        } else {

                            const json = JSON.stringify(res, null, 4);
                            tc0.classList.add("SysWeaverCode");
                            tc0.innerHTML = ValueFormat.jsonToHtml(json);
                        }
                    }
                }
                if (title === titleJson) {
                    const opt = new EditOptions();
                    opt.ReadOnly = true;
                    const output = new Edit(data.Return, null, opt);
                    //const output = new Edit(retType, null, opt);
                    await output.SetObject(res);

                    const tc1 = document.createElement("SysWeaver-TabContent");
                    tc1.appendChild(output.Element);
                    target.appendChild(tc1);
                    results.AddTab(
                        _TF("RETURN", "The text of a tab header that will display the detailed result of an API call"),
                        tc1);
                }
                target.appendChild(tc0);
                results.AddTab(title, tc0);
                results.Select();
            }
            catch (e) {
                Fail(e);
            }
            finally {
                abutton.StopWorking();
            }
        });
        button.appendChild(abutton.Element);
        target.appendChild(button);
    }
    catch (e) {
        Fail(e, true);
    }
    finally {
        removeLoading();
    }

}
