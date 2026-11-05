/**
 * @file core/contractmock.js - the request allow-list of the core contract test (core/contracttest.html): loaded before the core.
 *
 * Purpose: replaces window.fetch so no request of the test leaves the page. Allowed: the mocked test apis below "Api/ct/" (fixed
 * answers: json, slow answers, refusals with X-Sw-Reason / X-Sw-Outcome / X-Sw-Denied, a 500), "serverTime" (answered here) and GET of
 * static files of this site that are not apis (.json / .js / .css / .svg / .png / .html). Anything else (a write, any other api, another
 * site) is answered 404 here, never sent, and listed in window.SwContractMock.Blocked.
 * Dependencies: none. Globals defined: window.SwContractMock { Blocked, Calls, Route(url) }.
 */
(function () {
    "use strict";

    const realFetch = window.fetch.bind(window);
    const mock = window.SwContractMock = { Blocked: [], Calls: [], Errors: [] };

    //  A library that doesn't load (a syntax error in a source or a bundle, ex: a name declared twice) is a failure of the test
    window.addEventListener("error", e => {
        mock.Errors.push((e.filename ? e.filename.replace(location.origin, "") + ":" + e.lineno + " " : "") + (e.message || "error"));
    });

    /**
     * A mocked answer.
     * @param {number} status The status.
     * @param {*} body The body (an object: json).
     * @param {Object<string, string>} [headers] Extra headers.
     * @returns {Response} The response.
     */
    function answer(status, body, headers) {
        const text = typeof body === "string" ? body : JSON.stringify(body);
        const h = Object.assign({ "Content-Type": typeof body === "string" ? "text/plain; charset=utf-8" : "application/json" }, headers || {});
        return new Response(text, { status, statusText: status === 200 ? "OK" : (status === 404 ? "Not Found" : (status === 409 ? "Conflict" : (status === 403 ? "Forbidden" : (status === 410 ? "Gone" : "Error")))), headers: h });
    }

    /**
     * Wait, abortable like fetch.
     * @param {number} ms The time.
     * @param {AbortSignal} [signal] The signal.
     * @returns {Promise<void>}
     */
    function wait(ms, signal) {
        return new Promise((resolve, reject) => {
            const t = setTimeout(resolve, ms);
            if (signal)
                signal.addEventListener("abort", () => {
                    clearTimeout(t);
                    reject(signal.reason || new DOMException("Aborted", "AbortError"));
                }, { once: true });
        });
    }

    window.fetch = async function (input, init) {
        const url = new URL(typeof input === "string" ? input : input.url, location.href);
        const method = String((init && init.method) || (input && typeof input === "object" && input.method) || "GET").toUpperCase();
        const signal = (init && init.signal) || (input && typeof input === "object" ? input.signal : null);
        const path = url.pathname;
        mock.Calls.push({ Method: method, Url: url.href });
        if (signal && signal.aborted)
            throw signal.reason || new DOMException("Aborted", "AbortError");
        //  The test apis
        const m = /\/Api\/ct\/([A-Za-z0-9]+)$/.exec(path);
        if (m && url.origin === location.origin) {
            const name = m[1];
            let arg = null;
            try {
                const q = decodeURIComponent(url.search.substring(1));
                arg = q ? JSON.parse(q) : (input && typeof input === "object" && typeof input.clone === "function" && method === "POST" ? await input.clone().json() : null);
            }
            catch {
                arg = null;
            }
            switch (name) {
                case "Echo": return answer(200, { Echo: arg, Method: method });
                case "Slow": await wait(arg && arg.Ms ? arg.Ms : 600, signal); return answer(200, { Q: arg && arg.Q });
                case "Refused": return answer(409, "Process #42 is protected by policy.", { "X-Sw-Reason": "1" });
                case "Already": return answer(409, "Process #42 has already ended.", { "X-Sw-Reason": "1", "X-Sw-Outcome": "AlreadyDone" });
                case "Gone": return answer(410, "The invitation was withdrawn.", { "X-Sw-Reason": "1", "X-Sw-Outcome": "Gone" });
                case "NotFound": return answer(404, "The table to export was not found.", { "X-Sw-Reason": "1" });
                case "Role": return answer(403, "Requires Admin.", { "X-Sw-Reason": "1", "X-Sw-Denied": "role" });
                case "Invalid": return answer(400, "The name is too long.", { "X-Sw-Reason": "1", "X-Sw-Outcome": "Invalid" });
                case "Fail": return answer(500, "System.Exception: disk full\n   at X.Y()");
                default: return answer(404, "");
            }
        }
        if (/\/serverTime$/i.test(path) && url.origin === location.origin)
            return answer(200, String(Date.now()));
        //  Static files of this site (not apis): read only
        if (method === "GET" && url.origin === location.origin && !/\/Api\//i.test(path) && /\.(?:json|js|css|svg|png|html)$/i.test(path))
            return realFetch(input, init);
        mock.Blocked.push(method + " " + url.href);
        return answer(404, "");
    };
})();
