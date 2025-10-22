using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{

    public static class Ffmpeg
    {


        static readonly IReadOnlyDictionary<AVHWDeviceType, int> Ranks = new Dictionary<AVHWDeviceType, int>()
        {
            { AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, 1 },
            { AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA, 2 },
            { AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN, 3 },
            { AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA, 1000 },
            { AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL, 5 },
            { AVHWDeviceType.AV_HWDEVICE_TYPE_QSV, 6 },
        }.Freeze();



        static int RankType(AVHWDeviceType type)
        {
            if (Ranks.TryGetValue(type, out var v))
                return v;
            return 100;
        }

        static String HwName(AVHWDeviceType type)
        {
            if (type == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                return "(none)";
            return type.ToString().Replace("AV_HWDEVICE_TYPE_", "").FastToLower();
        }

        static Ffmpeg()
        {
            var path = EnvInfo.NativePath;
//#if DEBUG
            Console.WriteLine("[Ffmpeg] Version map:");
            var t = DynamicallyLoadedBindings.LibraryVersionMap;
            foreach (var x in t)
                Console.WriteLine("[Ffmpeg]  " + x.Key.ToQuoted() + ": " + x.Value);
            Console.WriteLine("[Ffmpeg] Binary path used: " + path);
//#endif//DEBUG
            DynamicallyLoadedBindings.LibrariesPath = path;
            DynamicallyLoadedBindings.Initialize();

            var hw = new Dictionary<String, AVHWDeviceType>(StringComparer.Ordinal);
            var type = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            while ((type = ffmpeg.av_hwdevice_iterate_types(type)) != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                hw.Add(HwName(type), type);
            var l = hw.Count;
            if (l > 0)
            {
                var k = hw.ToArray();
                Array.Sort(k, (a, b) =>
                {
                    return RankType(a.Value) - RankType(b.Value);
                });
                DefaultHwEncoder = k[0].Key;
                HwEncoders = k.Select(x => x.Key).ToArray();
            }else
            {
                HwEncoders = [];
            }
            hw.Add("null", AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
            hw.Add("none", AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
            hw.Add(HwName(AVHWDeviceType.AV_HWDEVICE_TYPE_NONE), AVHWDeviceType.AV_HWDEVICE_TYPE_NONE);
            InternalHwEncoders = hw.Freeze();

            //#if DEBUG
            Console.WriteLine("[Ffmpeg] Hardware devices:");
            foreach (var x in HwEncoders)
                Console.WriteLine("[Ffmpeg]   " + x);
            Console.WriteLine("[Ffmpeg] Default hw device: " + (HwEncoder ?? "(none)"));
            Console.WriteLine("[Ffmpeg] Version used: " + ffmpeg.av_version_info());
//#endif//DEBUG

        }

        /// <summary>
        /// The default hardware encoder (detected as "best")
        /// </summary>
        public static readonly String DefaultHwEncoder;

        /// <summary>
        /// The hardware encoder in use (or null for none)
        /// </summary>
        public static String HwEncoder => HwName(SelectedEncoder);

        /// <summary>
        /// The available hardware encoders
        /// </summary>
        public static IReadOnlyList<String> HwEncoders;

        /// <summary>
        /// Select a specific harware encoder
        /// </summary>
        /// <param name="encoder"></param>
        public static void SetHwEncoder(String encoder)
        {
            if (encoder == null)
            {
                SelectedEncoder = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                return;
            }
            if (InternalHwEncoders.TryGetValue(encoder, out var e))
            {
                SelectedEncoder = e;
                return;
            }
        }

        static AVHWDeviceType GetHwEncoder(String encoder)
        {
            if (encoder == null)
                return AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
            if (InternalHwEncoders.TryGetValue(encoder, out var e))
                return e;
            return SelectedEncoder;
        }

        static AVHWDeviceType SelectedEncoder = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;


        static readonly IReadOnlyDictionary<String, AVHWDeviceType> InternalHwEncoders;

        static internal AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
        {
            switch (hWDevice)
            {
                case AVHWDeviceType.AV_HWDEVICE_TYPE_NONE:
                    return AVPixelFormat.AV_PIX_FMT_NONE;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU:
                    return AVPixelFormat.AV_PIX_FMT_VDPAU;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA:
                    return AVPixelFormat.AV_PIX_FMT_CUDA;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI:
                    return AVPixelFormat.AV_PIX_FMT_VAAPI;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2:
                    return AVPixelFormat.AV_PIX_FMT_NV12;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_QSV:
                    return AVPixelFormat.AV_PIX_FMT_QSV;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX:
                    return AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA:
                    return AVPixelFormat.AV_PIX_FMT_NV12;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_DRM:
                    return AVPixelFormat.AV_PIX_FMT_DRM_PRIME;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL:
                    return AVPixelFormat.AV_PIX_FMT_OPENCL;
                case AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC:
                    return AVPixelFormat.AV_PIX_FMT_MEDIACODEC;
                default:
                    return AVPixelFormat.AV_PIX_FMT_NONE;
            }
        }

        static readonly AsyncLock Lock = new AsyncLock();


        public static async Task<MediaInfo> GetMediaInfo(string url, int width, int height, bool fill, string baseName, String hwCodec = "")
        {
            MagickImage image = null;
            try
            {
                int w, h;
                double fps;
                double duration;
                IReadOnlyDictionary<String, String> metaData;
                IReadOnlyDictionary<String, String> videoMetaData;
                using (await Lock.Lock().ConfigureAwait(false))
                using (var vsd = new VideoStreamDecoder(url, GetHwEncoder(hwCodec)))
                {
                    w = vsd.FrameWidth;
                    h = vsd.FrameHeight;
                    fps = (double)vsd.Fps;
                    duration = (double)vsd.Duration;
                    var targetTime = Math.Min(3.0M, vsd.Duration * 0.25M);
                    var targetTimeEnd = Math.Min(targetTime + 5.0M, vsd.Duration * 0.9M);
                    metaData = vsd.MetaData;
                    videoMetaData = vsd.VideoMetaData;
                    bool ok = false;
                    AVFrame frame;
                    bool usedHw;
                    while (vsd.TryDecodeNextFrame(out frame, out usedHw))
                    {
                        if (vsd.Time >= targetTime)
                        {
                            if ((vsd.Time >= targetTimeEnd) || vsd.IsKeyFrame)
                            {
                                ok = true;
                                break;
                            }
                        }
                    }
                    if (ok)
//                    if (vsd.TryDecodeNextFrame(out var frame, out var usedHw))
                    {
                        using var conv = new VideoFrameConverter(usedHw ? vsd.HardwareFormat : vsd.SoftwareFormat, w, h, AVPixelFormat.AV_PIX_FMT_BGR24);
                        var convF = conv.Convert(frame);
                        image = new MagickImage(MagickColors.White, (uint)w, (uint)h);
                        image.ImportPixels(conv.DestBuffer, new PixelImportSettings((uint)w, (uint)h, StorageType.Char, PixelMapping.BGR));


                        var rotStr = vsd.GetMetaData("rotate");
                        if (rotStr != null)
                        {
                            if (double.TryParse(rotStr, out var rot))
                            {
                                image.Rotate(-rot);
                            }

                        }
                    }
                }
                String iconFilename = null;
                if ((width > 0) && (height > 0) && (image != null))
                {
                    if (fill)
                        ImageTools.FillInto(image, width, height);
                    else
                        ImageTools.FitInto(image, width, height);
                    iconFilename = baseName + "_Icon.png";
                    image.SetBitDepth(8, Channels.All);
                    await image.WriteAsync(iconFilename, MagickFormat.Png).ConfigureAwait(false);
                }
                var m = new MediaInfo
                {
                    Width = w,
                    Height = h,
                    Fps = fps,
                    Duration = duration,
                    Desc = DescFromMetaData(metaData, image == null ? true : (image.HasAlpha ? image.IsOpaque : true)),
                    IconFile = iconFilename,
                };
                return m;
            }
            catch
            {
            }
            finally
            {
                image?.Dispose();
            }
            return null;
        }

        static readonly IReadOnlyDictionary<String, String> KeepMeta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
        }.Freeze();

        static String DescFromMetaData(IReadOnlyDictionary<String, String> metaKeys, bool opaque)
        {
            StringBuilder b = new StringBuilder();
            if (!opaque)
                b.AppendLine("Alpha Transparent");
            if (metaKeys != null)
            {
                var km = KeepMeta;
                foreach (var x in metaKeys)
                {
                    if (!km.TryGetValue(x.Key, out var name))
                        continue;
                    if (String.IsNullOrEmpty(x.Value))
                        continue;
                    b.Append(name).Append(": ").AppendLine(x.Value);
                }
            }
            return b.Length > 0 ? b.ToString() : null;
        }

        public static async Task<bool> MakeThumbnailPng(string url, int width = 0, int height = 0, string destName = null, decimal atTime = 1, String hwCodec = "")
        {
            MagickImage image = null;
            try
            {
                int w, h;
                using (var vsd = new VideoStreamDecoder(url, GetHwEncoder(hwCodec)))
                {
                    w = vsd.FrameWidth;
                    h = vsd.FrameHeight;
                    if (width <= 0)
                        width = w;
                    if (height <= 0)
                        height = h;
                    if (destName == null)
                    {
                        if (PathExt.IsWeb(url))
                            destName = PathExt.ExtractWebFilename(url) + ".png";
                        else
                            destName = url + ".png";
                    }
                    if (!vsd.TryDecodeNextFrame(out var frame, out var usedHw))
                        return false;
                    while (vsd.Time < atTime)
                    {
                        var prev = frame;
                        if (!vsd.TryDecodeNextFrame(out frame, out usedHw))
                        {
                            frame = prev;
                            break;
                        }
                    }
                    using var conv = new VideoFrameConverter(usedHw ? vsd.HardwareFormat : vsd.SoftwareFormat, w, h, AVPixelFormat.AV_PIX_FMT_ABGR);
                    var convF = conv.Convert(frame);
                    image = new MagickImage(MagickColors.White, (uint)w, (uint)h);
                    image.ImportPixels(conv.DestBuffer, new PixelImportSettings((uint)w, (uint)h, StorageType.Char, PixelMapping.ABGR));
                }
                ImageTools.FitInto(image, width, height);
                image.SetBitDepth(8, Channels.All);
                await image.WriteAsync(destName, MagickFormat.Png).ConfigureAwait(false);
                return true;
            }
            catch
            {
            }
            finally
            {
                image?.Dispose();
            }
            return false;
        }


    }
}
