using System;
using System.Globalization;
using System.IO;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IPower"/> for Linux: "shutdown" (delays in whole minutes, the message is sent to logged on users with wall), "shutdown -c" to abort,
    /// "systemctl suspend / hibernate". The scheduled shutdown is read from /run/systemd/shutdown/scheduled (systemd).
    /// </summary>
    sealed class UnixPower : IPower
    {
        public UnixPower(String shutdown, String systemctl)
        {
            ShutdownCmd = shutdown;
            Systemctl = systemctl;
        }

        readonly String ShutdownCmd;
        readonly String Systemctl;

        public OsPowerActions Supported
            => (ShutdownCmd != null ? (OsPowerActions.Shutdown | OsPowerActions.Restart | OsPowerActions.Abort) : OsPowerActions.None)
             | (Systemctl != null ? (OsPowerActions.Sleep | OsPowerActions.Hibernate) : OsPowerActions.None);

        public int MaxMessageLength => ShutdownCmd != null ? 200 : 0;

        public int DelayGranularity => 60;

        public void Shutdown(bool restart, int delaySeconds, bool force, String message)
        {
            if (ShutdownCmd == null)
                throw new PlatformNotSupportedException("The \"shutdown\" command wasn't found!");
            var when = delaySeconds <= 0 ? "now" : ("+" + ((delaySeconds + 59) / 60).ToString(CultureInfo.InvariantCulture));
            message = (message ?? "").Trim();
            if (message.Length > MaxMessageLength)
                message = message.Substring(0, MaxMessageLength);
            if (message.StartsWith('-'))
                message = " " + message;
            if (message.Length > 0)
                UnixCommand.Change(ShutdownCmd, restart ? "-r" : "-P", when, message);
            else
                UnixCommand.Change(ShutdownCmd, restart ? "-r" : "-P", when);
        }

        public void Abort()
        {
            if (ShutdownCmd == null)
                throw new PlatformNotSupportedException("The \"shutdown\" command wasn't found!");
            UnixCommand.Change(ShutdownCmd, "-c");
        }

        public void Suspend(bool hibernate)
        {
            if (Systemctl == null)
                throw new PlatformNotSupportedException("Suspend requires systemd (systemctl)!");
            UnixCommand.Change(Systemctl, hibernate ? "hibernate" : "suspend");
        }

        public OsPowerSchedule GetScheduled()
        {
            const String file = "/run/systemd/shutdown/scheduled";
            try
            {
                if (!File.Exists(file))
                    return null;
                long usec = 0;
                String mode = "";
                foreach (var l in File.ReadAllLines(file))
                {
                    if (l.StartsWith("USEC=", StringComparison.Ordinal))
                        long.TryParse(l.AsSpan(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out usec);
                    else if (l.StartsWith("MODE=", StringComparison.Ordinal))
                        mode = l.Substring(5).Trim();
                }
                if ((usec <= 0) || mode.StartsWith("dry", StringComparison.Ordinal))
                    return null;
                return new OsPowerSchedule
                {
                    Action = mode is "reboot" or "kexec" ? OsPowerActions.Restart : OsPowerActions.Shutdown,
                    Time = DateTime.UnixEpoch.AddTicks(usec * 10),
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
