using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// A http module that redirects requests whose full url starts with a configured prefix (ex: http to https upgrades),
    /// and that serves the "external info" redirects (country, currency, user agent, ip and mac lookups on external sites).
    /// </summary>
    /// <remarks>
    /// Redirections come from <see cref="RedirectHttpServerModuleParams.Redirections"/> or from a monitored text file (<see cref="RedirectHttpServerModuleParams.Filename"/>).
    /// If no redirections are configured (and no file is used), <see cref="HttpRedirection.HttpToHttps"/> is used.
    /// A '*' in a redirection is replaced with the bare host name of the request (ex: "example.com") and default ports are removed (request urls never contain them), the resolved redirections are cached per host.
    /// The redirect response is written directly (<see cref="HttpServerTools.AlreadyHandled"/>), so no auth checks are made.
    /// </remarks>
    public sealed class RedirectHttpServerModule : IHttpServerModule, IDisposable
    {
        static readonly IReadOnlySet<int> ValidCodes = ReadOnlyData.Set(
            301, 302, 307, 308
        );

        const String Prefix = "[RedirectModule] ";

        /// <summary>
        /// Create a redirect module.
        /// </summary>
        /// <param name="p">Parameters (null to use defaults, i.e an http to https redirection)</param>
        /// <param name="messageHandler">Optional message host for warnings about invalid redirections and file reloads</param>
        public RedirectHttpServerModule(RedirectHttpServerModuleParams p = null, IMessageHost messageHandler = null)
        {
            p = p ?? new RedirectHttpServerModuleParams();
            CaseSensitive = p.CaseSensitive;
            Msg = messageHandler;
            var cs = p.CaseSensitive;
            var cmp = cs ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var d = new Dictionary<string, Tuple<string, int>>(cmp);
            Redirs = d;
            Cache = new ConcurrentDictionary<string, Tuple<Dictionary<string, Tuple<string, int>>, StringTree>>(cmp);
            var fn = p.Filename?.Trim();
            if (String.IsNullOrEmpty(fn))
            {
                var r = p.Redirections;
                if (r != null)
                {
                    var valid = ValidCodes;
                    foreach (var x in r)
                    {
                        if (x == null)
                        {
                            messageHandler?.AddMessage(Prefix + "Redirection may not be null, ignoring!", MessageLevels.Warning);
                            continue;
                        }
                        var f = x.From;
                        if (String.IsNullOrEmpty(f))
                        {
                            messageHandler?.AddMessage(Prefix + "Redirection from may not be empty, ignoring!", MessageLevels.Warning);
                            continue;
                        }
                        var t = x.To;
                        if (String.IsNullOrEmpty(t))
                        {
                            messageHandler?.AddMessage(Prefix + "Redirection to may not be empty, ignoring!", MessageLevels.Warning);
                            continue;
                        }
                        var code = x.Code;
                        if (!valid.Contains(code))
                        {
                            messageHandler?.AddMessage(Prefix + "Redirection code must be 301, 302, 307 or 308, ignoring!", MessageLevels.Warning);
                            continue;
                        }
                        d[f] = Tuple.Create(t, code);
                    }
                }
                if (d.Count <= 0)
                {
                    var x = HttpRedirection.HttpToHttps;
                    d[x.From] = Tuple.Create(x.To, x.Code);
                }
            }else
            {
                fn = PathTemplate.Resolve(fn);
                TryLoad(fn);
                Fc = new OnFileChange(fn, TryLoad, 2000);
            }
        }

        /// <summary>
        /// Stop monitoring the redirection file (if any).
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref Fc, null)?.Dispose();
        }

        readonly IMessageHost Msg;

        OnFileChange Fc;

        static readonly IReadOnlySet<Char> UnquotedEnd = ReadOnlyData.Set(" \t\r\n".ToCharArray());
        static readonly IReadOnlySet<Char> QuotedEnd = ReadOnlyData.Set(" \t\r\n\"".ToCharArray());


        void SkipWhite(ref int pos, String t)
        {
            var tl = t.Length;
            var w = UnquotedEnd;
            while (pos < tl)
            {
                if (!w.Contains(t[pos]))
                    break;
                ++pos;
            }
        }

        String ParseOne(ref int pos, String t)
        {
            SkipWhite(ref pos, t);
            var tl = t.Length;
            if (pos >= tl)
                return null;
            bool q = t[pos] == '"';
            var s = UnquotedEnd;
            if (q)
            {
                ++pos;
                s = QuotedEnd;
            }
            var start = pos;
            while (pos < tl)
            {
                var c = t[pos];
                if (s.Contains(c))
                {
                    var val = t.Substring(start, pos - start);
                    if (q)
                        ++pos;
                    return val;
                }
                ++pos;
            }
            if (start == pos)
               return null;
            return t.Substring(start, pos - start);
        }

        /// <summary>
        /// Load redirections from a file, the current redirections are kept if the file doesn't exist or contains any invalid row.
        /// </summary>
        void TryLoad(String filename)
        {
            var msg = Msg;
            var cs = CaseSensitive;
            var cmp = cs ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var d = new Dictionary<string, Tuple<string, int>>(cmp);
            msg?.AddMessage(Prefix + "Parsing redirect file " + filename.ToQuoted(), MessageLevels.Debug);
            try
            {
                if (!File.Exists(filename))
                {
                    msg?.AddMessage(Prefix + "File " + filename.ToQuoted() + " does not exist!", MessageLevels.Warning);
                    return;
                }
                var valid = ValidCodes;
                var lines = FileExt.ReadLines(filename, null, true, true);
                int rowNumber = 0;
                foreach (var line in lines)
                {
                    ++rowNumber;
                    var ci = line.IndexOf('#');
                    if (ci == 0)
                        continue;
                    var row = line;
                    if (ci > 0)
                        row = row.Substring(0, ci).TrimEnd();
                    var rl = row.Length;
                    if (rl <= 0)
                        continue;
                    int pos = 0;
                    var f = ParseOne(ref pos, row);
                    if (String.IsNullOrEmpty(f))
                    {
                        msg?.AddMessage(Prefix + "Expected a from prefix in " + filename.ToQuoted() + " at row " + rowNumber + ", ignoring changes!", MessageLevels.Warning);
                        return;
                    }
                    var t = ParseOne(ref pos, row);
                    if (String.IsNullOrEmpty(t))
                    {
                        msg?.AddMessage(Prefix + "Expected a to prefix in " + filename.ToQuoted() + " at row " + rowNumber + ", ignoring changes!", MessageLevels.Warning);
                        return;
                    }
                    var codeStr = ParseOne(ref pos, row) ?? "302";
                    if (!int.TryParse(codeStr, out var code))
                    {
                        msg?.AddMessage(Prefix + "Expected a valid code in " + filename.ToQuoted() + " at row " + rowNumber + ", ignoring changes!", MessageLevels.Warning);
                        return;
                    }
                    if (!valid.Contains(code))
                    {
                        msg?.AddMessage(Prefix + "Expected a valid code in " + filename.ToQuoted() + " at row " + rowNumber + ", ignoring changes!", MessageLevels.Warning);
                        return;
                    }
                    d[f] = Tuple.Create(t, code);
                    using (msg?.Tab())
                        msg?.AddMessage(Prefix + "Found redirect " + f.ToQuoted() + " => " + t.ToQuoted() + " using code " + code, MessageLevels.Debug);
                }
            }
            catch (Exception ex)
            {
                msg?.AddMessage(Prefix + "Failed to parse " + filename.ToQuoted() + ", ignoring changes!", ex, MessageLevels.Warning);
                return;
            }
            var c = Cache;
            lock (c)
            {
                c.Clear();
                Redirs = d;
            }
            msg?.AddMessage(Prefix + "Redirections updated from file " + filename.ToQuoted());
        }

        readonly bool CaseSensitive;
        Dictionary<String, Tuple<String, int>> Redirs;
        readonly ConcurrentDictionary<String, Tuple<Dictionary<String, Tuple<String, int>>, StringTree>> Cache;

        /// <summary>
        /// The maximum number of host names in <see cref="Cache"/>, the cache is cleared when full.
        /// </summary>
        const int MaxCachedHosts = 1024;


        static readonly String This = "[Implicit folder] from Redirect Module";

        /// <summary>
        /// Enumerates the external info end points (the configured redirections are not enumerated).
        /// </summary>
        /// <param name="root">Null to enumerate all, else the folder to enumerate</param>
        /// <returns>End point information</returns>
        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            var t = This;
            var lwt = HttpServerTools.StartedTime;
            var etag = HttpServerTools.StartedETag;
            var p = TableDataConsts.ExternalInfoPath;
            if (root == null)
            {
                foreach (var x in ExternalInfos.Keys)
                    yield return new HttpServerEndPoint(p + x, t, lwt, etag);
            }else
            {
                if (root == "")
                {
                    yield return new HttpServerEndPoint(p.Substring(0, p.Length - 1), t, lwt, etag);
                }
                if (root == p)
                {
                    foreach (var x in ExternalInfos.Keys)
                        yield return new HttpServerEndPoint(p + x, t, lwt, etag);
                }
            }
        }

        static Dictionary<String, String> GetCurrencyLinks()
        {
            const String links = "aed:aed-emirati-dirham,afn:afn-afghan-afghani,all:all-albanian-lek,amd:amd-armenian-dram,ang:ang-dutch-guilder,aoa:aoa-angolan-kwanza,ars:ars-argentine-peso,aud:aud-australian-dollar,awg:awg-aruban-or-dutch-guilder,azn:azn-azerbaijan-manat,bam:bam-bosnian-convertible-mark,bbd:bbd-barbadian-or-bajan-dollar,bdt:bdt-bangladeshi-taka,bgn:bgn-bulgarian-lev,bhd:bhd-bahraini-dinar,bif:bif-burundian-franc,bmd:bmd-bermudian-dollar,bnd:bnd-bruneian-dollar,bob:bob-bolivian-bol%C3%ADviano,bov:,brl:brl-brazilian-real,bsd:bsd-bahamian-dollar,btn:btn-bhutanese-ngultrum,bwp:bwp-botswana-pula,byn:byn-belarusian-ruble,bzd:bzd-belizean-dollar,cad:cad-canadian-dollar,cdf:cdf-congolese-franc,che:,chf:chf-swiss-franc,chw:,clf:,clp:clp-chilean-peso,cny:cny-chinese-yuan-renminbi,cop:cop-colombian-peso,cou:,crc:crc-costa-rican-colon,cuc:cuc-cuban-convertible-peso,cup:cup-cuban-peso,cve:cve-cape-verdean-escudo,czk:czk-czech-koruna,dem:,djf:djf-djiboutian-franc,dkk:dkk-danish-krone,dop:dop-dominican-peso,dzd:dzd-algerian-dinar,egp:egp-egyptian-pound,ern:ern-eritrean-nakfa,etb:etb-ethiopian-birr,eur:eur-euro,fjd:fjd-fijian-dollar,fkp:fkp-falkland-island-pound,gbp:gbp-british-pound,gel:gel-georgian-lari,ghs:ghs-ghanaian-cedi,gip:gip-gibraltar-pound,gmd:gmd-gambian-dalasi,gnf:gnf-guinean-franc,gtq:gtq-guatemalan-quetzal,gyd:gyd-guyanese-dollar,hkd:hkd-hong-kong-dollar,hnl:hnl-honduran-lempira,hrk:hrk-croatian-kuna,htg:htg-haitian-gourde,huf:huf-hungarian-forint,idr:idr-indonesian-rupiah,ils:ils-israeli-shekel,inr:inr-indian-rupee,iqd:iqd-iraqi-dinar,irr:irr-iranian-rial,isk:isk-icelandic-krona,jmd:jmd-jamaican-dollar,jod:jod-jordanian-dinar,jpy:jpy-japanese-yen,kes:kes-kenyan-shilling,kgs:kgs-kyrgyzstani-som,khr:khr-cambodian-riel,kmf:kmf-comorian-franc,kpw:kpw-north-korean-won,krw:krw-south-korean-won,kwd:kwd-kuwaiti-dinar,kyd:kyd-caymanian-dollar,kzt:kzt-kazakhstani-tenge,lak:lak-lao-kip,lbp:lbp-lebanese-pound,lkr:lkr-sri-lankan-rupee,lrd:lrd-liberian-dollar,lsl:lsl-basotho-loti,lyd:lyd-libyan-dinar,mad:mad-moroccan-dirham,mdl:mdl-moldovan-leu,mga:mga-malagasy-ariary,mkd:mkd-macedonian-denar,mmk:mmk-burmese-kyat,mnt:mnt-mongolian-tughrik,mop:mop-macau-pataca,mro:,mur:mur-mauritian-rupee,mvr:mvr-maldivian-rufiyaa,mwk:mwk-malawian-kwacha,mxn:mxn-mexican-peso,mxv:,myr:myr-malaysian-ringgit,mzn:mzn-mozambican-metical,nad:nad-namibian-dollar,ngn:ngn-nigerian-naira,nio:nio-nicaraguan-cordoba,nok:nok-norwegian-krone,npr:npr-nepalese-rupee,nzd:nzd-new-zealand-dollar,omr:omr-omani-rial,pab:pab-panamanian-balboa,pen:pen-peruvian-sol,pgk:pgk-papua-new-guinean-kina,php:php-philippine-peso,pkr:pkr-pakistani-rupee,pln:pln-polish-zloty,pyg:pyg-paraguayan-guarani,qar:qar-qatari-riyal,ron:ron-romanian-leu,rsd:rsd-serbian-dinar,rub:rub-russian-ruble,rwf:rwf-rwandan-franc,sar:sar-saudi-arabian-riyal,sbd:sbd-solomon-islander-dollar,scr:scr-seychellois-rupee,sdg:sdg-sudanese-pound,sek:sek-swedish-krona,sgd:sgd-singapore-dollar,shp:shp-saint-helenian-pound,sll:sll-sierra-leonean-leone,sos:sos-somali-shilling,srd:srd-surinamese-dollar,ssp:,std:,svc:svc-salvadoran-colon,syp:syp-syrian-pound,szl:szl-swazi-lilangeni,thb:thb-thai-baht,tjs:tjs-tajikistani-somoni,tmt:tmt-turkmenistani-manat,tnd:tnd-tunisian-dinar,top:top-tongan-pa&#x27;anga,try:try-turkish-lira,ttd:ttd-trinidadian-dollar,twd:twd-taiwan-new-dollar,tzs:tzs-tanzanian-shilling,uah:uah-ukrainian-hryvnia,ugx:ugx-ugandan-shilling,usd:usd-us-dollar,usn:,uyi:,uyu:uyu-uruguayan-peso,uzs:uzs-uzbekistani-som,vef:vef-venezuelan-bol%C3%ADvar,vnd:vnd-vietnamese-dong,vuv:vuv-ni-vanuatu-vatu,wst:wst-samoan-tala,xaf:xaf-central-african-cfa-franc-beac,xcd:xcd-east-caribbean-dollar,xof:xof-cfa-franc,xpf:xpf-cfp-franc,yer:yer-yemeni-rial,zar:zar-south-african-rand,zmw:zmw-zambian-kwacha,zwd:zwd-zimbabwean-dollar,stn:stn-sao-tomean-dobra,sle:sle-sierra-leonean-leone";
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in links.Split(","))
            {
                var t = x.Split(':');
                d.Add(t[0], t[1]);
            }
            return d;
        }

        static readonly IReadOnlyDictionary<String, String> CurrencyLinks = GetCurrencyLinks().Freeze();

        static readonly IReadOnlyDictionary<String, Func<String, String>> ExternalInfos = new Dictionary<string, Func<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "country", v => "https://countrycode.org/" + Uri.EscapeDataString(v) },
            { "currency", v => CurrencyLinks.TryGetValue(v.FastToLower(), out var x) ? ("https://www.xe.com/currency/" + x) : null },
            { "useragent", v => "https://gs.statcounter.com/detect?useragent=" + Uri.EscapeDataString(v) },
            { "ip", v => "https://ip.me/ip/" + Uri.EscapeDataString(v).Replace("%3A", ":") },
            { "mac", v => "https://maclookup.app/search/result?mac=" + Uri.EscapeDataString(v) },
        }.Freeze();

        /// <summary>
        /// Handles a request to the external info path ("{ExternalInfoPath}{type}/{value}") by responding with a 302 redirect to an external lookup site.
        /// </summary>
        /// <param name="context">The request, the local url must start with <see cref="TableDataConsts.ExternalInfoPath"/></param>
        /// <returns><see cref="HttpServerTools.AlreadyHandled"/> if a redirect was written, else null</returns>
        /// <remarks>The (decoded) value is url encoded before it's appended to the external url.</remarks>
        public IHttpRequestHandler ExternalInfoHandler(HttpServerRequest context)
        {
            var lp = context.LocalUrl.Substring(TableDataConsts.ExternalInfoPath.Length);
            var fi = lp.IndexOf('/');
            if (fi < 0)
                return null;
            if (!ExternalInfos.TryGetValue(lp.Substring(0, fi), out var fn))
                return null;
            var url = fn(lp.Substring(fi + 1));
            if (url == null)
                return null;
            context.SetResStatusCode(302);
            context.SetResHeader("Location", url);
            return HttpServerTools.AlreadyHandled;
        }

        const String IndexFile = "index.html";

        static readonly Char[] HostEnd = ['/', ':', '?', '#'];
        static readonly Char[] AuthorityEnd = ['/', '?', '#'];

        /// <summary>
        /// Get the bare host name of an absolute url (the part between "://" and the port / path), ex: "example.com" for "http://example.com:8080/a".
        /// IPv6 hosts are returned with brackets (ex: "[::1]").
        /// </summary>
        static String GetHostName(String url)
        {
            var start = url.IndexOf("://", StringComparison.Ordinal);
            if (start < 0)
                return String.Empty;
            start += 3;
            var ul = url.Length;
            int end;
            if ((start < ul) && (url[start] == '['))
            {
                end = url.IndexOf(']', start);
                end = end < 0 ? ul : end + 1;
            }
            else
            {
                end = url.IndexOfAny(HostEnd, start);
                if (end < 0)
                    end = ul;
            }
            return url.Substring(start, end - start);
        }

        /// <summary>
        /// Remove an explicit default port (":80" for http, ":443" for https) from an absolute url, since request urls never contain a default port.
        /// </summary>
        static String RemoveDefaultPort(String url)
        {
            String port;
            int start;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                port = ":80";
                start = 7;
            }
            else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                port = ":443";
                start = 8;
            }
            else
                return url;
            var end = url.IndexOfAny(AuthorityEnd, start);
            if (end < 0)
                end = url.Length;
            var pl = port.Length;
            if (((end - start) <= pl) || (String.CompareOrdinal(url, end - pl, port, 0, pl) != 0))
                return url;
            return String.Concat(url.AsSpan(0, end - pl), url.AsSpan(end));
        }

        /// <summary>
        /// Chars that can be copied as is to a Location header (printable ASCII that is valid in an url, '%' is handled separately).
        /// </summary>
        static readonly SearchValues<Char> SafeLocationChars = SearchValues.Create(
            Enumerable.Range(0x21, 0x7f - 0x21).Select(x => (Char)x).Where(x => "%\"<>\\^`{|}".IndexOf(x) < 0).ToArray());

        const String HexChars = "0123456789ABCDEF";

        /// <summary>
        /// True if there is a valid "%XX" escape at the given index
        /// </summary>
        static bool IsEscape(ReadOnlySpan<Char> s, int i)
            => ((i + 2) < s.Length) && Char.IsAsciiHexDigit(s[i + 1]) && Char.IsAsciiHexDigit(s[i + 2]);

        /// <summary>
        /// Percent encode the chars of an url (remainder) that are invalid in an url or in a header value (controls, space, non-ASCII as UTF-8, '"', '&lt;', '&gt;', '\', '^', '`', '{', '|', '}'),
        /// and any '%' that doesn't start a valid "%XX" escape. Valid escapes and reserved chars ('?', '#', '/', '&amp;' etc) are kept as is.
        /// </summary>
        /// <param name="s">The text</param>
        /// <param name="start">The index of the first char to use</param>
        /// <returns>The (possibly escaped) text from <paramref name="start"/></returns>
        static String EscapeLocation(String s, int start)
        {
            var span = s.AsSpan(start);
            var safe = SafeLocationChars;
            var i = span.IndexOfAnyExcept(safe);
            //  Skip valid escapes
            while ((i >= 0) && (span[i] == '%') && IsEscape(span, i))
            {
                var n = span.Slice(i + 3).IndexOfAnyExcept(safe);
                i = n < 0 ? -1 : (i + 3 + n);
            }
            //  Nothing to escape (the common case)
            if (i < 0)
                return start == 0 ? s : s.Substring(start);
            var sb = new StringBuilder(span.Length + 16);
            sb.Append(span.Slice(0, i));
            Span<Byte> utf8 = stackalloc Byte[4];
            var l = span.Length;
            while (i < l)
            {
                var c = span[i];
                if (safe.Contains(c))
                {
                    sb.Append(c);
                    ++i;
                    continue;
                }
                if (c == '%')
                {
                    if (IsEscape(span, i))
                    {
                        sb.Append(span.Slice(i, 3));
                        i += 3;
                    }
                    else
                    {
                        sb.Append("%25");
                        ++i;
                    }
                    continue;
                }
                //  Encode the code point as UTF-8 (a lone surrogate becomes U+FFFD)
                int cl = (Char.IsHighSurrogate(c) && ((i + 1) < l) && Char.IsLowSurrogate(span[i + 1])) ? 2 : 1;
                var bl = Encoding.UTF8.GetBytes(span.Slice(i, cl), utf8);
                for (int j = 0; j < bl; ++j)
                {
                    var b = utf8[j];
                    sb.Append('%');
                    sb.Append(HexChars[b >> 4]);
                    sb.Append(HexChars[b & 15]);
                }
                i += cl;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Find the index in the raw (undecoded) url where a decoded prefix ends, i.e the smallest index where the url decoded part of the raw url equals the decoded prefix.
        /// </summary>
        /// <param name="raw">The raw url</param>
        /// <param name="url">The decoded url</param>
        /// <param name="pl">The length of the decoded prefix</param>
        /// <returns>The index in the raw url, -1 if the decoded prefix can't be mapped to the raw url</returns>
        static int GetRawPrefixLength(String raw, String url, int pl)
        {
            var rl = raw.Length;
            if (rl < pl)
                return -1;
            var dp = url.AsSpan(0, pl);
            //  Fast path: the prefix part isn't encoded (normally the case, the authority is never encoded)
            var rp = raw.AsSpan(0, pl);
            if (!rp.ContainsAny('%', '+'))
                return rp.SequenceEqual(dp) ? pl : -1;
            //  Slow path (only if the matched prefix contains encoded chars): decode increasingly longer parts of the raw url until it matches the prefix.
            //  A decoded char comes from at most 9 raw chars (a 3 byte UTF-8 sequence)
            var max = (int)Math.Min(rl, (pl * 9L) + 9);
            var rented = ArrayPool<Char>.Shared.Rent(max);
            try
            {
                for (int k = pl; k <= max; ++k)
                {
                    var n = HttpServerTools.UrlDecode(raw.AsSpan(0, k), rented, out _);
                    if ((n == pl) && rented.AsSpan(0, n).SequenceEqual(dp))
                        return k;
                }
            }
            finally
            {
                ArrayPool<Char>.Shared.Return(rented);
            }
            return -1;
        }

        /// <summary>
        /// Get the part of the request url after the matched prefix, as it should be appended to the redirect url.
        /// </summary>
        /// <param name="context">The request</param>
        /// <param name="url">The decoded request url</param>
        /// <param name="pl">The length of the matched (decoded) prefix</param>
        /// <returns>The remainder, url encoded</returns>
        static String GetRemainder(HttpServerRequest context, String url, int pl)
        {
            var raw = context.RawUrl;
            //  Prefer the raw url (not decoded and without any inserted "index.html"), so that encoded chars keeps their encoding
            if ((raw != null) && !String.Equals(raw, url, StringComparison.Ordinal))
            {
                var rs = GetRawPrefixLength(raw, url, pl);
                if (rs >= 0)
                    return EscapeLocation(raw, rs);
            }
            //  Fallback: the raw url is the decoded url (ex: manual requests), or the prefix couldn't be mapped to the raw url
            if (context.DidIndex)
            {
                //  Don't redirect to the "index.html" inserted by the server for directory requests (keep the url as requested)
                //  QueryStringStart is the index after the '?' (0 if there is no query string)
                var qs = context.QueryStringStart - 1;
                var e = qs < 0 ? url.Length : qs;
                var s = e - IndexFile.Length;
                if ((s >= pl) && (String.CompareOrdinal(url, s, IndexFile, 0, IndexFile.Length) == 0))
                    url = String.Concat(url.AsSpan(0, s), url.AsSpan(e));
            }
            return EscapeLocation(url, pl);
        }

        /// <summary>
        /// If the request url starts with a configured prefix, writes a redirect response (the matched prefix is replaced, the rest of the url is kept).
        /// </summary>
        /// <param name="context">The request</param>
        /// <returns><see cref="HttpServerTools.AlreadyHandled"/> if a redirect was written, else null</returns>
        /// <remarks>
        /// A '*' in a redirection is replaced with the bare host name of the request (ex: "example.com"), and default ports (":80" for http, ":443" for https)
        /// are removed from the resolved redirections, since request urls never contain them (ex: "http://*:80/" matches "http://example.com/").
        /// The prefix is matched against the decoded request url (<see cref="HttpServerRequest.Url"/>), but the remainder is taken from the undecoded url (<see cref="HttpServerRequest.RawUrl"/>),
        /// so percent encoded chars (ex: "%3F", "%23", "%25", "%20", "%0D%0A") keep their encoding (no changed meaning, no header injection).
        /// If the raw url is the decoded url (ex: <see cref="ManualHttpServerRequest"/>), or the prefix can't be mapped to the raw url, the remainder is taken from the decoded url.
        /// Chars that are invalid in an url or in a header (controls, space, non-ASCII etc) are always percent encoded.
        /// The "index.html" inserted by the server for directory requests (<see cref="HttpServerRequest.DidIndex"/>) is not included in the remainder.
        /// </remarks>
        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            if (context.LocalUrl.FastStartsWith(TableDataConsts.ExternalInfoPath))
                return ExternalInfoHandler(context);

            var url = context.Url;
            var host = GetHostName(url);
            var c = Cache;
            var cs = CaseSensitive;
            if (!c.TryGetValue(host, out var fn))
            {
                lock (c)
                {
                    if (!c.TryGetValue(host, out fn))
                    {
                        var map = new Dictionary<String, Tuple<String, int>>(cs ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                        foreach (var x in Redirs)
                        {
                            var rd = x.Value;
                            var from = RemoveDefaultPort(x.Key.Replace("*", host));
                            //  A from of only "*" resolves to an empty string for an empty host, empty strings can't be added to a string tree (and would match every url)
                            if (from.Length <= 0)
                                continue;
                            map[from] = Tuple.Create(RemoveDefaultPort(rd.Item1.Replace("*", host)), rd.Item2);
                        }
                        fn = Tuple.Create(map, StringTree.Build(map.Keys, cs));
                        //  The host name may be client controlled (wild card prefixes), so limit the cache size
                        if (c.Count >= MaxCachedHosts)
                            c.Clear();
                        c[host] = fn;
                    }
                }
            }
            var pre = fn.Item2.StartsWithAny(url);
            if (pre == null)
                return null;
            var to = fn.Item1[pre];
            var newUrl = to.Item1 + GetRemainder(context, url, pre.Length);
            context.SetResStatusCode(to.Item2);
            context.SetResHeader("Location", newUrl);
            return HttpServerTools.AlreadyHandled;
        }

        public override string ToString() =>
            String.Concat(TableDataConsts.ExternalInfoPath, '[', String.Join(", ", ExternalInfos.Select(x => x.Key)), ']');

    }

}
