/**
 * @file Users.js - the Users page of SysWeaver.MicroService.UserManager (auth/Users.html): the one place for the user
 * accounts of a site, shown as Management > Users in the user menu (next to Invite user, where the original had them).
 *
 * A Design v2 fill page (Design v2, ui/README.md Page anatomy / PORTING.md B13): a page header with the actions the signed in user may use (asked with
 * Sw.Access.CanCall, no token names here):
 *  - "Invite user" (primary; Api/auth/InviteUser, opens auth/InviteUser.html, Administration > Users > Invite user),
 *  - "Force password reset..." (Api/auth/ForcePasswordResetUser: asks for the user id, confirmed, then the user must choose a new password at
 *    the next sign in and is signed out),
 *  - "Delete user..." (Api/auth/AdminDeleteUser, only when the server has it and the user may use it: asks for the user, a typed confirmation
 *    of the user name, then deletes the account; the way back for an account nobody can use any more),
 * then two tabs:
 *  - "Users": the live table of the accounts (Api/auth/UserAdminTable: the accounts with their row actions, not cached on the server, so the
 *    re-read after an action shows the new state at once; the search box searches user name, domain, language and nick name),
 *    each row has the server's row actions that fit the user's state (Force password reset, Undo forced reset, Disable, Enable; shown to
 *    users that may manage users; a viewer that may not (no Api/auth/ForcePasswordResetUser) reads the plain Api/auth/UserDataTable
 *    instead, as the original "View users": the same columns, no Actions column),
 *  - "Pending invitations" (only when the user may invite, Api/auth/InvitationTable): the sent invitations and sign ups that were not used
 *    yet, with the "Withdraw invitation" row action (Api/auth/CancelInvitation); the tab badge is the number of pending invitations.
 * The page opens on the tab of the url (?tab=invitations, so InviteUser.html can link to it); the chosen tab is kept in the page state
 * (Sw.History), so Back to this page (and a reload) shows the tab the user left, while a new visit from the menu starts on Users.
 * The built-in users of the server configuration (SimpleAuthorizer) are a separate page: Administration > Built-in users.
 *
 * Load order: ../sw/core.js, ../sw/ui.js, ../edit/Edit.js, ../explore/table.js (Sw.Table), then this file; started by
 * `<body onload="usersMain()">`. Globals defined: `usersMain`.
 */
"use strict";

/**
 * The consequences of a forced password reset the server computed for a user (FindPasswordResetUser / ForcePasswordResetUser:
 * `Consequences`, a list of sentences or one sentence, ex: "c4r5d has no e-mail address, phone or passkey: after the reset they can't
 * sign in again."). The page never guesses them: an older server without the field gives none.
 * @param {?object} r The server's result.
 * @returns {Array<string>} The sentences.
 */
function usersConsequences(r) {
    const c = r ? r.Consequences : null;
    const list = Array.isArray(c) ? c : (typeof c === "string" ? [c] : []);
    return list.map(x => String(x || "").trim()).filter(x => x);
}

/** @type {Map<string, Set<{Close: function():void}>>} The result toasts of the header actions per user (the user name and the typed user
 *  id, lower case), so deleting that user closes them (Admin N50: "… must choose a new password" stayed next to "… was deleted"). */
const usersToasts = new Map();

/**
 * Remember a result toast of a header action for its user (usersCloseToasts).
 * @param {?{Close: function():void}} toast The toast (Sw.Info / Sw.Fail).
 * @param {...string} names The user name and what was typed.
 * @returns {?object} The toast.
 */
function usersToast(toast, ...names) {
    if (toast && typeof toast.Close === "function")
        for (const n of names) {
            const k = String(n || "").trim().toLowerCase();
            if (!k)
                continue;
            if (!usersToasts.has(k))
                usersToasts.set(k, new Set());
            usersToasts.get(k).add(toast);
        }
    return toast;
}

/**
 * Close the result toasts shown for a user (usersToast), ex: after the user was deleted.
 * @param {...string} names The user name and what was typed.
 */
function usersCloseToasts(...names) {
    for (const n of names) {
        const k = String(n || "").trim().toLowerCase();
        const set = k ? usersToasts.get(k) : null;
        if (!set)
            continue;
        usersToasts.delete(k);
        for (const t of set)
            try {
                t.Close();
            }
            catch {
            }
    }
}

/**
 * Ask for the user of a forced password reset: the user id (user name, e-mail address or phone number), checked on the server
 * (FindPasswordResetUser, nothing is changed) before the dialog closes, so an unknown user or a user without a password is told at the
 * field, before any confirmation.
 * @returns {Promise<?{User: string, Consequences: Array<string>, LockOut: boolean}>} The user name of a user with a password and the
 * consequences of a reset the server returned (LockOut: the user could not sign in again), null if cancelled.
 */
async function usersAskResetUser() {
    return usersAskUser({
        Title: _TF("Force a password reset", "Title of the dialog on the users page that forces a user to choose a new password"),
        //  (the same consequences as the confirmation and the row action, Admin N49)
        Text: _TF("The user must choose a new password at the next sign in: the current password stops working and their open sessions end. Passkeys keep working. A user who signs in with a passkey only has no password to reset.", "Text of the force password reset dialog on the users page"),
        //  (the server says why a reset can't be done, ex: no address, phone or passkey: they could not sign in again, Admin N33)
        Accept: (r, user) => r.Refused
            ? String(r.Refused).trim().replace(/([^.!?])$/, "$1.")
            : !r.HadPassword
                ? _T("{0} has no password (signs in with a passkey only), there is nothing to reset.", r.UserName || user, "Error of a forced password reset when the user has no password.{0} is replaced with the user")
                : null,
    });
}

/**
 * Ask for a user (the user id: user name, e-mail address or phone number), looked up on the server (FindPasswordResetUser, read only)
 * before the dialog closes, so an unknown user, or one the action can't be done for (o.Accept), is told at the field.
 * @param {{Title: string, Text: string, Accept: function(object, string): ?string}} o The dialog's title and text, and the check of
 * the found user (the server's lookup result): a reason why the action can't be done, or null.
 * @returns {Promise<?{User: string, Consequences: Array<string>, LockOut: boolean, Info: object}>} The user name, the consequences the
 * server returned, and the lookup result; null if cancelled.
 */
async function usersAskUser(o) {
    const tb = new Sw.TextBox({
        Placeholder: _TF("User name, email address or phone number", "Placeholder of the user id field of the force password reset dialog"),
        Label: _TF("User", "Label of the user id field of the force password reset dialog"),
        Required: _TF("Enter the user name, email address or phone number of the user", "Validation message of the force password reset dialog"),
        Validate: v => String(v || "").trim() ? null : _TF("Enter the user name, email address or phone number of the user", "Validation message of the force password reset dialog"),
    });
    //  (a lookup field, not data that can be lost: Escape closes without "Discard changes?", Admin N18)
    if (tb.Input)
        tb.Input.setAttribute("data-sw-nodirty", "");
    let found = null;
    let busy = false;
    /** Check the typed user; true when it can be reset (found is its user name), else the reason is shown at the field. */
    const check = async () => {
        if (busy || !tb.Commit())
            return false;
        const user = String(tb.Get() || "").trim();
        busy = true;
        try {
            const r = await Sw.Net.Send(Sw.Url.Root + "Api/auth/FindPasswordResetUser", user, { NoNavigate: true, ReadOnly: true });
            if (!r || !r.Found) {
                tb.SetError(_T("There is no user {0}. Check the user name, email address or phone number.", user, "Error of a forced password reset when the user was not found.{0} is replaced with what was typed"));
                return false;
            }
            const why = o.Accept ? o.Accept(r, user) : null;
            if (why) {
                tb.SetError(why);
                return false;
            }
            found = { User: r.UserName || user, Consequences: usersConsequences(r), LockOut: !!(r.LockOut || r.Blocks), Info: r };
            return true;
        }
        catch (x) {
            tb.SetError(Sw.Access.IsDenied(x)
                ? _TF("You may not look up users.", "Error of the user dialog of the users page when the signed in user may not look up users")
                : Sw.Net.Message(_TF("Could not look up the user.", "Error of the force password reset dialog when the user could not be looked up (the server's reason follows)"), x));
            return false;
        }
        finally {
            busy = false;
        }
    };
    const buttons = [
        { Text: _TF("Cancel", "The text of a button on a dialog that when pressed will close the dialog without performing the action"), Value: null },
        { Text: _TF("Continue", "Button of the force password reset dialog that goes on to the confirmation"), Kind: "primary", Value: "ok", OnClick: check },
    ];
    const v = await Sw.Dialog.Show({
        Title: o.Title,
        Content: Sw.el("div.sw-dialog-prompt", null, Sw.el("div.sw-dialog-text", { text: o.Text }), tb.E),
        Buttons: buttons,
        //  Enter runs Continue (with its check): the dialog closes only when the user can be reset
        EnterValue: () => {
            const b = buttons[1].Button;
            if (b && b.E)
                b.E.click();
            return undefined;
        },
        Focus: tb.Input,
    });
    return v === "ok" ? found : null;
}

/**
 * Ask for a user and force a password reset (ForcePasswordResetUser): the user id (checked first, usersAskResetUser), a danger
 * confirmation that names the user and says what happens, with the consequences the server computed for this user first (Admin N33,
 * UX guide SAF-2; ex: no e-mail address, phone or passkey, so the user could not sign in again: then the user name must be typed), then
 * the call; the result as a toast; a refusal (ex: the server refuses a reset that would lock the user out) shows the server's reason.
 * @returns {Promise<void>}
 */
async function usersForceReset() {
    const f = await usersAskResetUser();
    if (!f)
        return;
    const user = f.User;
    //  (the same sentence as the row action's confirmation, DbUserAdminRow, and the server's consequences: Admin N49)
    const what = _TF("They must choose a new password at the next sign in: the current password stops working and their open sessions end. Passkeys keep working.", "Text of the confirmation of a forced password reset");
    //  (the server's consequences are the facts for this user and say what happens; the general sentence only from an older server)
    const text = f.Consequences.length ? f.Consequences.join(" ") : what;
    const title = _T("Force a password reset for {0}?", user, "Title of the confirmation of a forced password reset.{0} is replaced with the user");
    const okText = _TF("Force password reset", "Confirm button of a forced password reset");
    const ok = f.LockOut && Sw.Cells && Sw.Cells.ConfirmDanger
        //  (the user would be locked out: the consequence first, and the user name must be typed: one click can't do it)
        ? await Sw.Cells.ConfirmDanger({
            Title: title,
            ItemLabel: _TF("User:", "Label before the user name in the confirmation of a forced password reset"),
            Item: user,
            Text: text,
            Confirm: user,
            OkText: okText,
        })
        : await Sw.Dialog.Confirm(title, text, okText, { Danger: true });
    if (!ok)
        return;
    try {
        const r = await Sw.Net.Send(Sw.Url.Root + "Api/auth/ForcePasswordResetUser", user, { NoNavigate: true });
        const name = r && r.UserName ? r.UserName : user;
        if (!r || !r.Found)
            usersToast(Sw.Fail(_T("There is no user {0}. Check the user name, email address or phone number.", user, "Error of a forced password reset when the user was not found.{0} is replaced with what was typed")), user);
        else if (!r.HadPassword)
            usersToast(Sw.Info(_T("{0} has no password, nothing was changed.", r.UserName || user, "Message when a forced password reset found no password (ex: a passkey only user).{0} is replaced with the user")), user, name);
        else if (r.SignedOut > 0)
            //  (the same words as the row action's done text, DbUserAdminRow: no count, so no "1 sessions")
            usersToast(Sw.Info(_T("{0} must choose a new password at the next sign in. Their open sessions ended.", r.UserName || user, "Message after a forced password reset that also signed out the user.{0} is replaced with the user")), user, name);
        else
            usersToast(Sw.Info(_T("{0} must choose a new password at the next sign in.", r.UserName || user, "Message after a forced password reset.{0} is replaced with the user")), user, name);
        //  (the user's row offers Undo forced reset now)
        if (r && r.HadPassword)
            usersRefresh();
    }
    catch (x) {
        //  (a refusal: the server's reason, ex: the reset would leave the user without a way to sign in)
        Sw.Fail(Sw.Access.IsDenied(x)
            ? _TF("You may not force a password reset.", "Error when the signed in user may not force a password reset")
            : Sw.Net.Message(_TF("Could not force a password reset.", "Error of a forced password reset (the server's reason follows)"), x));
    }
}

/** @type {?object} The users table (Sw.Table), refreshed after a delete or a forced reset of the header actions. */
let usersTable = null;

/**
 * Show the users as they are now, after an action of the page header (delete, forced reset): the table's api (Api/auth/UserAdminTable) is
 * not cached on the server, and the re-read is shown like a user's refresh (not held under the pointer like a live one), so a deleted
 * row leaves with its buttons and a changed row shows its new actions at once (Admin N37).
 */
function usersRefresh() {
    if (usersTable && usersTable.Changed)
        usersTable.Changed(true);
    else if (usersTable && usersTable.UpdateNow)
        usersTable.UpdateNow();
}

/**
 * Delete a user account (the administrator's way back for an account nobody can use any more, Admin N33): the user id (looked up
 * first, usersAskUser), a danger confirmation that names the account and must be typed (Sw.Cells.ConfirmDanger; the server checks the
 * typed name too), then Api/auth/AdminDeleteUser { User, Confirm }; the server's sentence of what was done as the result, a refusal
 * (ex: your own account) with its reason.
 * @returns {Promise<void>}
 */
async function usersDeleteUser() {
    const f = await usersAskUser({
        Title: _TF("Delete a user", "Title of the dialog on the users page that deletes a user account"),
        Text: _TF("The account, its sign in methods, addresses, roles and data are removed, and its open sessions end.", "Text of the delete user dialog on the users page"),
        Accept: () => null,
    });
    if (!f)
        return;
    const user = f.User;
    const ok = await Sw.Cells.ConfirmDanger({
        Title: _T("Delete {0}?", user, "Title of the confirmation of the delete user action of the users page.{0} is replaced with the user name"),
        ItemLabel: _TF("User:", "Label before the user name in the confirmation of a forced password reset"),
        Item: user,
        Text: _TF("The account, its sign in methods, addresses, roles and all data stored for it are removed, every link sent for it stops working and its open sessions end. The person can't sign in with it again.", "Text of the confirmation of the delete user action of the users page"),
        Confirm: user,
        OkText: _TF("Delete user", "Confirm button of the delete user action of the users page"),
    });
    if (!ok)
        return;
    try {
        const r = await Sw.Net.Send(Sw.Url.Root + "Api/auth/AdminDeleteUser", { User: user, Confirm: user }, { NoNavigate: true });
        //  (the earlier results about this user, ex: "… must choose a new password", are not true any more: Admin N50)
        usersCloseToasts(user, f.Info && f.Info.UserName);
        Sw.Info(r && r.Text ? String(r.Text) : _T("{0} was deleted.", user, "Message after a user was deleted on the users page.{0} is replaced with the user name"));
        //  (the list follows at once, the deleted row leaves with its buttons (Admin N37): the table's api is not cached on the server and
        //  the re-read is shown like a user's refresh, not held under the pointer like a live one)
        usersRefresh();
    }
    catch (x) {
        Sw.Fail(Sw.Net.Message(_T("{0} was not deleted.", user, "Error of the delete user action of the users page (the server's reason follows).{0} is replaced with the user name"), x));
    }
}

/**
 * The number of pending invitations (for the tab badge).
 * @returns {Promise<number|null>} The count, null if it could not be read.
 */
async function usersInvitationCount() {
    try {
        const d = await Sw.Net.Send(Sw.Url.Root + "Api/auth/InvitationTable", { Row: 0, MaxRowCount: 1, LookAheadCount: 999 }, { NoNavigate: true, Post: true });
        return d ? (d.RowCount || 0) : null;
    }
    catch {
        return null;
    }
}

/**
 * Page entry point (body onload): the page header with the allowed actions, then the Users / Pending invitations tabs.
 * @returns {Promise<void>}
 */
function usersMain() {
    return Sw.Page.Main(async () => {
        const root = Sw.Url.Root;
        //  The tab the user left (Back, reload: Sw.History keeps it for this history entry, the shell clears it for a new visit), else the url's
        const keptTab = Sw.History ? Sw.History.Get("UsersTab", null) : null;
        const startTab = keptTab === "invitations" || keptTab === "users" ? keptTab
            : (new URLSearchParams(location.search).get("tab") === "invitations" || location.hash === "#invitations" ? "invitations" : "users");
        const page = Sw.PageLayout({
            Main: "main.um-users",
            Type: "fill",
            Compact: false,
            Title: _TF("Users", "Title of the users page (the user accounts of the site)"),
            Description: _TF("The user accounts of this site. The users of the service configuration are under Built-in users.", "Description of the users page"),
        });
        const [canInvite, canReset, canDelete] = await Promise.all([
            Sw.Access.CanCall(root + "Api/auth/InviteUser"),
            Sw.Access.CanCall(root + "Api/auth/ForcePasswordResetUser"),
            Sw.Access.CanCall(root + "Api/auth/AdminDeleteUser"),
        ]);
        if (canReset !== false)
            page.Header.AddAction(new Sw.Button({
                Text: _TF("Force password reset...", "Button on the users page that forces a user to choose a new password"),
                Icon: "key",
                Title: _TF("Make a user choose a new password at the next sign in", "Tool tip of the force password reset button on the users page"),
                OnClick: usersForceReset,
            }));
        //  (an older server without the api answers false / null: no button)
        if (canDelete === true)
            page.Header.AddAction(new Sw.Button({
                Text: _TF("Delete user...", "Button on the users page that deletes a user account (asks for the user and a typed confirmation first)"),
                Icon: "trash",
                Title: _TF("Delete a user account, ex: one nobody can sign in with any more", "Tool tip of the delete user button on the users page"),
                OnClick: usersDeleteUser,
            }));
        if (canInvite !== false) {
            //  How an invitation can be sent (the coms of the server: Email; Sms and WhatsApp send to a phone number): none = no way to
            //  invite, the button says why (an older server without the props: by e-mail, as before)
            let coms = null;
            try {
                const p = await Sw.Net.Send(root + "Api/auth/UserManagerProps");
                coms = p && Array.isArray(p.Coms) ? p.Coms : null;
            }
            catch {
            }
            const byEmail = !coms || coms.includes("Email");
            const byPhone = !!coms && coms.some(x => x === "Sms" || x === "WhatsApp");
            page.Header.AddAction(new Sw.Button({
                Text: _TF("Invite user", "Button on the users page that opens the invite user page"),
                Icon: "plus",
                Kind: "primary",
                Title: byEmail && byPhone
                    ? _TF("Invite a new user by email or phone", "Tool tip of the invite user button on the users page when invitations can be sent by email and to phone numbers")
                    : (byPhone
                        ? _TF("Invite a new user by phone", "Tool tip of the invite user button on the users page when invitations can only be sent to phone numbers")
                        : _TF("Invite a new user by email", "Tool tip of the invite user button on the users page")),
                Disabled: (byEmail || byPhone) ? false : _TF("Invitations can't be sent: this site has no way to send emails or text messages", "Why the invite user button on the users page is disabled"),
                OnClick: () => Sw.Page.Navigate("InviteUser.html", "_self"),
            }));
        }
        const usersHost = Sw.el("div.sw-fill.um-users-table");
        //  A viewer without the right to manage users (ex: Debug, Ops) gets no row action: it reads the plain users table, as the original
        //  "View users" (explore/table.html?q=../Api/auth/UserDataTable): the same columns without an empty Actions column (Admin N45)
        const users = () => Sw.Table.Create({
            Url: canReset === false ? "../Api/auth/UserDataTable" : "../Api/auth/UserAdminTable",
            Element: usersHost,
            Page: true,
            Header: false,
            CardTitle: "UserName",
            Name: _TF("Users", "Title of the users page (the user accounts of the site)"),
            EmptyTitle: _TF("No users yet", "Title of the empty users table"),
            EmptyText: _TF("Invite a user, or let people sign up.", "Text of the empty users table"),
        });
        //  Without the right to invite there is nothing but the users table (as before)
        if (canInvite === false) {
            page.Add(usersHost);
            return usersTable = await users();
        }
        const invHost = Sw.el("div.sw-fill.um-users-table");
        let invTable = null;
        /** @type {number} Counts the shows of the invitations tab: a table made for an older show (the tab was left meanwhile) is dropped. */
        let invShow = 0;
        /** @type {?number} The last row count of the invitations table (the badge follows it). */
        let lastCount = null;
        const tabs = new Sw.Tabs({
            Class: "um-users-tabs sw-fill",
            Label: _TF("User accounts and pending invitations", "Accessible name of the tabs of the users page"),
            OnSelect: async tab => {
                //  Kept for Back / reload (SAF-12). The frame url is not rewritten: the page state belongs to the url the shell knows.
                if (Sw.History)
                    Sw.History.Set("UsersTab", tab.Id);
                //  The result of a row action on the other tab ("Enable (…): done") is not read out on this one (Admin N42)
                document.querySelectorAll(".sw-page-live").forEach(r => r.textContent = "");
                //  A hidden invitations table doesn't poll (Admin N43: it was refreshed every 5 s behind the Users tab): it is made when
                //  its tab is shown and removed when another tab is (the badge has its own count, usersInvitationCount)
                if (tab.Id !== "invitations") {
                    ++invShow;
                    if (invTable) {
                        const old = invTable;
                        invTable = null;
                        lastCount = null;
                        old.Destroy();
                        invHost.replaceChildren();
                    }
                    return;
                }
                if (invTable)
                    return;
                const show = ++invShow;
                const made = await Sw.Table.Create({
                    Url: "../Api/auth/InvitationTable",
                    Element: invHost,
                    Page: true,
                    Header: false,
                    CardTitle: "Target",
                    Name: _TF("Pending invitations", "Title of the pending invitations table of the users page"),
                    EmptyTitle: _TF("No pending invitations", "Title of the empty pending invitations table"),
                    EmptyText: _TF("Invitations and sign ups show up here until they are used or expire (after 24 hours).", "Text of the empty pending invitations table"),
                });
                //  (the tab was left, or shown again, while the table was made: this one is not needed)
                if (show !== invShow || invTable) {
                    made?.Destroy?.();
                    if (!invTable)
                        invHost.replaceChildren();
                    return;
                }
                invTable = made;
            },
        });
        const usersTab = tabs.Add({ Id: "users", Title: _TF("Users", "Tab of the users page with the user accounts"), Icon: "user", Content: usersHost });
        const invTab = tabs.Add({
            Id: "invitations", Title: _TF("Pending invitations", "Tab of the users page with the invitations (and sign ups) that were not used yet"), Icon: "mail",
            Tip: _TF("Invitations and sign ups that were sent but not used yet: withdraw one to make its link stop working", "Tool tip of the pending invitations tab of the users page"),
            Content: invHost, Select: false,
        });
        page.Add(tabs.E);
        const t = usersTable = await users();
        //  The tab badge follows the invitations: counted at the start, again when the invitations table shows another row count
        //  (ex: after "Withdraw invitation" the table refreshes) and when the page is shown again (ex: back from an invite in another tab)
        const updateBadge = () => usersInvitationCount().then(n => {
            if (n !== null)
                invTab.SetBadge(n ? String(n) : "");
        });
        updateBadge();
        if (window.MutationObserver)
            new MutationObserver(() => {
                const n = invTable && invTable.Data ? invTable.Data.RowCount : null;
                if (n === null || n === undefined || n === lastCount)
                    return;
                const first = lastCount === null;
                lastCount = n;
                if (!first)
                    updateBadge();
            }).observe(invHost, { childList: true, subtree: true, characterData: true });
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState === "visible")
                updateBadge();
        });
        if (startTab === "invitations")
            tabs.Select(invTab);
        else
            tabs.Select(usersTab);
        return t;
    }, { Loading: true });
}
