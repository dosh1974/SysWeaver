using System;
using System.Threading;
using SysWeaver.Data;
using SysWeaver.Remote;

namespace SysWeaver.MicroService
{

    /// <summary>
    /// Information about a service instance that is registered to the service manager
    /// </summary>
    [TableDataPrimaryKey(nameof(Type))]
    public sealed class ServiceInfo
    {
#if DEBUG
        public override string ToString() => String.Concat(Remote ? "[Remote] " : "[Local] ", Name, " #", Order, " (", When, ')');
#endif//DEBUG

        /// <summary>
        /// The type of the service
        /// </summary>
        [TableDataFormat]
        public Type Type => Instance?.GetType();

        /// <summary>
        /// Name of the service instance, can be used for end point mapping for multiple instances of the same type
        /// </summary>
        public readonly String Name;

        /// <summary>
        /// Registration order
        /// </summary>
        public readonly long Order;

        /// <summary>
        /// When the service instance was registered
        /// </summary>
        public readonly DateTime When;

        /// <summary>
        /// True if the service is owned by the service manager, if so the service manager will dispose it
        /// </summary>
        public readonly bool Owned;

        /// <summary>
        /// True if the service is a remote service (else it's a local in process service)
        /// </summary>
        public readonly bool Remote;

        internal ServiceInfo(Object instance, string name, bool owned, Type paramType)
        {
            Name = name;
            Remote = instance is IRemoteApi;
            When = DateTime.UtcNow;
            Order = Interlocked.Increment(ref CcIndex);
            Instance = instance;
            Owned = owned;
            ParamType = paramType;
        }

        internal void OnUse() => Interlocked.Increment(ref IntUseCount);

        static long CcIndex;
        long IntUseCount;

        /// <summary>
        /// Number of times something have requested this serice
        /// </summary>
        public long UseCount => Interlocked.Read(ref IntUseCount);

        /// <summary>
        /// Time taken to create this instance.
        /// If zero, the instance wasn't created by the service manager.
        /// </summary>
        [TableDataDuration("-")]
        public TimeSpan StartDuration { get; internal set; }

        /// <summary>
        /// Type of the parameters object (can be null)
        /// </summary>
        public readonly Type ParamType;

        public String GetUniqueName()
        {
            var n = Name;
            var t = Type?.ToString();
            if (String.IsNullOrEmpty(n))
                return t;
            return String.Concat(t, " [", n, ']');
        }

        /// <summary>
        /// The instance object
        /// </summary>
        public readonly Object Instance;


    }

}
