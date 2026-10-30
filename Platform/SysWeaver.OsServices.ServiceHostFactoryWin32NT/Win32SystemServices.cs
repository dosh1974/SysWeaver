using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;

#pragma warning disable CA1416

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="ISystemServices"/> for Windows, uses the Service Control Manager (advapi32) to list services and <see cref="ServiceController"/> to control them.
    /// Only Win32 services are listed (not drivers).
    /// </summary>
    sealed class Win32SystemServices : ISystemServices
    {
        public String Name => "Windows Service Control Manager";

        public IReadOnlyList<OsServiceStartModes> StartModes { get; } = [OsServiceStartModes.Automatic, OsServiceStartModes.AutomaticDelayed, OsServiceStartModes.Manual, OsServiceStartModes.Disabled];

        /// <summary>
        /// Max time to wait for a service to reach the requested state
        /// </summary>
        static readonly TimeSpan WaitTime = TimeSpan.FromSeconds(30);

        public IReadOnlyList<OsServiceInfo> GetServices()
        {
            var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE);
            if (scm == 0)
                ThrowLast("Failed to open the service control manager");
            try
            {
                var res = new List<OsServiceInfo>();
                foreach (var (name, display, status) in Enumerate(scm))
                {
                    var x = new OsServiceInfo
                    {
                        Name = name,
                        DisplayName = display,
                        Description = "",
                        Account = "",
                        Command = "",
                        File = "",
                    };
                    ApplyStatus(x, status);
                    var h = OpenService(scm, name, SERVICE_QUERY_CONFIG);
                    if (h != 0)
                    {
                        try
                        {
                            ApplyConfig(x, h, null);
                        }
                        finally
                        {
                            CloseServiceHandle(h);
                        }
                    }
                    UpdateActions(x, status.dwControlsAccepted);
                    res.Add(x);
                }
                return res;
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        public OsServiceInfo GetService(String name)
        {
            if (String.IsNullOrEmpty(name))
                return null;
            var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm == 0)
                ThrowLast("Failed to open the service control manager");
            try
            {
                var h = OpenService(scm, name, SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS);
                if (h == 0)
                {
                    var le = Marshal.GetLastWin32Error();
                    if (le == ERROR_SERVICE_DOES_NOT_EXIST)
                        return null;
                    throw new Win32Exception(le, "Failed to open the service \"" + name + "\": " + new Win32Exception(le).Message);
                }
                try
                {
                    var x = new OsServiceInfo
                    {
                        Name = name,
                        DisplayName = name,
                        Description = "",
                        Account = "",
                        Command = "",
                        File = "",
                    };
                    var props = new List<KeyValuePair<String, String>>();
                    var status = QueryStatus(h);
                    ApplyStatus(x, status);
                    ApplyConfig(x, h, props);
                    UpdateActions(x, status.dwControlsAccepted);
                    //  Real name casing
                    try
                    {
                        using var sc = new ServiceController(name);
                        x.Name = sc.ServiceName;
                        x.Dependents = sc.DependentServices.Select(d => d.ServiceName).ToArray();
                    }
                    catch
                    {
                        x.Dependents ??= [];
                    }
                    void add(String key, String value)
                    {
                        if (!String.IsNullOrEmpty(value))
                            props.Add(new(key, value));
                    }
                    add("Controls accepted", ControlsText(status.dwControlsAccepted));
                    add("Win32 exit code", status.dwWin32ExitCode == 0 ? "0" : String.Concat(status.dwWin32ExitCode, " (", new Win32Exception((int)status.dwWin32ExitCode).Message, ")"));
                    add("Service specific exit code", status.dwServiceSpecificExitCode.ToString());
                    if (status.dwCheckPoint != 0)
                        add("Check point", status.dwCheckPoint.ToString());
                    if (status.dwWaitHint != 0)
                        add("Wait hint", status.dwWaitHint + " ms");
                    if ((status.dwServiceFlags & 1) != 0)
                        add("Runs in", "A system process");
                    x.Properties = props.ToArray();
                    return x;
                }
                finally
                {
                    CloseServiceHandle(h);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        static ServiceController Get(String name)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("No service name!");
            var sc = new ServiceController(name);
            try
            {
                _ = sc.Status;
            }
            catch (Exception ex)
            {
                sc.Dispose();
                throw new Exception("The service \"" + name + "\" doesn't exist (or access is denied): " + Inner(ex));
            }
            return sc;
        }

        static String Inner(Exception ex)
        {
            var i = ex.InnerException;
            return i == null ? ex.Message : String.Concat(ex.Message, " ", i.Message);
        }

        static void Wait(ServiceController sc, ServiceControllerStatus status)
        {
            try
            {
                sc.WaitForStatus(status, WaitTime);
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                //  Still pending, the status will show it
            }
        }

        static void Do(String name, String what, Action<ServiceController> a)
        {
            using var sc = Get(name);
            try
            {
                a(sc);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to " + what + " \"" + name + "\": " + Inner(ex));
            }
        }

        public void Start(String name) => Do(name, "start", sc =>
        {
            if (sc.Status == ServiceControllerStatus.Running)
                return;
            sc.Start();
            Wait(sc, ServiceControllerStatus.Running);
        });

        public void Stop(String name) => Do(name, "stop", sc =>
        {
            if (sc.Status == ServiceControllerStatus.Stopped)
                return;
            sc.Stop(true);
            Wait(sc, ServiceControllerStatus.Stopped);
        });

        public void Restart(String name) => Do(name, "restart", sc =>
        {
            //  Restart dependents that are running after the service have been started
            var running = new List<String>();
            foreach (var d in sc.DependentServices)
            {
                using (d)
                {
                    if (d.Status == ServiceControllerStatus.Running)
                        running.Add(d.ServiceName);
                }
            }
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop(true);
                sc.WaitForStatus(ServiceControllerStatus.Stopped, WaitTime);
            }
            sc.Start();
            Wait(sc, ServiceControllerStatus.Running);
            foreach (var d in running)
            {
                try
                {
                    using var dc = new ServiceController(d);
                    if (dc.Status == ServiceControllerStatus.Stopped)
                        dc.Start();
                }
                catch
                {
                }
            }
        });

        public void Pause(String name) => Do(name, "pause", sc =>
        {
            sc.Pause();
            Wait(sc, ServiceControllerStatus.Paused);
        });

        public void Continue(String name) => Do(name, "continue", sc =>
        {
            sc.Continue();
            Wait(sc, ServiceControllerStatus.Running);
        });

        public void Reload(String name)
            => throw new NotSupportedException("Windows services can't be asked to reload their configuration!");

        public void SetStartMode(String name, OsServiceStartModes mode)
        {
            uint st = mode switch
            {
                OsServiceStartModes.Automatic => SERVICE_AUTO_START,
                OsServiceStartModes.AutomaticDelayed => SERVICE_AUTO_START,
                OsServiceStartModes.Manual => SERVICE_DEMAND_START,
                OsServiceStartModes.Disabled => SERVICE_DISABLED,
                _ => throw new ArgumentException("The start mode " + mode + " can't be used!"),
            };
            WithService(name, SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG, "change the start mode of", h =>
            {
                if (!ChangeServiceConfig(h, SERVICE_NO_CHANGE, st, SERVICE_NO_CHANGE, null, null, 0, null, null, null, null))
                    ThrowLast("Failed to change the start mode of \"" + name + "\"");
                if (st == SERVICE_AUTO_START)
                {
                    var p = Marshal.AllocHGlobal(4);
                    try
                    {
                        Marshal.WriteInt32(p, mode == OsServiceStartModes.AutomaticDelayed ? 1 : 0);
                        if (!ChangeServiceConfig2(h, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, p))
                            ThrowLast("Failed to change the delayed start of \"" + name + "\"");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(p);
                    }
                }
            });
        }

        public void Delete(String name)
        {
            try
            {
                Stop(name);
            }
            catch
            {
                //  Delete anyway (it's deleted once it stops)
            }
            WithService(name, DELETE, "delete", h =>
            {
                if (!DeleteService(h))
                    ThrowLast("Failed to delete \"" + name + "\"");
            });
        }

        static void WithService(String name, uint access, String what, Action<nint> a)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentException("No service name!");
            var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
            if (scm == 0)
                ThrowLast("Failed to open the service control manager");
            try
            {
                var h = OpenService(scm, name, access);
                if (h == 0)
                    ThrowLast("Failed to " + what + " \"" + name + "\"");
                try
                {
                    a(h);
                }
                finally
                {
                    CloseServiceHandle(h);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }

        #region Helpers

        static void ThrowLast(String message)
        {
            var le = Marshal.GetLastWin32Error();
            throw new Win32Exception(le, String.Concat(message, ": ", new Win32Exception(le).Message));
        }

        static void ApplyStatus(OsServiceInfo x, SERVICE_STATUS_PROCESS s)
        {
            (x.Status, x.StatusText) = s.dwCurrentState switch
            {
                1 => (ServiceStatus.Stopped, "Stopped"),
                2 => (ServiceStatus.StartPending, "Starting"),
                3 => (ServiceStatus.StopPending, "Stopping"),
                4 => (ServiceStatus.Running, "Running"),
                5 => (ServiceStatus.ContinuePending, "Continuing"),
                6 => (ServiceStatus.PausePending, "Pausing"),
                7 => (ServiceStatus.Paused, "Paused"),
                _ => (ServiceStatus.Unknown, "Unknown"),
            };
            x.ProcessId = (int)s.dwProcessId;
            x.ExitCode = (int)(s.dwWin32ExitCode == ERROR_SERVICE_SPECIFIC_ERROR ? s.dwServiceSpecificExitCode : s.dwWin32ExitCode);
            x.ServiceType = TypeText(s.dwServiceType);
        }

        static void UpdateActions(OsServiceInfo x, uint controls)
        {
            var a = OsServiceActions.SetStartMode | OsServiceActions.Delete;
            var canStop = (controls & SERVICE_ACCEPT_STOP) != 0;
            var canPause = (controls & SERVICE_ACCEPT_PAUSE_CONTINUE) != 0;
            switch (x.Status)
            {
                case ServiceStatus.Stopped:
                    if (x.StartMode != OsServiceStartModes.Disabled)
                        a |= OsServiceActions.Start;
                    break;
                case ServiceStatus.Running:
                    if (canStop)
                        a |= OsServiceActions.Stop | OsServiceActions.Restart;
                    if (canPause)
                        a |= OsServiceActions.Pause;
                    break;
                case ServiceStatus.Paused:
                    if (canStop)
                        a |= OsServiceActions.Stop | OsServiceActions.Restart;
                    if (canPause)
                        a |= OsServiceActions.Continue;
                    break;
            }
            x.Actions = a;
        }

        static String TypeText(uint t)
        {
            String s;
            if ((t & 0x10) != 0)
                s = "Own process";
            else if ((t & 0x20) != 0)
                s = "Shared process";
            else if ((t & 0x1) != 0)
                s = "Kernel driver";
            else if ((t & 0x2) != 0)
                s = "File system driver";
            else
                s = "0x" + t.ToString("x");
            if ((t & 0x40) != 0)
                s += ", user";
            if ((t & 0x80) != 0)
                s += ", instance";
            if ((t & 0x100) != 0)
                s += ", interactive";
            return s;
        }

        static String ControlsText(uint c)
        {
            var l = new List<String>();
            if ((c & 0x1) != 0) l.Add("Stop");
            if ((c & 0x2) != 0) l.Add("Pause / continue");
            if ((c & 0x4) != 0) l.Add("Shutdown");
            if ((c & 0x8) != 0) l.Add("Parameter change");
            if ((c & 0x10) != 0) l.Add("Net bind change");
            if ((c & 0x20) != 0) l.Add("Hardware profile change");
            if ((c & 0x40) != 0) l.Add("Power event");
            if ((c & 0x80) != 0) l.Add("Session change");
            if ((c & 0x100) != 0) l.Add("Pre shutdown");
            if ((c & 0x200) != 0) l.Add("Time change");
            if ((c & 0x400) != 0) l.Add("Trigger event");
            if ((c & 0x800) != 0) l.Add("User logoff");
            if ((c & 0x1000) != 0) l.Add("Low resources");
            return l.Count == 0 ? "None" : String.Join(", ", l);
        }

        static String Str(nint p) => p == 0 ? "" : (Marshal.PtrToStringUni(p) ?? "");

        static String[] MultiStr(nint p)
        {
            var res = new List<String>();
            if (p == 0)
                return [];
            for (; ; )
            {
                var s = Marshal.PtrToStringUni(p) ?? "";
                if (s.Length == 0)
                    break;
                res.Add(s);
                p += (s.Length + 1) * 2;
            }
            return res.ToArray();
        }

        static SERVICE_STATUS_PROCESS QueryStatus(nint h)
        {
            var size = Marshal.SizeOf<SERVICE_STATUS_PROCESS>();
            var buf = Marshal.AllocHGlobal(size);
            try
            {
                if (!QueryServiceStatusEx(h, SC_STATUS_PROCESS_INFO, buf, size, out _))
                    return default;
                return Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        /// <summary>
        /// Call QueryServiceConfig2 with a buffer, the callback reads the result
        /// </summary>
        static bool QueryConfig2(nint h, uint level, Action<nint> read)
        {
            QueryServiceConfig2(h, level, 0, 0, out var needed);
            if (needed == 0)
                return false;
            var buf = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!QueryServiceConfig2(h, level, buf, needed, out _))
                    return false;
                read(buf);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        static void ApplyConfig(OsServiceInfo x, nint h, List<KeyValuePair<String, String>> props)
        {
            QueryServiceConfig(h, 0, 0, out var needed);
            if (needed > 0)
            {
                var buf = Marshal.AllocHGlobal((int)needed);
                try
                {
                    if (QueryServiceConfig(h, buf, needed, out _))
                    {
                        var c = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buf);
                        x.Command = Str(c.lpBinaryPathName);
                        x.Account = Str(c.lpServiceStartName);
                        var dn = Str(c.lpDisplayName);
                        if (dn.Length > 0)
                            x.DisplayName = dn;
                        (x.StartMode, x.StartModeText) = c.dwStartType switch
                        {
                            SERVICE_BOOT_START => (OsServiceStartModes.Boot, "Boot"),
                            SERVICE_SYSTEM_START => (OsServiceStartModes.System, "System"),
                            SERVICE_AUTO_START => (OsServiceStartModes.Automatic, "Automatic"),
                            SERVICE_DEMAND_START => (OsServiceStartModes.Manual, "Manual"),
                            SERVICE_DISABLED => (OsServiceStartModes.Disabled, "Disabled"),
                            _ => (OsServiceStartModes.Unknown, c.dwStartType.ToString()),
                        };
                        if (props != null)
                        {
                            x.Dependencies = MultiStr(c.lpDependencies);
                            props.Add(new("Error control", c.dwErrorControl switch
                            {
                                0 => "Ignore",
                                1 => "Normal",
                                2 => "Severe",
                                3 => "Critical",
                                _ => c.dwErrorControl.ToString(),
                            }));
                            var g = Str(c.lpLoadOrderGroup);
                            if (g.Length > 0)
                                props.Add(new("Load order group", g));
                            if (c.dwTagId != 0)
                                props.Add(new("Tag", c.dwTagId.ToString()));
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            QueryConfig2(h, SERVICE_CONFIG_DESCRIPTION, b => x.Description = ResolveIndirect(Str(Marshal.ReadIntPtr(b))));
            if (x.StartMode == OsServiceStartModes.Automatic)
            {
                QueryConfig2(h, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, b =>
                {
                    if (Marshal.ReadInt32(b) != 0)
                    {
                        x.StartMode = OsServiceStartModes.AutomaticDelayed;
                        x.StartModeText = "Automatic (delayed)";
                    }
                });
            }
            if ((x.StartMode is OsServiceStartModes.Manual or OsServiceStartModes.Automatic) && (props != null || x.StartMode == OsServiceStartModes.Manual))
            {
                //  Trigger started services
                QueryConfig2(h, SERVICE_CONFIG_TRIGGER_INFO, b =>
                {
                    var count = Marshal.ReadInt32(b);
                    if (count > 0)
                    {
                        x.StartModeText += " (trigger)";
                        props?.Add(new("Triggers", count.ToString()));
                    }
                });
            }
            if (props == null)
                return;
            QueryConfig2(h, SERVICE_CONFIG_FAILURE_ACTIONS, b =>
            {
                var f = Marshal.PtrToStructure<SERVICE_FAILURE_ACTIONS>(b);
                var acts = new List<String>();
                var size = Marshal.SizeOf<SC_ACTION>();
                for (int i = 0; i < f.cActions; ++i)
                {
                    var a = Marshal.PtrToStructure<SC_ACTION>(f.lpsaActions + i * size);
                    var t = a.Type switch
                    {
                        0 => "Take no action",
                        1 => "Restart the service",
                        2 => "Restart the computer",
                        3 => "Run a program",
                        _ => a.Type.ToString(),
                    };
                    if ((a.Type != 0) && (a.Delay > 0))
                        t = String.Concat(t, " after ", FormatMs(a.Delay));
                    acts.Add(t);
                }
                String[] names = ["First failure", "Second failure", "Subsequent failures"];
                for (int i = 0; i < acts.Count; ++i)
                    props.Add(new(i < 2 ? names[i] : (i == acts.Count - 1 ? names[2] : String.Concat("Failure ", i + 1)), acts[i]));
                if (acts.Count > 0)
                    props.Add(new("Reset fail count after", f.dwResetPeriod == uint.MaxValue ? "Never" : FormatMs((ulong)f.dwResetPeriod * 1000)));
                var cmd = Str(f.lpCommand);
                if (cmd.Length > 0)
                    props.Add(new("Run program", cmd));
                var msg = Str(f.lpRebootMsg);
                if (msg.Length > 0)
                    props.Add(new("Reboot message", msg));
            });
            QueryConfig2(h, SERVICE_CONFIG_SERVICE_SID_INFO, b => props.Add(new("Service SID type", Marshal.ReadInt32(b) switch
            {
                0 => "None",
                1 => "Unrestricted",
                3 => "Restricted",
                var v => v.ToString(),
            })));
            QueryConfig2(h, SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO, b =>
            {
                var p = MultiStr(Marshal.ReadIntPtr(b));
                if (p.Length > 0)
                    props.Add(new("Required privileges", String.Join(", ", p)));
            });
            QueryConfig2(h, SERVICE_CONFIG_PRESHUTDOWN_INFO, b => props.Add(new("Pre shutdown timeout", FormatMs((uint)Marshal.ReadInt32(b)))));
        }

        static String FormatMs(ulong ms)
        {
            if (ms < 1000)
                return ms + " ms";
            var s = ms / 1000.0;
            if (s < 120)
                return s.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " seconds";
            var m = s / 60;
            if (m < 120)
                return m.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " minutes";
            var hours = m / 60;
            if (hours < 48)
                return hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " hours";
            return (hours / 24).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " days";
        }

        /// <summary>
        /// Resolve an indirect string, ex: "@%SystemRoot%\system32\spoolsv.exe,-2"
        /// </summary>
        internal static String ResolveIndirect(String s)
        {
            if (String.IsNullOrEmpty(s) || (s[0] != '@'))
                return s ?? "";
            var buf = new char[2048];
            if (SHLoadIndirectString(s, buf, buf.Length, 0) != 0)
                return s;
            var i = Array.IndexOf(buf, '\0');
            return new String(buf, 0, i < 0 ? buf.Length : i);
        }

        static IEnumerable<(String Name, String Display, SERVICE_STATUS_PROCESS Status)> Enumerate(nint scm)
        {
            uint resume = 0;
            var res = new List<(String, String, SERVICE_STATUS_PROCESS)>();
            var esize = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
            for (; ; )
            {
                EnumServicesStatusEx(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL, 0, 0, out var needed, out _, ref resume, null);
                if (needed == 0)
                    break;
                var buf = Marshal.AllocHGlobal((int)needed);
                try
                {
                    var ok = EnumServicesStatusEx(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL, buf, needed, out _, out var count, ref resume, null);
                    var le = ok ? 0 : Marshal.GetLastWin32Error();
                    if ((!ok) && (le != ERROR_MORE_DATA))
                        throw new Win32Exception(le, "Failed to list the services: " + new Win32Exception(le).Message);
                    for (int i = 0; i < count; ++i)
                    {
                        var e = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(buf + i * esize);
                        res.Add((Str(e.lpServiceName), Str(e.lpDisplayName), e.ServiceStatusProcess));
                    }
                    if (ok)
                        break;
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            return res;
        }

        #endregion//Helpers

        #region Interop

        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
        const uint SERVICE_QUERY_CONFIG = 0x0001;
        const uint SERVICE_CHANGE_CONFIG = 0x0002;
        const uint SERVICE_QUERY_STATUS = 0x0004;
        const uint DELETE = 0x10000;
        const uint SERVICE_WIN32 = 0x30;
        const uint SERVICE_STATE_ALL = 3;
        const uint SC_ENUM_PROCESS_INFO = 0;
        const uint SC_STATUS_PROCESS_INFO = 0;
        const uint SERVICE_NO_CHANGE = 0xffffffff;
        const uint SERVICE_BOOT_START = 0;
        const uint SERVICE_SYSTEM_START = 1;
        const uint SERVICE_AUTO_START = 2;
        const uint SERVICE_DEMAND_START = 3;
        const uint SERVICE_DISABLED = 4;
        const uint SERVICE_ACCEPT_STOP = 1;
        const uint SERVICE_ACCEPT_PAUSE_CONTINUE = 2;
        const uint SERVICE_CONFIG_DESCRIPTION = 1;
        const uint SERVICE_CONFIG_FAILURE_ACTIONS = 2;
        const uint SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;
        const uint SERVICE_CONFIG_SERVICE_SID_INFO = 5;
        const uint SERVICE_CONFIG_REQUIRED_PRIVILEGES_INFO = 6;
        const uint SERVICE_CONFIG_PRESHUTDOWN_INFO = 7;
        const uint SERVICE_CONFIG_TRIGGER_INFO = 8;
        const int ERROR_MORE_DATA = 234;
        const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
        const uint ERROR_SERVICE_SPECIFIC_ERROR = 1066;

        [StructLayout(LayoutKind.Sequential)]
        struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
            public uint dwProcessId;
            public uint dwServiceFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ENUM_SERVICE_STATUS_PROCESS
        {
            public nint lpServiceName;
            public nint lpDisplayName;
            public SERVICE_STATUS_PROCESS ServiceStatusProcess;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct QUERY_SERVICE_CONFIG
        {
            public uint dwServiceType;
            public uint dwStartType;
            public uint dwErrorControl;
            public nint lpBinaryPathName;
            public nint lpLoadOrderGroup;
            public uint dwTagId;
            public nint lpDependencies;
            public nint lpServiceStartName;
            public nint lpDisplayName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SERVICE_FAILURE_ACTIONS
        {
            public uint dwResetPeriod;
            public nint lpRebootMsg;
            public nint lpCommand;
            public int cActions;
            public nint lpsaActions;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SC_ACTION
        {
            public int Type;
            public uint Delay;
        }

        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern nint OpenSCManager(String lpMachineName, String lpDatabaseName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern nint OpenService(nint hSCManager, String lpServiceName, uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CloseServiceHandle(nint hSCObject);

        [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool EnumServicesStatusEx(nint hSCManager, uint infoLevel, uint dwServiceType, uint dwServiceState, nint lpServices, uint cbBufSize, out uint pcbBytesNeeded, out uint lpServicesReturned, ref uint lpResumeHandle, String pszGroupName);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool QueryServiceStatusEx(nint hService, uint infoLevel, nint lpBuffer, int cbBufSize, out int pcbBytesNeeded);

        [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryServiceConfig(nint hService, nint lpServiceConfig, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", EntryPoint = "QueryServiceConfig2W", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool QueryServiceConfig2(nint hService, uint dwInfoLevel, nint lpBuffer, uint cbBufSize, out uint pcbBytesNeeded);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfigW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool ChangeServiceConfig(nint hService, uint dwServiceType, uint dwStartType, uint dwErrorControl, String lpBinaryPathName, String lpLoadOrderGroup, nint lpdwTagId, String lpDependencies, String lpServiceStartName, String lpPassword, String lpDisplayName);

        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig2W", SetLastError = true)]
        static extern bool ChangeServiceConfig2(nint hService, uint dwInfoLevel, nint lpInfo);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool DeleteService(nint hService);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(String pszSource, [Out] char[] pszOutBuf, int cchOutBuf, nint ppvReserved);

        #endregion//Interop
    }
}
