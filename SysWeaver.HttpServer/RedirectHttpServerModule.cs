using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SysWeaver.Data;

namespace SysWeaver.Net
{

    /// <summary>
    /// A http module that can be used to redirect requests
    /// </summary>
    public sealed class RedirectHttpServerModule : IHttpServerModule, IDisposable
    {
        static readonly IReadOnlySet<int> ValidCodes = ReadOnlyData.Set(
            301, 302, 307, 308
        );

        const String Prefix = "[RedirectModule] ";

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
                var lines = File.ReadAllLines(filename);
                int rowNumber = 0;
                foreach (var line in lines)
                {
                    ++rowNumber;
                    var row = line?.Trim();
                    var ci = row.IndexOf('#');
                    if (ci == 0)
                        continue;
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


        static readonly String This = "[Implicit folder] from Redirect Module";

        public IEnumerable<IHttpServerEndPoint> EnumEndPoints(string root = null)
        {
            var t = This;
            var s = EnvInfo.AppStart;
            var p = TableDataConsts.ExternalInfoPath;
            if (root == null)
            {
                foreach (var x in ExternalInfos.Keys)
                    yield return new HttpServerEndPoint(p + x, t, s);
            }else
            {
                if (root == "")
                {
                    yield return new HttpServerEndPoint(p.Substring(0, p.Length - 1), t, s);
                }
                if (root == p)
                {
                    foreach (var x in ExternalInfos.Keys)
                        yield return new HttpServerEndPoint(p + x, t, s);
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
            { "country", v => "https://countrycode.org/" + v },
            { "currency", v => CurrencyLinks.TryGetValue(v.FastToLower(), out var x) ? ("https://www.xe.com/currency/" + x) : null },
            { "useragent", v => "https://gs.statcounter.com/detect?useragent=" + v },
            { "ip", v => "https://ip.me/ip/" + v },
            { "mac", v => "https://maclookup.app/search/result?mac=" + v },
        }.Freeze();

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

        public IHttpRequestHandler Handler(HttpServerRequest context)
        {
            var host = context.Uri.Host;
            if (context.LocalUrl.FastStartsWith(TableDataConsts.ExternalInfoPath))
                return ExternalInfoHandler(context);
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
                            map[x.Key.Replace("*", host)] = Tuple.Create(rd.Item1.Replace("*", host), rd.Item2);
                        }
                        fn = Tuple.Create(map, StringTree.Build(map.Keys, cs));
                        c[host] = fn;
                    }
                }
            }
            var url = context.Url;
            var pre = fn.Item2.StartsWithAny(url);
            if (pre == null)
                return null;
            var to = fn.Item1[pre];
            var newUrl = to.Item1 + url.Substring(pre.Length);
            context.SetResStatusCode(to.Item2);
            context.SetResHeader("Location", newUrl);
            return HttpServerTools.AlreadyHandled;
        }

        public override string ToString() =>
            String.Concat(TableDataConsts.ExternalInfoPath, '[', String.Join(", ", ExternalInfos.Select(x => x.Key)), ']');

    }
}
