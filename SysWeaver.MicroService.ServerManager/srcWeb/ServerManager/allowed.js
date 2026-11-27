/**
 * @file allowed.js - the ServerManager "Allowed addresses", "Allowed ports" and "Allowed countries" pages (allowed.html?k=ip|port|country,
 * entry `allowedMain`; menu items restricted to Roles.AdminOps).
 * Classic script loaded with `<script defer>` after ../sw/core.js, ../sw/ui.js, ../explore/table.js and smShared.js; started from `<body onload>`.
 *
 * A Design v2 fill page (Sw.PageLayout): the page header, an add box (a text box and an "Allow" button: AllowIp / AllowPort / AllowCountry,
 * hidden for users without Admin by Sw.Access.Guard) and the live table of the allowed values (SmShared.FillTable: AllowedIpsTable,
 * AllowedPortsTable or AllowedCountriesTable; its Remove row action is defined by C#), refreshed after a value was allowed.
 * The values that come from the service parameters are listed too, they can only be removed by changing the parameters.
 *
 * Globals defined: `allowedMain`.
 */

/**
 * What the page shows for each kind (url parameter "k").
 * Value(text) returns the value to send, or null when the text is invalid (the server checks again and explains).
 */
const AllowedKinds = {
    ip: {
        Title: () => _TF("Allowed addresses", "Title of the page that lists the ip addresses that are never flagged as intruders"),
        Description: () => _TF("Connections with these addresses or networks are never flagged as an intruder. Allow an address here, or from an intruder alert on the problems page or a flagged row of the external connections.", "Description of the allowed addresses page"),
        Placeholder: "203.0.113.7, 203.0.113.0/24",
        Label: () => _TF("Address or network", "Accessible name of the text box where an administrator enters an ip address or network (CIDR) to allow"),
        Api: "AllowIp",
        Table: "../ServerManager/AllowedIpsTable",
        Value: t => t.length > 0 ? t : null,
        Done: v => _T("{0} is allowed", v, "Message after an ip address was allowed (never flagged as an intruder), {0} is the address"),
    },
    port: {
        Title: () => _TF("Allowed ports", "Title of the page that lists the ports whose connections are never flagged as intruders"),
        Description: () => _TF("Connections on these ports (the local port of an incoming connection, else the remote port) are never flagged as an intruder.", "Description of the allowed ports page"),
        Placeholder: "8080",
        Label: () => _TF("Port", "Accessible name of the text box where an administrator enters a port to allow"),
        InputMode: "numeric",
        Api: "AllowPort",
        Table: "../ServerManager/AllowedPortsTable",
        Value: t => /^\d{1,5}$/.test(t) && (+t > 0) && (+t <= 65535) ? +t : null,
        Done: v => _T("Port {0} is allowed", v, "Message after a port was allowed (never flagged as an intruder), {0} is the port"),
    },
    country: {
        Title: () => _TF("Allowed countries", "Title of the page that lists the countries whose addresses are never flagged as intruders"),
        Description: () => _TF("Addresses located in these countries are never flagged as an intruder (requires an ip location service).", "Description of the allowed countries page"),
        Placeholder: "SE", /* SwWeb:no-tr (an ISO 3166 country code, the same in every language) */
        Label: () => _TF("Two letter country code", "Accessible name of the text box where an administrator enters an ISO 3166 country code to allow"),
        MaxLength: 2,
        Api: "AllowCountry",
        Table: "../ServerManager/AllowedCountriesTable",
        Value: t => /^[a-zA-Z]{2}$/.test(t) ? t.toUpperCase() : null,
        Done: v => _T("{0} is allowed", v, "Message after a country was allowed (never flagged as an intruder), {0} is the country code"),
    },
};

/**
 * The add box: a text box and the "Allow" button, hidden for users without the role of the api.
 * @param {object} kind - An entry of AllowedKinds.
 * @param {function(): ?object} getTable - Returns the live table (null until it is created).
 * @returns {HTMLElement} div.al-add.sw-row
 */
function allowedAddBox(kind, getTable) {
    let btn = null;
    const box = new Sw.TextBox({
        Placeholder: kind.Placeholder,
        AriaLabel: kind.Label(),
        InputMode: kind.InputMode,
        MaxLength: kind.MaxLength,
        Mono: true,
        Clear: true,
        Validate: v => {
            const t = (v || "").trim();
            if (t.length <= 0 || kind.Value(t) !== null)
                return null;
            return _TF("Not a valid value", "Error under the text box of the allowed addresses / ports / countries page when the typed value is invalid");
        },
        OnEnter: () => btn && btn.Click(),
    });
    btn = new Sw.Button({
        Text: _TF("Allow", "Text of the button that allows the typed address, port or country (it is never flagged as an intruder)"),
        Icon: "plus",
        Kind: "primary",
        Saves: false,
        Done: false,
        Title: _TF("Allow the value: it is never flagged as an intruder", "Tool tip of the Allow button of the allowed addresses / ports / countries page"),
        OnClick: async () => {
            const t = (box.Get() || "").trim();
            const v = kind.Value(t);
            if (v === null) {
                box.SetError(t.length <= 0
                    ? _TF("Enter a value", "Error under the text box of the allowed addresses / ports / countries page when it is empty")
                    : _TF("Not a valid value", "Error under the text box of the allowed addresses / ports / countries page when the typed value is invalid"));
                box.Focus(true);
                return false;
            }
            //  Throws on a refusal (400 invalid, 409 already allowed, 403 not Admin): the button shows the server's reason
            await Sw.Net.Send(kind.Api, v);
            box.Set("");
            Sw.Info(kind.Done(String(v)));
            const table = getTable();
            if (table)
                table.UpdateNow();
            return true;
        },
    });
    const row = Sw.el("div.al-add.sw-row", null, box.E, btn.E);
    Sw.Access.Guard(row, { Url: kind.Api, Hide: true });
    return row;
}

/**
 * Entry point of allowed.html.
 * @returns {Promise<void>}
 */
function allowedMain() {
    return Sw.Page.Main(async () => {
        const kind = AllowedKinds[(Sw.Url.Get("k") || "ip").toLowerCase()];
        if (!kind) {
            Sw.Page.ShowNotFound({ Hint: _TF("The page parameter k must be ip, port or country", "Hint of the not found state of the allowed page when the url is wrong") });
            return;
        }
        let table = null;
        const page = Sw.PageLayout({
            Main: ".al-page",
            Type: "fill",
            Compact: false,
            Title: kind.Title(),
            Description: kind.Description(),
        });
        page.Add(allowedAddBox(kind, () => table));
        table = await SmShared.FillTable(page, kind.Table);
    }, { Loading: true });
}
