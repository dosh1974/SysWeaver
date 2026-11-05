/**
 * @file services.js - the ServerManager "Managed services" page (services.html, entry `servicesMain`).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js, ../edit/Edit.js, ../explore/table.js and
 * smShared.js; started from `<body onload>`.
 *
 * A Design v2 fill page (Sw.PageLayout): the page header with an "Add service" primary action (for every signed in user, as the original page;
 * the server requires Roles.Admin for AddService) and the live table of managed services (SmShared.FillTable("../ServerManager/ServicesTable"); the row
 * links / actions are defined by the server: name -> serviceInfo.html?p=<name>, "Remove" -> RemoveService).
 *
 * Globals defined: `servicesMain`.
 * Library globals used: `Sw.Table` (explore/table.js, through SmShared.FillTable), `Edit`, `EditOptions` (edit/Edit.js, the object editor
 * used for the new service dialog).
 */

/**
 * Show the "Add new service" dialog: an object editor for SysWeaver.MicroService.ManagedService.
 * @returns {Promise<?object>} The new service, or null when cancelled.
 */
async function smEditNewService() {
    const typeInfo = await Sw.Net.Send(Sw.Url.Root + "edit/GetTypeInfo", "SysWeaver.MicroService.ManagedService");
    if (!typeInfo)
        throw new Error(_TF("Invalid typename!", "Error message displayed when a type with the given name wasn't found"));
    let editor = null;
    let result = null;
    const v = await Sw.Dialog.Show({
        Title: _TF("Add new service", "Title of a dialog that adds a managed service"),
        Wide: true,
        Content: async () => {
            const opt = new Sw.Edit.Options();
            opt.Title = false;
            opt.Border = false;
            editor = new Sw.Edit(typeInfo, undefined, opt);
            await editor.SetObject(null);
            return Sw.el("div.ss-edit", null,
                Sw.el("p.sw-dialog-text", { text: SmLines(
                    _TF("Enter a valid service name below and select Add.", "Help text of the add service dialog"),
                    _TF("The name may only contain valid filename characters", "Help text of the add service dialog")) }),
                editor.Element);
        },
        Buttons: [
            { Text: _TF("Cancel", "Button text that closes a dialog without doing anything"), Value: false },
            {
                Text: _TF("Add", "Button text that adds a new managed service"),
                Icon: "../icons/plus.svg",
                Title: _TF("Add this new service", "Tool tip of the button that adds a new managed service"),
                Kind: "primary",
                Value: true,
                OnClick: () => {
                    result = editor ? editor.GetObject() : null;
                },
            },
        ],
    });
    return v === true ? result : null;
}

/**
 * Join lines (translated literals may not contain line breaks).
 * @param {...string} lines - Lines.
 * @returns {string} The text.
 */
function SmLines(...lines) {
    return lines.join("\n");
}

/**
 * Entry point of services.html: a fill page (Design v2): the page header ("Managed services", what the page is for, the primary
 * action "Add service" for every signed in user, as the original page) and the live services table taking the rest of the height (SmShared.FillTable).
 * @returns {Promise<void>}
 */
function servicesMain() {
    return Sw.Page.Main(async () => {
        const page = Sw.PageLayout({
            Main: ".ss-page",
            Type: "fill",
            Compact: false,
            //  (the original page title "Services": S5-B-1; in the app shell the header shows the menu name "Managed services")
            Title: _TF("Services", "Title of the managed services page (the original page title)"),
            Description: _TF("The services this server manager installs, runs and updates. Select a service for its details, versions and configuration.", "Description of the managed services page"),
        });
        //  As the original page: every signed in user (a user with tokens) gets the button, the server checks the role of AddService
        //  (Admin; a refused request shows the error). One request: GET ../Api/auth/GetUser.
        const user = await Sw.Net.Get(Sw.Url.Root + "Api/auth/GetUser", { NoNavigate: true }).catch(() => null); /* SwWeb:allow ux-fbk-5 (no user: no Add button, as the original page) */
        if (user && user.Tokens) {
            page.Header.AddAction(new Sw.Button({
                Text: _TF("Add service", "Text of a button that adds a new managed service"),
                Icon: "plus",
                Kind: "primary",
                Title: _TF("Add a new managed service", "Tool tip of a button that adds a new managed service"),
                OnClick: async () => {
                    const res = await smEditNewService();
                    if (!res)
                        return;
                    if (!await Sw.Net.Send("AddService", res)) {
                        Sw.Fail(_TF("Failed to create new service.", "Error message when adding a managed service failed"));
                        return;
                    }
                    //  The live table shows the new service with its next refresh (as the original page)
                    Sw.Info(_TF("Created new managed service", "Message when a managed service was added"));
                },
            }));
        }
        await SmShared.FillTable(page, "../ServerManager/ServicesTable");
    }, { Loading: true });
}
