# Security

[⬆ Documents](README.md)

> Executive summary of the security model: what protects stored data, sign in, sessions and the protected end points, how abuse is slowed down and trapped, how strong each protection is against each kind of attack, the known weaknesses, and what operators must do.

## Summary

A copy of a users file or database can't be used to sign in: the server stores SCRAM verifiers, not password equivalents. Stored passwords, personal data, API keys and links are useless to someone who only has a copy of the data, sign in requests can't be replayed, and the sign in API's give the same kind of answer for unknown users as for real ones. Every protected end point is checked against the session in the server pipeline, also when a service reads an url on behalf of a user. Another site can't make a signed in browser call an API (cross site request forgery protection). Sign in, token sign in and the password policy are rate limited per process and per client address, repeated probing is slowed down and reported, and clients that probe for leaked secrets (".env", ".git/config" and similar) are trapped: slowed down, fed a decompression bomb and banned.

The main remaining weaknesses are a few account flows (e-mail change undo, sign up) and account enumeration through sign up and user images. See [Known weaknesses](#known-weaknesses).

## Key strengths

### Stolen password data can't be used to sign in

- **SCRAM style sign in** (RFC 5802 / RFC 7677): the server stores a verifier, not a password equivalent. A stolen users file or database can't be used to sign in. Finding a password costs a full PBKDF2-SHA256 computation per guess and per user (200 000 iterations by default).
- **No replay:** every sign in proof is bound to a single use, short lived (10 s) server one time pad and a random client nonce. The user name comes from the server side pad, not from the client, so a proof for one user can't be used for another. A captured request (logs, HAR files, inspecting proxies) is worthless.
- **Bound to the application:** stored values include an application binding string, so a value taken from one application doesn't work in another.
- **Constant time comparisons** are used throughout. The browser never sends the password: the proof is computed in the page (Web Crypto, with a verified pure JavaScript fallback for plain HTTP on a LAN).

### Users can't be enumerated

- **Unknown users get answers of the same shape as real ones** (salt, SCRAM parameters, password policy), keyed with a persisted secret so they can't be computed, and stable across restarts.
- **With several authorizers**, an unknown user is made to look like a user of one of them. The authorizer is chosen from a keyed hash of the user name: the last 3 bytes of an HMAC, modulo the number of authorizers. Its salt format, SCRAM parameters and password policy are all used, so `GetUserSalt` and `GetPasswordPolicy` don't tell which names exist.
- **The fake answers are normalized like the real lookups** (trimmed, lower cased), so "Bob", "bob" and " bob" can't be compared to tell a real user from an unknown one.
- **A signed in caller** gets the same fake answer, shaped like its own authorizer, so it can't list the users of its own authorizer either.
- **Passkeys:** the passkey challenge of a user without passkeys is refused for seven in eight user names and answered with a made up credential for the others (fixed per name).
- **Forgotten password:** every case gets the same answer and timing (the work runs in the background).

### Personal data is unreadable in a leaked database

- **User names, nick names, e-mail addresses and phone numbers** are stored as an indexable keyed lookup hash (for fast look ups) plus an AES-256-GCM encrypted value with a fresh random value per row.
- **Link data** (invitations, password resets, e-mail / phone changes) is encrypted the same way, and bound to its row.
- **The secret lives outside the database** (per application and computer, or a shared key file), so a database dump alone reveals nothing.
- **Safe by construction:** a database protected with another secret is refused (nothing is read or written), so a misconfiguration can't corrupt data.

### Links, codes and sessions can't be lifted or guessed

- **Action tokens** (invite, reset, undo links) have 192 random bits and are stored only as keyed hashes, so a database copy contains no working links.
- **Single use:** a token is consumed inside the transaction of its action, so of two concurrent uses only one wins. Its type and expiry are checked at use, and the user always comes from the encrypted token data, never from the request.
- **Withdrawn invitations stop working** at once.
- **Short codes** (6 digits) are bound to the session and get 10 tries per code. Codes that belong to an account also have per account limits (3 per 5 minutes, 50 per day).
- **Session ids** are 144 bit random values. An unknown session cookie is never adopted (no session fixation), and the session token is rotated on every sign in.
- **Session cookies** are `HttpOnly`, `SameSite=Lax` and `Secure` on https, also behind a trusted proxy that ends the TLS (it says so with `X-Forwarded-Proto`, SysWeaver proxies add it). `SecureCookies` makes them always `Secure`.
- **Session tokens never leave the session map.** Debug views, audits, logs, error reports and background jobs use a derived session id (a hash) instead, so sessions can still be traced safely, and an operator can look up a session from its id.

### Protected end points stay protected

- **One gate for every handler:** the required tokens of an end point (API, static file, template, proxy, optional end point) are checked against the session before the handler runs, and before any cached response or "not modified" answer is sent. Cached responses are never served across sessions that may not see them.
- **Internal reads are checked too:** when a service reads an url for a user (store a file or link, export a chart, load an image), the handler only runs if the user's session may use it. A user can't make the server run an administrator API on their behalf.
- **Fail closed:** proxied end points are never served without a session that has the required tokens, and an internal read of a protected handler without a session returns nothing.
- **Open redirects are blocked:** `auth/redirect` only redirects to relative or same origin urls.
- **User storage paths are contained:** every url segment of a stored file or link must be a plain name (no `..`, separators, drive letters, alternate data streams, control characters or trailing dots), and the full path must be inside the storage folder. This holds after url decoding, for reading and deleting.
- **Shared caches re-check access:** a table parsed from the user storage is cached per url, so the caller's access is checked first on every call.
- **No cross site requests** (`CsrfProtection`, on by default): an API call or a `logout` of a signed in browser session is refused (403) when the browser says it comes from another site, unless it has the `X-Sw-Api` header.
  - "From another site" means `Sec-Fetch-Site` is `cross-site` or `same-site`, or, for a browser that doesn't send it, the `Origin` (else `Referer`) host isn't this site.
  - `Sw.Net` adds the header to every same origin request. Another site can't send it: a cross origin request with a custom header needs a CORS preflight, which the server never allows.
  - The site's own images, links and beacons to API urls are `same-origin` and still work. API keys, header sign ins and clients that send no browser headers are not affected.
  - `auth/logout_user` (the front channel logout of an external authorizer) and `auth/redirect` (the sign in redirect back from one) come from another site by design, so they aren't checked.

### Service access is narrow and controllable

- **Header auth (Basic / Bearer / API key headers) is for API keys only** by default. Normal users always use the sign in page, and the browser never silently falls back to sending a plain password.
- **API keys are stored encrypted** and can be restricted to client addresses or networks, which is enforced on every request. They only reach end points that require one of their tokens, never the end points open to any logged in user.
- **Per key tokens:** every key has its own tokens, "service" and / or scoped "service_<scope>" tokens. Each is a token of its own: a scoped token only reaches its own scope, not "service" end points. "debug" and "dev" users reach every service end point. So a key can be limited to the services that use it.
- **Checking a key costs no PBKDF2**, so header auth stays fast.
- **No keys in urls:** API keys are never accepted as one time tokens in a url (`?t=`). Those come from external authorizers and are validated server to server.
- **Run time users** of the simple authorizer (the "Stored users" page) are stored encrypted like the API keys and managed by "admin" or "debug" users. Creating, removing and changing API keys and users is audited.
- **No role escalation:** a caller may only give roles (tokens) it has itself, and may only change, remove, disable, enable, unlock or force a password reset on an account whose roles it all has. That holds for stored users, invitations and the user manager's administration of accounts. So an "admin" without "UserManagement" can't give it, and a "UserManagement" user can't touch an "Admin" account. A "debug" user has every role.

### Abuse is slowed down, reported and trapped

- **Rate limits on sign in:** password sign in, `GetUserSalt`, `GetPasswordPolicy`, the token sign in (`TokenAuth`, `auth/redirect`), passkey sign in, forgotten password and sign up. Each has a permissive process wide limit (many users, many sites) combined with a conservative per address limit (IPv6 grouped by /64, forwarded addresses only trusted from known proxies).
  - Built-in end points declare their limits with the end point. `auth/redirect` allows 300 requests per minute for the whole server and 20 per minute per address.
- **Intrusion detection:** error responses (scanning for API's and files) and calls to key API's (sign in attempts, audited operations) are tracked per address. Above a threshold the attempt is audited, listed as a problem and logged, and the address is slowed down with random delays.
- **Intruder alerts (server manager):** with an ip location service, an Internet address with an established connection older than 30 s on a port that isn't allowed (80 and 443 by default) and whose address and country aren't allowed is listed as a problem, logged and audited. Administrators allow the address, the port or the country from the problem (audited); the allow lists are shown to Admin and Ops. See [SysWeaver.MicroService.ServerManager](../SysWeaver.MicroService.ServerManager/README.md).
- **Decoy secrets:** a probe for a known secret file (`.env`, `.aws/credentials`, `.git/*`, terraform state, kube / docker configs, backups of web app configs, SQL dumps, cloud service accounts, ssh keys, shell histories and more) gets a believable, normal looking file with credentials that belong to nothing.
  - The values are derived from a secret of the installation, so nobody can tell them from real ones by computing them.
  - One client address always gets the same identity, so all its files agree.
  - Every served decoy is logged with the client address.
  - With real AWS canary keys (IAM users without permissions, `CanaryAwsKeys`), a thief that tries a key shows up in CloudTrail, and the log tells when and to whom it was given.
  - A strange answer would only tell the probe that it found a honeypot; a decoy makes it spend its time on dead credentials.
- **Probe trap:** any other request that hits no end point, for an url that only probes ask for (about 100 urls: `.DS_Store`, `wp-config.php`, `phpinfo.php` and the like, and the same below any folder), gets a hostile answer:
  - A delay of 3 to 9 seconds.
  - Status 200, so the probe reports a find and its results fill up with garbage.
  - A fake, old server stack (Apache 2.2 / PHP 5.3), which sends the attacker after exploits that don't apply.
  - Links and a refresh that lead crawlers to more trap urls.
  - About 60 KB of junk headers and 20 large cookies that expire with the ban.
  - A decompression bomb in the best compression the client accepts: brotli 10 GB from 17 KB, zstd 1 GB from 90 KB, gzip / deflate 50 MB from 127 KB. A client that accepts no compression gets 1 MB of plain text.

  The address is then banned for an hour (loopback never is), and the session that probed is banned too. A banned address gets no new session, and every request it makes gets the trap answer. At most 1000 trap answers are delayed at once; above that, answers are sent at once and empty, so a flood can't tie up the server or its bandwidth. Probe hits, banned requests, banned addresses and 404's are counted in the server statistics.

### Passkeys are a shared capability

- Passkey storage is part of the authorizer contract, so any authorizer can offer passkeys: the user manager (database) and the simple authorizer (key value store) both do.
- Passkey ceremonies are random, bound to the session, single use and short lived.
- Origin and relying party id are verified, user verification is required by default, and sign counters and user handles are checked.
- Adding a passkey needs a strong (non API key) sign in.

## Attack resistance

How well the system resists each kind of attack, with the default configuration, as a strength from 0 % (no protection) to 100 % (not possible with the current design). The figures are an assessment, not a measurement.

### Resistant

| Attack | Strength | Why |
|---|---|---|
| Sign in with a stolen users file or database (pass the hash) | 100 % | Only SCRAM verifiers are stored, no password equivalents are accepted |
| Offline password cracking of a stolen database | 90 % | PBKDF2-SHA256 with 200 000 iterations per guess and per user, per user salts; weak passwords still fall eventually |
| Replay of a captured sign in request | 100 % | Single use, 10 s one time pad plus client nonce; the user comes from the server side pad |
| Reading personal data from a database dump | 95 % | AES-256-GCM with a secret outside the database; only an attacker that also has the secret (the computer) can read it |
| Using links (reset, invite, undo) from a database copy | 100 % | Only keyed hashes of action tokens are stored |
| Guessing action tokens (links) | 100 % | 192 random bits |
| Session hijack by guessing or fixing a session id | 100 % | 144 bit random ids, unknown cookies never adopted, rotation on sign in |
| Session theft by script (XSS cookie read) or over plain http | 90 % | `HttpOnly`, `Secure` on https (also behind a TLS ending proxy); a plain http deployment can't be protected |
| Reaching a protected end point without the required tokens | 95 % | One gate in the pipeline for every handler type, before caches; internal reads checked; misconfigured (missing) auth attributes are the residual risk |
| Making the server run a protected API through an url parameter (confused deputy) | 95 % | Internal reads check the handler's tokens against the user's session before running it |
| Cached response served to another user | 95 % | Auth is checked before the cache; per session caches for session dependent API's |
| Path traversal to read or delete files (user storage, file folders) | 95 % | Plain segment checks plus containment in the storage folder after decoding; file folders combine safely per folder |
| Open redirect to a phishing site | 100 % | Only relative and same origin redirects |
| Online password guessing from one address | 90 % | 20 sign ins per minute per address, intrusion detection with delays, random delays per attempt |
| User enumeration through the sign in API's | 85 % | Same shaped, keyed, normalized fake answers, also for signed in callers; small timing differences and a few side channels remain (see weaknesses) |
| Brute forcing a password reset code of one account | 95 % | 6 digits, session bound, 10 tries per code, 3 per 5 minutes and 50 per day per account |
| Using an API key outside its scope or from another network | 95 % | Per key tokens, scoped service tokens, address restrictions checked on every request |
| Escalating to a role you don't have ("debug", "admin", "UserManagement", "ops" ...) | 95 % | Only a caller that has a role can give it or act on an account that has it ("debug" has every role); the residual risk is a role that is itself too broad |
| Passkey assertion for another user or origin | 95 % | Origin / relying party checks, user handle and credential owner checks, single use ceremonies |
| Scanning for leaked secret files (.env, .git, cloud credentials) | 95 % | Nothing is there to find: known secret files get consistent decoys with values that can't be computed (optionally real AWS canary keys that alert on use), other probes are slowed, bombed and banned |
| Spoofing the client address (to get past per address limits, bans, API key address restrictions, audits) | 90 % | Only `X-Forwarded-For` / `X-Forwarded-Proto` are read, and only from trusted proxies (default loopback, others must be listed); the address is the rightmost one that isn't a trusted proxy; `Forwarded` is never read; SysWeaver proxies replace a client's forwarding headers. A trusted network with machines that aren't proxies is the residual risk |
| Cross site request forgery (CSRF) against a signed in user | 90 % | Fetch metadata (`Sec-Fetch-Site`) plus the `X-Sw-Api` header; old browsers without fetch metadata fall back to `Origin` / `Referer`, which a page can suppress (`no-referrer`); sign in CSRF through `auth/redirect` remains |
| Clickjacking | 90 % | `frame-ancestors` / `X-Frame-Options` (configurable for sites that are shown in frames) |

### Weaker

| Attack | Strength | Why |
|---|---|---|
| Distributed password guessing (many addresses) | 60 % | Per address limits and detection don't catch a bot net; the process wide limits only slow it down |
| Account takeover through a recycled old mailbox | 60 % | The undo link of an e-mail change goes to the old address, can't be revoked by the owner and works for 7 days |
| Registering someone else's e-mail or phone (sign up code guessing) | 65 % | Sign up codes belong to no account, so only the per code tries and the per address sign up limit apply |
| Account enumeration through sign up and user images | 50 % | Sign up says when an account already exists. `auth/UserImages/<guid>` is public: simple authorizer guids are an unkeyed hash of the name, user manager guids are sequential |
| Password reset code guessing across many accounts | 75 % | `ValidateResetCode` has no per address limit, so a large, noisy campaign over many accounts can hit some |
| User enumeration by timing or SCRAM parameters | 70 % | A config user answers before a database lookup; API keys report 4096 iterations, other users the default; clear text simple users get a new SCRAM salt per process |
| Active man in the middle on plain http | 40 % | Only https protects against it. The page accepts a legacy (non SCRAM) answer, and stripping the SCRAM fields gives a proof that is cheap to crack. Plain http should only be used on a trusted LAN |
| Basic auth password guessing (only if `AllowBasicAuth` is set for users) | 50 % | One SHA256 per guess, no per address limit; header guesses at one url don't reach the intrusion threshold (it counts distinct urls) |
| Same origin content from proxied services | 60 % | Reverse proxy tunnel clients and proxied end points serve content under the main origin, so a compromised upstream can run script with every visitor's session |
| Phishing with a live proxy on a look-alike domain | 20 % | No password protocol can stop it; only passkeys (origin bound) do |
| Compromise of the server computer | 10 % | The data protection secret and the API key secret are on the same computer: they protect copies of the data, not a compromised machine |

## Known weaknesses

The ones that matter most, with the suggested direction:

- **Undo e-mail link** (medium): let the owner revoke it after re-authenticating, or shorten its lifetime.
- **Sign up codes and enumeration** (medium / low): add per target backoff and a global limit on wrong sign up codes; give the same answer for an existing account.
- **User images** (medium): keyed guids, or a uniform image for unknown guids and no initials for anonymous viewers.
- **`ValidateResetCode`** (low): add a per address limit, or use 8 digits.
- **Timing and SCRAM parameters** (low): pad the user lookups to a fixed time; give API keys the default iteration count in answers.
- **Legacy proof fallback** (low): the page should refuse a non SCRAM answer unless the server is configured for a legacy authorizer.
- **Proxy headers** (low): `x-api-key` / `x-goog-api-key` are forwarded to proxied services and are not part of the proxy cache key.
- **File folders on Windows** (low, needs a nested configuration): web folder matching is case sensitive while the file system isn't, so a protected folder inside a public folder's disc path can be reached through the public one.
- **Double decoding** (info): urls are decoded again after the web server decoded them. The modules check their paths, but a module that authorizes by path prefix would be exposed.
- **Public information** (info): `ServerManager.GetStorageStats` shows the storage folders and sizes to anyone.
- **Sign in CSRF** (low): `auth/redirect?t=` signs a browser in with a token from an external authorizer, so another site can sign a victim into the attacker's account (only with an authorizer that issues one time tokens).
- **Old browsers** (low): a browser without fetch metadata (before 2023) is only checked by `Origin` / `Referer`, and a page can send neither on a GET navigation.

## Verification

- Unit tests cover:
  - The sign in protocol, including the published RFC 7677 test vector, computed by both the C# server and the JavaScript client.
  - The fake answers for unknown users: the same authorizer for salt, SCRAM and policy, stable, case and white space insensitive, also for signed in callers.
  - The protection of personal data, the address restrictions, the rate limiter keys, the intrusion detection and the end point rate limits (`auth/redirect`).
  - Internal reads of protected handlers.
  - The role escalation rule: only held tokens can be given, accounts with tokens the caller lacks can't be changed or removed, "debug" has every token, no caller gives nothing.
  - The forwarding headers: client address and https only from trusted proxies (default loopback), the rightmost untrusted address, `Forwarded` ignored, what the proxy passes on.
  - The CSRF protection: refused cross site calls (fetch metadata, origin, referer, logout), allowed own and marked calls, header auth, anonymous sessions.
  - The user storage path checks.
  - The decoys: the detection of secret file urls (and of urls that aren't), valid formats (json, xml, PEM keys, ssh public keys, git index), consistent and per installation values, real AWS canary keys, the identity look up, a normal looking answer.
  - The probe trap: the bomb in every encoding, plain and HEAD answers, address and session bans, no session for a banned address, statistics.

## Operator checklist

| Action | Why |
|---|---|
| Back up the data protection secret: `SysWeaver.DataProtection.Secret` in the application key value store, or the configured secret file (an Admin can read the secret in use with `auth/GetDataProtectionSecret`) | Without it user names, e-mail addresses and phone numbers can't be read |
| Services that share a user database: same `PasswordHashString` and same secret (`DataProtectionSecretFile`, copied to every machine) | Otherwise the second service refuses to use the database (an error is logged, sign in is unavailable), and passwords don't work across services |
| Replace the clear text passwords of `SimpleAuthorizer` user files with `$s1$` verifiers (the *Generate password hash* page or the `hash` verb of the service host); 44 char password hashes are refused | A users file with verifiers can't be used to sign in |
| Create new API keys, and give each the narrowest tokens (`service_<scope>`) | API keys made before the per key tokens are not read, and a narrow key only reaches its own services |
| Behind a proxy that ends the TLS and doesn't send `X-Forwarded-Proto`: set `SecureCookies` | Else the session cookie isn't marked `Secure` |
| A proxy on another machine (ex: a SysWeaver reverse proxy): add its address to `TrustedProxies` (only loopback is trusted by default; avoid "private") | Else every client seems to come from the proxy: they share the per address limits, and the probe trap can ban the proxy. A missing proxy is logged once as a warning |
| Restrict API keys to the addresses of the services that use them (API key management page) | A leaked key then only works from those addresses |
| Behind a local reverse proxy that doesn't tell the client address: keep "loopback" in `ProbeTrap.NeverBan` | Else the probe trap would ban the proxy, and with it every client |
| Behind a local reverse proxy that doesn't tell the client address: empty `ProbeTrap.NeverTrapHosts` | Every client then has a loopback address, so a client that sends `Host: localhost` or `Host: dev.sysweaver.com` would never be trapped |
| Add the probe urls seen in the "NotFound" statistics to `ProbeTrap.ExtraUrls` | The trap then answers them too |
| Server manager: register an ip location service (`IIpLocation`), list the ports the server is expected to serve to everyone (`Network.AllowedPorts`) and the networks of the administrators (`Network.AllowedIps`, rather than allowing ex: port 22 for everyone) and check the intruder alerts in Problems | Without a location service no address is checked; an unexpected long lived connection is often the first sign of a compromise |
| Optional: create AWS IAM users without permissions, list their access keys in `ProbeTrap.CanaryAwsKeys` and alert on their use in CloudTrail | A thief that tries a decoy key is reported (address, user agent), and the "Probe decoy" log line tells what probe got it |
| Back up the decoy secret (`HttpServer.ProbeDecoySecret` in the application key value store) if you trace decoy values | The values (and `ProbeDecoy.FindIdentity`) depend on it |
| Change the passwords of the accounts of user files and databases that were copied before the upgrade | A password that may have leaked earlier is only made worthless by a new password |
| Serve over https (plain http only on a trusted LAN) | Plain http can't protect sessions or the sign in from an active attacker |

## Related

- [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md): SCRAM, `DataProtection`, `SimpleAuthorizer`, API keys, the fake answers for unknown users
- [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md): the sign in protocol (`GetUserSalt`, `Login`, `GetPasswordPolicy`, `TokenAuth`), its rate limits
- [SysWeaver.MicroService.UserManager](../SysWeaver.MicroService.UserManager/README.md): protected personal data, action tokens, shared databases
- [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md): the auth gate, session ids, rate limits, intrusion detection, the probe trap
- [SysWeaver.MicroService.PassKey](../SysWeaver.MicroService.PassKey/README.md): passkeys
