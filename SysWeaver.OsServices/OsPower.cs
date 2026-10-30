using System;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// Power actions.
    /// </summary>
    [Flags]
    public enum OsPowerActions
    {
        None = 0,

        /// <summary>
        /// Turn the system off
        /// </summary>
        Shutdown = 1,

        /// <summary>
        /// Restart the system
        /// </summary>
        Restart = 2,

        /// <summary>
        /// Suspend to RAM
        /// </summary>
        Sleep = 4,

        /// <summary>
        /// Suspend to disk
        /// </summary>
        Hibernate = 8,

        /// <summary>
        /// Cancel a scheduled shutdown or restart
        /// </summary>
        Abort = 16,
    }


    /// <summary>
    /// A scheduled shutdown or restart.
    /// </summary>
    public sealed class OsPowerSchedule
    {
        /// <summary>
        /// <see cref="OsPowerActions.Shutdown"/> or <see cref="OsPowerActions.Restart"/>
        /// </summary>
        public OsPowerActions Action;

        /// <summary>
        /// When it happens (UTC)
        /// </summary>
        public DateTime Time;
    }


    /// <summary>
    /// Shut down, restart, suspend or hibernate the system.
    /// Get an instance using <see cref="OsSystem.Power"/> (created by <see cref="IServiceHostFactory.CreatePower"/>).
    /// Requires elevation (admin / root), failures are reported by throwing an exception (with a description).
    /// </summary>
    public interface IPower
    {
        /// <summary>
        /// The actions that are supported
        /// </summary>
        OsPowerActions Supported { get; }

        /// <summary>
        /// Max length of the message shown to logged on users (0 if messages aren't supported)
        /// </summary>
        int MaxMessageLength { get; }

        /// <summary>
        /// The delay granularity in seconds (ex: 60 on Linux, where shutdown takes minutes)
        /// </summary>
        int DelayGranularity { get; }

        /// <summary>
        /// Shut down or restart the system.
        /// </summary>
        /// <param name="restart">True to restart, false to shut down</param>
        /// <param name="delaySeconds">The delay (0 = now), rounded up to <see cref="DelayGranularity"/></param>
        /// <param name="force">True to close applications without asking them (Windows)</param>
        /// <param name="message">An optional message shown to logged on users</param>
        void Shutdown(bool restart, int delaySeconds, bool force, String message);

        /// <summary>
        /// Cancel a scheduled shutdown or restart.
        /// </summary>
        void Abort();

        /// <summary>
        /// Suspend the system to RAM (sleep) or disk (hibernate).
        /// </summary>
        /// <param name="hibernate">True to hibernate, false to sleep</param>
        void Suspend(bool hibernate);

        /// <summary>
        /// Get the scheduled shutdown or restart (if it can be read from the system).
        /// </summary>
        /// <returns>The schedule or null if none is scheduled (or unknown)</returns>
        OsPowerSchedule GetScheduled() => null;
    }
}
