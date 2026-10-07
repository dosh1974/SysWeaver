using System;
using System.Text.RegularExpressions;

namespace SysWeaver
{
    public static partial class ExceptionExt
    {
        /// <summary>
        /// The text that replaces a removed (masked) value
        /// </summary>
        public const String MaskedText = "***";

        /// <summary>
        /// The text that is returned if an exception text couldn't be filtered (should never happen)
        /// </summary>
        public const String RemovedText = "[Details removed]";

        /// <summary>
        /// Get the <see cref="Exception.Message"/> of an exception with sensitive information removed (see <see cref="SafeExceptionText(String)"/>).
        /// Use this when an exception message is sent to a client (http responses, json error objects, web socket / MCP errors, chat messages etc).
        /// </summary>
        /// <param name="ex">The exception, may be null</param>
        /// <returns>The filtered message, null if <paramref name="ex"/> is null</returns>
        /// <remarks>Normal messages (ex: "Language X is not supported!") are returned unchanged (same instance).</remarks>
        public static String SafeMessage(this Exception ex)
            => ex == null ? null : SafeExceptionText(ex.Message);

        /// <summary>
        /// Get the <see cref="Exception.ToString"/> of an exception (type, message, inner exceptions and stack trace) with sensitive information removed (see <see cref="SafeExceptionText(String)"/>).
        /// Use this when the full exception text is sent to a client (ex: in DEBUG builds).
        /// </summary>
        /// <param name="ex">The exception, may be null</param>
        /// <returns>The filtered text, null if <paramref name="ex"/> is null</returns>
        /// <remarks>Stack trace source locations keeps the file name and line number (ex: "in x.cs:line 12"), the folders are removed.</remarks>
        public static String SafeText(this Exception ex)
            => ex == null ? null : SafeExceptionText(ex.ToString());

        /// <summary>
        /// Remove sensitive information from an exception text (or any text that may contain one), so that it can be sent to a client.
        /// <para>Removed / masked (replaced with <see cref="MaskedText"/>):</para>
        /// <list type="bullet">
        /// <item>File system paths (Windows drive and UNC paths, Unix paths starting with a well known root folder or "~"), only the file name (or the last folder name) is kept, ex: "C:\Users\me\app\x.cs:line 12" becomes "x.cs:line 12".</item>
        /// <item>Values of secret / connection string keys (password, pwd, user id, uid, user, server, host, data source, api key, secret, token, key, sig etc) in "key=value" and json ("key": "value") form.</item>
        /// <item>User info in urls (ex: "https://user:pass@host" becomes "https://***@host"), bearer and basic authorization tokens, JWT's and well known api key formats (sk-..., AKIA..., AIza..., ghp_..., xox?-...).</item>
        /// <item>Private (LAN) IP addresses (10.x.x.x, 172.16-31.x.x, 192.168.x.x, 169.254.x.x, fc00::/7, fe80::/10) and internal host names (*.local, *.internal, *.lan, *.corp, *.intranet, *.localdomain, *.home.arpa).</item>
        /// <item>SQL details: quoted values in SQL statements, the "near '...'" fragment and "Duplicate entry '...'" value of (MySQL) syntax errors and the user / host in "Access denied for user '...'@'...'".</item>
        /// </list>
        /// <para>Kept: normal messages, file names, public IP addresses and host names, e-mail addresses, guids, numbers, assembly versions and stack trace method names.</para>
        /// </summary>
        /// <param name="text">The text, may be null</param>
        /// <returns>The filtered text (the same instance if nothing was removed), null if <paramref name="text"/> is null</returns>
        /// <remarks>
        /// This is a best effort (heuristic) filter, it can't detect every kind of sensitive information (ex: secrets that aren't in a key / value form), so don't put secrets in exception messages.
        /// The regular expressions are source generated (compiled at build time) and each one is only used if the text contains a char it requires, so texts without any special chars are only scanned for tokens.
        /// </remarks>
        public static String SafeExceptionText(String text)
        {
            if (String.IsNullOrEmpty(text))
                return text;
            try
            {
                var s = text.AsSpan();
                bool hasEq = s.Contains('=');
                bool hasQuote = s.Contains('\'');
                bool hasDQuote = s.Contains('"');
                bool hasColon = s.Contains(':');
                bool hasDot = s.Contains('.');
                bool hasSep = s.ContainsAny('/', '\\');
                //  Secrets
                if (hasEq)
                    text = SecretKeyValueRegex().Replace(text, "${k}" + MaskedText);
                if (hasDQuote && hasColon)
                    text = SecretJsonRegex().Replace(text, "${k}\"" + MaskedText + "\"");
                if (hasColon && hasSep && s.Contains('@'))
                    text = UrlUserInfoRegex().Replace(text, "${s}" + MaskedText + "@");
                text = AuthTokenRegex().Replace(text, "${k}" + MaskedText);
                text = KnownTokenRegex().Replace(text, MaskedText);
                //  SQL
                if (hasQuote)
                {
                    text = SqlNearRegex().Replace(text, "${k}" + MaskedText);
                    text = SqlDuplicateRegex().Replace(text, "${k}" + MaskedText);
                    text = SqlUserRegex().Replace(text, "${k}" + MaskedText + "'@'" + MaskedText + "'");
                    text = SqlStatementRegex().Replace(text, SqlStatementEval);
                }
                //  Network
                if (hasDot)
                {
                    text = PrivateIpV4Regex().Replace(text, MaskedText);
                    text = InternalHostRegex().Replace(text, MaskedText);
                }
                if (hasColon)
                    text = PrivateIpV6Regex().Replace(text, MaskedText);
                //  Paths
                if (hasSep)
                {
                    text = WindowsPathRegex().Replace(text, PathEval);
                    text = UnixPathRegex().Replace(text, PathEval);
                }
                return text;
            }
            catch (RegexMatchTimeoutException)
            {
                return RemovedText;
            }
        }

        /// <summary>
        /// Max time for a single regex operation (the expressions are linear, so this should never happen)
        /// </summary>
        const int RegexTimeoutMs = 1000;

        static readonly MatchEvaluator PathEval = PathReplace;
        static readonly MatchEvaluator SqlStatementEval = SqlStatementReplace;

        /// <summary>
        /// Keep the file name, or the last folder name if the path ends with a separator
        /// </summary>
        static String PathReplace(Match m)
        {
            var f = m.Groups["f"].Value;
            if (f.Length > 0)
                return f;
            var d = m.Groups["d"];
            if (d.Success)
                return d.Value + m.Value[m.Length - 1];
            return MaskedText;
        }

        /// <summary>
        /// Mask all quoted values in an SQL statement
        /// </summary>
        static String SqlStatementReplace(Match m)
            => SqlLiteralRegex().Replace(m.Value, "'" + MaskedText + "'");


        //  key=value secrets (connection strings, query strings, settings), the key must not be a part of a longer word (ex: "PublicKeyToken=" is kept)
        [GeneratedRegex(@"(?<k>(?<![A-Za-z0-9_\-])(?:password|passwd|pwd|pass|passphrase|user\s?id|uid|user\s?name|user|login|account\s?key|shared\s?access\s?key|shared\s?access\s?signature|client[_\-]?secret|secret|api[_\-]?key|access[_\-]?key|access[_\-]?token|refresh[_\-]?token|id[_\-]?token|auth[_\-]?token|token|key|sig|signature|credentials?|data\s?source|server|host|address|addr)\s*=\s*)(?<v>""[^""]*""|'[^']*'|[^;&,\s""'<>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SecretKeyValueRegex();

        //  "key": "value" json secrets
        [GeneratedRegex(@"(?<k>""(?:password|passwd|pwd|passphrase|secret|client_?secret|api_?key|access_?key|access_?token|refresh_?token|id_?token|auth_?token|token|authorization|connection_?string)""\s*:\s*)""(?:[^""\\]|\\.)*""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SecretJsonRegex();

        //  https://user:pass@host
        [GeneratedRegex(@"(?<s>\b[A-Za-z][A-Za-z0-9+.\-]*://)[^/\s@'""<>]+@", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex UrlUserInfoRegex();

        //  Bearer / Basic authorization values (must contain a digit, a symbol or a lower case letter followed by an upper case letter, so that "Bearer authentication" is kept)
        [GeneratedRegex(@"(?<k>\b(?:[Bb]earer|BEARER)\s+)(?=[A-Za-z0-9\-._~+/]*(?:[0-9\-._~+/]|[a-z][A-Z]))[A-Za-z0-9\-._~+/]{8,}=*|(?<k>\b(?:[Bb]asic|BASIC)\s+)(?=[A-Za-z0-9+/]*(?:[0-9+/=]|[a-z][A-Z]))[A-Za-z0-9+/]{8,}={0,2}", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex AuthTokenRegex();

        //  JWT's and well known api key formats
        [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]*|\b(?:sk|pk|rk)-[A-Za-z0-9_\-]{16,}|\bAKIA[0-9A-Z]{16}\b|\bAIza[0-9A-Za-z_\-]{30,}|\bgh[pousr]_[A-Za-z0-9]{30,}|\bxox[abposr]-[A-Za-z0-9\-]{10,}", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex KnownTokenRegex();

        //  MySQL: "... near 'fragment' at line 1"
        [GeneratedRegex(@"(?<k>\bnear ')(?s:.*?)(?=' at line \d)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SqlNearRegex();

        //  MySQL: "Duplicate entry 'value' for key 'x'"
        [GeneratedRegex(@"(?<k>\bDuplicate entry ')(?s:.*?)(?=' for key)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SqlDuplicateRegex();

        //  MySQL: "Access denied for user 'name'@'host'"
        [GeneratedRegex(@"(?<k>\bfor user ')[^'\r\n]*'@'[^'\r\n]*'", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SqlUserRegex();

        //  An (upper case) SQL statement, to the end of the line
        [GeneratedRegex(@"\b(?:SELECT|INSERT|UPDATE|DELETE|REPLACE|MERGE|UPSERT|CALL|EXEC|EXECUTE|VALUES|WHERE|SET)\b[^\r\n]*", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SqlStatementRegex();

        //  A quoted SQL value (an unterminated value is masked to the end)
        [GeneratedRegex(@"'(?:[^'\\]|\\.|'')*(?:'|$)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex SqlLiteralRegex();

        //  Private / link local IPv4 addresses (not part of a longer number or version, ex: "Version=10.0.0.0" is kept)
        [GeneratedRegex(@"(?<![\w.=])(?:10\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])|192\.168|169\.254)\.\d{1,3}\.\d{1,3}(?!\.?\d)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex PrivateIpV4Regex();

        //  Unique local (fc00::/7) and link local (fe80::/10) IPv6 addresses
        [GeneratedRegex(@"(?<![\w:])(?:f[cd][0-9a-f]{2}|fe[89ab][0-9a-f]):[0-9a-f:]*[0-9a-f](?:%\w+)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex PrivateIpV6Regex();

        //  Internal host names (lower case only, so that type names like "X.Internal" in stack traces are kept)
        [GeneratedRegex(@"(?<![\w\-.])(?:[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?\.)+(?:local|internal|lan|corp|intranet|localdomain|home\.arpa)(?![\w\-]|\.\w)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex InternalHostRegex();

        //  Windows drive ("C:\a\b.txt") and UNC ("\\server\share\b.txt") paths.
        //  Folder names may contain spaces if one of the next (up to 4) words is directly followed by a separator ("C:\Program Files (x86)\a.txt", but not "C:\a.txt or /x")
        [GeneratedRegex(@"(?<![A-Za-z0-9])(?:[A-Za-z]:|\\\\[^\\/\s""'<>|*?:]+)[\\/]+(?:(?<d>[^\\/\s""'<>|*?:]+(?: (?=(?:[^\\/\s""'<>|*?:]+ ){0,3}[^\\/\s""'<>|*?:]+[\\/])[^\\/\s""'<>|*?:]+)*)[\\/]+)*(?<f>[^\\/\s""'<>|*?:,;()\[\]]*)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex WindowsPathRegex();

        //  Unix paths starting with a well known root folder or "~" ("/home/me/a.txt")
        [GeneratedRegex(@"(?<=^|[\s""'(\[=,:/])(?:~|/(?:home|usr|var|etc|tmp|opt|mnt|srv|root|proc|sys|dev|app|apps|data|run|lib|lib64|bin|sbin|boot|media|Users|Volumes|private|snap|nix|workspace|workspaces|build|src|github|azp|agent|__w))/+(?:(?<d>[^\\/\s""'<>|*?:]+(?: (?=(?:[^\\/\s""'<>|*?:]+ ){0,3}[^\\/\s""'<>|*?:]+/)[^\\/\s""'<>|*?:]+)*)/+)*(?<f>[^\\/\s""'<>|*?:,;()\[\]]*)", RegexOptions.CultureInvariant, RegexTimeoutMs)]
        private static partial Regex UnixPathRegex();

    }
}
