# SysWeaver.HttpServer

[⬆ SysWeaver overview](../README.md)

> The server-agnostic heart of SysWeaver's web layer: request pipeline, module routing, the Web API engine, static/embedded/disc file serving, sessions and authentication hooks, caching, compression, templates, push messages, translation and the built-in web application shell.

| | |
|---|---|
| **Layer** | Web platform |
| **Kind** | Library |

## Purpose

Implement *everything* a SysWeaver web server does once, independent of the socket technology. Concrete listeners ([SysWeaver.NetHttpServer](../SysWeaver.NetHttpServer/README.md) on HttpListener, [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md) on Kestrel) only adapt requests into this pipeline; [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md) wires it into the service manager.

## How it fits into SysWeaver

```mermaid
flowchart TB
  Listener["Listener<br/>HttpListener or Kestrel"] --> Base["HttpServerBase pipeline"]
  Base --> Sess["Sessions, languages, auth"]
  Base --> Mods["Modules"]
  Mods --> ApiM["API module<br/>WebApi methods of all services"]
  Mods --> Static["Embedded web assets"]
  Mods --> Disc["Folders on disc"]
  Mods --> Redir["Redirects and proxies"]
  Mods --> Other["Service provided modules<br/>upload, chat, thumbnails, ..."]
  Base --> Trans["Transformers<br/>cached conversions by extension or MIME type"]
  Base --> Push["Push messages<br/>long polling"]
```

A request, end to end:

```mermaid
sequenceDiagram
  participant C as Client
  participant L as Listener
  participant S as HttpServerBase
  participant A as AuthManager
  participant M as Module / API entry
  participant X as Service method
  C->>L: HTTP request
  L->>S: adapt request
  S->>S: session cookie, language, rate limits
  S->>M: find handler by path
  M->>A: check required tokens if any
  A-->>M: allowed / denied
  M->>M: server cache hit?
  M->>X: invoke with deserialized parameter
  X-->>M: result
  M->>M: serialize per Accept, compress per Accept-Encoding
  M-->>C: response with cache headers
```

## Key features

- **Modules:** units that claim URL paths and produce request handlers (`IHttpServerModule`, or `IHttpServerRawModule`, which bypasses the whole pipeline including auth). Any registered service implementing a module interface is attached automatically by the server service.
- **Web API engine:** discovers `[WebApi]` methods on service instances and compiles fast invokers. URLs are `<root>/<class url>/<method>` (root defaults to `Api`, class url from `[WebApiUrl]`, else the type name), matched exactly and case sensitively. A method takes at most one input parameter (optionally followed by an `HttpServerRequest`, or a `SysWeaver.Auth.IAuthorizedRequest` for services that can't reference the http server); GET input is the whole query string (a single JSON value, XML, form data or compressed binary), POST input is the body, deserialized per `Content-Type`. Results are serialized per `Accept`, falling back to the default serializer. Caching, compression, rate limits and auditing are declared with attributes.
- **Sessions and auth:** cookie sessions (`HttpSession`) and device ids; authentication delegated to an `AuthManager` when registered; login redirect for protected pages; API keys via the `Authorization` header (Basic / Bearer) or the `x-api-key` / `x-goog-api-key` headers (which header logins are accepted is up to the authorizer: `SimpleAuthorizer` only accepts API keys unless `AllowBasicAuth` is set); an `Authorization` with `AllowedNetworks` (ex: an API key restricted to some addresses) is checked against the client address on every request, also when the session cookie of a header sign in is used (403 otherwise). Auth per end point is `null` (open), `""` (any logged in user, except API keys: `Authorization.IsApiKey`), `"-"` (explicitly open) or a comma separated list of tokens (any one suffices); for APIs method `[WebApiAuth]` > type `[WebApiAuth]` > module default, with `IRunTimeWebApiAuth` overrides on top.
- **Session security:** the session and device id cookies are `HttpOnly` with `SameSite=Lax` (`CookieSameSite`: `Lax`, `Strict` or empty) and `Secure` on https requests (`HttpServerRequest.IsHttps`: the connection is https, or a trusted proxy that ended the TLS says so with `X-Forwarded-Proto: https`; `SecureCookies` adds it always; `CorsCookies` uses `SameSite=None;Secure` instead); the session cookie lives 7 days by default (`SessionCookieLifetime`; the server side session expires after `SessionExtendLifetime` minutes without use anyway); the session token is rotated on login; short-lived anonymous sessions expire quickly; `auth/redirect` only redirects to relative or same-origin URLs.
- **Cross site request forgery protection** (`CsrfProtection`, on by default): an API call or `logout` of a signed in session, without header auth and not an API key, is refused with a 403 when the browser says it comes from another site. That is `Sec-Fetch-Site` `cross-site` / `same-site`, else an `Origin` / `Referer` host that isn't the site. A request is exempt when it has the `X-Sw-Api` header (`HttpServerBase.ApiRequestHeader`), which `Sw.Net` adds to every same origin request. Refusals are counted in the `CrossSite.*` statistics.
- **Session ids:** the session token (the value of the session cookie) is a secret that is only kept as the key of the server's session map (`HttpSession.Token` is internal). Everything else identifies a session by `HttpSession.TokenHash`: base64url of the first 192 bits of SHA256(UTF8(token)), 32 chars (`HttpSession.HashToken`), a second map `TokenHash => session` is kept in sync (create, token rotation on login, close, expiry). The id is used by the active sessions and active users tables (hidden `TokenHash` column, safe to filter and sort), the audits (`Session`), data references and `ToString()`; it changes when the token is rotated on login. Find a session from its id with `HttpServerBase.FindSessionByTokenHash` or `Api/debug/SessionDetails?"<id>"` (Ops, active and recently ended sessions).
- **Intrusion detection** (`IntrusionDetection`, `HttpServerBaseParams.IntrusionDetection`, on by default): per client address (IPv6 by /64, kept 5 minutes after the last tracked request) it counts error responses (400, 401, 403, 404, 405) by distinct url, and calls to key API's (all audited API's and the ones with `[WebApiIntrusionTrack]`; `[WebApiIntrusionTrack(false)]` opts an audited API out). Above a threshold (50 urls / 50 calls) an intrusion is reported at most once per 5 minutes and address: an audit entry (group "intrusion", the input is the details and the urls, max 2 KB, the output is a short json summary), a problem (`HttpServer Intrusion`), a warning in the log and the `OnIntrusion` event. Error responses get a random 200-2000 ms delay before the body is written, and so do tracked API calls from an address that was reported within the window. Per address tracking catches a single scanner or guesser, not a distributed attack.
- **Probe trap** (`HttpProbeTrapParams`, `HttpServerBaseParams.ProbeTrap`, on by default, null disables it):
  - **Trigger:** a request that hits no end point, for an url that only probes ask for. The built-in list is [srcData/ProbeUrls.txt](srcData/ProbeUrls.txt) (`.env*`, `.git/*`, cloud and docker credentials, terraform state, keys, dumps), plus `ExtraUrls`, plus every url that `ProbeDecoy.Detect` knows as a secret file. Matching is case insensitive, also below any folder, and ignores the query.
  - **Decoys** (`UseDecoys`, on by default): a probe for a known secret file gets a believable file instead of the hostile answer ([`ProbeDecoy`](ProbeDecoy.cs)). A strange answer tells a probe it found a honeypot; a working looking secret makes it spend time using it.
    - **Kinds:** `.env`, AWS credentials, git / hg / svn internals (including a valid empty git index), terraform state and variables, kube and docker configs, compose files, npm / pypi / netrc / pgpass / htpasswd / my.cnf, backups of `wp-config.php` and other php configs, `appsettings*.json`, `web.config`, Rails configs and `master.key`, SQL dumps, GCP service accounts (real RSA keys), sftp / FileZilla configs, ssh keys (real RSA keys with matching public keys), shell histories.
    - **A normal answer:** a short delay (`DecoyMinDelayMs` - `DecoyMaxDelayMs`, 20-180 ms), the usual headers (`Last-Modified`, an nginx style `ETag`), no marker of any kind.
    - **Values:** derived (HMAC) from a secret of the installation (`HttpServer.ProbeDecoySecret` in the application key value store). They are stable across restarts but can't be computed by anyone else, so they can't be used to recognize a SysWeaver decoy.
    - **Identities:** a client address always gets the same identity (one of `DecoyIdentities`, 512), so every file it gets tells the same story, and the identity tells what client got a value. Every served decoy is logged ("Probe decoy: identity N (AWS key AKIA...) served to <ip> for <url>"). `ProbeDecoy.FindIdentity(value)` finds the identity of a value from an alert or a leak.
    - **Real tripwires:** list AWS keys of IAM users without any permissions in `CanaryAwsKeys` ("AKIA...:secret"). They replace the generated (inert) keys, round robin over the identities. A use then shows up in CloudTrail (AccessDenied, with the thief's address and user agent), and the log tells what client got the key and when. canarytokens.org AWS keys work too.
    - Other probes of a banned address for secret files get decoys of the same identity.
  - **The answer** (any other probe url, and any other request of a banned client):
    - A random 3-9 s delay.
    - Status 200.
    - Hostile but valid headers: a fake old server stack, huge validators, links and a refresh to more trap urls, about 32 KB of junk headers, and 20 large cookies that expire with the ban.
    - A pre-compressed decompression bomb ([trapData](trapData), written once) in the best accepted encoding: brotli 10 GB from 17 KB, zstd 1 GB from 90 KB, gzip / deflate 50 MB from 127 KB. Without an accepted encoding the body is 1 MB of the message. HEAD gets no body.
  - **The ban:** the address (IPv6 by /64, except `NeverBan`, default loopback) and the session are banned for `BanMinutes` (60). A banned address gets no session (checked in `CreateSession`, nothing is looked up while no address is banned); a banned session costs one field read per request. Every request of a banned address or session gets the trap answer.
  - **Development hosts:** a request for a host in `NeverTrapHosts` (default `localhost` and `dev.sysweaver.com`, which resolves to 127.0.0.1) from a `NeverBan` address is never trapped: a probe url gets a normal 404 and nothing is banned. Both must match, the Host header alone is client controlled.
  - **Limits:** at most `MaxConcurrent` (1000) answers are delayed at once; above that, answers are sent at once and empty.
  - **Statistics:** `Probe.*` (probe hits), `Decoy.*` (decoy files served), `Banned.*` (requests of banned clients), `Banned.Addresses` and `NotFound.*` (other 404's): count, time, url and address of the last one.
- **Rate limits:** `[WebApiIpRateLimit]` limits an API per client address (`HttpIpRateLimiters`), checked before the per service and per session limits. The built-in end points declare their limits with the end point (`HttpEndPointRateLimits`, per address first, then shared): `auth/redirect` (sign in with a one time token) allows 300 requests per minute for the whole server (`AuthRedirectLimits`) and 20 per minute per address (`AuthRedirectIpLimits`), null disables a limit. Proxy end points fail closed (a protected end point is never served without a session that has the required tokens).
- **Not signed in / not allowed:** a protected API called without any auth answers **401** (with `X-Login-Url: <sign in page>` and `WWW-Authenticate: SysWeaver ...`, not `Basic`, so browsers don't show a password dialog); a browser navigation to an API url (`Sec-Fetch-Mode: navigate`) and other protected resources (pages, files) still get the **302** to `AuthRedirect`. A signed in user without the required tokens gets **403** "You are not allowed to do this: your account doesn't have the role it needs". `UserNotAllowedException` / `NoUserLoggedInException` thrown by a handler become 403 / 401 (not 500). `Api/auth/CanCall(string[] urls)` -> `{Url, Found, Allowed, Auth}[]` (open, max 128 urls) tells a user interface which APIs the current session may call (used by `Sw.Access`, core/access.js); `Api/debug/GetApiInfo` (the full signature) is restricted to Dev and Debug.
- **Refusal reasons (`X-Sw-Reason: 1`):** a response whose text was written for people (translated to the session language) carries the header `X-Sw-Reason: 1`, for any status: every `HttpResponseException` thrown with a message (`IsReason`, default true when a message is given; false for relayed texts), the 403 role refusal and the 401 "Sign in to do this" of an API. Clients show the text as the reason ("why") instead of a generic sentence for the status (core/net.js). Write the message as a sentence for the person that asked, without "Forbidden - " prefixes or " [403]" suffixes, ex: `throw new HttpResponseException(409, "The invitation could not be withdrawn: it was already used, has expired or was withdrawn")`.
- **Denied by role (`X-Sw-Denied`):** the reason text and the kind of refusal are separate signals. A 403 because the user (or visitor) lacks the role an end point needs adds `X-Sw-Denied: role` (`DeniedHeader`, `DeniedRole`), the server's own 401 "Sign in to do this" adds `X-Sw-Denied: signin`; every other refusal (a 403 / 409 / 400 with a reason from an end point) has none. A client disables the control that asked only on `role` (the user can never do it), whatever the reason text.
- **Outcome kind (`X-Sw-Outcome`):** every 4xx refusal (an `HttpResponseException`, an `IHttpRefusal`, the server's own 401 / role 403) also says what it means for the person that asked, so a client never guesses it from the status (`OutcomeHeader`, values `HttpOutcomes`): `AlreadyDone` (the goal is already met: "not needed"; only when the code that refused says so: `throw HttpResponseException.AlreadyDone("Process #12 has already ended")`), `Refused` (not done, the reason says why: the default of every 4xx except 401 / 404 / 410 / a missing role, also `HttpResponseException.Refused(...)`), `Gone` (the target no longer exists: the default of 404 / 410, also `HttpResponseException.Gone(...)` for a 409), `Denied` (401, or with `X-Sw-Denied`). No header on 2xx / 5xx. An `IHttpRefusal` type may give its kind (`Outcome`). Examples: Kill of an ended process and End session of an ended session = AlreadyDone, End session of your own session = Refused. Input that can't be read is a refusal too, never a 500 server problem: an unknown body type 415, a body / query that doesn't parse 400, both with the reason.
- **Refusals declared by the exception type:** an exception that implements `IHttpRefusal` (SysWeaver.Common, `ResponseCode` 4xx) answers that status with its message as the reason, like an `HttpResponseException` (ex: the UserManager account exceptions: a dead link 410, an address in use 409), never a 500 and never a problem.
- **A client that leaves is no problem:** when the client closes the connection while the request is handled or the response is written (an aborted fetch, a closed tab, a lost network: `HttpListenerException` 22 / 64 / 995 / 1229 / 1236, a socket reset, Kestrel's connection reset), the server counts it in the `ClientGone` statistic and nothing else: it is not logged (clients end long polls like `GetMessages` all the time) and not counted by an exception tracker, so it never shows as a "Failing" problem with a stack trace (`HttpServerBase.IsClientGone`).
- **End a session:** `Api/debug/EndSession` (Admin, Ops) signs the user of another session out (the browser gets a new anonymous session on its next request); the token comes from the "End session" row action of the active sessions table (an HMAC of the session id, random key per process: it can't be guessed, and a crafted link or an image GET can't use it); your own session can't be ended there (sign out instead).
- **Background jobs** (`HttpServerBase.Jobs`, [`BackgroundJobs`](BackgroundJobs.cs)): long work whose result must survive a page reload (an export, a report). A service starts one from its own API with `request.Server.StartJob(request, "Exporting x.csv", async ctx => { ctx.Report(0.5, "Reading the rows"); ...; return memoryFile; }, key, "export")` and returns the `BackgroundJobInfo` (`{Id, Key, Kind, Title, State: "running"|"done"|"failed"|"cancelled", Progress 0..1|null, Text, Error, Started, Ended, Expires, ResultName, ResultMime, ResultSize, ResultUrl, IsNew, Removed}`); the work must not use the original request (use `ctx.Request`, a detached copy) and should honour `ctx.Cancel`. A job belongs to the signed in user (every session of the user sees it) or, for a visitor, to the session; one running job per `key` and owner (the running one is returned, `IsNew` false); at most 4 running and 20 kept per owner (429 with a reason beyond that); a job and its result are kept 30 minutes after it ended (`KeepFor`), in memory only. The client APIs (open, only the caller's jobs, another owner's id is a 404): `Api/jobs/GetJobs` (after a reload), `GetJob?"id"` (poll), `WaitJob {Id, Ms}` (long poll, max 25 s), `GetJobResult?"id"` (the `MemoryFile`, again until it expires; 409 with the reason while running / failed / cancelled), `CancelJob?"id"`, `RemoveJob?"id"`; the push message `sw.job` (`{Job}`, on start, progress (a new `Text` at once, `Progress` at most once per `ProgressPushInterval`, 1 s), stop asked, end and remove) goes to every session of the owner, so other tabs and devices of the user follow a job without a reload (`Sw.Session.Subscribe("sw.job", m => ...)`). A `CancelJob` that comes after the job is done (too late) sets `CancelRequested: true` on the done job (pushed), so every device shows "finished before it could stop". Example user: `Api/StartExportTableApi` / `Api/StartExportTableData` (SysWeaver.MicroService.HttpServer), used by the data table export (explore/table.js). **Honest cancel:** `CancelJob` (and `RemoveJob` of a running job) asks the work to stop (`ctx.Cancel`); the job stays `"running"` with `Stopping: true` and keeps its running slot until the work returned, then it is `"cancelled"` (or `"done"` with `CancelRequested: true` when the work finished before it could stop; a removed job is removed then). **Announced:** `Api/jobs/AckJob?"id"` (and fetching the result) sets `Announced: true`: a client announces an ended job only when Announced is false, so restored jobs (reload, another session or device) stay quiet. **Detached request:** the work gets `ctx.Request`, a copy of the starting request (session, user, url, address) valid until the job ended: use it instead of the original request. **Any result:** `request.Server.StartWork(request, title, async ctx => BackgroundJobResult.FromText("1 204 users imported") / FromFile(file) / null, key, kind)`; the job then has `ResultText` (and no `ResultUrl` without a file).
- **Row action outcomes:** an API behind a table row action (`TableDataActions` / `TableDataActionsEx`) returns true when done and throws `HttpResponseException(409, reason)` when it can't be done (gone, already used, already ended), never a bare false: the reason is translated, shown in the row and not counted as a server problem (500s are). `Api/debug/EndSession` does so ("The session has already ended ..."), and the active sessions table has a hidden `IsCurrent` column (true on the caller's own session, for a "you" mark).
- **`OptimizedSend`** (`HttpServerBaseParams`, default false): debug builds of the web client POST every api argument; true makes them use the release GET encodings too (`?json`, `_u`, `_d` when small enough), to test them while debugging (published in the templated `app/manifest.json` as `sysweaver_optimized_send`).
- **Framing (clickjacking):** every response has `Content-Security-Policy: frame-ancestors 'self'` and `X-Frame-Options: SAMEORIGIN` by default. `HttpServerBaseParams.FrameAncestors` (in the `*.Services.json` params of the http server service) sets the sources that may frame the site: `'none'` (also `X-Frame-Options: DENY`), a space separated list such as `'self' https://portal.mydomain.com` (no `X-Frame-Options`), or empty / `*` for no header. A host that embeds SysWeaver pages from another site needs that site here (and `CorsCookies` for the session cookie).
- **Content serving:** embedded resources, disc folders (`FileHttpServerModule`), in-memory content, redirects and forwarding to other servers (`HttpProxy`: multiple end points, memory and disc caching of the compressed source data, ETag revalidation and stale data while the source is offline; `FileProxy` is a single end point wrapper kept for existing configs), with pre-compressed variants, ETags / 304, client cache headers and server-side response caches (global or per session).
- **Templates:** text files matching template patterns (by default `index.html`, `debug.html`, `app/manifest.json`, `app/Home.html`, `common/theme.css`) get `${Variable}` substitution. Only server defined variables are replaced (static ones such as colors and configured `Key=Value` pairs, per-request ones such as `Session.Lang` or `Session.UserName`, and groups like `Env.*`, `EnvInfo.*` or service-provided groups); other `${...}` text, e.g. JavaScript template literals, is kept. Encoding modifiers (`#` html text, `@` attributes, `$` JavaScript / JSON strings, `£` JavaScript values) must be used to match the context.
- **Transformers:** services implementing `IHttpTransformerService` register conversions by file extension or MIME type; `CachedTransformer` is the base for transformers whose results (smaller or better supported variants) are cached on disc. They run for non-dynamic responses with an ETag; a query string starting with `raw` bypasses them.
- **Push messages:** a long-poll channel through which the server pushes messages to a session, a user, all logged in users or all sessions.
- **Translation:** with a translator registered, responses and web assets are translated to the session language. `Api/application/GetSupportedLanguages`, `GetLocalizedLanguages`, `GetLanguage` and `SetLanguage` read and change the session language; `GetSelectableLanguages` returns the localized languages only when a translator is registered (null otherwise: the app shell then offers no language choice).
- **Built-in end points:** `logout`, `auth/redirect`, `auth/logout_user` and `serverTime`, plus fall-backs used when no module serves them (`login`, `basic_auth`, the generated app icon, logo and banner and the bitmaps rendered from them, see [App icon, logo and banner](#app-icon-logo-and-banner), empty `app/app.css` / `app/app.js`).
- **Web application shell:** an embedded single-page application (the client core `Sw`, the UI component library, the shell with header, menu and content frame, home and welcome pages, themes and file viewers, see [`srcWeb/`](srcWeb/README.md)) that other projects extend with their own pages and menus.
- **HTTPS and firewall:** certificate binding from certificate provider services (`CertificateBinder`) and optional firewall rules per listen prefix (`FirewallHandler.Instance` picks `WindowsFirewallHandler`, `LinuxFirewallHandler` or `NoFirewallHandler`).
- **Diagnostics:** tables of APIs, sessions, users, caches, MIME types, template variables and transformer caches.
- **Audit:** services implementing `IApiAuditService` receive the calls to methods marked `[WebApiAudit]`, services implementing `IApiAuditReportService` make them searchable in the audit browser ([`srcWeb/audit`](srcWeb/audit/README.md)).
- **In-process API invocation** (`IApiHttpServerEndPoint.InvokeAsync`) for AI tools and other services. It performs no auth, rate limit or cache checks, so callers must check `Auth` themselves.

## App icon, logo and banner

The server always has three source images, served in the site root:

| File | Aspect | Example size | Generated when missing (`SvgScene`) |
|---|---|---|---|
| `icon.svg` | 1:1 | 256 x 256 | `AddAppIcon(AppDisplayName, colors)` |
| `logo.svg` | 4:3 | 512 x 384 | `AddAppLogo(AppDisplayName, AppDescription, colors)` |
| `banner.svg` | 9:2 | 1260 x 280 | `AddAppBanner(AppDisplayName, AppDescription, colors)` |

These are optional end points: they are generated only if no module serves the file, e.g. an embedded `web/icon.svg` in any project or a file in a folder served from disc (`FileHttpServerModule`). The generated images use the app's display name, description and colors (`HashColors.AppColors`, derived from the app name).

All other icons and logos are rendered from these three files, whether they are provided or generated. They are never needed as files:

| File | Size | Rendered from |
|---|---|---|
| `favicon.ico` | 192 x 192, 48 x 48, 32 x 32 and 16 x 16 (four png images in one file) | `icon.svg` |
| `apple-touch-icon.png` | 180 x 180 | `icon.svg` |
| `icon-180.png` | 180 x 180 | `icon.svg` |
| `icon-192.png` | 192 x 192 | `icon.svg` |
| `icon-512.png` | 512 x 512 | `icon.svg` |
| `logo.png` | 1024 x 768 | `logo.svg` |
| `banner.png` | 1260 x 280 | `banner.svg` |

A module can still serve any of these bitmaps itself, which overrides the rendered one. To brand an application, provide the three svg files and let the server render the rest. Keep the aspect ratios, because the bitmaps are rendered at the sizes above.

The rendered bitmaps are cached in memory. The server checks the source svg for changes at most every 15 seconds and renders the bitmaps again when it has changed. With the `debug` token, `icon_debug.svg`, `logo_debug.svg` and `banner_debug.svg` generate the images with another color seed given in the query string (e.g. `icon_debug.svg?42`).

## Key components

- [`HttpServerBase`](HttpServerBase.cs) — the abstract pipeline (partial, split over `HttpServerBase*.cs`), configured by [`HttpServerBaseParams`](HttpServerBaseParams.cs)
- [`ApiHttpServerModule`](ApiHttpServerModule.cs) — the Web API engine, with [`ApiHttpEntry`](ApiHttpEntry.cs) as a compiled API method; a `[WebApiRaw]` method without a mime type returns a `MemoryFile` (`ApiMemoryFile`: the mime type is taken from the file per call, or derived from its name, and a name is sent as an `inline` `Content-Disposition`)
- [`HttpServerRequest`](HttpServerRequest.cs) — the abstract request that the listeners implement: method, url, headers, body, session, client address
- [`HttpSession`](HttpSession.cs) — per client session state, user and push message queue; `TokenHash` is the safe session id
- [`IntrusionDetection`](IntrusionDetection.cs) — per client address tracking of error responses and key API calls (`IntrusionDetectionParams`)
- [`HttpIpRateLimiters`](HttpIpRateLimiters.cs) — the per client address rate limiters of `[WebApiIpRateLimit]`
- [`HttpEndPointRateLimits`](HttpEndPointRateLimits.cs) — the rate limits of the built-in end points (shared and per client address)
- [`HttpProbeTrapParams`](HttpProbeTrapParams.cs), [`HttpServerBase_ProbeTrap.cs`](HttpServerBase_ProbeTrap.cs) — the probe trap (probe urls, bans, the hostile answer)
- [`ProbeDecoy`](ProbeDecoy.cs) — the decoy secret files of the probe trap (detection of secret file urls, per installation and per client values, real AWS canary keys)
- [`HttpRequestTracker`](HttpRequestTracker.cs) — count, time, url and address of the last request of a kind (probe hits, banned requests, 404's)
- [`BackgroundJobs`](BackgroundJobs.cs) — the background jobs of `HttpServerBase.StartJob` / `StartWork` and their client APIs
- [`FileHttpServerModule`](FileHttpServerModule.cs) — serves folders on disc
- [`HttpProxy`](HttpProxy.cs) — forwards url prefixes to other servers, with memory and disc caching
- [`HttpServerBase_Favicon`](HttpServerBase_Favicon.cs) — the generated app icon, logo and banner and the bitmaps rendered from them
- [`CachedTransformer`](CachedTransformer/CachedTransformer.cs) — base class of the disc-cached file transformers

## Using it

Normally used indirectly through a server micro service. Modules with parameter-only constructors can be declared directly in the manifest and are picked up by the server:

```json
[
  { "Type": "SysWeaver.MicroService.NetHttpServerService, SysWeaver.MicroService.NetHttpServer",
    "Params": { "ListenOn": [ { "Prefix": "http://localhost:8080" } ] } },
  { "Type": "SysWeaver.Net.FileHttpServerModule, SysWeaver.HttpServer",
    "Params": { "Folders": [ { "DiscFolder": "D:/Site", "WebFolder": "site" } ] } },
  { "Type": "SysWeaver.MicroService.ApiHttpServerService, SysWeaver.MicroService.HttpServer" }
]
```

Calling an API: `GET /Api/<class url>/<method>?"value"` or POST the value as the body, see [Calling APIs](#calling-apis).

## Calling APIs

A `[WebApi]` method takes at most one input value (plus an optional `HttpServerRequest`). That value can be sent in two ways, and both reach the method as the same deserialized object:

- **POST**: the value is the request body.
- **GET**: the value is the whole query string (everything after `?`). This "posts" the value in the url, so small arguments can be sent as GETs: one less round trip for preflight free requests, readable urls in logs and the browser, and responses that a browser can cache by url (`[WebApiClientCache]`; browsers never cache POST responses).

A request without input (a GET without a query string, an empty POST body) calls the method with the default value of its parameter type (null for classes).

### POST body

- `Content-Type` picks the deserializer (`application/json`, `text/xml`, ... any registered `ISerializerType` whose input is enabled in `ApiIoParams`); without a `Content-Type` the default input serializer (json) is used. An unknown type is refused with **415**.
- `Content-Encoding` (optional) decompresses the body first: any registered compression (`CompManager`: `br`, `gzip`, `deflate`, and `zstd` when registered). An unknown encoding is refused with **415**.
- Size limits: `ApiHttpServerModuleParams.MaxRequestSize` (the body as received) and `MaxDecompressedSize` (after decompression), 64 MB each by default, **413** when exceeded.

```http
POST /Api/auth/ValidateEmailAddress HTTP/1.1
Content-Type: application/json
Content-Encoding: br

<brotli compressed bytes of "someone@example.com">
```

### GET query string

The query string is url decoded (`%XX` sequences only: a `+` stays a `+`, so encode spaces as `%20`) and its **first character** picks the format (`ApiIoParams.Get<T>`, `ApiIoParams.cs`):

| First character | Format | Example |
|---|---|---|
| `{` `[` `"` `'` `-` `+` `.` `0`-`9` | JSON | `?"SysWeaver.MicroService.ChartJsService"` (encoded `?%22SysWeaver.MicroService.ChartJsService%22`), `?42`, `?{"Param":"1","MaxRowCount":20}` |
| `<` | XML: needs an xml input serializer (`NetXmlSerializer`, SysWeaver.Serialization) registered and enabled (`ApiHttpServerModuleParams.InputSerializers`, default `"json, xml, proto, bson"`) | `?<TableDataRequest>...</TableDataRequest>` |
| `null` `undefined` `true` `false` `nan` (the whole text, case sensitive) | JSON constants (`undefined` = `null`, `nan` = `0`) | `?true` (for a `bool` parameter) |
| `_` | A base64url payload, optionally compressed, see below | `?_dUwquLA5PTSxLLdLzzUwuyg9OLSrLTE7Vc85ILCrxKoZylQA` |
| any other letter | Form data (`application/x-www-form-urlencoded`): needs a deserializer with that mime type registered and enabled. SysWeaver has none, so such a query is refused ("No enabled uri deserializer") | `?Name=Anna&Age=42` |

Anything else (ex: `?!x`) is refused with **400** "The data sent could not be read (...)", as is a value that doesn't match the parameter type (ex: `?true` for a `String` parameter).

**Payloads (`_`)**: `_` + a type letter + [a serializer name + `,`] + the data as base64url (`-` and `_` instead of `+` and `/`; the `=` padding is optional):

| Letter | Data |
|---|---|
| `u` | The serialized value as is (utf-8 bytes of the json) |
| `d` | Compressed with raw deflate (`deflate`) |
| `g` | Compressed with gzip |
| `b` | Compressed with brotli (`br`) |
| `z` | Compressed with zstd (only when a zstd implementation is registered in `CompManager`) |

The lower case letter means the data is JSON. The **upper case letter** is the same compression with a named serializer: the serializer name (the extension of an enabled input serializer, ex: `json`, `xml`, `proto`, `bson`) and a comma come before the data, ex: `_Ujson,<base64url>` or `_Bxml,<base64url of brotli compressed xml>`; an unknown or disabled name is refused ("Can't find an enabled serializer named ..."). Binary serializers (`proto`, `bson`) can only be sent this way or as a POST. The decompressed size is limited by `MaxDecompressedSize` (as a POST body). `ApiIoParams.BinaryArgTypes` lists the letters a server accepts.

Examples, all the same call (`Api/HaveService` with the string `"SysWeaver.MicroService.ChartJsService"`, answer `true`):

```text
GET /Api/HaveService?%22SysWeaver.MicroService.ChartJsService%22                         json, url encoded
GET /Api/HaveService?_uIlN5c1dlYXZlci5NaWNyb1NlcnZpY2UuQ2hhcnRKc1NlcnZpY2Ui              base64url of the json
GET /Api/HaveService?_Ujson,IlN5c1dlYXZlci5NaWNyb1NlcnZpY2UuQ2hhcnRKc1NlcnZpY2Ui         the same, with a named serializer
GET /Api/HaveService?_dUwquLA5PTSxLLdLzzUwuyg9OLSrLTE7Vc85ILCrxKoZylQA                   raw deflate, base64url
```

An object, `{"Param":"1","MaxRowCount":20}`: `?%7B%22Param%22%3A%221%22%2C%22MaxRowCount%22%3A20%7D` (json), `?_ueyJQYXJhbSI6IjEiLCJNYXhSb3dDb3VudCI6MjB9` (base64url), `?_dq1YKSCxKzFWyUjJU0lHyTawIyi93zi_NK1GyMjKoBQA` (deflate).

To build a payload: serialize the value as JSON (utf-8), optionally compress it (raw deflate: no zlib header), encode it as base64url, and prefix `_` and the letter. The query string must stay within the url length the listener allows (http.sys: 16 KB by default), so large values are POSTed.

### What the web client sends

`Sw.Net.Send(url, value)` (`srcWeb/core`, the legacy `sendRequest` of `common/common.js`) picks the shortest form for the value serialized with `Sw.Net.ToTypedJson` (`$type` first):

1. A GET is only considered when the json is under 16 KB (and not in debug builds, which POST everything unless the web server Params have `"OptimizedSend": true`).
2. `?<url encoded json>` when that is under 128 characters.
3. Else the shorter of `?_d<base64url deflate>` and `?_u<base64url>` (deflate only when it is at least 1/8 smaller), as a GET when the query is under 4096 characters.
4. Else a POST with `Content-Type: application/json`, the body deflated (`Content-Encoding: deflate`) when that was smaller.

### The response

The result is serialized by the serializer the `Accept` header asks for (the default output serializer, json, otherwise) and compressed by the server's response compression (`Accept-Encoding`, `[WebApiCompression]` per api). `[WebApiRaw]` methods return their bytes as they are, with their own mime type.

## Limitations and considerations

- Certificate binding for the HttpListener based server uses Windows tooling (`netsh`, elevated rights); on other platforms use the Kestrel server for HTTPS.
- The listeners' default listen prefix is external HTTPS on port 443 (`HttpServerPrefix.DefaultExternalHttps`), which requires a certificate provider (and, on Windows with HttpListener, elevated rights). Always configure prefixes explicitly, e.g. `http://localhost:8080`.
- With a wildcard prefix (`https://*:8097/`) the host of a request's `Prefix` is the client's Host header: build links that leave the request (e-mails, text messages) with `GetLinkRoot(request)` (the request prefix only for a fixed host prefix, else the configured `ExternalRootUri`) and set `ExternalRootUri` for such a listener.
- HTTP range requests are not handled for cached content (TODO in source), which matters for media streaming; suffix and multi-range requests are rejected.
- The client IP (`HttpServerRequest.GetIpAddress`) is the address of the connection, unless the connection comes from a trusted proxy (`HttpServerBaseParams.TrustedProxies`, default `loopback` only; a proxy on another machine must be listed by its address or network, `private` trusts all private networks). Then it is the first address from the right of the `X-Forwarded-For` chain that is not a trusted proxy itself (`IsForwarded` is true), and `X-Forwarded-Proto: https` marks the request as https.
  - The standard `Forwarded` header is never read: proxies pass a client's one on unchanged, so it would let a client choose its address.
  - A client that connects directly from a trusted address can set the headers itself, so only trust addresses where every machine is a proxy.
  - An `X-Forwarded-For` from an address that isn't trusted is ignored, with a warning logged once, so a proxy missing from the list is noticed.
  - The built-in proxy components (`HttpProxy`, `FileProxy`, `ProxyRequestCache`) add the connection's address to `X-Forwarded-For` and write `X-Forwarded-Proto`. They keep the incoming forwarding headers only from a trusted proxy (else they come from the client and are replaced), and never pass a `Forwarded` header on. `HttpProxy` / `FileProxy` strip the Cookie and Authorization headers unless `ForwardCookies` / `ForwardAuthorization` are set; the reverse proxy forwards Authorization and only the cookies that the client itself has set (see SysWeaver.ReverseProxy).
- The API engine limits POST bodies and their decompressed size (64 MB each by default, `ApiHttpServerModuleParams.MaxRequestSize` / `MaxDecompressedSize`, 413 when exceeded). Other modules apply no request body size limit.
- Request path handling and template output have known open hardening issues; expose only folders meant to be public and don't rely on the server as the only protection for sensitive files.
- Firewall handling: Windows uses `netsh advfirewall` (needs elevation); Linux detects firewalld, ufw or plain nftables (needs root, never uses `sudo`, does nothing when no firewall is active, iptables-only systems aren't supported, nftables rules are runtime only); macOS has no firewall support. Rules the handler didn't add are never changed or removed.
- The build pre-compresses the embedded web assets with a Windows tool from `_tools`, so the project builds as-is only on Windows.

## Folder layout

| Folder | Content |
|---|---|
| [`Audit/`](Audit/README.md) | Types and tools for searching and reporting audited API calls (`IApiAuditReportService`) |
| [`Auth/`](Auth/README.md) | Exceptions for missing sessions, missing or existing logins and insufficient tokens |
| [`CachedTransformer/`](CachedTransformer/README.md) | Base class and contracts of the disc-cached file transformers |
| [`Security/`](Security/README.md) | Firewall handlers for Windows (netsh) and Linux (ufw, firewalld, nftables) |
| [`Services/`](Services/README.md) | Service contracts for user images and user storage |
| [`srcData/`](srcData/ProbeUrls.txt) | Embedded data files (compressed into `data/` by the build): `ProbeUrls.txt`, the built-in probe urls of the probe trap |
| [`trapData/`](trapData) | The pre-compressed decompression bombs of the probe trap (`GetAJob.txt.br` / `.gz` / `.deflate` / `.zst`, written once, embedded as is) |
| [`srcWeb/`](srcWeb/README.md) | Source of the embedded web client: the start page `index.html`, the client core, UI library and application shell (`core/`, `ui/`, `shell/`, bundled into `sw/`), file viewers (`view/`), start pages (`app/`), the audit browser (`audit/`), the theme and legacy bundle (`common/`), brand fonts (`brand/`) and template help (`help/`) |

## Relationships

- **Project:** [`SysWeaver.HttpServer.csproj`](SysWeaver.HttpServer.csproj)
- **Builds on:** [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md), [SysWeaver.Common](../SysWeaver.Common/README.md), [SysWeaver.Compression](../SysWeaver.Compression/README.md), [SysWeaver.IsoData](../SysWeaver.IsoData/README.md), [SysWeaver.Media.Svg](../SysWeaver.Media.Svg/README.md), [SysWeaver.MicroServices](../SysWeaver.MicroServices/README.md), [SysWeaver.Serialization](../SysWeaver.Serialization/README.md), [SysWeaver.TableData](../SysWeaver.TableData/README.md)
- **Used by:** [SysWeaver.AI](../SysWeaver.AI/README.md), [SysWeaver.AI.Tools](../SysWeaver.AI.Tools/README.md), [SysWeaver.AI.OpenAI](../SysWeaver.AI.OpenAI/README.md), [SysWeaver.AspHttpServer](../SysWeaver.AspHttpServer/README.md), [SysWeaver.Chat](../SysWeaver.Chat/README.md), [SysWeaver.HttpServer.ExploreModule](../SysWeaver.HttpServer.ExploreModule/README.md), [SysWeaver.HttpServer.IconModule](../SysWeaver.HttpServer.IconModule/README.md), [SysWeaver.HttpServer.IsoDataModule](../SysWeaver.HttpServer.IsoDataModule/README.md), [SysWeaver.HttpTransformer.Compress](../SysWeaver.HttpTransformer.Compress/README.md), [SysWeaver.HttpTransformer.Image](../SysWeaver.HttpTransformer.Image/README.md), [SysWeaver.HttpTransformer.Svg](../SysWeaver.HttpTransformer.Svg/README.md), [SysWeaver.HttpTransformer.Translate](../SysWeaver.HttpTransformer.Translate/README.md), [SysWeaver.MicroService.AspHttpServer](../SysWeaver.MicroService.AspHttpServer/README.md), [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md), [SysWeaver.MicroService.Avatar](../SysWeaver.MicroService.Avatar/README.md), [SysWeaver.MicroService.CustomUserImage](../SysWeaver.MicroService.CustomUserImage/README.md), [SysWeaver.MicroService.Edit](../SysWeaver.MicroService.Edit/README.md), [SysWeaver.MicroService.FileUploader](../SysWeaver.MicroService.FileUploader/README.md), [SysWeaver.MicroService.HttpServer](../SysWeaver.MicroService.HttpServer/README.md), [SysWeaver.MicroService.Map](../SysWeaver.MicroService.Map/README.md), [SysWeaver.MicroService.MediaThumbnail](../SysWeaver.MicroService.MediaThumbnail/README.md), [SysWeaver.MicroService.NetHttpServer](../SysWeaver.MicroService.NetHttpServer/README.md), [SysWeaver.MicroService.Thumbnail](../SysWeaver.MicroService.Thumbnail/README.md), [SysWeaver.MicroService.Thumbnail.Web](../SysWeaver.MicroService.Thumbnail.Web/README.md), [SysWeaver.MicroService.UserManager](../SysWeaver.MicroService.UserManager/README.md), [SysWeaver.MicroService.UserStorage](../SysWeaver.MicroService.UserStorage/README.md), [SysWeaver.MicroServices.ServiceManager](../SysWeaver.MicroServices.ServiceManager/README.md), [SysWeaver.NetHttpServer](../SysWeaver.NetHttpServer/README.md), [SysWeaver.ReverseProxy](../SysWeaver.ReverseProxy/README.md), [SysWeaver.Security](../SysWeaver.Security/README.md), [SysWeaver.TextMessage.Twilio](../Communication/SysWeaver.TextMessage.Twilio/README.md), [SysWeaver.TextMessage.WhatsApp](../Communication/SysWeaver.TextMessage.WhatsApp/README.md)
