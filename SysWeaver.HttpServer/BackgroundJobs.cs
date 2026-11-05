using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Net
{
    /// <summary>
    /// The states of a background job (see <see cref="BackgroundJobInfo.State"/>), the same names as the client's <c>Sw.Activity</c> job states.
    /// </summary>
    public static class BackgroundJobStates
    {
        /// <summary>
        /// The job is running (also while it stops after a cancel, see <see cref="BackgroundJobInfo.Stopping"/>).
        /// </summary>
        public const String Running = "running";
        /// <summary>
        /// The job ended with a result (see <see cref="BackgroundJobInfo.ResultUrl"/> and <see cref="BackgroundJobInfo.ResultText"/>).
        /// </summary>
        public const String Done = "done";
        /// <summary>
        /// The job failed (see <see cref="BackgroundJobInfo.Error"/>).
        /// </summary>
        public const String Failed = "failed";
        /// <summary>
        /// The job was cancelled (by the user, or the server stopped) and the work stopped.
        /// </summary>
        public const String Cancelled = "cancelled";
    }

    /// <summary>
    /// A snapshot of a background job, as sent to the client (see <see cref="BackgroundJobs"/>).
    /// </summary>
    public sealed class BackgroundJobInfo
    {
        /// <summary>
        /// A short description, ex: "Exporting Log.csv" [running]
        /// </summary>
        /// <returns>A short description</returns>
        public override string ToString() => String.Concat(Title.ToQuoted(), " [", State, Stopping ? ", stopping]" : "]");

        /// <summary>
        /// The id of the job (random, can't be guessed), use it with the job APIs (ex: "Api/jobs/GetJob").
        /// </summary>
        public String Id;

        /// <summary>
        /// The key given when the job was started, null if none.
        /// Only one running job per key and owner: starting a job with the key of a running one returns the running one (<see cref="IsNew"/> false).
        /// </summary>
        public String Key;

        /// <summary>
        /// The kind of job, ex: "export", null if not given.
        /// </summary>
        public String Kind;

        /// <summary>
        /// What runs, ex: "Exporting Log 2026-10-20 15-04 UTC+2.csv".
        /// </summary>
        public String Title;

        /// <summary>
        /// The state, one of the <see cref="BackgroundJobStates"/>: "running", "done", "failed" or "cancelled".
        /// </summary>
        public String State;

        /// <summary>
        /// The progress from 0 to 1, null if unknown.
        /// </summary>
        public double? Progress;

        /// <summary>
        /// What the job does right now (the phase), ex: "Reading the rows", null if nothing is reported.
        /// </summary>
        public String Text;

        /// <summary>
        /// Why the job failed or was cancelled (a message for the user), null otherwise.
        /// </summary>
        public String Error;

        /// <summary>
        /// When the job started (UTC).
        /// </summary>
        public DateTime Started;

        /// <summary>
        /// When the job ended (UTC), null while it runs. Show this as the end time ("Done at"), also for a job restored after a reload.
        /// </summary>
        public DateTime? Ended;

        /// <summary>
        /// When the job (and its result) is removed from the server (UTC), null while it runs.
        /// </summary>
        public DateTime? Expires;

        /// <summary>
        /// The (file) name of the result, null until the job is done (or if the job has no file).
        /// </summary>
        public String ResultName;

        /// <summary>
        /// The mime type of the result, null until the job is done (or if the result is a link, see <see cref="MemoryFile"/>, or the job has no file).
        /// </summary>
        public String ResultMime;

        /// <summary>
        /// The size of the result in bytes, 0 until the job is done (or if the result is a link, or the job has no file).
        /// </summary>
        public long ResultSize;

        /// <summary>
        /// The url (relative to the site root) that returns the result as a <see cref="MemoryFile"/>, ex: "Api/jobs/GetJobResult?\"id\"", null until the job is done
        /// (and null for a job without a file).
        /// </summary>
        public String ResultUrl;

        /// <summary>
        /// The result of a job without a file, a text for the user, ex: "1 204 users imported" (see <see cref="BackgroundJobResult"/>), null otherwise.
        /// A job can have both a file and a text.
        /// </summary>
        public String ResultText;

        /// <summary>
        /// True if the job was started by this request, false if a running job with the same <see cref="Key"/> was returned (only set by the start APIs).
        /// </summary>
        public bool IsNew;

        /// <summary>
        /// True if the job was removed (by <see cref="BackgroundJobs.Remove"/>), sent in the "sw.job" push message so other windows drop it too.
        /// </summary>
        public bool Removed;

        /// <summary>
        /// True while the job stops: a cancel was asked for (<see cref="BackgroundJobs.Cancel"/>) but the work still runs.
        /// The <see cref="State"/> stays "running" (and the job keeps its running slot) until the work really stopped, then it is "cancelled"
        /// (or "done" if the work finished before it could stop, see <see cref="CancelRequested"/>). Show "Stopping…".
        /// </summary>
        public bool Stopping;

        /// <summary>
        /// True if a cancel was asked for (stays true after the end): with <see cref="State"/> "done" it means that the work finished before it could stop
        /// (the result exists, ex: "Too late: the file was ready"); "cancelled" means it stopped without a result.
        /// </summary>
        public bool CancelRequested;

        /// <summary>
        /// True if the end of the job was shown to the user (acknowledged with "Api/jobs/AckJob", or the result was fetched with "Api/jobs/GetJobResult").
        /// A client announces (toasts) an ended job only when this is false, then acknowledges it: a job restored after a reload, in a new session
        /// or on another device of the user, with Announced true, is listed quietly.
        /// </summary>
        public bool Announced;
    }

    /// <summary>
    /// The result of a background job started with <see cref="BackgroundJobs.StartWork"/> (or <see cref="HttpServerBase.StartWork"/>): a file, a text for the user, both or nothing.
    /// </summary>
    public sealed class BackgroundJobResult
    {
        /// <summary>
        /// The file made by the job (the client offers "Open" / "Save"), null if none.
        /// </summary>
        public MemoryFile File;

        /// <summary>
        /// What the job did, a text for the user, ex: "1 204 users imported", null if none.
        /// </summary>
        public String Text;

        /// <summary>
        /// A result with a file.
        /// </summary>
        /// <param name="file">The file</param>
        /// <param name="text">Optional text for the user</param>
        /// <returns>The result</returns>
        public static BackgroundJobResult FromFile(MemoryFile file, String text = null) => new BackgroundJobResult { File = file, Text = text };

        /// <summary>
        /// A result without a file, only a text for the user.
        /// </summary>
        /// <param name="text">What the job did, ex: "1 204 users imported"</param>
        /// <returns>The result</returns>
        public static BackgroundJobResult FromText(String text) => new BackgroundJobResult { Text = text };
    }

    /// <summary>
    /// Passed to the work of a background job: the cancellation, the detached request and a way to report progress.
    /// </summary>
    public sealed class BackgroundJobContext
    {
        internal BackgroundJobContext(BackgroundJobs.Job job)
        {
            J = job;
        }

        readonly BackgroundJobs.Job J;

        /// <summary>
        /// The id of the job.
        /// </summary>
        public String Id => J.Id;

        /// <summary>
        /// Signalled when the job is cancelled (by the user, or the server stops), the work should stop as soon as it can:
        /// throw (ex: <c>ctx.Cancel.ThrowIfCancellationRequested()</c>) to end as "cancelled". The job shows "stopping" (and keeps its running slot) until the work returned;
        /// a work that returns a result anyway ends as "done" with <see cref="BackgroundJobInfo.CancelRequested"/> true (it finished before it could stop).
        /// </summary>
        public CancellationToken Cancel => J.Cts.Token;

        /// <summary>
        /// A detached copy of the request that started the job (the same session, user, url, prefix and client address, no body),
        /// valid while the job runs (disposed when it ended). Use it in the work instead of the original request, which ends when the start api answered.
        /// Null if the job was started without a request (<see cref="BackgroundJobs.Start"/> / <see cref="BackgroundJobs.StartWork"/> with owner keys only).
        /// </summary>
        public HttpServerRequest Request => J.GetRequest();

        /// <summary>
        /// Report the progress. It is pushed to every session of the owner ("sw.job", so other tabs and devices follow it without polling):
        /// a new text (phase) at once, a progress change at most once per <see cref="BackgroundJobs.ProgressPushInterval"/>.
        /// </summary>
        /// <param name="progress">The progress from 0 to 1, null if unknown (other values are clamped, NaN / infinity are unknown)</param>
        /// <param name="text">What the job does right now (the phase), null to keep the current text</param>
        public void Report(double? progress, String text = null)
        {
            if (progress.HasValue)
            {
                var p = progress.Value;
                progress = Double.IsFinite(p) ? Math.Clamp(p, 0, 1) : null;
            }
            bool push = false;
            lock (J)
            {
                if (J.State != BackgroundJobStates.Running)
                    return;
                var newText = (text != null) && !String.Equals(text, J.Text, StringComparison.Ordinal);
                var changed = newText || (J.Progress != progress);
                J.Progress = progress;
                if (text != null)
                    J.Text = text;
                if (changed)
                {
                    var now = DateTime.UtcNow.Ticks;
                    if (newText || (now >= J.NextProgressPush))
                    {
                        J.NextProgressPush = now + J.ProgressPushTicks;
                        push = true;
                    }
                }
            }
            if (push)
                J.OnProgress?.Invoke(J);
        }
    }

    /// <summary>
    /// The body of the "Api/jobs/WaitJob" long poll (see <see cref="BackgroundJobs.Wait"/>).
    /// </summary>
    public sealed class BackgroundJobWaitRequest
    {
        /// <summary>
        /// The id of the job.
        /// </summary>
        public String Id;

        /// <summary>
        /// Wait at most this many milliseconds for the job to end (default 20000, at most <see cref="BackgroundJobs.MaxWaitMs"/>).
        /// </summary>
        public int Ms = 20000;
    }

    /// <summary>
    /// Background jobs of the clients of a server (one instance per <see cref="HttpServerBase"/>: <see cref="HttpServerBase.Jobs"/>): long work (an export, a report, an import)
    /// whose result is kept on the server, so it survives a page reload or leaving the page.
    /// <para>A job is owned by the signed in user that started it (any session of that user sees it) or, for a visitor that isn't signed in, by the session.
    /// The client starts a job with an API of the service that does the work (ex: "Api/StartExportTableApi"), gets its state with "Api/jobs/GetJobs" (after a reload),
    /// "Api/jobs/GetJob" (poll) or "Api/jobs/WaitJob" (long poll) and the "sw.job" push message (start, stop asked, end), fetches the result with "Api/jobs/GetJobResult",
    /// cancels with "Api/jobs/CancelJob" and acknowledges the end it showed with "Api/jobs/AckJob" (<see cref="BackgroundJobInfo.Announced"/>).
    /// Results are kept for <see cref="KeepFor"/> after the job ended, then removed. Nothing is written to disk: jobs end with the process.</para>
    /// <para>Cancel is honest: the job stays "running" with <see cref="BackgroundJobInfo.Stopping"/> true (and keeps its running slot) until the work returned.</para>
    /// </summary>
    /// <remarks>Thread safe. Owners are strings (see <see cref="OwnersOf"/>), so the class can be used and tested without a server.</remarks>
    public sealed class BackgroundJobs : IDisposable
    {
        /// <summary>
        /// The maximum wait of <see cref="Wait"/> in milliseconds.
        /// </summary>
        public const int MaxWaitMs = 25000;

        /// <summary>
        /// How long a job and its result are kept after it ended (default 30 minutes).
        /// </summary>
        public TimeSpan KeepFor = TimeSpan.FromMinutes(30);

        /// <summary>
        /// The maximum number of running jobs per owner (default 4), a start beyond that is refused (429 with a reason). A stopping job counts as running.
        /// </summary>
        public int MaxRunningPerOwner = 4;

        /// <summary>
        /// The maximum number of jobs (running or ended) kept per owner (default 20), the oldest ended ones are removed first.
        /// </summary>
        public int MaxPerOwner = 20;

        /// <summary>
        /// The maximum number of jobs kept in total (default 2000), a start beyond that is refused (429 with a reason).
        /// </summary>
        public int MaxJobs = 2000;

        /// <summary>
        /// Called when a job started, a stop was asked for (also a cancel that came too late: a done job with <see cref="BackgroundJobInfo.CancelRequested"/>), it ended or was removed,
        /// and on progress (a new text at once, a progress change at most once per <see cref="ProgressPushInterval"/>), with the job and its owner.
        /// Used by the server to push "sw.job" messages to every session of the owner.
        /// </summary>
        public event Action<BackgroundJobInfo, String> OnChanged;

        /// <summary>
        /// The least time between two progress changes reported by <see cref="OnChanged"/> for one job (default 1 s; a new text is reported at once).
        /// </summary>
        public TimeSpan ProgressPushInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Create a job list.
        /// </summary>
        public BackgroundJobs()
        {
            PruneTimer = new Timer(_ => Prune(), null, 60000, 60000);
        }

        /// <summary>
        /// Cancel all running jobs and stop the expiry timer.
        /// </summary>
        public void Dispose()
        {
            PruneTimer.Dispose();
            Job[] all;
            lock (Jobs)
                all = Jobs.Values.ToArray();
            foreach (var j in all)
                if (j.TryEnd(BackgroundJobStates.Cancelled, null, "The service stopped", KeepFor))
                    j.Cts.Cancel();
        }

        readonly Timer PruneTimer;

        readonly Dictionary<String, Job> Jobs = new Dictionary<string, Job>(StringComparer.Ordinal);

        /// <summary>
        /// The owner keys of a caller: the first one owns new jobs ("u:" + user guid when signed in, else "s:" + session token), any of them gives access
        /// (a visitor that signs in keeps the jobs of the session).
        /// </summary>
        /// <param name="userGuid">The guid of the signed in user, null if not signed in</param>
        /// <param name="sessionToken">The session token, may be null</param>
        /// <returns>The owner keys, the owner of new jobs first</returns>
        public static String[] OwnersOf(String userGuid, String sessionToken)
        {
            var s = String.IsNullOrEmpty(sessionToken) ? null : "s:" + sessionToken;
            if (String.IsNullOrEmpty(userGuid))
                return s == null ? [] : [s];
            var u = "u:" + userGuid;
            return s == null ? [u] : [u, s];
        }

        /// <summary>
        /// The url of the result of a job, relative to the site root.
        /// </summary>
        /// <param name="id">The job id</param>
        /// <returns>The url, ex: "Api/jobs/GetJobResult?\"id\""</returns>
        public static String ResultUrlOf(String id) => String.Concat("Api/jobs/GetJobResult?\"", id, '"');

        /// <summary>
        /// Start a job that makes a file (or return the running job with the same key and owner).
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>), the first one owns the job</param>
        /// <param name="title">What runs, ex: "Exporting Log.csv"</param>
        /// <param name="work">The work, runs on the thread pool, returns the file (null is a failure "The work made no result"), should stop when <see cref="BackgroundJobContext.Cancel"/> is signalled.
        /// An exception fails the job with its message (an <see cref="HttpResponseException"/> message as is, others without sensitive information).</param>
        /// <param name="key">Optional key, only one running job per key and owner</param>
        /// <param name="kind">Optional kind, ex: "export"</param>
        /// <returns>The job (<see cref="BackgroundJobInfo.IsNew"/> false if a running job with the key was returned)</returns>
        /// <exception cref="ArgumentException">No owner or no work</exception>
        /// <exception cref="HttpResponseException">429: too many running jobs for the owner, or in total</exception>
        public BackgroundJobInfo Start(IReadOnlyList<String> owners, String title, Func<BackgroundJobContext, Task<MemoryFile>> work, String key = null, String kind = null)
        {
            ArgumentNullException.ThrowIfNull(work);
            return StartWork(owners, title, async ctx =>
            {
                var f = await work(ctx).ConfigureAwait(false);
                if (f == null)
                    throw new HttpResponseException(500, "The work made no result");
                return BackgroundJobResult.FromFile(f);
            }, key, kind, null);
        }

        /// <summary>
        /// Start a job with any result: a file, a text for the user ("1 204 users imported"), both or nothing (or return the running job with the same key and owner).
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>), the first one owns the job</param>
        /// <param name="title">What runs, ex: "Importing users"</param>
        /// <param name="work">The work, runs on the thread pool, returns the result (null = done without a result), should stop when <see cref="BackgroundJobContext.Cancel"/> is signalled.
        /// An exception fails the job with its message (an <see cref="HttpResponseException"/> message as is, others without sensitive information).</param>
        /// <param name="key">Optional key, only one running job per key and owner</param>
        /// <param name="kind">Optional kind, ex: "import"</param>
        /// <param name="detach">Optional: makes the detached request of the job (<see cref="BackgroundJobContext.Request"/>), called once when the work asks for it, disposed when the job ended</param>
        /// <returns>The job (<see cref="BackgroundJobInfo.IsNew"/> false if a running job with the key was returned)</returns>
        /// <exception cref="ArgumentException">No owner or no work</exception>
        /// <exception cref="HttpResponseException">429: too many running jobs for the owner, or in total</exception>
        public BackgroundJobInfo StartWork(IReadOnlyList<String> owners, String title, Func<BackgroundJobContext, Task<BackgroundJobResult>> work, String key = null, String kind = null, Func<HttpServerRequest> detach = null)
        {
            if ((owners == null) || (owners.Count <= 0) || String.IsNullOrEmpty(owners[0]))
                throw new ArgumentException("A background job needs an owner (a session)", nameof(owners));
            ArgumentNullException.ThrowIfNull(work);
            var owner = owners[0];
            if (String.IsNullOrEmpty(key))
                key = null;
            Job job;
            List<Job> removed = null;
            lock (Jobs)
            {
                PruneLocked(DateTime.UtcNow, ref removed);
                var mine = Jobs.Values.Where(x => owners.Contains(x.Owner)).ToList();
                if (key != null)
                {
                    var same = mine.FirstOrDefault(x => x.IsRunning && (!x.IsStopping) && key.FastEquals(x.Key));
                    if (same != null)
                        return same.Info(false);
                }
                var running = mine.Count(x => x.IsRunning);
                if (running >= MaxRunningPerOwner)
                {
                    var stopping = mine.Count(x => x.IsStopping);
                    throw new HttpResponseException(429, stopping > 0
                        ? $"{running} background jobs are still running ({stopping} of them are stopping): wait until one has ended and try again"
                        : $"{running} background jobs are already running: wait until one has ended, or cancel one, and try again");
                }
                if (Jobs.Count >= MaxJobs)
                    throw new HttpResponseException(429, "The service has too many background jobs right now, try again in a few minutes");
                var ended = mine.Where(x => !x.IsRunning).OrderBy(x => x.EndTime ?? x.Started).ToList();
                var over = mine.Count + 1 - Math.Max(1, MaxPerOwner);
                for (int i = 0; (i < over) && (i < ended.Count); ++i)
                {
                    Jobs.Remove(ended[i].Id);
                    (removed ??= new List<Job>()).Add(ended[i]);
                }
                job = new Job(NewId(), owner, key, kind, String.IsNullOrEmpty(title) ? "Background work" : title, detach);
                job.ProgressPushTicks = Math.Max(0, ProgressPushInterval.Ticks);
                job.OnProgress = j => Notify(j.Info(false), j.Owner);
                Jobs.Add(job.Id, job);
            }
            NotifyRemoved(removed);
            Notify(job.Info(true), owner);
            _ = Task.Run(() => Run(job, work));
            return job.Info(true);
        }

        async Task Run(Job job, Func<BackgroundJobContext, Task<BackgroundJobResult>> work)
        {
            try
            {
                var res = await work(new BackgroundJobContext(job)).ConfigureAwait(false);
                //  A work that returns (even after a cancel) has done its work: report it honestly (CancelRequested tells the client)
                End(job, BackgroundJobStates.Done, res, null);
            }
            catch (Exception) when (job.Cts.IsCancellationRequested)
            {
                //  Stopped after a cancel (an OperationCanceledException, or a failure caused by the cancel)
                End(job, BackgroundJobStates.Cancelled, null, job.CancelReason ?? "Cancelled");
            }
            catch (Exception ex)
            {
                End(job, BackgroundJobStates.Failed, null, ex is HttpResponseException ? ex.Message : ex.SafeMessage());
            }
            finally
            {
                job.DisposeRequest();
            }
        }

        void End(Job job, String state, BackgroundJobResult result, String error)
        {
            if (!job.TryEnd(state, result, error, KeepFor))
                return;
            if (job.RemoveWhenEnded)
            {
                lock (Jobs)
                    Jobs.Remove(job.Id);
                var i = job.Info(false);
                i.Removed = true;
                Notify(i, job.Owner);
                return;
            }
            Notify(job.Info(false), job.Owner);
        }

        void Notify(BackgroundJobInfo info, String owner)
        {
            try
            {
                OnChanged?.Invoke(info, owner);
            }
            catch
            {
            }
        }

        void NotifyRemoved(List<Job> removed)
        {
            if (removed == null)
                return;
            foreach (var x in removed)
            {
                var i = x.Info(false);
                i.Removed = true;
                Notify(i, x.Owner);
            }
        }

        static String NewId() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Job Find(IReadOnlyList<String> owners, String id)
        {
            if (String.IsNullOrEmpty(id) || (owners == null))
                return null;
            List<Job> removed = null;
            Job j;
            lock (Jobs)
            {
                PruneLocked(DateTime.UtcNow, ref removed);
                if (!Jobs.TryGetValue(id, out j))
                    j = null;
            }
            NotifyRemoved(removed);
            return (j != null) && owners.Contains(j.Owner) ? j : null;
        }

        Job Get(IReadOnlyList<String> owners, String id) => Find(owners, id) ?? throw new HttpResponseException(404, "No such job: it was removed, it ended more than " + (int)Math.Ceiling(KeepFor.TotalMinutes) + " minutes ago (its result is gone), or it was started by someone else");

        /// <summary>
        /// The jobs of a caller, newest first.
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <returns>The jobs</returns>
        public BackgroundJobInfo[] List(IReadOnlyList<String> owners)
        {
            if ((owners == null) || (owners.Count <= 0))
                return [];
            List<Job> removed = null;
            Job[] mine;
            lock (Jobs)
            {
                PruneLocked(DateTime.UtcNow, ref removed);
                mine = Jobs.Values.Where(x => owners.Contains(x.Owner)).OrderByDescending(x => x.Started).ToArray();
            }
            NotifyRemoved(removed);
            return mine.Select(x => x.Info(false)).ToArray();
        }

        /// <summary>
        /// Get a job of a caller.
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired)</exception>
        public BackgroundJobInfo GetInfo(IReadOnlyList<String> owners, String id) => Get(owners, id).Info(false);

        /// <summary>
        /// Wait for a job to end (a long poll), returns at once if it isn't running (a stopping job is still running).
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <param name="ms">Wait at most this many milliseconds (clamped to 0..<see cref="MaxWaitMs"/>)</param>
        /// <returns>The job, as it is when it ended or the time is up</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired)</exception>
        public async Task<BackgroundJobInfo> Wait(IReadOnlyList<String> owners, String id, int ms)
        {
            var j = Get(owners, id);
            ms = Math.Clamp(ms, 0, MaxWaitMs);
            if (j.IsRunning && (ms > 0))
                await Task.WhenAny(j.EndedTask.Task, Task.Delay(ms)).ConfigureAwait(false);
            return j.Info(false);
        }

        /// <summary>
        /// Get the file of a job that is done (it can be fetched again until the job expires). Fetching it acknowledges the end (<see cref="BackgroundJobInfo.Announced"/>).
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <returns>The result</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired), 409: the job is running, failed, was cancelled or has no file (the message says which)</exception>
        public MemoryFile GetResult(IReadOnlyList<String> owners, String id)
        {
            var j = Get(owners, id);
            lock (j)
            {
                switch (j.State)
                {
                    case BackgroundJobStates.Done:
                        var f = j.Result?.File;
                        if (f == null)
                        {
                            var t = j.Result?.Text;
                            throw new HttpResponseException(409, String.IsNullOrEmpty(t) ? "This job made no file" : "This job made no file, its result is: " + t);
                        }
                        j.Announced = true;
                        return f;
                    case BackgroundJobStates.Running:
                        throw new HttpResponseException(409, j.CancelRequested ? "The job is stopping, there is no result" : "The job is still running, there is no result yet");
                    case BackgroundJobStates.Cancelled:
                        throw new HttpResponseException(409, "The job was cancelled, there is no result");
                    default:
                        throw new HttpResponseException(409, "The job failed, there is no result: " + j.Error);
                }
            }
        }

        /// <summary>
        /// Ask a running job to stop: the job stays "running" with <see cref="BackgroundJobInfo.Stopping"/> true (and keeps its running slot) until the work returned,
        /// then it is "cancelled" (or "done" with <see cref="BackgroundJobInfo.CancelRequested"/> true if the work finished anyway).
        /// A cancel of a job that is already done (it came too late) is recorded too: the job gets <see cref="BackgroundJobInfo.CancelRequested"/> true
        /// (pushed to every session), so every tab and device of the user shows "finished before it could stop", never a plain "done".
        /// A failed or cancelled job is returned unchanged.
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <returns>The job after the cancel was asked for</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired)</exception>
        public BackgroundJobInfo Cancel(IReadOnlyList<String> owners, String id)
        {
            var j = Get(owners, id);
            if (j.RequestCancel("Cancelled by the user") || j.MarkCancelTooLate())
                Notify(j.Info(false), j.Owner);
            return j.Info(false);
        }

        /// <summary>
        /// Acknowledge that the end of a job was shown to the user (<see cref="BackgroundJobInfo.Announced"/>): later lists (a reload, another session or device of the user)
        /// show it quietly. A running job is returned unchanged.
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired)</exception>
        public BackgroundJobInfo Ack(IReadOnlyList<String> owners, String id)
        {
            var j = Get(owners, id);
            lock (j)
            {
                if (j.State != BackgroundJobStates.Running)
                    j.Announced = true;
            }
            return j.Info(false);
        }

        /// <summary>
        /// Remove a job (the user dismissed it). An ended job is removed at once; a running job is asked to stop and is removed when the work stopped
        /// (until then it is listed as stopping, <see cref="BackgroundJobInfo.Removed"/> false).
        /// </summary>
        /// <param name="owners">The owner keys of the caller (see <see cref="OwnersOf"/>)</param>
        /// <param name="id">The job id</param>
        /// <returns>The job as it was removed (<see cref="BackgroundJobInfo.Removed"/> true), or the stopping job</returns>
        /// <exception cref="HttpResponseException">404: no such job for this caller (or it expired)</exception>
        public BackgroundJobInfo Remove(IReadOnlyList<String> owners, String id)
        {
            var j = Get(owners, id);
            if (j.IsRunning)
            {
                j.RemoveWhenEnded = true;
                if (j.RequestCancel("Removed by the user"))
                    Notify(j.Info(false), j.Owner);
                if (j.IsRunning)
                    return j.Info(false);
                //  It ended in between (End removed it, or will)
            }
            lock (Jobs)
                Jobs.Remove(j.Id);
            var i = j.Info(false);
            i.Removed = true;
            Notify(i, j.Owner);
            return i;
        }

        /// <summary>
        /// Remove the expired jobs (called every minute and on every access).
        /// </summary>
        public void Prune()
        {
            List<Job> removed = null;
            lock (Jobs)
                PruneLocked(DateTime.UtcNow, ref removed);
            NotifyRemoved(removed);
        }

        void PruneLocked(DateTime now, ref List<Job> removed)
        {
            foreach (var x in Jobs.Values)
            {
                var e = x.Expires;
                if (e.HasValue && (e.Value <= now))
                    (removed ??= new List<Job>()).Add(x);
            }
            if (removed != null)
                foreach (var x in removed)
                    Jobs.Remove(x.Id);
        }

        /// <summary>
        /// The number of jobs kept (running or ended).
        /// </summary>
        public int Count
        {
            get
            {
                lock (Jobs)
                    return Jobs.Count;
            }
        }

        internal sealed class Job
        {
            public Job(String id, String owner, String key, String kind, String title, Func<HttpServerRequest> detach)
            {
                Id = id;
                Owner = owner;
                Key = key;
                Kind = kind;
                Title = title;
                Started = DateTime.UtcNow;
                Detach = detach;
            }

            public readonly String Id;
            public readonly String Owner;
            public readonly String Key;
            public readonly String Kind;
            public readonly String Title;
            public readonly DateTime Started;
            public readonly CancellationTokenSource Cts = new CancellationTokenSource();
            public readonly TaskCompletionSource EndedTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            readonly Func<HttpServerRequest> Detach;
            HttpServerRequest Request;
            bool RequestDisposed;

            public String State = BackgroundJobStates.Running;
            public double? Progress;
            public String Text;
            public String Error;
            public DateTime? EndTime;
            public DateTime? Expires;
            public BackgroundJobResult Result;
            public bool CancelRequested;
            public String CancelReason;
            public bool Announced;
            public volatile bool RemoveWhenEnded;
            //  The progress push (see BackgroundJobContext.Report): called outside the lock, throttled by NextProgressPush
            public Action<Job> OnProgress;
            public long NextProgressPush;
            public long ProgressPushTicks;

            public bool IsRunning
            {
                get
                {
                    lock (this)
                        return State == BackgroundJobStates.Running;
                }
            }

            public bool IsStopping
            {
                get
                {
                    lock (this)
                        return (State == BackgroundJobStates.Running) && CancelRequested;
                }
            }

            /// <summary>
            /// Ask the work to stop once (later calls and ended jobs return false).
            /// </summary>
            public bool RequestCancel(String reason)
            {
                lock (this)
                {
                    if ((State != BackgroundJobStates.Running) || CancelRequested)
                        return false;
                    CancelRequested = true;
                    CancelReason = reason;
                }
                try
                {
                    Cts.Cancel();
                }
                catch
                {
                }
                return true;
            }

            /// <summary>
            /// Record a cancel that came after the job was done (once): CancelRequested, the result is kept ("finished before it could stop").
            /// </summary>
            public bool MarkCancelTooLate()
            {
                lock (this)
                {
                    if ((State != BackgroundJobStates.Done) || CancelRequested)
                        return false;
                    CancelRequested = true;
                    CancelReason = "Cancelled by the user after it was done";
                    return true;
                }
            }

            public HttpServerRequest GetRequest()
            {
                var d = Detach;
                if (d == null)
                    return null;
                lock (this)
                {
                    if (RequestDisposed)
                        return null;
                    return Request ??= d();
                }
            }

            public void DisposeRequest()
            {
                HttpServerRequest r;
                lock (this)
                {
                    RequestDisposed = true;
                    r = Request;
                    Request = null;
                }
                try
                {
                    r?.Dispose();
                }
                catch
                {
                }
            }

            /// <summary>
            /// End the job once (later calls return false).
            /// </summary>
            public bool TryEnd(String state, BackgroundJobResult result, String error, TimeSpan keep)
            {
                lock (this)
                {
                    if (State != BackgroundJobStates.Running)
                        return false;
                    var now = DateTime.UtcNow;
                    State = state;
                    Result = result;
                    Error = error;
                    EndTime = now;
                    Expires = now + keep;
                    if (state == BackgroundJobStates.Done)
                        Progress = 1;
                }
                EndedTask.TrySetResult();
                return true;
            }

            public BackgroundJobInfo Info(bool isNew)
            {
                lock (this)
                {
                    var done = State == BackgroundJobStates.Done;
                    var f = done ? Result?.File : null;
                    return new BackgroundJobInfo
                    {
                        Id = Id,
                        Key = Key,
                        Kind = Kind,
                        Title = Title,
                        State = State,
                        Progress = Progress,
                        Text = Text,
                        Error = Error,
                        Started = Started,
                        Ended = EndTime,
                        Expires = Expires,
                        ResultName = f?.Name,
                        ResultMime = f?.Mime,
                        ResultSize = f?.Data?.LongLength ?? 0,
                        ResultUrl = f != null ? ResultUrlOf(Id) : null,
                        ResultText = done ? Result?.Text : null,
                        IsNew = isNew,
                        Stopping = (State == BackgroundJobStates.Running) && CancelRequested,
                        CancelRequested = CancelRequested,
                        Announced = Announced,
                    };
                }
            }
        }
    }
}
