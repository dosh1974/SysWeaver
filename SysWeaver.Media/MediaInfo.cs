using ImageMagick;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{

    public sealed class MediaInfo
    {

        static readonly IReadOnlyDictionary<String, String> Keep = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "exif:Make", "Make" },
            { "exif:MakerNote", "Maker note" },
            { "exif:Model", "Model" },
            { "exif:Software", "Software" },
        }.Freeze();

        static async Task<MediaInfo> GetImageInfo(String filename, int width, int height, bool fill, String baseName)
        {
            using var image = await ImageTools.ReadImage(filename).ConfigureAwait(false);
            if (image == null)
                return null;
            image.AutoOrient();
            var w = image.Width;
            var h = image.Height;
            StringBuilder p = new StringBuilder();
            var c = image.Comment;
            if (!String.IsNullOrEmpty(c))
                p.AppendLine(c);
            if (!image.IsOpaque)
                p.AppendLine("Alpha Transparent");
            foreach (var x in image.AttributeNames)
            {
                var name = x;
                var val = image.GetAttribute(x);
                if (String.IsNullOrEmpty(val))
                    continue;
                if (!Keep.TryGetValue(x, out name))
                    continue;
                p.Append(name).Append(": ").AppendLine(val);
            }
            String iconFilename = null;
            if ((width > 0) && (height > 0))
            {
                if (fill)
                    ImageTools.FillInto(image, width, height);
                else
                    ImageTools.FitInto(image, width, height);
                //ImageTools.FitInto(image, width, height, false, MagickColor.FromRgba(0, 0, 0, 0));
                iconFilename = baseName + "_Icon.png";
                image.SetBitDepth(8, Channels.All);
                await image.WriteAsync(iconFilename, MagickFormat.Png).ConfigureAwait(false);
            }
            var m = new MediaInfo
            {
                Width = (int)w,
                Height = (int)h,
                IconFile = iconFilename,
                Desc = p.Length > 0 ? p.ToString() : null,
            };
            return m;
        }

        static Task<MediaInfo> GetVideoInfo(String filename, int width, int height, bool fill, String baseName)
            => Ffmpeg.GetMediaInfo(filename, width, height, fill, baseName);


        static void Gradient(ushort[] rgb, double x, int offset = 0)
        {
            SysWeaver.Media.ColorSpace.OkHsvToSrgb(out var rr, out var gg, out var bb, 2.6 - x * 2.3, 0.8, 0.9 - x * 0.2);
            rgb[offset + 0] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, rr * 65535.0));
            rgb[offset + 1] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, gg * 65535.0));
            rgb[offset + 2] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, bb * 65535.0));
        }

        static async Task<MediaInfo> GetAudioInfo(String filename, int width, int height, bool fill, String baseName)
        {
            const int superSample = 3;
            const int chunkX = 2;
            MediaInfo info = new MediaInfo();
            float[] buffer;
            int read;
            using (var str = new AudioFileReader(filename))
            {
                info.Duration = str.TotalTime.TotalSeconds;
                int channelCount = str.WaveFormat.Channels;
                int sampleRate = str.WaveFormat.SampleRate;
                switch (channelCount)
                {
                    case 1:
                        info.Desc = String.Concat("Mono @ ", sampleRate.ToString("# ###"), " Hz");
                        break;
                    case 2:
                        info.Desc = String.Concat("Stereo @ ", sampleRate.ToString("# ###"), " Hz");
                        break;
                    default:
                        info.Desc = String.Concat(channelCount, " channels @ ", sampleRate.ToString("# ###"), " Hz");
                        break;
                }
                if ((width <= 0) || (height <= 0))
                    return info;
                var sampleCount = str.TotalTime.Ticks;
                var countPerSec = (long)channelCount * (long)sampleRate;
                sampleCount *= countPerSec;
                sampleCount += (TimeSpan.TicksPerSecond - 1);
                sampleCount /= TimeSpan.TicksPerSecond;
                sampleCount += (countPerSec * 3);
                buffer = new float[(int)sampleCount];
                read = str.Read(buffer, 0, (int)sampleCount);
            }

            var screen = filename + ".png";
            if (!File.Exists(screen))
            {
                screen = filename + ".jpg";
                if (!File.Exists(screen))
                {
                    screen = Path.ChangeExtension(filename, "jpg");
                    if (!File.Exists(screen))
                    {
                        screen = Path.ChangeExtension(filename, "png");
                        if (!File.Exists(screen))
                            screen = null;
                    }
                }
            }
            bool haveBack = screen != null;


            width *= superSample;
            height *= superSample;



            int baseLine = (height * 3) >> 2;
            int amp = baseLine - 2;
            if (haveBack)
            {
                baseLine = (height * 7) >> 3;
                amp = (baseLine >> 1) - 2;
            }
            int ampNeg = height - baseLine - 2;

            int[] img = new int[width * height];
            int mx = 0;
            for (int i = 0; i < read; ++i)
            {
                long xs = i;
                xs *= width;
                xs /= read;
                xs /= chunkX;
                xs *= chunkX;
                var xe = xs + chunkX;
                var val = buffer[i];
                if (val >= 0)
                {
                    var end = baseLine - (int)(val * amp);
                    for (int j = baseLine; j >= end; --j)
                    {
                        for (var x = xs; x < xe; ++x)
                        {
                            var o = j * width + x;
                            var v = img[o];
                            ++v;
                            img[o] = v;
                            if (v > mx)
                                mx = v;
                        }
                    }
                }
                else
                {
                    var end = baseLine - (int)(val * ampNeg);
                    for (int j = baseLine; j <= end; ++j)
                    {
                        for (var x = xs; x < xe; ++x)
                        {
                            var o = j * width + x;
                            var v = img[o];
                            ++v;
                            img[o] = v;
                            if (v > mx)
                                mx = v;
                        }
                    }

                }
            }
            var size = width * height;
            ushort[] imgRg = new ushort[size * 4];
            var hs = width * baseLine;
            for (int y = 0, i = 0; y < height; ++y)
            {
                var yy = (double)y;
                yy /= baseLine;
                yy = Math.Max(0, yy) * 0x8888 + 0x2222;
                var fadeTop = (ushort)yy;

                yy = (double)(y - baseLine);
                yy /= (height - baseLine);
                yy = Math.Max(0, 1.0 - yy) * 0x8888 + 0x5555;
                var fadeBottom = (ushort)yy;

                for (int x = 0; x < width; ++x, ++i)
                {
                    var v = img[i];
                    var ii = i << 2;
                    var isRef = i >= hs;
                    imgRg[ii + 3] = (ushort)(isRef ? fadeBottom : fadeTop);
                    if (v > 0)
                    {
                        double p = (double)v;
                        p /= mx;
                        p = 1.0 - p;
                        p *= p;
                        p *= p;
                        Gradient(imgRg, p, ii);
                        if (isRef)
                        {
                            imgRg[ii + 0] >>= 1;
                            imgRg[ii + 1] >>= 1;
                            imgRg[ii + 2] >>= 1;
                        }
                        imgRg[ii + 3] = (ushort)(isRef ? fadeBottom : 0xdddd);
                    }
                }
            }

  

            var iconFilename = baseName + "_Icon.png";
            using (var mi = new MagickImage(MagickColors.Transparent, (uint)width, (uint)height))
            {
                mi.ImportPixels(imgRg, 0, new PixelImportSettings((uint)width, (uint)height, StorageType.Quantum, PixelMapping.RGBA));
                width /= superSample;
                height /= superSample;
                if (superSample > 1)
                {
                    mi.FilterType = FilterType.Mitchell;
                    mi.Resize((uint)width, (uint)height);
                }
                if (haveBack)
                {
                    using var s = new MagickImage(screen);
                    s.FilterType = FilterType.Mitchell;
                    double scaleX = (double)width / (double)s.Width;
                    double scaleY = (double)height / (double)s.Height;
                    double scale = Math.Max(scaleX, scaleY);
                    var nw = (int)Math.Ceiling(scale * s.Width);
                    var nh = (int)Math.Ceiling(scale * s.Height);
                    s.Resize((uint)nw, (uint)nh);
                    s.Crop((uint)width, (uint)height, Gravity.Center);

                    using var dd = s.GetPixels();
                    using var ss = mi.GetPixels();
                    for (int y = 0; y < height; ++y)
                    {
                        for (int x = 0; x < width; ++x)
                        {
                            var pd = dd.GetValue(x, y);
                            var ps = ss.GetValue(x, y);
                            ulong a = ps[3];
                            ulong ia = 65535 - a;
                            ulong v = a * ps[0] + ia * pd[0];
                            pd[0] = (ushort)(v / 65535);
                            v = a * ps[1] + ia * pd[1];
                            pd[1] = (ushort)(v / 65535);
                            v = a * ps[2] + ia * pd[2];
                            pd[2] = (ushort)(v / 65535);
                            dd.SetPixel(x, y, pd);
                        }
                    }
                    s.Format = MagickFormat.Png8;
                    await s.WriteAsync(iconFilename).ConfigureAwait(false);
                }
                else
                {
                    mi.Format = MagickFormat.Png8;
                    await mi.WriteAsync(iconFilename).ConfigureAwait(false);
                }
            }
            info.IconFile = iconFilename;
            return info;
        }

        public delegate Task<MediaInfo> GetMediaInfoDel(String filename, int width, int height, bool fill, String baseName);

        public static IDisposable AddMediaInfoCreator(String fileExtension, GetMediaInfoDel handler)
        {
            var key = fileExtension.FastToLower();
            var m = ExternalMedia;
            if (!m.TryAdd(key, handler))
                return null;
            var x = new KeyValuePair<String, GetMediaInfoDel>(key, handler);
            return new AsDisposable(() => m.TryRemove(x));
        }


        public static IEnumerable<String> ExternalMediaTypes => ExternalMedia.Keys;

        static readonly ConcurrentDictionary<String, GetMediaInfoDel> ExternalMedia = new ConcurrentDictionary<string, GetMediaInfoDel>(StringComparer.Ordinal);


        static readonly GetMediaInfoDel[][] Orders =
        [
            [
                GetImageInfo, GetVideoInfo, GetAudioInfo,
            ],
            [
                GetImageInfo, GetVideoInfo, GetAudioInfo
            ],
            [
                GetVideoInfo, GetImageInfo, GetAudioInfo
            ],
            [
                GetAudioInfo, GetImageInfo, GetVideoInfo
            ],
        ];

        static async Task<MediaInfo> GetMediaInfo(String filename, int width, int height, bool fill, String baseName, PerfMonitor mon = null, String monName = "BuildThumb")
        {
            var extP = filename.LastIndexOf('.');
            var ext = extP > 0 ? filename.Substring(extP + 1).FastToLower() : "Unknown";
            MediaTypes type = MediaTypes.None;
            if (ExternalMedia.TryGetValue(ext, out var et))
            {
                using var __ = mon?.Track(monName + ".External." + ext);
                try
                {
                    var mt = await et(filename, width, height, fill, baseName).ConfigureAwait(false);
                    if (mt != null)
                        return mt;
                }
                catch
                {
                }
            }
            using var _ = mon?.Track(monName + "." + ext);
            type = MediaFileTypes.GetMediaType(ext);
            foreach (var t in Orders[(int)type])
            {
                try
                {
                    var mt = await t(filename, width, height, fill, baseName).ConfigureAwait(false);
                    if (mt != null)
                        return mt;
                }
                catch
                {
                }
            }
            return Empty;
        }

        static readonly ConcurrentDictionary<long, FileMetaDataDbAsync<MediaInfo>> Dbs = new ConcurrentDictionary<long, FileMetaDataDbAsync<MediaInfo>>();

        static FileMetaDataDbAsync<MediaInfo> GetDb(int width, int height, bool fill, PerfMonitor mon = null, String monName = "BuildThumb")
        {
            var key = (long)width;
            key <<= 32;
            key |= (long)((uint)height);
            if (fill)
                key |= 0x8000000L;
            var dbs = Dbs;
            if (dbs.TryGetValue(key, out var db))
                return db;
            lock (dbs)
            {
                if (dbs.TryGetValue(key, out db))
                    return db;
                var keyName = String.Concat(typeof(MediaInfo).Name, "_MediaInfo", width, 'x', height, fill ? "_fill" : "");
                db = new FileMetaDataDbAsync<MediaInfo>(keyName, async (filename, baseName, existing) =>
                {
                    if (existing != null)
                    {
                        var f = existing.IconFile;
                        if (String.IsNullOrEmpty(f))
                            return null;
                        if (File.Exists(f))
                            return null;
                    }
                    using (mon?.Track(monName))
                        return await GetMediaInfo(filename, width, height, fill, baseName, mon, monName).ConfigureAwait(false);
                }, 30);
                dbs[key] = db;
                return db;
            }
        }


        public static async Task<MediaInfo> GetAsync(String filename, int width = 128, int height = 64, bool fill = false, PerfMonitor mon = null, String monName = "BuildThumb")
        {
            var db = GetDb(width, height, fill, mon, monName);
            try
            {
                return await db.ProcessAsync(filename).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        static readonly MediaInfo Empty = new MediaInfo();


        public int Width;
        public int Height;
        public Double Duration;
        public String IconFile;
        public double Fps;
        public String Desc;
    }
}
