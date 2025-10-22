using System;
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
    public sealed class FileLogService : IDisposable
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
            var ex = await PathExt.TryDeleteFileAsync(H.Filename).ConfigureAwait(false);
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

    }

}
