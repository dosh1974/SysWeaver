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

    /// <summary>
    /// FFmpeg initialization (the native libraries are loaded from EnvInfo.NativePath), hardware decoder selection and thumbnails.
    /// The native libraries must have the major versions of <c>DynamicallyLoadedBindings.LibraryVersionMap</c> (FFmpeg.AutoGen 9.0.1.1: avcodec 63, avformat 63, avutil 61, swscale 10, swresample 7),
    /// named like the bindings load them: "avcodec-63.dll" on Windows, "libavcodec.so.63" (the SONAME) on Linux.
    /// The shipped Linux libraries are glibc builds: musl based distributions (Alpine) are not supported, the type initializer throws there.
    /// </summary>
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
        /// <param name="encoder">The name (see <see cref="HwEncoders"/>), null, "none", "null" or "(none)" for no hardware decoding. Unknown names are ignored</param>
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

        /// <summary>
        /// Get the device type of a hardware encoder
        /// </summary>
        /// <param name="encoder">The name (see <see cref="HwEncoders"/>), null, "none", "null" or "(none)" for no hardware decoding</param>
        /// <returns>The device type, the selected one (see <see cref="SetHwEncoder"/>) if the name is unknown</returns>
        public static AVHWDeviceType GetHwEncoder(String encoder)
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

        /// <summary>
        /// Write a png thumbnail of the first frame at or after a time of a video (or the last frame if the video is shorter)
        /// </summary>
        /// <param name="url">The video file name or url</param>
        /// <param name="width">The maximum width, 0 or less for the video width</param>
        /// <param name="height">The maximum height, 0 or less for the video height</param>
        /// <param name="destName">The png file name, null for the url file name + ".png"</param>
        /// <param name="atTime">The time in seconds</param>
        /// <param name="hwCodec">The hardware decoder name, see <see cref="GetHwEncoder"/></param>
        /// <returns>True if the thumbnail was written, false on any error (all exceptions are caught)</returns>
        public static async Task<bool> MakeThumbnailPng(string url, int width = 0, int height = 0, string destName = null, decimal atTime = 1, String hwCodec = "")
        {
            MagickImage image = null;
            try
            {
                image = DecodeThumbnail(url, ref width, ref height, ref destName, atTime, hwCodec);
                if (image == null)
                    return false;
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

        /// <summary>
        /// Decode the first frame at or after atTime (or the last frame if the video is shorter) into a new image (the caller must dispose it).
        /// </summary>
        /// <returns>The image, null if no frame could be decoded</returns>
        static unsafe MagickImage DecodeThumbnail(string url, ref int width, ref int height, ref string destName, decimal atTime, String hwCodec)
        {
            using var vsd = new VideoStreamDecoder(url, GetHwEncoder(hwCodec));
            var w = vsd.FrameWidth;
            var h = vsd.FrameHeight;
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
                return null;
            //  The buffers of a decoded frame are released by the next decode, so keep a reference to the previous frame (used if the video ends before atTime)
            AVFrame* prev = null;
            try
            {
                while (vsd.Time < atTime)
                {
                    if (prev == null)
                    {
                        prev = ffmpeg.av_frame_alloc();
                        if (prev == null)
                            throw new OutOfMemoryException("Failed to allocate a frame");
                    }
                    vsd.RefLastFrame(prev);
                    var prevHw = usedHw;
                    if (!vsd.TryDecodeNextFrame(out frame, out usedHw))
                    {
                        frame = *prev;
                        usedHw = prevHw;
                        break;
                    }
                }
                using var conv = new VideoFrameConverter(usedHw ? vsd.HardwareFormat : vsd.SoftwareFormat, w, h, AVPixelFormat.AV_PIX_FMT_ABGR);
                conv.Convert(frame);
                var image = new MagickImage(MagickColors.White, (uint)w, (uint)h);
                try
                {
                    image.ImportPixels(conv.DestBuffer, new PixelImportSettings((uint)w, (uint)h, StorageType.Char, PixelMapping.ABGR));
                }
                catch
                {
                    image.Dispose();
                    throw;
                }
                return image;
            }
            finally
            {
                //  av_frame_free unreferences the frame (if any) and sets prev to null
                if (prev != null)
                    ffmpeg.av_frame_free(&prev);
            }
        }

    }
}
