////// Logout ///////////////////////////////

async function logoutMain() {
    await AuthPage("IconAuthLogout", async target => {
        const text = AuthText(target, "");
        async function tryOnce() {
            text.classList.remove("Fail");
            text.classList.add("Info");
            AuthSetText(text, _TF("Signing out ..", "Text indicating that the user is being signed out"));
            button.Element.classList.add("Hide");
            try {
                let failed = false;
                try {
                    const r = await sendRequest("../Api/auth/GetUser");
                    if (!r.Succeeded) {
                        const m = _TF("No user is signed in!", "Error text indicating that a sign out can't be performed since no user is signed in");
                        AuthSetText(text, m);
                        Fail(m);
                        failed = true;
                    }
                }
                catch
                {
                }
                if (!failed) {
                    if (!await logoutRequest()) {
                        const m = _TF("Failed to sign out!", "Error text indicating that the sign out request failed");
                        AuthSetText(text, m);
                        Fail(m);
                    } else {
                        AuthSetText(text, _TF("Signed out successfully!", "Text explaining that the user was signed out correctly"));
                        return;
                    }
                }
            }
            catch (e) {
                const m = _TF("Failed to sign out!", "Error text indicating that the sign out request failed") + "\n\n" + e;
                AuthSetText(text, text.innerText);
                Fail(m);
            }
            text.classList.remove("Info");
            text.classList.add("Fail");
            button.Element.classList.remove("Hide");
        }
        const buttons = AuthButtonRow(target);
        const button = AuthButton(buttons,
            _TF("Retry", "Text on a button that when pressed will retry an operation that failed"),
            _TF("Click to retry sign out", "Tool tip description on a button that when pressed will retry to sign out the logged in user"),
            "IconRetry", tryOnce);
        button.Element.classList.add("Hide");
        target.OnLoaded();
        await tryOnce();
    });
}

////// Login ///////////////////////////////

async function loginMain() {
    await AuthPage("IconAuthLogin", async target => {

        const havePasskey = typeof PassKey !== "undefined";

        AuthLabel(target, _TF("User ID", "Label for an input box where a user should enter their user id (user name, email or phone number)"));
        const uname = AuthInput(target, _TF("Enter your user id", "Placeholder description on an input box where the user should enter their user id"), null, "username", async input => await setStatus(input.value, pwd.value));

        const pwdLabel = AuthLabel(target, _TF("Password", "Label for an input box where a user should enter their password"));
        const pwd = AuthPassword(target, _TF("Enter your password", "Placeholder description on an input box where the user should eneter their password"), null, "current-password", async input => await setStatus(uname.value, pwd.value),
            async el => {
                if (await setStatus(uname.value, pwd.value))
                    el.click();
            }
        );
        const info = AuthError(target, "", "error");
        const br = AuthButtonRow(target);

        async function setStatus(uText, pwText) {
            uText = AuthTrim(uText);
            pwText = AuthTrim(pwText);
            const p = await ValidatePassword(uText, pwText, target, newPolicy => {
                const policyText = AuthPolicyText(newPolicy);
                pwdLabel.title = policyText;
                info.title = policyText;
            });
            if ((!uText) || (uText.length < 1)) {
                AuthSetError(uname, true);
                AuthSetError(pwd, false);
                AuthSetText(info, _TF("Enter your user id", "Information text explaining that the user should enter their user id (user name, email or phone number)"));
                setButton.SetEnabled(false);
                return false;
            }
            AuthSetError(uname, false);
            AuthSetError(pwd, p);
            if (p)
                AuthSetText(info, p);
            else
                CheckPassword(info, pwText); // Don't await, run async
            setButton.SetEnabled(!p);
            return !p;
        }


        let passKeyButton = null;
        const setButton = AuthButton(br,
            _TF("Sign in", "Text of a button that when pressed will attempt to sign in a user using their entered credentials"),
            _TF("Click to sign in using the entered credentials", "Tool tip description on a button that when pressed will attempt to sign in a user using their entered credentials"),
            "IconAuthPassword", async button => {
            button.StartWorking();
            pwd.readOnly = true;
            uname.readOnly = true;
            if (passKeyButton)
                passKeyButton.SetEnabled(false);
            try {
                const usr = uname.value;
                AuthSetText(info, _T('Signing in "{0}" ..', usr, "Text displayed when a user is trying to sign in.{0} is replaced with the user id (user name, email or phone number)"));
                const pw = pwd.value;
                const res = await loginRequest(usr, pw);
                if (res && res.Succeeded) {
                    AuthSetText(info, _TF("User signed in.", "Text displayed when a user have successfully signed in"));
                    setButton.SetEnabled(false);
                    await AuthStartPage();
                    return;
                }
                Fail(_TF("Failed to login.", "Error message displayed when a sign in failed"));
            }
            catch (e) {
                Fail(_TF("Failed to login.", "Error message displayed when a sign in failed") + "\n\n" + e);
            }
            finally {
                button.StopWorking();
            }
            if (passKeyButton)
                passKeyButton.SetEnabled(true);
            await setStatus(uname.value, pwd.value);
            SetTemporaryInnerText(info, _TF("Failed to login, try again!?", "Message displayed when a sign in failed"), 5000);
            uname.readOnly = false;
            pwd.readOnly = false;
            pwd.focus();
        }, true);
        if (havePasskey) {
            passKeyButton = AuthButton(br,
                _TF("Use passkey", "Text on a button that when pressed will attempt to sign in using a passkey"), 
                _TF("Click to login using your passkey", "Tool tip description of a button that when pressed will attempt to sign in using a passkey"),
                "IconUsePasskey", async button => {
                button.StartWorking();
                uname.readOnly = true;
                pwd.readOnly = true;
                setButton.SetEnabled(false);
                try {
                    AuthSetText(info, _TF("Select your passkey ..", "Text explaining to the user that he should select their passkey to continue sign in"));
                    const res = await PassKey.LoginUsingPassKey();
                    if (res) {
                        AuthSetText(info, _TF("User signed in.", "Text displayed when a user have successfully signed in"));
                        passKeyButton.SetEnabled(false);
                        await AuthStartPage();
                        return;
                    }
                    Fail(_TF("Failed to login.", "Error message displayed when a sign in failed"));
                }
                catch (e) {
                    Fail(_TF("Failed to login.", "Error message displayed when a sign in failed") + "\n\n" + e);
                }
                finally {
                    button.StopWorking();
                }
                await setStatus(uname.value, pwd.value);
                SetTemporaryInnerText(info, _TF("Failed to login, try again!?", "Message displayed when a sign in failed"), 5000);
                uname.readOnly = false;
                pwd.readOnly = false;
            }, false);
        }
        setStatus(uname.value, pwd.value);
        uname.focus();

    });

}

// Common UI

function GenPassword(length, minUpper, minLower, minDigits, minSpecials) {
    if ((!length) || (length < 8))
        length = 16;
    if ((!minUpper) || (minUpper < 0))
        minUpper = 4;
    if ((!minLower) || (minLower < 0))
        minLower = 4;
    if ((!minDigits) || (minDigits < 0))
        minDigits = 4;
    if ((!minSpecials) || (minSpecials < 0))
        minSpecials = 0;
    if (length > 256)
        length = 256;
    const minTotal = minUpper + minLower + minDigits;
    if (length < minTotal)
        length = minTotal;
    const lower = "abcdefghjkmnpqrstuvwxyz";
    const upper = "ACDEFGHJKLMNOPQRSTUVWXY";
    const digits = "2345679";
    const specials = "!.?_";
    const all = lower + upper + digits + (minSpecials > 0 ? specials : "");
    const lengths = [lower.length, upper.length, digits.length, specials.length, all.length, length];
    const masks = [];
    for (let i = 0; i < 6; ++i) {
        const l = lengths[i];
        let m = 1;
        while (m < l)
            m += m;
        masks[i] = m - 1;
    }
    function getRandom(cl, mask) {
        const ri = CryptRandomInt32();
        let t = ri & mask;
        if (t < cl)
            return t;
        t = (ri >> 8) & mask;
        if (t < cl)
            return t;
        t = (ri >> 16) & mask;
        if (t < cl)
            return t;
        t = (ri & 0xffffff) % cl;
        return t;
    }

    function getRandomChar(chrs, cl, mask) {
        return chrs.charAt(getRandom(cl, mask));
    }

    const pwd = [];
    function Add(chrs, count, index) {
        const mask = masks[index];
        const cl = lengths[index];
        for (let i = 0; i < count; ++i)
            pwd.push(getRandomChar(chrs, cl, mask));
    }
    Add(lower, minLower, 0);
    Add(upper, minUpper, 1);
    Add(digits, minDigits, 2);
    Add(specials, minSpecials, 3);
    Add(all, length - minTotal, 4);
    const sc = length * 4;
    const mask = masks[5];
    for (let i = 0; i < sc; ++i) {
        const a = getRandom(length, mask);
        const b = getRandom(length, mask);
        const t = pwd[a];
        pwd[a] = pwd[b];
        pwd[b] = t;
    }
    return pwd.join("");
}

function CreateAuthWrapper() {
    const box = document.createElement("form");
    box.onsubmit = () => false;
    box.classList.add("SysWeaver-AuthBox");
    return box;

}


async function AuthPage(icon, build, iconIsFile) {
    SysWeaverIgnoreUserChanges();
    const box = CreateAuthWrapper();
    try {
        const target = document.body;
        box.OnLoaded = () => {
            PageLoaded();
            box.OnLoaded = () => { };
        };

        target.appendChild(box);
        let img = null;
        if (icon) {
            if ((icon.indexOf('.') > 0) || (icon.indexOf('/') > 0) || iconIsFile) {
                img = document.createElement("img");
                img.draggable = false;
                img.src = icon;
                box.appendChild(img);
            } else {
                img = new ColorIcon(icon, "IconColorThemeMain", 300, 300);
                box.appendChild(img.Element);
            }
        }
        if (build)
            await build(box, img);
        box.OnLoaded();
    }
    catch (e) {
        box.OnLoaded();
        Fail(e);
    }
}


async function AuthPageValidation(authPageElement, messageHtml, linkOnSuccess, timeout, instructions, placeHolder, buttonText) {
    if ((!timeout) || (timeout <= 0))
        timeout = 15 * 60;
    if (!instructions)
        instructions = _TF("Code", "Label text for an input box where the user should enter a short code sent through mail or sms");
    if (!placeHolder)
        placeHolder = _TF("Enter the sent code", "Placeholder text for an input box where the user should enter a short code sent through mail or sms");
    if (!buttonText)
        buttonText = _TF("Continue", "Text of a button that when clicked will apply a short code that was sent through mail or sms");
    while (authPageElement.childElementCount > 1)
        authPageElement.lastElementChild.remove();
    let icon = null;
    const cl = authPageElement.firstElementChild.classList;
    const cll = cl.length;
    for (let i = 0; i < cll; ++i) {
        const cin = cl[i];
        if (cin.startsWith("IconColor"))
            continue;
        icon = cin;
        break;
    }

    if (messageHtml) {
        AuthText(authPageElement, messageHtml, null, true);
        AuthBr(authPageElement);
    }
    if (instructions)
        AuthLabel(authPageElement, instructions, instructions, false);
    let prevCode = null;


    const code = AuthInput(authPageElement, placeHolder, placeHolder, null, ev => {
        const shortCode = cleanUpShortCode(code.value, true);
        if (code.value !== shortCode)
            code.value = shortCode;
        if (prevCode === shortCode)
            return;
        prevCode = shortCode;
        AuthSetText(err, "");
        conButton.SetEnabled(cleanUpShortCode(shortCode) !== null);
    });
    code.minLength = 3;
    code.maxLength = 16;
    const buttons = AuthButtonRow(authPageElement);
    const conButton = AuthButton(buttons, buttonText, buttonText, icon, async button => {
        button.StartWorking();
        button.SetEnabled(false);
        try {
            const shortCode = cleanUpShortCode(code.value);
            if (!shortCode) {
                code.focus();
                throw new Error(_TF("Invalid code syntax", "Error message displayed when an entered short code has an invalid syntax"));
            }
            const valid = await validateShortCode(shortCode);
            if (valid < 0) {
                code.disabled = true;
                throw new Error(_TF("Code expired or too many tries", "Error message displayed when an entered short code have expired or if it's being used multiple times"));
            }
            if (valid <= 0) {
                code.focus();
                throw new Error(_TF("Invalid code, try again", "Error message displayed when an entered short code is invalid"));
            }
            const url = linkOnSuccess + shortCode;
            Open(url, "_self");
        }
        catch (e) {
            AuthSetText(err, e.message);
            button.StopWorking();
        }
    }, true);
    const err = AuthError(authPageElement, "", "");
    AuthSetError(err, true);
    code.focus();
}

function AuthUserHtml(emailOrUsernameArray) {
    if (typeof emailOrUsernameArray === "string")
        return "<em>" + makeHtmlSafe(emailOrUsernameArray.trim()) + "</em>";
    const t = [];
    emailOrUsernameArray.forEach(v => t.push(makeHtmlSafe(v.trim())));
    return "<em>" + t.join("</em>, <em>") + "</em>";
}

function AuthText(target, text, title, isHtml) {
    const e = document.createElement("SysWeaver-AuthText");
    target.appendChild(e);
    if (text)
        if (isHtml)
            e.innerHTML = text;
        else
            e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

function AuthLabel(target, text, title, isHtml) {
    const e = document.createElement("SysWeaver-AuthLabel");
    target.appendChild(e);
    if (text)
        if (isHtml)
            e.innerHTML = text;
        else
            e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

/**
 * Create a "tab" bar
 * @param {HtmlElement} target where to add the tab bar
 * @param {string} title the tool tip to show on hover (if any)
 * @param {function(number)} onChangeFn the function that gets called whenever a tab is changed
 * @returns {HtmlElement} the created element
 */
function AuthTab(target, title, onChangeFn) {
    const e = document.createElement("SysWeaver-AuthTab");
    target.appendChild(e);
    if (title)
        e.title = title;
    e.Selected = -1;
    const headers = [];
    function activateIndex(newIndex, noFn) {
        e.Selected = newIndex;
        const hl = headers.length;
        for (let i = 0; i < hl; ++i) {
            const h = headers[i];
            if (i === newIndex) {
                if (h.Icon)
                    h.Icon.ChangeColor("IconColorThemeBackground");
                h.classList.add("Selected");
                h.tabIndex = -1;
            } else {
                if (h.Icon)
                    h.Icon.ChangeColor("IconColorThemeAcc2");
                h.classList.remove("Selected");
                h.tabIndex = 0;
            }
        }
        if (!noFn)
            onChangeFn(newIndex);
    }


    e.Add = (text, title, isHtml, icon) => {
        const t = document.createElement("SysWeaver-AuthTabHeader");
        keyboardClick(t);
        if (isHtml)
            t.innerHTML = text;
        else
            t.innerText = text;
        if (title)
            t.title = title;
        e.appendChild(t);
        const index = headers.length;
        const isSelected = e.Selected < 0;
        if (isSelected) {
            e.Selected = index;
            t.tabIndex = -1;
            t.classList.add("Selected");
        }
        if (icon) {
            icon = new ColorIcon(icon, isSelected ? "IconColorThemeBackground" : "IconColorThemeAcc2", 24, 24);
            t.appendChild(icon.Element);
            t.Icon = icon;
        }
        headers.push(t);
        t.onclick = ev => {
            if (badClick(ev))
                return;
            activateIndex(index);
        };
    };
    e.SetIndex = activateIndex;



    return e;
}

function AuthError(target, text, title) {
    const e = document.createElement("SysWeaver-AuthError");
    target.appendChild(e);
    if (text)
        e.innerText = text;
    if (title)
        e.title = title;
    ValueFormat.copyOnClick(e);
    return e;
}

function AuthNewPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, passwordPolicy) {

    const e = AuthPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn);
    let len = 16;
    let minUpper = 1;
    let minLower = 1;
    let minDigits = 1;
    let minSpecials = 0;
    if (passwordPolicy) {
        let minLen = passwordPolicy.MinLength;
        if (minLen < 1)
            minLen = 1;
        let maxLen = passwordPolicy.MaxLength;
        if (maxLen < minLen)
            maxLen = minLen;
        if (maxLen > 128)
            maxLen = 128;
        if (len < minLen)
            len = minLen;
        if (len > maxLen)
            len = maxLen;
        if (passwordPolicy.MixedSpecial)
            minSpecials = 1;
    }
    AuthAddIcon(e, "IconAuthRandom", async inp => {
        const v = GenPassword(len, minUpper, minLower, minDigits, minSpecials);
        inp.value = v;
        await ValueFormat.copyToClipboardInfo(v);
        await inp.OnChange();
    }, null, _TF("Click to generate a new random password and copy it to the clipboard", "Tool tip description of a button that when pressed will generated a random safe password in the input field and also copy it to the clipboard"), null, 1);

    return e;
}


function AuthSetText(target, text, isHtml) {
    target.className = "";
    target.LastCheck = null;
    if (!text) {
        target.innerHTML = "&nbsp;";
        return;
    }
    if (isHtml) {
        target.innerHTML = text;
        return;
    }
    target.innerText = text;
}

function AuthSetError(target, haveError) {
    if (haveError)
        target.classList.add("Error");
    else
        target.classList.remove("Error");
}

function AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle, size, index) {

    if ((!size) || (size <= 0))
        size = 32;
    if ((!index) || (index < 0))
        index = 0;
    const iconSpacing = 4;
    const index0 = index;
    ++index;
    if (!iconEnabledFn)
        iconEnabledFn = e => true;
    const oc = e.onchange;
    async function onChange(ev) {
        b.SetEnabled(iconEnabledFn(e));
        if (oc)
            await oc(ev);
    }
    e.OnChange = onChange;
    let b = null;
    e.onchange = onChange;
    const oi = e.oninput;
    e.oninput = async ev => {
        b.SetEnabled(iconEnabledFn(e));
        b.Element.tabIndex = "-1";
        if (oi)
            await oi(ev);
    };
    e.style.paddingRight = (size * index + (iconSpacing * index0) + 16) + "px";
    b = new ColorIcon(iconClass, "IconColorThemeMain", size, size, iconTitle ?? null, async () => {
        await iconFn(e);
    });
    e["Icon" + index0] = b;
    if (index0 <= 0)
        e.Icon = b;
    b.TabStop = false;
    const be = b.Element;
    const cs = getComputedStyle(e);
    const height = parseFloat(cs.height);
    const mb = parseFloat(cs.marginBottom);
    const p = -0.5 * (height + mb + size);
    be.style.top = p + "px";
    be.style.left = "calc(100% - " + (size * index + index0 * (size + iconSpacing) + 8) + "px)";
    be.classList.add("InsideInput");
    b.SetEnabled(iconEnabledFn(e));
    e.parentElement.appendChild(be);
    //e.appendChild(be);
}

function AuthPassword(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    e.type = "password";
    e.placeholder = placeholder;
    e.autocomplete = autocomplete;
    e.required = true;
    e.minLength = 8;
    e.maxLength = 128;
    target.appendChild(e);
    if (title)
        e.title = title;
    if (onChange) {
        const oc = async () => {
            await onChange(e);
        };
        e.onchange = oc;
        e.oninput = oc;
        e.OnChange = oc;
    }
    tabToNextOnEnter(e, onNextItemFn);
    if (!iconClass) {
        iconClass = "IconAuthView";
        const shw = _TF("Click to show password", "Tool tip description of a button that when clicked will show the password entered in an input box");
        iconFn = inp => {
            const icon = inp.Icon;
            if (inp.type === "password") {
                inp.type = "text";
                icon.ChangeImage("IconAuthViewOff");
                icon.SetTitle(_TF("Click to hide password", "Tool tip description of a button that when clicked will hide the password entered in an input box"));
            }
            else {
                inp.type = "password";
                icon.ChangeImage("IconAuthView");
                icon.SetTitle(shw);
            }
        };
        iconTitle = shw;
    }
    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}

function AuthInput(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    e.type = "text";
    e.placeholder = placeholder;
    if (autocomplete)
        e.autocomplete = autocomplete;
    e.required = true;
    e.minLength = 1;
    e.maxLength = 128;
    target.appendChild(e);
    if (title)
        e.title = title;
    if (onChange) {
        const oc = async () => {
            await onChange(e);
        };
        e.onchange = oc;
        e.oninput = oc;
        e.OnChange = oc;
    }
    tabToNextOnEnter(e, onNextItemFn);

    if (!iconClass)
        iconClass = "IconAuthCopy";
    if (!iconFn) {
        iconFn = async inp => {
            const v = inp.value;
            if (inp)
                await ValueFormat.copyToClipboardInfo(v);
        };
        iconEnabledFn = e => !!e.value;
        iconTitle = _TF("Clip to copy text", "Tool tip description on a button that when clicked will copy the content of an input box to the clipboard");
    }
    if (!iconEnabledFn)
        iconEnabledFn = e => true;

    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}

function AuthInputPhone(target, placeholder, title, autocomplete, onChange, onNextItemFn, iconClass, iconFn, iconEnabledFn, iconTitle) {
    const e = document.createElement("input");
    const group = document.createElement("SysWeaver-AuthPhone");
    group.appendChild(e);
    const cc = document.createElement("SysWeaver-AuthPhoneCc");

    function setCountry(iso, title, allSame) {
        if (!iso)
            iso = "_";
        if (!title)
            title = _TF("Click to select country code prefix", "Tool tip description on a button that when pressed will let the user select a country code calling prefix");
        else {
            title = title + "\n\n" + _TF("Click to select country code prefix", "Tool tip description on a button that when pressed will let the user select a country code calling prefix");
        }
        if (allSame)
            cc.classList.add("AllSame");
        else
            cc.classList.remove("AllSame");
            

        cc.style.backgroundImage = "url('../iso_data/country/" + iso.toUpperCase() + ".svg')";
        cc.title = title;
    }

    e.type = "tel";
    e.placeholder = placeholder;
    e.autocomplete = autocomplete;;
    e.required = true;
    e.minLength = 1;
    e.maxLength = 128;
    e.classList.add("Phone");
    target.appendChild(group);
    if (title)
        e.title = title;
    let isInternal = false;
    let old = null;
    let prefixes = null;
    const oc = async () => {
        if (isInternal)
            return;
        const val = e.value.trim();
        if (val === old)
            return;
        old = val;
        let found = false;
        if (val.length > 0) {
            try {
                prefixes = await sendRequest("../Api/auth/GetPhonePrefixInfo", val);
                if (prefixes) {
                    const pl = prefixes.length;
                    let allSame = true;
                    for (let i = 1; i < pl; ++i) {
                        allSame = prefixes[0].CountryCode === prefixes[i].CountryCode;
                        if (!allSame)
                            break;
                    }
                    let name = "";
                    let fiso = null;
                    for (let i = 0; i < pl; ++i) {
                        const pp = prefixes[i];
                        const iso = pp.IsoCountry;
                        if (iso) {
                            if (!fiso)
                                fiso = iso;
                            if (name.length > 0)
                                name += "\n";
                            name += ("+" + pp.CountryCode + " ");
                            const rp = pp.RegionPrefixes;
                            if (rp) 
                                name += (rp + " ");
                            name += pp.Name;
                        }
                    }
                    if (fiso) {
                        setCountry(fiso, name, allSame);
                        found = true;
                    }
                }
            }
            catch
            {
            }
        }
        if (!found)
            setCountry();
        if (onChange)
            await onChange(e);
    };
    e.onkeydown = evt => {
        if (evt.ctrlKey)
            return;
        if (evt.key.length > 1)
            return;
        if (evt.key === ' ')
            return;
        if (evt.key === '+') {
            if (e.value.indexOf('+') < 0)
                return;
        }
        if (/[0-9.]/.test(evt.key))
            return;
        evt.preventDefault();
    };
    e.onchange = oc;
    e.oninput = oc;
    e.OnChange = oc;
    tabToNextOnEnter(e, onNextItemFn);

    if (!iconClass)
        iconClass = "IconAuthCopy";
    if (!iconFn) {
        iconFn = async inp => {
            const v = inp.value;
            if (inp)
                await ValueFormat.copyToClipboardInfo(v);
        };
        iconEnabledFn = e => !!e.value;
        iconTitle = _TF("Clip to copy text", "Tool tip description on a button that when clicked will copy the content of an input box to the clipboard");
    }
    if (!iconEnabledFn)
        iconEnabledFn = e => true;

        
    group.appendChild(cc);
    setCountry();
    cc.onclick = async (ev) => {
        if (badClick(ev))
            return;

    };
    keyboardClick(cc);
    if (iconClass && (iconClass !== "-"))
        AuthAddIcon(e, iconClass, iconFn, iconEnabledFn, iconTitle);
    return e;
}



function AuthButtonRow(target) {
    const e = document.createElement("SysWeaver-CenterBlock");
    target.appendChild(e);
    return e;
}

function AuthButton(target, name, title, icon, onClick, disabled) {
    const button = new Button(null, name, title, icon, !disabled, async fn => {
        await onClick(button);
    });
    target.appendChild(button.Element);
    return button;
}

function AuthHr(target) {
    target.appendChild(document.createElement("hr"));
}

function AuthBr(target) {
    target.appendChild(document.createElement("br"));
}

async function AuthStartPage() {
    const loc = window.location;
    const url = new URL(loc.href);
    url.search = "";
    const t = url.pathname.split('/');
    t.splice(t.length - 2, 2);
    url.pathname = t.join('/');
    let startUrl = url.href;
    if (!startUrl.endsWith("/"))
        startUrl += "/";
    await delay(3000);
    history.replaceState({}, "", startUrl);
    location.reload(true);
}

function IsAutoFilled(inputElement) {
    try {
        return inputElement.current.matches(':autofill');
    } catch (err) {
        try {
            return inputElement.current.matches(':-webkit-autofill');
        } catch (er) {
        }
    }
    return false;
}