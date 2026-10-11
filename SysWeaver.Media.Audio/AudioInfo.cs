using ImageMagick;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Media
{
    /// <summary>
    /// Get information about audio files (using NAudio) and render waveform icons
    /// </summary>
    public static class AudioInfo
    {

        static void Gradient(ushort[] rgb, double x, int offset = 0)
        {
            SysWeaver.Media.ColorSpace.OkHsvToSrgb(out var rr, out var gg, out var bb, 2.6 - x * 2.3, 0.8, 0.9 - x * 0.2);
            rgb[offset + 0] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, rr * 65535.0));
            rgb[offset + 1] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, gg * 65535.0));
            rgb[offset + 2] = (ushort)Math.Min(0xffff, (uint)Math.Max(0, bb * 65535.0));
        }
        
        /// <summary>
        /// Same as <see cref="GetAudioInfo"/>, this is the method that <see cref="MediaInfo"/> finds by reflection (by name) to get the info for audio files.
        /// </summary>
        /// <param name="filename">The audio file name</param>
        /// <param name="width">The width of the waveform icon, 0 or less for no icon</param>
        /// <param name="height">The height of the waveform icon, 0 or less for no icon</param>
        /// <param name="fill">Not used</param>
        /// <param name="baseName">The icon is written to baseName + "_Icon.png"</param>
        /// <returns>The media info</returns>
        public static Task<MediaInfo> GetMediaInfo(String filename, int width, int height, bool fill, String baseName)
            => GetAudioInfo(filename, width, height, fill, baseName);

        /// <summary>
        /// Get information about an audio file (using NAudio's AudioFileReader) and optionally render a waveform icon.
        /// The whole file is decoded when an icon is requested: in blocks, up to <see cref="MaxBufferedSamples"/> samples are kept in memory,
        /// longer files are decoded twice (first to count the samples, then to build the waveform) using little memory.
        /// </summary>
        /// <param name="filename">The audio file name</param>
        /// <param name="width">The width of the waveform icon, 0 or less for no icon</param>
        /// <param name="height">The height of the waveform icon, 0 or less for no icon</param>
        /// <param name="fill">Not used</param>
        /// <param name="baseName">The icon is written to baseName + "_Icon.png" (an 8-bit palette png)</param>
        /// <returns>The media info: <see cref="MediaInfo.Duration"/> (in seconds), <see cref="MediaInfo.Desc"/> (ex: "Stereo @ 44 100 Hz")
        /// and <see cref="MediaInfo.IconFile"/> (if an icon was requested). Width and Height are 0.
        /// If an image named filename + ".png", filename + ".jpg" or the file name with the extension changed to ".jpg" or ".png" exists, it's used as the icon background (scaled to fill).</returns>
        /// <exception cref="Exception">Exceptions from NAudio if the file can't be read (no exception is caught)</exception>
        public static async Task<MediaInfo> GetAudioInfo(String filename, int width, int height, bool fill, String baseName)
        {
            const int superSample = 3;
            const int chunkX = 2;
            MediaInfo info = new MediaInfo();
            int[] img;
            int baseLine;
            bool haveBack;
            String screen;
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

                screen = filename + ".png";
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
                haveBack = screen != null;


                width *= superSample;
                height *= superSample;



                baseLine = (height * 3) >> 2;
                int amp = baseLine - 2;
                if (haveBack)
                {
                    baseLine = (height * 7) >> 3;
                    amp = (baseLine >> 1) - 2;
                }
                int ampNeg = height - baseLine - 2;

                //  Decode in blocks (a whole number of sample frames), buffer them until MaxBufferedSamples is reached
                var blockSize = Math.Max(1, channelCount) * 16384;
                var blocks = new List<float[]>();
                var lastBlockLength = 0;
                long total = 0;
                var buffered = true;
                var block = new float[blockSize];
                for (; ; )
                {
                    var read = ReadBlock(str, block);
                    if (read <= 0)
                        break;
                    total += read;
                    if (buffered)
                    {
                        if (total > MaxBufferedSamples)
                        {
                            //  Too long, only count the samples (the file is decoded again below)
                            buffered = false;
                            blocks.Clear();
                        }
                        else
                        {
                            blocks.Add(block);
                            lastBlockLength = read;
                            block = new float[blockSize];
                        }
                    }
                }
                var wave = new Waveform(width, height, baseLine, amp, ampNeg, chunkX, total);
                if (buffered)
                {
                    var bc = blocks.Count;
                    for (int i = 0; i < bc; ++i)
                        wave.Add(blocks[i], 0, i == (bc - 1) ? lastBlockLength : blockSize);
                }
                else
                {
                    using var str2 = new AudioFileReader(filename);
                    for (; ; )
                    {
                        var read = ReadBlock(str2, block);
                        if (read <= 0)
                            break;
                        if (!wave.Add(block, 0, read))
                            break;
                    }
                }
                img = wave.ToImage();
            }
            int mx = 0;
            foreach (var v in img)
                if (v > mx)
                    mx = v;
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

        /// <summary>
        /// The maximum number of decoded samples (all channels) that are kept in memory while building a waveform icon (32M samples = 128 MB).
        /// Longer files are decoded twice instead. Not readonly so that tests can lower it.
        /// </summary>
        static long MaxBufferedSamples = 1 << 25;

        /// <summary>
        /// Read until the block is full or the end of the stream is reached
        /// </summary>
        /// <returns>The number of samples read, less than the block size only at the end of the stream</returns>
        static int ReadBlock(AudioFileReader str, float[] block)
        {
            var len = block.Length;
            var pos = 0;
            while (pos < len)
            {
                var read = str.Read(block.AsSpan(pos, len - pos));
                if (read <= 0)
                    break;
                pos += read;
            }
            return pos;
        }

        /// <summary>
        /// Accumulates the waveform histogram of a known number of samples, block by block (only O(width * height) memory).
        /// Gives exactly the same image as drawing each sample: sample i is drawn in the chunkX columns starting at
        /// x = chunkX * floor(floor(i * width / total) / chunkX) (when the width isn't a multiple of chunkX, the last column spills into column 0 of the next row),
        /// from the base line up to the row of the sample value (scaled by amp) or down to it (scaled by ampNeg).
        /// Rows outside the image are clamped (values outside [-1, 1] used to throw an IndexOutOfRangeException).
        /// </summary>
        sealed class Waveform
        {
            public Waveform(int width, int height, int baseLine, int amp, int ampNeg, int chunkX, long total)
            {
                Width = width;
                Height = height;
                BaseLine = baseLine;
                Amp = amp;
                AmpNeg = ampNeg;
                ChunkX = chunkX;
                Total = total;
                Stride = height + 1;
                Buckets = (width - 1) / chunkX + 1;
                //  Per column bucket: a difference array over the rows (+1 where a span starts, -1 after it ends)
                Diff = new int[Buckets * Stride];
                NextStart = BucketStart(1);
            }

            readonly int Width;
            readonly int Height;
            readonly int BaseLine;
            readonly int Amp;
            readonly int AmpNeg;
            readonly int ChunkX;
            readonly long Total;
            readonly int Stride;
            readonly int Buckets;
            readonly int[] Diff;

            /// <summary>
            /// The index of the next sample
            /// </summary>
            long Index;
            /// <summary>
            /// The bucket of the next sample
            /// </summary>
            int Bucket;
            /// <summary>
            /// The index of the first sample of the next bucket
            /// </summary>
            long NextStart;

            /// <summary>
            /// The first sample index i where floor(i * Width / Total) >= bucket * ChunkX, i.e. ceil(bucket * ChunkX * Total / Width)
            /// </summary>
            long BucketStart(int bucket) => ((long)bucket * ChunkX * Total + Width - 1) / Width;

            /// <summary>
            /// Add the next samples
            /// </summary>
            /// <returns>False if all (total) samples have been added (the remaining samples are ignored)</returns>
            public bool Add(float[] samples, int offset, int count)
            {
                var total = Total;
                var index = Index;
                var bucket = Bucket;
                var nextStart = NextStart;
                var bo = bucket * Stride;
                var baseLine = BaseLine;
                var amp = Amp;
                var ampNeg = AmpNeg;
                var maxRow = Height - 1;
                var diff = Diff;
                var end = offset + count;
                for (int k = offset; k < end; ++k, ++index)
                {
                    if (index >= total)
                        break;
                    while (index >= nextStart)
                    {
                        ++bucket;
                        bo += Stride;
                        nextStart = BucketStart(bucket + 1);
                    }
                    var val = samples[k];
                    int lo, hi;
                    if (val >= 0)
                    {
                        var e = baseLine - (int)(val * amp);
                        if (e > baseLine)
                            continue;
                        lo = e < 0 ? 0 : e;
                        hi = baseLine;
                    }
                    else
                    {
                        var e = baseLine - (int)(val * ampNeg);
                        if (e < baseLine)
                            continue;
                        lo = baseLine;
                        hi = e > maxRow ? maxRow : e;
                    }
                    ++diff[bo + lo];
                    --diff[bo + hi + 1];
                }
                Index = index;
                Bucket = bucket;
                NextStart = nextStart;
                return index < total;
            }

            /// <summary>
            /// Get the image, each value is the number of samples drawn at that pixel
            /// </summary>
            /// <returns>Width * Height values</returns>
            public int[] ToImage()
            {
                var width = Width;
                var height = Height;
                var size = width * height;
                var img = new int[size];
                var diff = Diff;
                var chunkX = ChunkX;
                var stride = Stride;
                var buckets = Buckets;
                for (int b = 0; b < buckets; ++b)
                {
                    var bo = b * stride;
                    var xs = b * chunkX;
                    var xe = xs + chunkX;
                    var v = 0;
                    for (int j = 0; j < height; ++j)
                    {
                        v += diff[bo + j];
                        if (v == 0)
                            continue;
                        var o = j * width + xs;
                        for (var x = xs; x < xe; ++x, ++o)
                        {
                            if (o < size)
                                img[o] += v;
                        }
                    }
                }
                return img;
            }
        }


    }
}
