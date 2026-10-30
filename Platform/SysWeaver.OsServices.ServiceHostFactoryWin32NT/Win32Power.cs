using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IPower"/> for Windows: shutdown.exe for shutdown, restart and abort (it handles the privileges and shows the message),
    /// SetSuspendState for sleep and hibernate (after enabling the shutdown privilege).
    /// </summary>
    sealed class Win32Power : IPower
    {
        public OsPowerActions Supported => OsPowerActions.Shutdown | OsPowerActions.Restart | OsPowerActions.Sleep | OsPowerActions.Hibernate | OsPowerActions.Abort;

        public int MaxMessageLength => 512;

        public int DelayGranularity => 1;

        /// <summary>
        /// Max delay of shutdown.exe (10 years in seconds)
        /// </summary>
        const int MaxDelay = 315360000;

        static void RunShutdown(params String[] args)
        {
            using var p = new Process();
            var s = p.StartInfo;
            s.FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe");
            foreach (var a in args)
                s.ArgumentList.Add(a);
            s.UseShellExecute = false;
            s.CreateNoWindow = true;
            s.RedirectStandardOutput = true;
            s.RedirectStandardError = true;
            p.Start();
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30000))
                throw new Exception("shutdown.exe didn't exit!");
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                var msg = (e.Result + " " + o.Result).Trim();
                throw new Exception(String.Concat("shutdown.exe failed (", p.ExitCode, ")", msg.Length > 0 ? (": " + msg) : ""));
            }
        }

        public void Shutdown(bool restart, int delaySeconds, bool force, String message)
        {
            delaySeconds = Math.Clamp(delaySeconds, 0, MaxDelay);
            var args = new System.Collections.Generic.List<String> { restart ? "/r" : "/s", "/t", delaySeconds.ToString() };
            //  A delay > 0 implies /f
            if (force && (delaySeconds == 0))
                args.Add("/f");
            //  Planned, other
            args.Add("/d");
            args.Add("p:0:0");
            message = (message ?? "").Trim();
            if (message.Length > 0)
            {
                if (message.Length > MaxMessageLength)
                    message = message.Substring(0, MaxMessageLength);
                args.Add("/c");
                args.Add(message);
            }
            RunShutdown(args.ToArray());
        }

        public void Abort() => RunShutdown("/a");

        [DllImport("powrprof.dll", SetLastError = true)]
        static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        public void Suspend(bool hibernate)
        {
            if (!Win32Privileges.Enable("SeShutdownPrivilege"))
                throw new Exception("The process doesn't have the shutdown privilege");
            if (!SetSuspendState(hibernate, false, false))
            {
                var err = Marshal.GetLastWin32Error();
                throw new Exception(String.Concat("Failed to ", hibernate ? "hibernate" : "sleep", " (error ", err, ")", hibernate ? ", is hibernation enabled? (powercfg /hibernate on)" : ""));
            }
        }
    }
}
