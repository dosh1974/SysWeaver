using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver
{

    /// <summary>
    /// Message handler that output's messages to the console
    /// </summary>
    public sealed class FileLogMessageHandler : TextMessageHandler, IHaveStats, IPerfMonitored
    {
        public FileLogMessageHandler(String filename, Message.TextStyles style, Modes mode, long maxSize = 2 << 20) : base(style, mode)
        {
            Filename = filename;
            DownloadName = Path.GetFileName(filename);
            MaxSize = Math.Max(1 << 10, maxSize);
            WriteText("== File log started " + DateTime.UtcNow.ToLocalTime().ToString("yy-MM-dd HH:mm:ss") + " ==\n").AsTask().RunAsync();
        }

        public readonly String DownloadName;
        public readonly String Filename;
        readonly long MaxSize;

        public override string ToString()
        {
            return String.Concat(Filename.ToQuoted(), " ", Mode, " ", Style);
        }

        volatile bool IsTruncating;





        protected override async ValueTask WriteText(string text)
        {
            var fn = Filename;
            var l = MaxSize;
            for (; ; )
            {
                try
                {
                    //  If we're updating, wait until we're not (uncommon)
                    while (IsTruncating)
                        Thread.Sleep(1);
                    //  Write the file, this can fail in rare cases due to an update being in process
                    await File.AppendAllTextAsync(fn, text).ConfigureAwait(false);
                    Interlocked.Add(ref WrittenChars, text.Length);
                    if (new FileInfo(fn).Length <= l)
                        return;
                    //  We need to truncate, get exclusive
                    lock (Exceptions)
                    {
                        // Check again to make sure that some other thread haven't trunctaed already
                        if (new FileInfo(fn).Length <= l)
                            return;
                        // Tell others that we're updating
                        IsTruncating = true;
                        using (PerfMon.Track("Truncate"))
                        {
                            try
                            {
                                using (var fs = new FileStream(fn, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                                {
                                    //  Make the new size approx 75% of max
                                    long s = fs.Length - ((l * 3) >> 2);
                                    if (s < 0)
                                        s = 0;
                                //  Buffer size 16kb max
                                    var bs = (int)Math.Min(s, 16 << 10);
                                    var searchSize = Math.Min(bs, 1 << 10);
                                    var buf = GC.AllocateUninitializedArray<Byte>(bs);
                                    int found = 0;
                                    long dest = 0;
                                    for (; ; )
                                    {
                                        //  Read 1kb at the target position
                                        fs.Position = s;
                                        var bl = fs.Read(buf, 0, searchSize);
                                        if (bl < 0)
                                            break;
                                        //  Find a new line
                                        for (int i = 0; i < bl; ++i)
                                        {
                                            var b = buf[i];
                                            bool ok = false;
                                            //  First find a new line (10), then find the next "valid" char (not 10 or 13)
                                            if (found == 0)
                                                ok = b == 10;
                                            else
                                                ok = (b != 10) && (b != 13);
                                            if (ok)
                                            {
                                                ++found;
                                                if (found == 2)
                                                {
                                                    //  First char in a new line is found
                                                    s += i;
                                                    break;
                                                }
                                            }
                                        }
                                        //  If we've found the start of a new line
                                        if (found == 2)
                                        {
                                            //  Copy from [S, L - S] to [0, L - S]
                                            var copySize = fs.Length - s;
                                            while (copySize > 0)
                                            {
                                                var copy = copySize;
                                                if (copy > bs)
                                                    copy = bs;
                                                copySize -= copy;
                                                fs.Position = s;
                                                fs.Read(buf, 0, (int)copy);
                                                fs.Position = dest;
                                                fs.Write(buf, 0, (int)copy);
                                                s += copy;
                                                dest += copy;
                                            }
                                            break;
                                        }
                                        // Need to search more
                                        s += bl;
                                    }
                                    //  Truncate the file
                                    fs.SetLength(dest);
                                }
                            }
                            catch (Exception ex)
                            {
                                Exceptions.OnException(ex);
                            }
                            finally
                            {
                                //  Truncation completed, continue
                                IsTruncating = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (IsTruncating)
                        continue;
                    Exceptions.OnException(ex);
                }
                break;
            }
        }


        long WrittenChars;

        readonly ExceptionTracker Exceptions = new ExceptionTracker();

        const String System = "FileLog";

        public PerfMonitor PerfMon { get; private set; } = new PerfMonitor(System);

        public IEnumerable<Stats> GetStats()
        {
            yield return new Stats(System, "Written chars", Interlocked.Read(ref WrittenChars), "The total number of chars written to the log file (not bytes since UTF8 is used)");
            foreach (var x in Exceptions.GetStats(System, "Log."))
                yield return x;
        }

    }

}
