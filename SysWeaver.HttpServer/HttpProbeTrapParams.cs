using System;

namespace SysWeaver.Net
{
    /// <summary>
    /// Parameters of the probe trap of the <see cref="HttpServerBase"/>: a request that hits no end point (a 404) for an url that only probes ask for
    /// (leaked secrets, repositories, backups: ".env", ".git/config" etc) is answered with a slow, hostile answer and the client address is banned for a while.
    /// </summary>
    /// <remarks>
    /// The trap answer: a delay (<see cref="MinDelayMs"/> - <see cref="MaxDelayMs"/>), status 200 (the probe reports a find, so its results fill up with garbage),
    /// many large and misleading headers (cookies, links to more trap urls, a fake server), and a decompression bomb in the best compression the client accepts
    /// (brotli: 10 GB, zstd: 1 GB, gzip / deflate: 50 MB of "GET A JOB - FBI IS ON THE WAY! ", a few KB to 130 KB sent), or <see cref="UncompressedSize"/> bytes of it.
    /// While banned, every request of the address (and of the sessions that probed) gets the trap answer, and no session is created for it.
    /// </remarks>
    public sealed class HttpProbeTrapParams
    {
        /// <summary>
        /// The probe urls of the trap (relative to the site root, compared case insensitively, also when found below any folder), in addition to the built-in list
        /// (".env", ".git/config", ".aws/credentials", "terraform.tfstate" and many more, see srcData/ProbeUrls.txt of SysWeaver.HttpServer).
        /// </summary>
        public String[] ExtraUrls;

        /// <summary>
        /// Set to false to ignore the built-in probe url list (only <see cref="ExtraUrls"/> are used)
        /// </summary>
        public bool UseBuiltInUrls = true;

        /// <summary>
        /// How long a client address that probed is banned, in minutes (0 or less: no ban, the trap only answers the probe urls).
        /// IPv6 addresses are banned by their /64 prefix.
        /// </summary>
        public int BanMinutes = 60;

        /// <summary>
        /// Addresses that are never banned (they still get the trap answer for a probe url): addresses, networks (ex: "10.0.0.0/8"), "loopback" or "private".
        /// The default is "loopback": the address of a local reverse proxy that doesn't tell the client address must not be banned (that bans every client).
        /// </summary>
        public String[] NeverBan = ["loopback"];

        /// <summary>
        /// Host names (the Host header, compared case insensitively, without the port) that are never trapped when the client address is in <see cref="NeverBan"/>:
        /// a probe url is answered with a normal 404, and neither the address nor the session is banned.
        /// The default is "localhost" and "dev.sysweaver.com" (resolves to 127.0.0.1 and has a real certificate, used for development).
        /// Both must match, the Host header alone is client controlled. Behind a local reverse proxy that doesn't tell the client address, every client has a <see cref="NeverBan"/> address,
        /// so clients that send one of these host names aren't trapped: empty this list (or make the proxy tell the client address) on such servers.
        /// </summary>
        public String[] NeverTrapHosts = ["localhost", "dev.sysweaver.com"];

        /// <summary>
        /// The maximum number of banned addresses (when exceeded, expired bans are removed, and if that isn't enough the oldest half)
        /// </summary>
        public int MaxBannedAddresses = 100_000;

        /// <summary>
        /// The minimum delay of a trap answer, in ms
        /// </summary>
        public int MinDelayMs = 3000;

        /// <summary>
        /// The maximum delay of a trap answer, in ms (0 or less: no delay)
        /// </summary>
        public int MaxDelayMs = 9000;

        /// <summary>
        /// The maximum number of trap answers that are delayed at the same time. Above it, trap answers are sent at once and without a body,
        /// so a flood of banned requests can't tie up the server or use its bandwidth.
        /// </summary>
        public int MaxConcurrent = 1000;

        /// <summary>
        /// The body size of a trap answer to a client that accepts no compression (the message repeated), in bytes
        /// </summary>
        public int UncompressedSize = 1 << 20;

        /// <summary>
        /// Answer probes for known secret files (".env", ".aws/credentials", ".git/config", "terraform.tfstate", "wp-config.php.bak" etc, see <see cref="ProbeDecoy.Detect(string)"/>)
        /// with a believable decoy file instead of the hostile answer: a normal answer (no delay, no strange headers) with credentials that belong to nothing.
        /// A probe that thinks it found secrets spends time on using them (and with <see cref="CanaryAwsKeys"/> it tells you so), a strange answer only tells
        /// it that it found a honeypot. The client address is still banned, and its later probes for secret files get decoys of the same identity.
        /// </summary>
        public bool UseDecoys = true;

        /// <summary>
        /// The name used in the decoy files (app, database and host names, ex: "checkout-api" gives "db-primary.checkout-api.internal"), null for the app name
        /// </summary>
        public String DecoyName;

        /// <summary>
        /// The number of decoy identities (sets of credentials). A client address always gets the same identity, so all files it gets agree with each other,
        /// and the identity tells what client got a value (see <see cref="ProbeDecoy.FindIdentity(string)"/>).
        /// </summary>
        public int DecoyIdentities = 512;

        /// <summary>
        /// Real AWS canary keys ("AKIA...:secret access key"), used instead of the generated (inert) AWS keys of the decoys, round robin over the identities.
        /// Create an IAM user without any permissions (no policies, or an explicit deny all), create access keys for it and list them here: every use of a key
        /// shows up in CloudTrail (an AccessDenied with the address and user agent of the thief), alert on it (ex: an EventBridge rule).
        /// The probe trap log ("Probe decoy ... AWS key ...") tells what client got the key and when. Canary services (ex: canarytokens.org AWS keys) work too.
        /// </summary>
        public String[] CanaryAwsKeys;

        /// <summary>
        /// The minimum delay of a decoy answer, in ms (like a normal server: a long delay is a honeypot tell)
        /// </summary>
        public int DecoyMinDelayMs = 20;

        /// <summary>
        /// The maximum delay of a decoy answer, in ms
        /// </summary>
        public int DecoyMaxDelayMs = 180;
    }
}
