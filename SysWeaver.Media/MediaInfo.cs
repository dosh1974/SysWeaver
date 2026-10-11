using ImageMagick;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{

    /// <summary>
    /// Information about a media file (image, video or audio), and an optional icon (thumbnail) file.
    /// Images are handled by ImageMagick, videos by SysWeaver.Media.Video (VideoInfo.GetMediaInfo) and audio by SysWeaver.Media.Audio (AudioInfo.GetMediaInfo),
    /// both found by reflection (by name) if the assemblies are loaded / can be resolved.
    /// </summary>
    public sealed class MediaInfo
    {

        /// <summary>
        /// The function used to get video info, if null it's resolved (once) using reflection: SysWeaver.Media.VideoInfo.GetMediaInfo in the SysWeaver.Media.Video assembly
        /// </summary>
        public static volatile GetMediaInfoDel GetVideoInfoFunc;
        /// <summary>
        /// The function used to get audio info, if null it's resolved (once) using reflection: SysWeaver.Media.AudioInfo.GetMediaInfo in the SysWeaver.Media.Audio assembly
        /// </summary>
        public static volatile GetMediaInfoDel GetAudioInfoFunc;



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

        static readonly Task<MediaInfo> NullMediaTask = Task.FromResult((MediaInfo)null);

        static readonly GetMediaInfoDel NullInfo = (f, w, h, fi, ba) => NullMediaTask;

        static GetMediaInfoDel GetDelegate(String typeName)
        {
            try
            {
                var t = TypeFinder.Get(typeName);
                if (t == null)
                    return NullInfo;
                var mi = t.GetMethod("GetMediaInfo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null)
                    return NullInfo;
                var pf = Expression.Parameter(typeof(String));
                var pw = Expression.Parameter(typeof(int));
                var ph = Expression.Parameter(typeof(int));
                var pfill = Expression.Parameter(typeof(bool));
                var pbase = Expression.Parameter(typeof(String));
                return Expression.Lambda<GetMediaInfoDel>(Expression.Call(mi, pf, pw, ph, pfill, pbase), pf, pw, ph, pfill, pbase).Compile();
            }
            catch
            {
                return NullInfo;
            }
        }

        static Task<MediaInfo> GetVideoInfo(String filename, int width, int height, bool fill, String baseName)
        {
            var o = GetVideoInfoFunc;
            if (o != null)
                return o(filename, width, height, fill, baseName);
            lock (NullMediaTask)
            {
                o = GetVideoInfoFunc;
                if (o != null)
                    return o(filename, width, height, fill, baseName);
                o = GetDelegate("SysWeaver.Media.VideoInfo, SysWeaver.Media.Video");
                GetVideoInfoFunc = o;
                return o(filename, width, height, fill, baseName);
            }
        }

        static Task<MediaInfo> GetAudioInfo(String filename, int width, int height, bool fill, String baseName)
        {
            var o = GetAudioInfoFunc;
            if (o != null)
                return o(filename, width, height, fill, baseName);
            lock (NullMediaTask)
            {
                o = GetAudioInfoFunc;
                if (o != null)
                    return o(filename, width, height, fill, baseName);
                o = GetDelegate("SysWeaver.Media.AudioInfo, SysWeaver.Media.Audio");
                GetAudioInfoFunc = o;
                return o(filename, width, height, fill, baseName);
            }
        }


        /// <summary>
        /// A function that gets the information about a media file
        /// </summary>
        /// <param name="filename">The media file name</param>
        /// <param name="width">The icon width, 0 or less for no icon</param>
        /// <param name="height">The icon height, 0 or less for no icon</param>
        /// <param name="fill">True to scale the icon to fill the size (cropping), false to fit it inside the size</param>
        /// <param name="baseName">The base name for the files to create, the icon is typically baseName + "_Icon.png"</param>
        /// <returns>The media info, or null if the file isn't handled</returns>
        public delegate Task<MediaInfo> GetMediaInfoDel(String filename, int width, int height, bool fill, String baseName);

        /// <summary>
        /// Add a media info creator for a file extension, it's used before the built in ones (if it returns null or throws, the built in ones are used).
        /// </summary>
        /// <param name="fileExtension">The file extension without a '.' (case insensitive)</param>
        /// <param name="handler">The function to call</param>
        /// <returns>Dispose to remove the creator, null if a creator for the extension already exists</returns>
        public static IDisposable AddMediaInfoCreator(String fileExtension, GetMediaInfoDel handler)
        {
            var key = fileExtension.FastToLower();
            var m = ExternalMedia;
            if (!m.TryAdd(key, handler))
                return null;
            var x = new KeyValuePair<String, GetMediaInfoDel>(key, handler);
            return new AsDisposable(() => m.TryRemove(x));
        }


        /// <summary>
        /// The (lower case) file extensions that have a media info creator added using <see cref="AddMediaInfoCreator"/>
        /// </summary>
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
            //  Only the file name part (a '.' in a folder name is not an extension)
            var ext = Path.GetExtension(filename);
            ext = ext.Length > 1 ? ext.Substring(1).FastToLower() : "Unknown";
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

        static readonly ConcurrentDictionary<(int Width, int Height, bool Fill), FileMetaDataDbAsync<MediaInfo>> Dbs = new ConcurrentDictionary<(int Width, int Height, bool Fill), FileMetaDataDbAsync<MediaInfo>>();

        static FileMetaDataDbAsync<MediaInfo> GetDb(int width, int height, bool fill, PerfMonitor mon = null, String monName = "BuildThumb")
        {
            var key = (width, height, fill);
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


        /// <summary>
        /// Get the information about a media file (cached in a temp folder, see FileMetaDataDbAsync, the icon is created there).
        /// The file extension selects the order of the handlers: external creators first, then images, videos and audio (image first for unknown extensions).
        /// </summary>
        /// <param name="filename">The media file name (or url)</param>
        /// <param name="width">The icon width, 0 or less for no icon</param>
        /// <param name="height">The icon height, 0 or less for no icon</param>
        /// <param name="fill">True to scale the icon to fill the size (cropping), false to fit it inside the size</param>
        /// <param name="mon">Optional performance monitor</param>
        /// <param name="monName">The name prefix used in the performance monitor</param>
        /// <returns>The media info (an empty instance if no handler could read the file), null if an exception was thrown</returns>
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


        /// <summary>
        /// Width in pixels (0 for audio)
        /// </summary>
        public int Width;
        /// <summary>
        /// Height in pixels (0 for audio)
        /// </summary>
        public int Height;
        /// <summary>
        /// Duration in seconds (videos and audio)
        /// </summary>
        public Double Duration;
        /// <summary>
        /// The icon (thumbnail) file name, null if none
        /// </summary>
        public String IconFile;
        /// <summary>
        /// Frames per second (videos)
        /// </summary>
        public double Fps;
        /// <summary>
        /// Not set by the built in handlers
        /// </summary>
        public double LoadTime;
        /// <summary>
        /// A description (one item per line), null if none
        /// </summary>
        public String Desc;
    }
}
