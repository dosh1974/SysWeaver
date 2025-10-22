
async function welcomeMain() {
    const onLoaded = AddLoading();

    //  Check if login is available
    if (await sendRequest("../Api/auth/GetUserSalt", "dummy")) {
        const e = document.body.getElementsByTagName("img")[0];
        const t = _TF("Click to sign in", "The tool tip description of a button that when clicked will open the login/sign in dialog");
        const fn = ev => {
            if (badClick(ev))
                return;
            open("../auth/Login.html", "_self");
        };
        e.onclick = fn;
        e.title = t;
        e.classList.add("Click");

        const p = document.createElement("p");
        p.innerText = t;
        p.onclick = fn;
        keyboardClick(p);

        e.parentElement.appendChild(p);

        p.focus();

    }
    onLoaded();
}