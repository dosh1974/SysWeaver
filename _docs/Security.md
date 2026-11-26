# Security

[⬆ Documents](README.md)

> Executive summary of the authentication and data protection model: what protects stored data, sign in and sessions, why it matters, and what operators must do.

## Summary

A copy of a users file or database can't be used to sign in: the server stores SCRAM verifiers, not password equivalents, and no older password format is accepted. Stored passwords, personal data, API keys and links are all useless to someone who only has a copy of the data, sign in requests can't be replayed, and repeated probing is slowed down and reported. Existing databases are upgraded automatically on start (personal data is encrypted, old password rows are removed: those users choose a new password with "forgot password"), and the SolidAdmin authorizers are supported.

## Key strengths

### Stolen password data can't be used to sign in

- **SCRAM style sign in** (RFC 5802 / RFC 7677): the server stores a verifier, not a password equivalent. A stolen users file or database can't be used to sign in; finding a password costs a full PBKDF2-SHA256 computation (200 000 iterations by default) per guess, per user.
- **No replay:** every sign in proof is bound to a single use, short lived server one time pad and a random client nonce. A captured request (logs, HAR files, inspecting proxies) is worthless.
- **Bound to the application:** stored values include an application binding string, so a value taken from one application doesn't work in another.
- **No user enumeration:** unknown users get answers of exactly the same shape as real ones, keyed with a secret so they can't be told apart, and stable across restarts. The passkey challenge of a user without passkeys is refused for seven in eight user names and answered with a made up credential for the others (fixed per name), so a challenge doesn't tell that a user exists.
- **Constant time comparisons** throughout, and the browser never sends the password: the proof is computed in the page (Web Crypto, with a verified pure JavaScript fallback for plain HTTP on a LAN).

### Personal data is unreadable in a leaked database

- **User names, nick names, e-mail addresses and phone numbers** are stored as an indexable keyed lookup hash (for fast look ups) plus an AES-256-GCM encrypted value with a fresh random value per row.
- **Link data** (invitations, password resets, e-mail / phone changes) is encrypted the same way, and bound to its row.
- **The secret lives outside the database** (per application and computer, or a shared key file), so a database dump alone reveals nothing.
- **Safe by construction:** a database protected with another secret is refused (nothing is read or written), so a misconfiguration can't corrupt data.

### Links and sessions can't be lifted from stored data

- **Action tokens** (invite, reset, undo links) are stored only as keyed hashes: a database copy contains no working links.
- **Session cookies are `Secure` on https**, also behind a proxy that ends the TLS (it says so with `X-Forwarded-Proto` / `Forwarded`, SysWeaver proxies add it), or always (`SecureCookies`).
- **Session tokens never leave the session map.** Debug views, audits, logs, error reports and background jobs use a derived session id (a hash) instead, so sessions can still be traced safely, and an operator can look up a session from its id.

### Service access is narrow and controllable

- **Header auth (Basic / Bearer / API key headers) is for API keys only** by default; normal users always use the sign in page. The browser never silently falls back to sending a plain password.
- **API keys are stored encrypted**, can be restricted to client addresses or networks (enforced on every request) and only reach end points that require one of their tokens, never the end points open to any logged in user. Every key has its own tokens: "service" and / or scoped "service_<scope>" tokens (each is a token of its own: a scoped token only reaches its own scope, not "service" end points; "debug" and "dev" users reach every service end point), so a key can be limited to the services that use it. Checking a key costs no PBKDF2, so header auth stays fast. API keys are never accepted as one time tokens in a url (`?t=`): those come from external authorizers, validated server to server.
- **Run time users** of the simple authorizer (the "Stored users" page) are stored encrypted like the API keys, managed by "admin" or "debug" users; only a "debug" user may give the "debug" token or change a user that has it. Creating, removing and changing API keys and users is audited.

### Abuse is slowed down and reported

- **Rate limits on sign in** (password, passkey, forgotten password and sign up): a permissive process wide limit (many users, many sites) combined with a conservative per address limit (IPv6 grouped by /64, forwarded addresses only trusted from known proxies).
- **Intrusion detection:** per address tracking of error responses (scanning for API's and files) and of calls to key API's (sign in attempts, audited operations). Above a threshold the attempt is audited, listed as a problem and logged, and the address is slowed down with random delays.
- **Fail closed:** proxied end points are never served without a session that has the required rights.

### Passkeys are a shared capability

- Passkey storage is part of the authorizer contract, so any authorizer can offer passkeys: the user manager (database) and the simple authorizer (key value store) both do.

## Verification

- Unit tests cover the sign in protocol (including the published RFC 7677 test vector, computed by both the C# server and the JavaScript client), the protection of personal data, the address restrictions, the rate limiter keys and the intrusion detection.

## Operator checklist

| Action | Why |
|---|---|
| Back up the data protection secret (`SysWeaver.DataProtection.Secret` in the application key value store, or the configured secret file; an Admin can read the secret in use with `auth/GetDataProtectionSecret`) | Without it user names, e-mail addresses and phone numbers can't be read |
| Services that share a user database: same `PasswordHashString` and same secret (`DataProtectionSecretFile`, copied to every machine) | Otherwise the second service refuses to use the database (an error is logged, sign in is unavailable), and passwords don't work across services |
| Replace the clear text passwords of `SimpleAuthorizer` user files with `$s1$` verifiers (the *Generate password hash* page or the `hash` verb of the service host); 44 char password hashes are refused | A users file with verifiers can't be used to sign in |
| Create new API keys, and give each the narrowest tokens (`service_<scope>`) | API keys made before the per key tokens are not read, and a narrow key only reaches its own services |
| Behind a proxy that ends the TLS and doesn't send `X-Forwarded-Proto`: set `SecureCookies` | Else the session cookie isn't marked `Secure` |
| Restrict API keys to the addresses of the services that use them (API key management page) | A leaked key then only works from those addresses |
| Change the passwords of the accounts of user files and databases that were copied before the upgrade | A password that may have leaked earlier is only made worthless by a new password |

## Known limits

- A live phishing proxy on a look-alike domain can't be stopped by any password protocol; only origin bound credentials (passkeys, a future second factor) can.
- Per address limits and detection catch a single scanner or guesser, not a widely distributed attack.
- The default data protection secret (and the API key secret) is stored in the application key value store on the same computer: it protects a copied database or key list, not a fully compromised machine.
- Password rows of a user manager database that an earlier build already converted to verifiers can't be told apart from passwords set normally, they are kept (the checklist covers changing those passwords).

## Related

- [SysWeaver.Auth.Core](../SysWeaver.Auth.Core/README.md): SCRAM, `DataProtection`, `SimpleAuthorizer`, API keys
- [SysWeaver.MicroService.Auth](../SysWeaver.MicroService.Auth/README.md): the sign in protocol (`GetUserSalt`, `Login`), its rate limits
- [SysWeaver.MicroService.UserManager](../SysWeaver.MicroService.UserManager/README.md): protected personal data, action tokens, shared databases
- [SysWeaver.HttpServer](../SysWeaver.HttpServer/README.md): session ids, rate limits, intrusion detection
- [SysWeaver.MicroService.PassKey](../SysWeaver.MicroService.PassKey/README.md): passkeys
