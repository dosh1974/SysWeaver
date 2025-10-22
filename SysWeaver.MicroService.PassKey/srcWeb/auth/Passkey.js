

//https://github.com/passwordless-lib/fido2-net-lib?tab=readme-ov-file#examples
//https://developer.mozilla.org/en-US/docs/Web/API/PublicKeyCredentialCreationOptions#pubkeycredparams

class PassKey {

    static ApiPrefix = "../Api/auth/passkey/";


    static async GetLoggedInUser() {
        const u = await getRequest(PassKey.ApiPrefix + "../GetUser", true);
        if (!u)
            return null;
        return u.Succeeded ? u : null;
    }

    static async LoginUsingPassKey() {
        const p = PassKey.ApiPrefix;
        let c = await sendRequest(p + "GetAuthChallenge", window["PlatformId"], true);
        if (!c)
            throw new Error("Failed to get auth challenge!");
        RemoveNulls(c);
        c.publicKey.challenge = base64ToArray(c.publicKey.challenge);
        if (c.publicKey.allowCredentials)
            c.publicKey.allowCredentials.forEach(x => x.id = base64ToArray(x.id));
        const cc = await navigator.credentials.get(c);
        if (!cc)
            throw new Error("Validation cancelled!");
        const res = cc.response;
        const user = await sendRequest(p + "Auth", {
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    authenticatorData: await bufferToBase64(res.authenticatorData),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                    signature: await bufferToBase64(res.signature),
                    userHandle: await bufferToBase64(res.userHandle),
                },
            type: cc.type,
        });
        if (!user) {
            sessionStorage.removeItem("SysWeaver.User");
            throw new Error("Failed to login user, sign up?");
        }
        if (user.Succeeded)
            sessionStorage.setItem("SysWeaver.User", JSON.stringify(user));
        else
            sessionStorage.removeItem("SysWeaver.User");
        return user;

    }


    static async AttachNewToAccount() {

        const p = PassKey.ApiPrefix;
        let c = await sendRequest(p + "GetCreateChallenge", window["PlatformId"], true);
        if (!c)
            throw new Error("Failed to get create challenge!");
        RemoveNulls(c);
        c.publicKey.challenge = base64ToArray(c.publicKey.challenge);
        c.publicKey.user.id = base64ToArray(c.publicKey.user.id);
        if (c.publicKey.excludeCredentials)
            c.publicKey.excludeCredentials.forEach(x => x.id = base64ToArray(x.id));
        const cc = await navigator.credentials.create(c);
        if (!cc)
            throw new Error("Validation cancelled!");
        const res = cc.response;
        return await sendRequest(p + "Create", {
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    attestationObject: await bufferToBase64(res.attestationObject),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                },
            type: cc.type ,
        });
    }

    static async NewAccount(token) {

        const p = PassKey.ApiPrefix;
        let c = await sendRequest(p + "GetNewChallenge", token, true);
        if (!c)
            throw new Error("Failed to get create challenge!");
        RemoveNulls(c);
        c.publicKey.challenge = base64ToArray(c.publicKey.challenge);
        c.publicKey.user.id = base64ToArray(c.publicKey.user.id);
        if (c.publicKey.excludeCredentials)
            c.publicKey.excludeCredentials.forEach(x => x.id = base64ToArray(x.id));
        const cc = await navigator.credentials.create(c);
        if (!cc)
            throw new Error("Validation cancelled!");
        const res = cc.response;
        const result = await sendRequest(p + "New", {
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    attestationObject: await bufferToBase64(res.attestationObject),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                },
            type: cc.type,
        });
        if (result)
            sessionStorage.setItem("SysWeaver.User", JSON.stringify(result));
        else
            sessionStorage.removeItem("SysWeaver.User");
        return result;


    }


    static async AttachNewToAccountFromToken(token) {

        const p = PassKey.ApiPrefix;
        let c = await sendRequest(p + "GetResetChallenge", token, true);
        if (!c)
            throw new Error("Failed to get create challenge!");
        RemoveNulls(c);
        c.publicKey.challenge = base64ToArray(c.publicKey.challenge);
        c.publicKey.user.id = base64ToArray(c.publicKey.user.id);
        if (c.publicKey.excludeCredentials)
            c.publicKey.excludeCredentials.forEach(x => x.id = base64ToArray(x.id));
        const cc = await navigator.credentials.create(c);
        if (!cc)
            throw new Error("Validation cancelled!");
        const res = cc.response;
        return await sendRequest(p + "New", {
            authenticatorAttachment: cc.authenticatorAttachment,
            id: cc.id,
            rawId: await bufferToBase64(cc.rawId),
            response: res == null ? null :
                {
                    attestationObject: await bufferToBase64(res.attestationObject),
                    clientDataJSON: await bufferToBase64(res.clientDataJSON),
                },
            type: cc.type,
        });
    }


}



async function addPassKeyMain()
{
    await AuthPage("IconAddPasskey", async (target, img) => {

        const ps = getUrlParams();
        const token = ps.get('token');

        const text = AuthText(target, "");
        async function tryOnce() {
            text.classList.remove("Fail");
            text.classList.add("Info");
            text.innerText = "Please add authentication on this device..";
            button.Element.classList.add("Hide");
            try {
                if (!await (token ? PassKey.AttachNewToAccountFromToken(token) : PassKey.AttachNewToAccount())) {
                    const m = "Failed to add authentication!";
                    text.innerText = m;
                    Fail(m);
                } else {
                    img.ChangeImage("IconAddPasskey");
                    text.innerText = "Added authentication successfully!";
                    if (token)
                        await AuthStartPage();
                    return;
                }
            }
            catch (e) {
                const m = "Failed to add authentication.\n" + e;
                text.innerText = m;
                Fail(m);
            }
            text.classList.remove("Info");
            text.classList.add("Fail");
            button.Element.classList.remove("Hide");
        }
        const buttons = AuthButtonRow(target);
        const button = AuthButton(buttons, "Retry", "Click to retry adding the authentication", "IconRetry", tryOnce);
        button.Element.classList.add("Hide");

        const user = await PassKey.GetLoggedInUser();
        if (token) {
            if (user) {
                text.classList.add("Info");
                text.innerText = "A user is already signed in!\nPlease sign out before creating a new passkey.";
                img.ChangeImage("IconWarning");
                return;
            }
        } else {
            if (!user) {
                text.classList.add("Info");
                text.innerText = "No user is signed in!\nPlease sign in before assigning a passkey.";
                img.ChangeImage("IconWarning");
                return;
            }
        }
        target.OnLoaded();
        await tryOnce();
    });

}



async function usePassKeyMain() {
    await AuthPage("IconUsePasskey", async (target, img) => {

        const text = AuthText(target, "");
        async function tryOnce() {
            text.classList.remove("Fail");
            text.classList.add("Info");
            text.innerText = "Please authenticate yourself..";
            button.Element.classList.add("Hide");
            try {
                if (!await PassKey.LoginUsingPassKey()) {

                    const m = "Failed to authenticate!";
                    text.innerText = m;
                    Fail(m);
                } else {
                    img.ChangeImage("IconAddPasskey");
                    text.innerText = "Authenticated successfully!";
                    await AuthStartPage();
                    return;
                }
            }
            catch (e) {
                const m = "Failed to authenticate.\n" + e;
                text.innerText = m;
                Fail(m);
            }
            text.classList.remove("Info");
            text.classList.add("Fail");
            button.Element.classList.remove("Hide");
        }
        const buttons = AuthButtonRow(target);
        const button = AuthButton(buttons, "Retry", "Click to retry authentication", "IconRetry", tryOnce);
        button.Element.classList.add("Hide");

        const user = await PassKey.GetLoggedInUser();
        if (user) {
            text.classList.add("Info");
            text.innerText = "The user " + user.Username + " is already signed in!\nPlease sign out before signing in with a new user.";
            img.ChangeImage("IconWarning");
            return;
        }
        target.OnLoaded();
        await tryOnce();
    });

}


async function addPassKeyOtherMain() {
    await AuthPage("../Api/auth/passkey/GetQR.svg", async (target, img) => {

        const user = await PassKey.GetLoggedInUser();
        AuthLabel(target, 'Allow <em>' + makeHtmlSafe(user.Username) + '</em> to login on another device using a passkey, by scanning the QR code and follow the link.', null, true);
        AuthText(target, "The QR code may only be used once and expires in 15 minutes.");
    });
}