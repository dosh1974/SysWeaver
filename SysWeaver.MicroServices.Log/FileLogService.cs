using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Message handler that output's messages to the console
    /// </summary>
    [IsMicroService]
    [WebApiUrl("logFile")]
    [WebMenuEmbedded(null, "Debug/LogFile", "Log file", "logFile/logfile.html", "View the log file", "IconTableLog", 0, "debug,ops,admin")]
    public sealed class FileLogService : IDisposable, IHaveTextFiles
    {
        public FileLogService(ServiceManager manager, FileLogParams p = null)
        {
            M = manager;
            p = p ?? new FileLogParams();
            var fn = EnvInfo.MakeAbsoulte(PathTemplate.Resolve(p.Filename));
            if (String.IsNullOrEmpty(fn))
                fn = EnvInfo.ExecutableBase + ".log";
            var h = new FileLogMessageHandler(fn, p.Style, p.Mode, p.MaxSize);
            H = h;
            manager.Register(h, null, false);
        }

        public override string ToString() => "[Service] " + H;

        FileLogMessageHandler H;
        readonly ServiceManager M;

        /// <summary>
        /// Get the content of the log file.
        /// </summary>
        /// <returns></returns>
        [WebApi(nameof(LogFile) + ".txt")]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiRawText]
        [WebApiCompression(WebApiCompress.Balanced)]
        [WebApiClientCache(5)]
        [WebApiRequestCache(4)]
        public async Task<ReadOnlyMemory<Byte>> LogFile()
        {
            try
            {
                return await File.ReadAllBytesAsync(H.Filename).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                return ReadOnlyMemory<Byte>.Empty;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Name of the log file
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.AdminOps)]
        [WebApiClientCacheStatic]
        [WebApiRequestCacheStatic]
        public String DownloadName() => H.DownloadName;


        /// <summary>
        /// Delete the logfile (from disc)
        /// </summary>
        /// <returns></returns>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit("Log")]
        public async Task<bool> DeleteLogFile()
        {
            var fd = M.FileDeleter;
            var fn = H.Filename;
            var ex = fd == null ? await PathExt.TryDeleteFileAsync(fn).ConfigureAwait(false) : await fd(fn).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            return true;
        }


        

        public void Dispose()
        {
            var h = Interlocked.Exchange(ref H, null);
            if (h == null)
                return;
            h.Dispose();
            M.Unregister(h);
        }

        #region IHaveTextFiles

        

        public IEnumerable<TextFile> GetTextFiles()
        {
            var h = H;
            var fn = h.Filename;
            var fi = new FileInfo(fn);
            if (fi.Exists)
                yield return new TextFile
                {
                    Auth = Roles.AdminOps,
                    Description = "This is the application log file.",
                    Name = H.DownloadName,
                    Filename = fn,
                    Size = fi.Length,
                    LastUpdate = fi.LastWriteTimeUtc,
                    OpenParams = "d=../Api/logFile/" + nameof(DeleteLogFile) + "&scrollToEnd=true&",
                };
        }

        public async ValueTask<Byte[]> TryReadTextFile(String name)
        {
            var h = H;
            if (name.FastEquals(h.DownloadName))
                return await File.ReadAllBytesAsync(h.Filename);
            return null;
        }

        #endregion IHaveTextFiles

    }

}
