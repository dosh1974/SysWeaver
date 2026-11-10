using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using SysWeaver.Auth;
using SysWeaver.Net;
using SysWeaver.Security;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Message handler that output's messages to the console
    /// </summary>
    [IsMicroService]
    [WebApiUrl("logFile")]
    [WebMenuEmbedded(null, "Monitoring/LogFile", "Log file", "logFile/logfile.html", "View the log file", "IconTableLog", 2, Roles.AdminOps)]
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
            DumpStatsOnExit = p.DumpStatsOnExit;
            DeleteNeedsToken = p.DeleteNeedsToken;
        }


        readonly bool DumpStatsOnExit;

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
                return await FileExt.ReadBytesAsync(H.Filename).ConfigureAwait(false);
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
        /// The warning about deleting the log file (sent with the delete token, for the confirmation).
        /// </summary>
        public const String DeleteWarning = "The log is the history of what happened on this service (sign ins, user changes, errors). A deleted log can't be restored.";

        /// <summary>
        /// How long a delete token is valid.
        /// </summary>
        public static readonly TimeSpan DeleteTokenLifetime = TimeSpan.FromMinutes(5);

        readonly bool DeleteNeedsToken;


        /// <summary>
        /// The purpose of the delete tokens (see <see cref="OneTimeTokens{T}"/>).
        /// </summary>
        const String DeleteTokenPurpose = "delete-log";

        /// <summary>
        /// Delete tokens, one live token per session (a new one revokes the older one), one use, 5 minutes.
        /// </summary>
        readonly OneTimeTokens<bool> DeleteTokens = new OneTimeTokens<bool>(DeleteTokenLifetime);

        /// <summary>
        /// Get a one time token for deleting the log file (see <see cref="DeleteLogFile"/>), with the facts and the warning the confirmation should show.
        /// The token is valid for 5 minutes, for this session only, and can be used once; asking for a new one revokes the older one of the session.
        /// </summary>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The token and the facts about the file</returns>
        /// <exception cref="HttpResponseException">409: the request has no session</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        public DeleteLogFileToken GetDeleteLogFileToken(HttpServerRequest context)
        {
            var session = context?.Session?.Token ?? throw new HttpResponseException(409, "Your session has ended: sign in again to delete the log file");
            var token = DeleteTokens.Issue(DeleteTokenPurpose, session, true, out var exp);
            var h = H;
            var fi = new FileInfo(h.Filename);
            var exists = fi.Exists;
            return new DeleteLogFileToken
            {
                Token = token,
                Expires = exp,
                Name = h.DownloadName,
                Exists = exists,
                Size = exists ? fi.Length : 0,
                LastWrite = exists ? fi.LastWriteTimeUtc : null,
                Warning = DeleteWarning,
            };
        }

        /// <summary>
        /// Delete the log file (from the disk).
        /// The delete must be confirmed: send the token from <see cref="GetDeleteLogFileToken"/> (unless the service is configured with DeleteNeedsToken false).
        /// The delete is logged (as the first line of the new log, with who and from where) and audited.
        /// </summary>
        /// <param name="r">The delete token (the body may contain other fields, ex: the text viewer sends {Url, Name}: they are ignored)</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>True when the file was deleted (never false)</returns>
        /// <exception cref="HttpResponseException">409 with the reason: no valid delete token (missing, expired, used, revoked by a newer one or from another session)</exception>
        [WebApi]
        [WebApiAuth(Roles.Admin)]
        [WebApiAudit("Log")]
        public async Task<bool> DeleteLogFile(DeleteLogFileRequest r, HttpServerRequest context)
        {
            if (DeleteNeedsToken)
            {
                //  Client: get a token with Api/logFile/GetDeleteLogFileToken, show its facts, then send it here
                var t = r?.Token;
                if (String.IsNullOrEmpty(t))
                    throw new HttpResponseException(409, "The delete was not confirmed: choose Delete again and confirm it");
                if (!DeleteTokens.TryUse(t, DeleteTokenPurpose, context?.Session?.Token ?? "", out _))
                    throw new HttpResponseException(409, "The confirmation is no longer valid (it expired, was used, or a newer one was asked for): choose Delete again and confirm it");
            }
            var fd = M.FileDeleter;
            var fn = H.Filename;
            long size = 0;
            try
            {
                var fi = new FileInfo(fn);
                if (fi.Exists)
                    size = fi.Length;
            }
            catch
            {
            }
            var ex = fd == null ? await PathExt.TryDeleteFileAsync(fn).ConfigureAwait(false) : await fd(fn).ConfigureAwait(false);
            if (ex != null)
                throw ex;
            String ip = null;
            try
            {
                ip = context?.GetIpAddress();
            }
            catch
            {
            }
            M.AddMessage($"The log file {H.DownloadName.ToQuoted()} ({size} bytes) was deleted by {(context?.Session?.Auth?.Username ?? "?").ToQuoted()} from {ip ?? "?"}", MessageLevels.Warning);
            return true;
        }


        

        public void Dispose()
        {
            var h = Interlocked.Exchange(ref H, null);
            if (h == null)
                return;
            if (DumpStatsOnExit)
            {
                try
                {
                    var baseName = PathExt.StripExtension(h.Filename);
                    M.DumpStats(baseName).RunAsync();
                }
                catch
                {
                }
            }
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
                    AuthTokens = Authorization.RoleAdminOps,
                    Description = "This is the application log file.",
                    Name = H.DownloadName,
                    Filename = fn,
                    Size = fi.Length,
                    LastUpdate = fi.LastWriteTimeUtc,
                    OpenParams = "d=../Api/logFile/" + nameof(DeleteLogFile) + "&scrollToEnd=true&",
                };
        }

        public Task<ReadOnlyMemory<Byte>> TryReadTextFile(String name)
        {
            var h = H;
            return name.FastEquals(h.DownloadName) ? FileExt.TryReadMemoryAsync(h.Filename) : TaskExt.ReadonlyMemoryTask;
        }

        

        #endregion IHaveTextFiles

    }

}
