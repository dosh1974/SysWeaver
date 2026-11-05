using System;
using System.Threading.Tasks;
using SysWeaver.MicroService;

namespace SysWeaver.Net
{
    public abstract partial class HttpServerBase
    {
        /// <summary>
        /// The background jobs of the clients of this server (long work, ex: an export, whose result survives a page reload), see <see cref="BackgroundJobs"/>.
        /// A service starts a job with <see cref="StartJob"/> from one of its APIs and returns the <see cref="BackgroundJobInfo"/>, the client follows it with the "Api/jobs/*" APIs
        /// and the "sw.job" push message.
        /// </summary>
        public readonly BackgroundJobs Jobs = new BackgroundJobs();

        /// <summary>
        /// The push message type sent when a background job started, made progress (at most once per <see cref="BackgroundJobs.ProgressPushInterval"/>),
        /// was asked to stop, ended or was removed (to every session of the owner: every tab and device of the signed in user).
        /// </summary>
        public const String JobPushMessageType = "sw.job";

        /// <summary>
        /// The "sw.job" push message (see <see cref="JobPushMessageType"/>).
        /// </summary>
        public sealed class JobPushMessage : PushMessage
        {
            /// <summary>
            /// Create a message.
            /// </summary>
            /// <param name="job">The job</param>
            public JobPushMessage(BackgroundJobInfo job) : base(JobPushMessageType)
            {
                Job = job;
            }

            /// <summary>
            /// The job, as it is now.
            /// </summary>
            //  A field the serializer writes (a readonly field was left out of the message, so other sessions got "sw.job" without the job)
            public BackgroundJobInfo Job;
        }

        /// <summary>
        /// The owner keys of the session of a request (see <see cref="BackgroundJobs.OwnersOf"/>).
        /// </summary>
        /// <param name="request">The request</param>
        /// <returns>The owner keys</returns>
        public static String[] JobOwners(HttpServerRequest request)
        {
            var s = request?.Session;
            return BackgroundJobs.OwnersOf(s?.Auth?.Guid, s?.Token);
        }

        /// <summary>
        /// Start a background job that makes a file, owned by the caller of a request (the signed in user, or the session).
        /// The work must not use the original request (it ends when the response is sent): use <see cref="BackgroundJobContext.Request"/>, a detached copy.
        /// </summary>
        /// <param name="request">The request that starts the job</param>
        /// <param name="title">What runs, ex: "Exporting Log.csv"</param>
        /// <param name="work">The work, returns the file (null is a failure), see <see cref="BackgroundJobs.Start"/></param>
        /// <param name="key">Optional key, only one running job per key and owner (the running one is returned)</param>
        /// <param name="kind">Optional kind, ex: "export"</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">429: too many running jobs</exception>
        public BackgroundJobInfo StartJob(HttpServerRequest request, String title, Func<BackgroundJobContext, Task<MemoryFile>> work, String key = null, String kind = null)
        {
            ArgumentNullException.ThrowIfNull(work);
            return StartWork(request, title, async ctx =>
            {
                var f = await work(ctx).ConfigureAwait(false);
                if (f == null)
                    throw new HttpResponseException(500, "The work made no result");
                return BackgroundJobResult.FromFile(f);
            }, key, kind);
        }

        /// <summary>
        /// Start a background job with any result (a file, a text for the user such as "1 204 users imported", both or nothing), owned by the caller of a request
        /// (the signed in user, or the session). The work gets a detached copy of the request in <see cref="BackgroundJobContext.Request"/> (session, user, url, address):
        /// use it instead of the original request, which ends when the response is sent.
        /// <para>Ex: <c>return request.Server.StartWork(request, "Importing users", async ctx => { var n = await Import(ctx.Request, ctx.Cancel); return BackgroundJobResult.FromText(n + " users imported"); }, "import", "import");</c></para>
        /// </summary>
        /// <param name="request">The request that starts the job</param>
        /// <param name="title">What runs, ex: "Importing users"</param>
        /// <param name="work">The work, returns the result (null = done without a result), see <see cref="BackgroundJobs.StartWork"/></param>
        /// <param name="key">Optional key, only one running job per key and owner (the running one is returned)</param>
        /// <param name="kind">Optional kind, ex: "import"</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">429: too many running jobs</exception>
        public BackgroundJobInfo StartWork(HttpServerRequest request, String title, Func<BackgroundJobContext, Task<BackgroundJobResult>> work, String key = null, String kind = null)
            => Jobs.StartWork(JobOwners(request), title, work, key, kind, request == null ? null : ManualHttpServerRequest.Detacher(request));

        /// <summary>
        /// Push a job change to the sessions of its owner.
        /// </summary>
        void OnJobChanged(BackgroundJobInfo job, String owner)
        {
            if (String.IsNullOrEmpty(owner) || (owner.Length < 3))
                return;
            var m = new JobPushMessage(job);
            var id = owner.Substring(2);
            if (owner[0] == 'u')
            {
                PushMessageUser(id, m, false);
                return;
            }
            if (Sessions.TryGetValue(id.AsMemory(), out var s))
                s.PushMessage(m, false, false);
        }

        /// <summary>
        /// Get my background jobs (of the signed in user, or of this session), newest first. Use it after a page reload to show the jobs again.
        /// Jobs are kept for 30 minutes after they ended.
        /// </summary>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The jobs</returns>
        [WebApi("jobs/{0}")]
        public BackgroundJobInfo[] GetJobs(HttpServerRequest context) => Jobs.List(JobOwners(context));

        /// <summary>
        /// Get one of my background jobs (poll its state and progress).
        /// </summary>
        /// <param name="id">The id of the job</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">404: no such job (it expired, was removed, or isn't yours)</exception>
        [WebApi("jobs/{0}")]
        public BackgroundJobInfo GetJob(String id, HttpServerRequest context) => Jobs.GetInfo(JobOwners(context), id);

        /// <summary>
        /// Wait until one of my background jobs ended, at most the given time (a long poll), returns at once if it isn't running.
        /// </summary>
        /// <param name="r">The job id and the maximum wait</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The job, as it is when it ended or the time was up</returns>
        /// <exception cref="HttpResponseException">404: no such job (it expired, was removed, or isn't yours)</exception>
        [WebApi("jobs/{0}")]
        public Task<BackgroundJobInfo> WaitJob(BackgroundJobWaitRequest r, HttpServerRequest context) => Jobs.Wait(JobOwners(context), r?.Id, r?.Ms ?? 20000);

        /// <summary>
        /// Get the result of one of my background jobs that is done (as a file: name, mime type and data). It can be fetched again until the job expires.
        /// </summary>
        /// <param name="id">The id of the job</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The result</returns>
        /// <exception cref="HttpResponseException">404: no such job, 409: the job is still running, failed or was cancelled (the message says which)</exception>
        [WebApi("jobs/{0}")]
        public MemoryFile GetJobResult(String id, HttpServerRequest context) => Jobs.GetResult(JobOwners(context), id);

        /// <summary>
        /// Acknowledge that the end of one of my background jobs was shown to me (Announced true): it is then listed quietly after a reload,
        /// in another session or on another device. A running job is returned unchanged.
        /// </summary>
        /// <param name="id">The id of the job</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The job</returns>
        /// <exception cref="HttpResponseException">404: no such job (with the reason); a body that is not the job id as JSON (ex: text/plain "{}") is a 400 / 415 with the reason, never a 500</exception>
        [WebApi("jobs/{0}")]
        public BackgroundJobInfo AckJob(String id, HttpServerRequest context) => Jobs.Ack(JobOwners(context), id);

        /// <summary>
        /// Cancel one of my background jobs: it stays "running" with Stopping true until the work stopped, then it is "cancelled"
        /// (or "done" with CancelRequested true when the work finished before it could stop). A cancel of a job that is already done (too late) sets
        /// CancelRequested too, pushed to every session, so every device shows "finished before it could stop"; a failed or cancelled job is returned unchanged.
        /// </summary>
        /// <param name="id">The id of the job</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The job after the cancel was asked for (Stopping true if it was running)</returns>
        /// <exception cref="HttpResponseException">404: no such job</exception>
        [WebApi("jobs/{0}")]
        public BackgroundJobInfo CancelJob(String id, HttpServerRequest context) => Jobs.Cancel(JobOwners(context), id);

        /// <summary>
        /// Remove one of my background jobs and its result (a running job is asked to stop, and removed when the work stopped).
        /// </summary>
        /// <param name="id">The id of the job</param>
        /// <param name="context">Automatically populated by the request handler, don't use</param>
        /// <returns>The job as it was removed</returns>
        /// <exception cref="HttpResponseException">404: no such job</exception>
        [WebApi("jobs/{0}")]
        public BackgroundJobInfo RemoveJob(String id, HttpServerRequest context) => Jobs.Remove(JobOwners(context), id);
    }
}
