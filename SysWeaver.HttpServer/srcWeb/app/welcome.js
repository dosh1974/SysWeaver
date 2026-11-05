/**
 * @file Script of the Welcome page (app/Welcome.html), the first main-menu item when no user is signed in.
 * Loaded with a static `<script defer>` tag after ../sw/core.js and ../sw/ui.js; started by
 * `<body onload="welcomeMain()">`. The page shows the app logo (logo.svg) and the application name (the shell's
 * document title, else stand-alone the name in the templated app/manifest.json via `Sw.Page.AppName()`; Welcome.html is
 * not a server template; stand-alone the tab title is "<app name> - Welcome"); when signing in is possible a short lead text and a large
 * primary "Sign in" button are added, and the logo becomes clickable too.
 *
 * Globals defined: `welcomeMain`, `welcomeSignIn`, `welcomeAppName`.
 * Dependencies: Sw core / ui (`Sw.Page`, `Sw.Net`, `Sw.Url`, `Sw.Fail`, `Sw.el`, `Sw.Button`, `Sw.BadClick`).
 * Navigation: `Sw.Page.Navigate("../auth/Login.html", "_self")`, so the shell gets `IframeNavigating` (history entry,
 * deep link hash) before the content frame navigates.
 */
"use strict";

/**
 * Open the sign in page in this frame (through the shell protocol).
 * @param {Event} [ev] - The click event (clicks with modifier keys are ignored).
 * @returns {void}
 */
function welcomeSignIn(ev) {
    if (Sw.BadClick(ev))
        return;
    Sw.Page.Navigate("../auth/Login.html", "_self");
}

/**
 * The application name: the title of the shell (the templated index.html title, kept as `SysWeaverAppTitle`) when
 * this page is shown in a same origin shell, else null.
 * @returns {string|null} The name.
 */
function welcomeAppName() {
    try {
        const t = window.top.SysWeaverAppTitle;
        return (typeof t === "string" && t.trim()) ? t.replace(/\s+/g, " ").trim() : null;
    }
    catch {
        return null;
    }
}

/**
 * Page entry point (body onload): shows the loader, shows the application name and probes
 * `Api/auth/GetUserSalt("dummy")` to detect whether signing in is available (a missing auth service, 404, means no
 * and is not an error). If it is: the logo gets a click handler, a lead text and a large primary "Sign in" button are
 * added below the name and the button is focused.
 * @returns {Promise<void>}
 */
async function welcomeMain() {
    const done = Sw.Page.Loading();
    let button = null;
    //  The shell title, else (stand-alone, or the shell not ready yet) the name in the templated app/manifest.json
    const name = welcomeAppName() || await Sw.Page.AppName();
    const h = document.querySelector("h1.AppName");
    if (h) {
        if (name)
            h.textContent = name;
        else
            h.remove();
    }
    //  Stand-alone (a bookmark, the PWA): the tab names the app too
    if (name && !Sw.Page.IsEmbedded)
        document.title = name + " - " + _TF("Welcome", "Title of the welcome page shown before signing in (browser tab, after the application name)");
    try {
        let canSignIn = false;
        try {
            canSignIn = !!await Sw.Net.Send(Sw.Url.Root + "Api/auth/GetUserSalt", "dummy");
        }
        catch (e) {
            if (e.Status !== 404)
                throw e;
        }
        if (!canSignIn)
            return;
        const tip = _TF("Sign in", "The tool tip description of a button that when clicked will open the login/sign in dialog");
        const logo = document.querySelector("img.Logo");
        logo.title = tip;
        logo.classList.add("Click");
        logo.addEventListener("click", welcomeSignIn);
        const lead = Sw.el("p.Lead", { text: _TF("Sign in to continue.", "Text on the welcome page above the sign in button") });
        const b = new Sw.Button({
            Text: _TF("Sign in", "Text of the large button on the welcome page that opens the sign in page"),
            Icon: "login", Kind: "primary", Title: tip, Class: "SignIn",
            OnClick: (x, ev) => welcomeSignIn(ev),
        });
        button = b.E;
        (h && h.isConnected ? h : logo).after(lead, button);
    }
    catch (e) {
        Sw.Fail(e.message || String(e));
    }
    finally {
        done();
    }
    //  Focus after the loader released the focus trap (Enter signs in; no focus ring until the keyboard is used: a ring on load looked like an error)
    if (button)
        button.focus({ focusVisible: false });
}