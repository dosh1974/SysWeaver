using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using SysWeaver.Auth;
using SysWeaver.Compression;
using SysWeaver.Data;
using SysWeaver.Media;
using SysWeaver.MicroService;
using SysWeaver.Security;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {

        #region .ico

        //  https://en.wikipedia.org/wiki/ICO_(file_format)#Outline

        static readonly int[] IconSizes = [192, 48, 32, 16];

        static void WriteLE(Span<Byte> s, int offset, int value)
        {
            s[offset] = (Byte)value;
            ++offset;
            value >>= 8;
            s[offset] = (Byte)value;
            ++offset;
            value >>= 8;
            s[offset] = (Byte)value;
            ++offset;
            value >>= 8;
            s[offset] = (Byte)value;
        }


        static void WriteIcon(Byte[] icoData, int[] sizes, Memory<Byte>[] images)
        {
            var sl = sizes.Length;
            int destPos = 6 + 16 * sl;
            var s = icoData.AsSpan();
            s[2] = 1; // ICO
            s[4] = (Byte)sl; // # images
            int o = 6;
            for (int i = 0; i < sl; ++i)
            {
                var ss = sizes[i];
                s[o + 0] = (Byte)ss;
                s[o + 1] = (Byte)ss;
                var d = images[i];
                var dl = d.Length;
                WriteLE(s, o + 8, dl);
                WriteLE(s, o + 12, destPos);
                d.Span.CopyTo(s.Slice(destPos));
                destPos += dl;
                o += 16;
            }
        }

        static async Task<IHttpRequestHandler> HandleSvgToPng(HttpServerRequest data, HttpSession session, int sizeAndKey, SvgBitmapRenderer bm, ConcurrentDictionary<int, Tuple<IHttpRequestHandler, SvgBitmapRenderer>> cache, AsyncLock aslock, int height = 0)
        {
            if (cache.TryGetValue(sizeAndKey, out var ico))
                if (bm == ico.Item2)
                    return ico.Item1;
            using var _ = await aslock.Lock().ConfigureAwait(false);
            if (cache.TryGetValue(sizeAndKey, out ico))
                if (bm == ico.Item2)
                    return ico.Item1;
            if (sizeAndKey <= 0)
            {
                var sizes = IconSizes;
                var sl = sizes.Length;
                int size = 6 + 16 * sl;
                Memory<Byte>[] images = new Memory<byte>[sl];
                for (int i = 0; i < sl; ++i)
                {
                    var ss = sizes[i];
                    var d = bm.ToPng(ss, ss);
                    images[i] = d;
                    size += d.Length;
                }
                var icoData = new Byte[size];
                WriteIcon(icoData, sizes, images);
                IHttpRequestHandler icoHandler = new StaticMemoryHttpRequestHandler(data.LocalUrl, "Generated", icoData, IcoMime, null, 30, 25, HttpServerTools.StartedText, null);
                ico = Tuple.Create(icoHandler, bm);
            }
            else
            {
                var image = bm.ToPng(sizeAndKey, height <= 0 ? sizeAndKey : height);
                IHttpRequestHandler icoHandler = new StaticMemoryHttpRequestHandler(data.LocalUrl, "Generated", image, PngMime, null, 30, 25, HttpServerTools.StartedText, null);
                ico = Tuple.Create(icoHandler, bm);
            }
            cache[sizeAndKey] = ico;
            return ico.Item1;
        }

        static readonly HttpCompressionPriority SvgCompression = HttpCompressionPriority.GetSupportedEncoders();

        static readonly String IcoMime = MimeTypeMap.GetMimeType("ico").Item1;
        static readonly String PngMime = MimeTypeMap.GetMimeType("png").Item1;
        static readonly String SvgMime = MimeTypeMap.GetMimeType("svg").Item1;

        #endregion//.ico


        #region Favicon

        readonly AsyncLock FaviconLock = new AsyncLock();

        #region Auto gen

        ValueTask<IHttpRequestHandler> HandleFaviconSvg(HttpServerRequest data, HttpSession session)
        {
            var svg = CachedFaviconSvg;
            if (svg != null)
                return ValueTask.FromResult(svg);
            lock (FaviconLock)
            {
                svg = CachedFaviconSvg;
                if (svg != null)
                    return ValueTask.FromResult(svg);
                var svgS = new SvgScene(256, 256);
                svgS.AddFavIcon(EnvInfo.AppName, null, HashColors.AppColors);
                var svgText = svgS.ToSvg();
                var enc = Encoding.UTF8;
                var svgData = enc.GetBytes(svgText);
                var cmp = CompBrotliNET.Instance;
                var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
                svg = new StaticMemoryHttpRequestHandler("icon.svg", "Generated", svgMem, SvgMime, SvgCompression, 30, 25, HttpServerTools.StartedText, cmp);
                Interlocked.Exchange(ref CachedFaviconSvg, svg);
                return ValueTask.FromResult(svg);
            }
        }


        ValueTask<IHttpRequestHandler> HandleFaviconDebugSvg(HttpServerRequest data, HttpSession session)
        {
            var auth = session?.Auth?.Tokens;
            if (auth == null)
                return HttpServerTools.NullHttpRequestHandlerValueTask;
            if (!auth.Contains("debug"))
                return HttpServerTools.NullHttpRequestHandlerValueTask;
            int seed = int.Parse(data.GetRawQuery(EnvInfo.AppSeed.ToString()));
            var enc = Encoding.UTF8;
            var svgS = new SvgScene(256, 256);
            svgS.AddFavIcon(EnvInfo.AppName, null, new HashColors(EnvInfo.AppName, seed));
            var svgText = svgS.ToSvg();
            var svgData = enc.GetBytes(svgText);
            var cmp = CompBrotliNET.Instance;
            var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
            var svgHandler = new StaticMemoryHttpRequestHandler("icon_debug.svg", "Generated", svgMem, SvgMime, SvgCompression, 30, 25, HttpServerTools.StartedText, cmp);
            return ValueTask.FromResult((IHttpRequestHandler)svgHandler);
        }

        #endregion//Auto gen


        #region Render

        readonly AsyncLock FaviconRendererLock = new AsyncLock();
        volatile SvgBitmapRenderer SvgFaviconBitmapRenderer;
        volatile IHttpRequestHandler CachedFaviconSvg;

        DateTime LastFaviconCheck;
        volatile int FaviconCheck;
        volatile String FaviconLastModified;

        readonly ConcurrentDictionary<int, Tuple<IHttpRequestHandler, SvgBitmapRenderer>> CachedFaviconSizes = new();


        async Task<SvgBitmapRenderer> GetFaviconBitmapRenderer(HttpServerRequest data, HttpSession session)
        {
            var bm = SvgFaviconBitmapRenderer;
            var check = Interlocked.Exchange(ref FaviconCheck, 0) != 0;
            if ((bm != null) && check)
            {
                using var _ = await FaviconRendererLock.Lock().ConfigureAwait(false);
                var memLast = await InternalRead(data, data.Prefix + "icon.svg", session, FaviconLastModified).ConfigureAwait(false);
                if (memLast == null)
                    return bm;
                bm = new SvgBitmapRenderer(memLast.Item1);
                Interlocked.Exchange(ref FaviconLastModified, memLast.Item2);
                Interlocked.Exchange(ref SvgFaviconBitmapRenderer, bm)?.Dispose();
                CachedFaviconSizes.Clear();
            }
            else
            {
                if (bm != null)
                    return bm;
                using var _ = await FaviconRendererLock.Lock().ConfigureAwait(false);
                bm = SvgFaviconBitmapRenderer;
                if (bm != null)
                    return bm;
                var memLast = await InternalRead(data, data.Prefix + "icon.svg", session).ConfigureAwait(false);
                bm = new SvgBitmapRenderer(memLast.Item1);
                Interlocked.Exchange(ref FaviconLastModified, memLast.Item2);
                Interlocked.Exchange(ref SvgFaviconBitmapRenderer, bm)?.Dispose();
                return bm;
            }
            return bm;
        }

        #endregion//Render

        #region Handlers

        //https://atlasiko.com/blog/web-development/favicon-size/


        async ValueTask<IHttpRequestHandler> HandleFaviconPng(HttpServerRequest data, HttpSession session, int sizeAndKey)
        {
            var bm = await GetFaviconBitmapRenderer(data, session).ConfigureAwait(false);
            return await HandleSvgToPng(data, session, sizeAndKey, bm, CachedFaviconSizes, FaviconLock).ConfigureAwait(false);
        }

        ValueTask<IHttpRequestHandler> HandleFaviconIco(HttpServerRequest data, HttpSession session)
            => HandleFaviconPng(data, session, 0);

        ValueTask<IHttpRequestHandler> HandleFaviconPng180(HttpServerRequest data, HttpSession session)
            => HandleFaviconPng(data, session, 180);

        ValueTask<IHttpRequestHandler> HandleFaviconPng192(HttpServerRequest data, HttpSession session)
            => HandleFaviconPng(data, session, 192);

        ValueTask<IHttpRequestHandler> HandleFaviconPng512(HttpServerRequest data, HttpSession session)
            => HandleFaviconPng(data, session, 512);

        #endregion//Handlers


        #endregion//Favicon


        #region Logo

        readonly AsyncLock LogoLock = new AsyncLock();

        #region Auto gen

        ValueTask<IHttpRequestHandler> HandleLogoSvg(HttpServerRequest data, HttpSession session)
        {
            var svg = CachedLogoSvg;
            if (svg != null)
                return ValueTask.FromResult(svg);
            lock (LogoLock)
            {
                svg = CachedLogoSvg;
                if (svg != null)
                    return ValueTask.FromResult(svg);
                var enc = Encoding.UTF8;
                var svgS = new SvgScene(512, 384);
                svgS.AddFavIcon(EnvInfo.AppName, EnvInfo.AppDisplayName, HashColors.AppColors);
                var svgText = svgS.ToSvg();
                var svgData = enc.GetBytes(svgText);
                var cmp = CompBrotliNET.Instance;
                var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
                svg = new StaticMemoryHttpRequestHandler("logo.svg", "Generated", svgMem, SvgMime, SvgCompression, 30, 25, HttpServerTools.StartedText, cmp);
                Interlocked.Exchange(ref CachedLogoSvg, svg);
                return ValueTask.FromResult(svg);
            }
        }


        ValueTask<IHttpRequestHandler> HandleLogoDebugSvg(HttpServerRequest data, HttpSession session)
        {
            var auth = session?.Auth?.Tokens;
            if (auth == null)
                return HttpServerTools.NullHttpRequestHandlerValueTask;
            if (!auth.Contains("debug"))
                return HttpServerTools.NullHttpRequestHandlerValueTask;
            int seed = int.Parse(data.GetRawQuery(EnvInfo.AppSeed.ToString()));
            var enc = Encoding.UTF8;
            var svgS = new SvgScene(512, 384);
            svgS.AddFavIcon(EnvInfo.AppName, EnvInfo.AppDisplayName, new HashColors(EnvInfo.AppName, seed));
            var svgText = svgS.ToSvg();
            var svgData = enc.GetBytes(svgText);
            var cmp = CompBrotliNET.Instance;
            var svgMem = cmp.GetCompressed(svgData.AsSpan(), CompEncoderLevels.Best);
            var svgHandler = new StaticMemoryHttpRequestHandler("logo_debug.svg", "Generated", svgMem, SvgMime, SvgCompression, 30, 25, HttpServerTools.StartedText, cmp);
            return ValueTask.FromResult((IHttpRequestHandler)svgHandler);
        }

        #endregion//Auto gen


        #region Render

        readonly AsyncLock LogoRendererLock = new AsyncLock();
        volatile SvgBitmapRenderer SvgLogoBitmapRenderer;
        volatile IHttpRequestHandler CachedLogoSvg;

        DateTime LastLogoCheck;
        volatile int LogoCheck;
        volatile String LogoLastModified;

        readonly ConcurrentDictionary<int, Tuple<IHttpRequestHandler, SvgBitmapRenderer>> CachedLogoSizes = new();


        async Task<SvgBitmapRenderer> GetLogoBitmapRenderer(HttpServerRequest data, HttpSession session)
        {
            var bm = SvgLogoBitmapRenderer;
            var check = Interlocked.Exchange(ref LogoCheck, 0) != 0;
            if ((bm != null) && check)
            {
                using var _ = await LogoRendererLock.Lock().ConfigureAwait(false);
                var memLast = await InternalRead(data, data.Prefix + "logo.svg", session, LogoLastModified).ConfigureAwait(false);
                if (memLast == null)
                    return bm;
                bm = new SvgBitmapRenderer(memLast.Item1);
                Interlocked.Exchange(ref LogoLastModified, memLast.Item2);
                Interlocked.Exchange(ref SvgLogoBitmapRenderer, bm)?.Dispose();
                CachedLogoSizes.Clear();
            }
            else
            {
                if (bm != null)
                    return bm;
                using var _ = await LogoRendererLock.Lock().ConfigureAwait(false);
                bm = SvgLogoBitmapRenderer;
                if (bm != null)
                    return bm;
                var memLast = await InternalRead(data, data.Prefix + "logo.svg", session).ConfigureAwait(false);
                bm = new SvgBitmapRenderer(memLast.Item1);
                Interlocked.Exchange(ref LogoLastModified, memLast.Item2);
                Interlocked.Exchange(ref SvgLogoBitmapRenderer, bm)?.Dispose();
                return bm;
            }
            return bm;
        }

        #endregion//Render

        #region Handlers

        //https://atlasiko.com/blog/web-development/favicon-size/


        async ValueTask<IHttpRequestHandler> HandleLogoPng(HttpServerRequest data, HttpSession session, int sizeAndKey)
        {
            var bm = await GetLogoBitmapRenderer(data, session).ConfigureAwait(false);
            return await HandleSvgToPng(data, session, sizeAndKey, bm, CachedLogoSizes, LogoLock, (sizeAndKey * 3) / 4).ConfigureAwait(false);
        }

        ValueTask<IHttpRequestHandler> HandleLogoPng1024(HttpServerRequest data, HttpSession session)
            => HandleLogoPng(data, session, 1024);

        #endregion//Handlers


        #endregion//Logo

    }
}
