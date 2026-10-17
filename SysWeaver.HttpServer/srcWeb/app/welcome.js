
/**
 * @file Script of the Welcome page (app/Welcome.html), the first main-menu item when no user is logged in.
 * Loaded with a static `<script defer>` tag by Welcome.html after ../common/common.js; started by
 * `<body onload="welcomeMain()">`. The page itself only shows the logo inside a `<div>`.
 *
 * Globals defined: `welcomeMain`.
 * Dependencies (common.js): `AddLoading`, `sendRequest`, `_TF`, `badClick`, `keyboardClick`, `Fail`.
 * Styles: welcome.css (`img.Click`, `p.KeyClick`).
 */

/**
 * Page entry point (body onload). Shows the loading overlay and probes `../Api/auth/GetUserSalt` with the
 * argument "dummy" purely to detect whether login is available. If the call returns a truthy result:
 * the logo `<img>` gets an onclick handler, a "Click to sign in" tool tip and class `Click`, and a `<p>` with
 * the same text is appended after it (made keyboard clickable with `keyboardClick`, which adds `KeyClick`,
 * and focused). Both open `../auth/Login.html` in `_self` (i.e. inside the shell's iframe the login page
 * replaces the welcome page). Errors are reported with Fail(e.message); the overlay is always removed.
 * @returns {Promise<void>}
 */
async function welcomeMain() {
    const onLoaded = AddLoading();
    try {
        //  Check if login is available
        if (await sendRequest("../Api/auth/GetUserSalt", "dummy")) {
            const e = document.body.getElementsByTagName("img")[0];
            const t = _TF("Click to sign in", "The tool tip description of a button that when clicked will open the login/sign in dialog");
            /**
             * Click handler shared by the logo and the sign-in paragraph; ignores non-primary clicks
             * (`badClick`) and opens ../auth/Login.html in the current frame.
             * @param {Event} ev - The click (or keyboard-click) event.
             * @returns {void}
             */
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
    }
    catch (e) {
        Fail(e.message);
    }
    finally {
        onLoaded();
    }
}