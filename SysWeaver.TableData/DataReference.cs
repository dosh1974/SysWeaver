using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysWeaver.Data
{

    public class DataReference
    {
        public override string ToString() => String.Join(" expires at ", Id.ToQuoted(), InternalExpires);

        public DataReference()
        {
        }


        protected DataReference(DataReference cloneForResponse)
        {
            Id = cloneForResponse.Id;
        }

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
            D = Scheduler.Add(expTime, Remove, true);
        }

        /// <summary>
        /// The id that represents this data
        /// </summary>
        [EditOrder(-1)]
        public String Id;


        #region Server side

        /// <summary>
        /// Manually remove a data reference
        /// </summary>
        public void Remove()
        {
            var d = Interlocked.Exchange(ref D, null);
            if (d == null)
                return;
            var a = Interlocked.Exchange(ref Action, null);
            try
            {
                d.Dispose();
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
        /// The time when this data expires
        /// </summary>
        public DateTime Expires => InternalExpires;
        
        /// <summary>
        /// Number of seconds that this data is kept alive (when used it is reset again)
        /// </summary>
        public readonly int TimeToLive;

        /// <summary>
        /// The scope of the data
        /// </summary>
        public readonly DataScopes Scope;

        /// <summary>
        /// When the data was created
        /// </summary>
        public readonly DateTime Created;

        /// <summary>
        /// Number of times this data was been used (after creation)
        /// </summary>
        public long UseCounter => Interlocked.Read(ref InternalUseCounter);

        long InternalUseCounter;

        DateTime InternalExpires;
        volatile Action Action;
        readonly Object Data;
        IDisposable D;


        protected T DataGet<T>()
            => (T)Data;

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
            D = Scheduler.Add(expTime, Remove, true);



        }

        #endregion//Server side

    }


}
