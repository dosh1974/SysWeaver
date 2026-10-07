using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Data
{

    /// <summary>
    /// Base class for a server side stored piece of data that clients can refer to using a short string <see cref="Id"/>.
    /// The data is kept alive for <see cref="TimeToLive"/> seconds after creation or last use, and is then removed automatically
    /// using the <see cref="Scheduler"/>.
    /// </summary>
    /// <remarks>
    /// Instances are created by a <see cref="DataReferenceStorage"/> (see <see cref="TableDataReference"/>).
    /// The same type is also serialized to clients, in which case only the public fields are meaningful
    /// (the server side members such as <see cref="Expires"/> and <see cref="UseCounter"/> are only valid on the server instance).
    /// </remarks>
    public class DataReference
    {
        /// <summary>
        /// Returns the quoted id and the expiration time, for debugging.
        /// </summary>
        /// <returns>A text such as <c>"gXyz" expires at 2024-01-01 12:00:00</c>.</returns>
        public override string ToString() => String.Join(" expires at ", Id.ToQuoted(), InternalExpires);

        /// <summary>
        /// Create an empty reference (used by serializers).
        /// No data is attached and no expiration is scheduled.
        /// </summary>
        public DataReference()
        {
        }
        

        /// <summary>
        /// Create a lightweight copy for sending to a client, only the <see cref="Id"/> is copied.
        /// The copy holds no data and schedules no expiration.
        /// </summary>
        /// <param name="cloneForResponse">The server side reference to copy the id from.</param>
        protected DataReference(DataReference cloneForResponse)
        {
            Id = cloneForResponse.Id;
        }

        /// <summary>
        /// Create a server side reference and schedule its removal.
        /// </summary>
        /// <param name="scope">The scope (visibility) of the data.</param>
        /// <param name="id">The unique id of the data, including the scope prefix.</param>
        /// <param name="data">The data to keep, retrieved using <see cref="DataGet{T}"/>.</param>
        /// <param name="timeToLiveInSeconds">Number of seconds to keep the data alive after creation or last use, values below 10 are clamped to 10.</param>
        /// <param name="removeAction">Invoked (once) when the reference is removed, typically removes it from the owning storage.</param>
        protected DataReference(DataScopes scope, String id, Object data, int timeToLiveInSeconds, Action removeAction)
        {
            Scope = scope;
            Created = DateTime.UtcNow;
            if (timeToLiveInSeconds < 10)
                timeToLiveInSeconds = 10;
            Id = id;
            TimeToLive = timeToLiveInSeconds;
            Action = removeAction;
            Data = data;
            var expTime = DateTime.UtcNow.AddSeconds(timeToLiveInSeconds);
            InternalExpires = expTime;
            D = Scheduler.Add(expTime, Remove, "Remove data reference " + id);
        }

        /// <summary>
        /// The id that represents this data.
        /// The first char is the scope prefix (see <see cref="DataScopeTools.ScopePrefixes"/>).
        /// </summary>
        [EditOrder(-1)]
        public String Id;


        #region Server side

        /// <summary>
        /// Manually remove a data reference: cancels the scheduled expiration and invokes the remove action.
        /// Safe to call multiple times and from multiple threads, only the first call has any effect.
        /// Exceptions from the remove action are swallowed.
        /// </summary>
        public void Remove()
        {
            //  The action is the "not removed" flag, it's taken first so that a concurrent Renew (that temporarily holds D) can detect the removal
            var a = Interlocked.Exchange(ref Action, null);
            if (a == null)
                return;
            var d = Interlocked.Exchange(ref D, null);
            try
            {
                d?.Dispose();
            }
            catch
            {
            }
            try
            {
                a();
            }
            catch
            {
            }
        }

        /// <summary>
        /// The time (UTC) when this data expires, moved forward each time the data is used.
        /// </summary>
        public DateTime Expires => InternalExpires;
        
        /// <summary>
        /// Number of seconds that this data is kept alive after creation or last use (at least 10).
        /// </summary>
        public readonly int TimeToLive;

        /// <summary>
        /// The scope of the data
        /// </summary>
        public readonly DataScopes Scope;

        /// <summary>
        /// When the data was created (UTC).
        /// </summary>
        public readonly DateTime Created;

        /// <summary>
        /// Number of times this data has been used (renewed) after creation.
        /// </summary>
        public long UseCounter => Interlocked.Read(ref InternalUseCounter);

        long InternalUseCounter;

        DateTime InternalExpires;
        volatile Action Action;
        readonly Object Data;
        IDisposable D;


        /// <summary>
        /// Get the stored data.
        /// </summary>
        /// <typeparam name="T">The type of the stored data.</typeparam>
        /// <returns>The stored data, null on a client side copy.</returns>
        /// <exception cref="InvalidCastException">The stored data is not of type <typeparamref name="T"/>.</exception>
        protected T DataGet<T>()
            => (T)Data;

        /// <summary>
        /// Mark the data as used: increments <see cref="UseCounter"/> and reschedules expiration to <see cref="TimeToLive"/> seconds from now.
        /// Does nothing if the reference has already been removed.
        /// </summary>
        internal void Renew()
        {
            var d = Interlocked.Exchange(ref D, null);
            if (d == null)
                return;
            if (Action == null)
                return;
            d.Dispose();
            var expTime = DateTime.UtcNow.AddSeconds(TimeToLive);
            InternalExpires = expTime;
            Interlocked.Increment(ref InternalUseCounter);
            Interlocked.Exchange(ref D, Scheduler.Add(expTime, Remove, "Remove data reference " + Id));
            //  Removed while rescheduling (Remove found no D to cancel), cancel the new expiration (the remove action has already been invoked)
            if (Action == null)
                Interlocked.Exchange(ref D, null)?.Dispose();
        }

        #endregion//Server side

    }


}
