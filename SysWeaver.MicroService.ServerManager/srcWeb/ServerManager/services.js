/**
 * @file Script of the ServerManager "Managed services" page (services.html).
 * Loaded with a static `<script defer>` tag by services.html (after common.js, app/application.js, edit/Edit.js
 * and explore/table.js) and started from `<body onload="servicesMain()">`.
 * Shows an "add managed service" plus-button (when the user has tokens) and the table of managed services.
 *
 * Globals defined: `servicesMain` (no immediately executed code).
 *
 * Dependencies (globals from other files):
 * - common/common.js: `getRequest`, `sendRequest`, `ColorIcon`, `Fail`, `Info`, `PageLoaded`.
 * - app/application.js (+ edit/Edit.js): `PopUpEdit`.
 * - explore/table.js: `Table.addTable`.
 *
 * Conventions: `sendRequest("AddService", ...)` is page-relative, i.e. resolves to `ServerManager/AddService`.
 * Whether the user may add a service is enforced on the server only.
 */



/**
 * Entry point of services.html.
 * Fetches the current user (`GET ../Api/auth/GetUser`). If the user has a `Tokens` array it creates a
 * 64x64 `ColorIcon` plus-button inside `<ss-add>` (which gets the class `Show`). Clicking the button shows
 * `PopUpEdit("SysWeaver.MicroService.ManagedService", ...)`; if a value is returned it is sent with
 * `sendRequest("AddService", res)` and the outcome is reported with `Info` / `Fail`. The icon shows
 * `StartWorking` / `StopWorking` while busy.
 * Note: the role loop computes an `ok` flag by comparing the array itself (not the element) with
 * "debug" / "admin"; the flag is never used, so the button is shown whenever `Tokens` exists.
 * Finally renders `../ServerManager/ServicesTable` into `<ss-table>` and calls `PageLoaded()`.
 * @returns {Promise<void>} Resolves when the services table has been created.
 */
async function servicesMain() {

    /**
     * The current user from the auth service (null/undefined when not logged in).
     * @type {?{Tokens:?Array<string>}}
     */
    const user = await getRequest("../Api/auth/GetUser");
    if (user) {
        const ut = user.Tokens;
        if (ut) {
            let ok = false;
            const utl = ut.length;
            for (let i = 0; i < utl; ++i) {
                const t = ut[i];
                ok |= ut === "debug";
                ok |= ut === "admin";
                if (ok)
                    break;
            }
            const b = new ColorIcon("../icons/plus.svg", "IconColorThemeMain", 64, 64, "Click to add a new managed service", async () => {
                b.StartWorking();
                try {
                    const res = await PopUpEdit(
                        "SysWeaver.MicroService.ManagedService",
                        null,
                        "Add",
                        "../icons/plus.svg",
                        "Click to add this new service",
                        "Add new service",
                        "Enter a valid service name below and click add.\nThe name may only contain valid filename characters",
                        "Service name");
                    if (res) {
                        if (!await sendRequest("AddService", res)) {
                            Fail("Failed to create new service. ");
                            return;
                        }
                        Info("Create new managed service");
                    }
                }
                catch (e) {
                    Fail("Failed to create new service. " + e.message);
                }
                finally {
                    b.StopWorking();
                }
            });
            const ss = document.body.getElementsByTagName("ss-add")[0]
            ss.appendChild(b.Element);
            ss.classList.add("Show");

        }
    }


    await Table.addTable("../ServerManager/ServicesTable", document.body.getElementsByTagName("ss-table")[0]);
    PageLoaded();
}