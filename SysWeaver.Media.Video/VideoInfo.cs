using FFmpeg.AutoGen.Abstractions;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{
    /// <summary>
    /// Get information about video files (using FFmpeg, see <see cref="Ffmpeg"/>).
    /// </summary>
    public static class VideoInfo
    {

        /// <summary>
        /// Same as <see cref="GetMediaInfoAcc"/> using the selected hardware decoder, this is the method that <see cref="MediaInfo"/> finds by reflection (by name)
        /// </summary>
        /// <param name="url">The video file name or url</param>
        /// <param name="width">The icon width, 0 or less for no icon</param>
        /// <param name="height">The icon height, 0 or less for no icon</param>
        /// <param name="fill">True to scale the icon to fill the size (cropping), false to fit it inside the size</param>
        /// <param name="baseName">The icon is written to baseName + "_Icon.png"</param>
        /// <returns>The media info, null on any error</returns>
        public static Task<MediaInfo> GetMediaInfo(string url, int width, int height, bool fill, string baseName)
            => GetMediaInfoAcc(url, width, height, fill, baseName, "");

        static readonly AsyncLock Lock = new AsyncLock();

        /// <summary>
        /// Get the size, fps, duration and an icon from a frame (a key frame at about 3 seconds or 25% of the duration) of a video.
        /// Calls are serialized (a global lock).
        /// </summary>
        /// <param name="url">The video file name or url</param>
        /// <param name="width">The icon width, 0 or less for no icon</param>
        /// <param name="height">The icon height, 0 or less for no icon</param>
        /// <param name="fill">True to scale the icon to fill the size (cropping), false to fit it inside the size</param>
        /// <param name="baseName">The icon is written to baseName + "_Icon.png"</param>
        /// <param name="hwCodec">The hardware decoder name (see <see cref="Ffmpeg.HwEncoders"/>), null or "none" for software decoding, an unknown name uses the selected one</param>
        /// <returns>The media info, null on any error (all exceptions are caught)</returns>
        public static async Task<MediaInfo> GetMediaInfoAcc(string url, int width, int height, bool fill, string baseName, String hwCodec)
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
                using (var vsd = new VideoStreamDecoder(url, Ffmpeg.GetHwEncoder(hwCodec)))
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



    }
}
