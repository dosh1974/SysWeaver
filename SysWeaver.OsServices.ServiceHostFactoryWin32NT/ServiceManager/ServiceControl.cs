#pragma warning disable CA1416

namespace SysWeaver.OsServices.ServiceManager
{
    /// <summary>
    /// Win32 SERVICE_CONTROL_* codes sent to a service using ControlService.
    /// </summary>
    enum ServiceControl : uint
    {
        /// <summary>
        /// SERVICE_CONTROL_STOP, stop the service.
        /// </summary>
        Stop = 0x00000001,
        /// <summary>
        /// SERVICE_CONTROL_PAUSE, pause the service.
        /// </summary>
        Pause = 0x00000002,
        /// <summary>
        /// SERVICE_CONTROL_CONTINUE, resume a paused service.
        /// </summary>
        Continue = 0x00000003,
        /// <summary>
        /// SERVICE_CONTROL_INTERROGATE, request a status update.
        /// </summary>
        Interrogate = 0x00000004,
        /// <summary>
        /// SERVICE_CONTROL_SHUTDOWN, the system is shutting down (only sent by the system).
        /// </summary>
        Shutdown = 0x00000005,
        /// <summary>
        /// SERVICE_CONTROL_PARAMCHANGE, the start up parameters have changed.
        /// </summary>
        ParamChange = 0x00000006,
        /// <summary>
        /// SERVICE_CONTROL_NETBINDADD, a new network component is available.
        /// </summary>
        NetBindAdd = 0x00000007,
        /// <summary>
        /// SERVICE_CONTROL_NETBINDREMOVE, a network component has been removed.
        /// </summary>
        NetBindRemove = 0x00000008,
        /// <summary>
        /// SERVICE_CONTROL_NETBINDENABLE, a network binding has been enabled.
        /// </summary>
        NetBindEnable = 0x00000009,
        /// <summary>
        /// SERVICE_CONTROL_NETBINDDISABLE, a network binding has been disabled.
        /// </summary>
        NetBindDisable = 0x0000000A,

        /// <summary>
        /// SERVICE_CONTROL_DEVICEEVENT (only sent by the system).
        /// </summary>
        DEVICEEVENT = 0x0000000B,
        /// <summary>
        /// SERVICE_CONTROL_HARDWAREPROFILECHANGE (only sent by the system).
        /// </summary>
        HARDWAREPROFILECHANGE = 0x0000000C,
        /// <summary>
        /// SERVICE_CONTROL_POWEREVENT (only sent by the system).
        /// </summary>
        POWEREVENT = 0x0000000D,
        /// <summary>
        /// SERVICE_CONTROL_SESSIONCHANGE (only sent by the system).
        /// </summary>
        SESSIONCHANGE = 0x0000000E
    }

}

