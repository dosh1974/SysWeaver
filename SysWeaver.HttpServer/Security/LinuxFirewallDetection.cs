using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysWeaver.Security
{
    /// <summary>
    /// The Linux firewalls that <see cref="LinuxFirewallHandler"/> can detect and use.
    /// </summary>
    public enum LinuxFirewallKinds
    {
        /// <summary>
        /// No firewall (none installed or none active).
        /// </summary>
        None = 0,
        /// <summary>
        /// ufw ("Uncomplicated Firewall"), the default front end on Ubuntu (and available on Debian, Mint, Arch etc).
        /// </summary>
        Ufw,
        /// <summary>
        /// firewalld ("firewall-cmd"), the default on RHEL, Fedora, CentOS Stream, Rocky / Alma Linux, openSUSE / SLES.
        /// It uses nftables underneath (its own "firewalld" tables), when it's running the firewall is firewalld (not plain nftables).
        /// </summary>
        Firewalld,
        /// <summary>
        /// Plain nftables ("nft"), ex: Debian's "nftables.service" loading "/etc/nftables.conf", or any hand written nftables ruleset.
        /// </summary>
        Nftables,
    }

    /// <summary>
    /// A tri-state value used by <see cref="LinuxFirewallInfo"/>.
    /// </summary>
    public enum LinuxFirewallStates
    {
        /// <summary>
        /// The state couldn't be determined (typically because the process isn't running as root, or because there is no systemd).
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// No (not enabled / not active).
        /// </summary>
        No,
        /// <summary>
        /// Yes (enabled / active).
        /// </summary>
        Yes,
    }

    /// <summary>
    /// What <see cref="LinuxFirewallHandler.Detect"/> found out about one installed firewall.
    /// </summary>
    public sealed class LinuxFirewallInfo
    {
        /// <summary>
        /// The kind of firewall.
        /// </summary>
        public LinuxFirewallKinds Kind { get; internal set; }

        /// <summary>
        /// The full name of the program used to control it (ex: "/usr/sbin/ufw", "/usr/bin/firewall-cmd", "/usr/sbin/nft").
        /// </summary>
        public String Program { get; internal set; }

        /// <summary>
        /// The version (ex: "0.36.2"), null if it couldn't be determined.
        /// </summary>
        public String Version { get; internal set; }

        /// <summary>
        /// True if the firewall is enabled (started at boot):
        /// ufw: "ENABLED=yes" in "/etc/ufw/ufw.conf", firewalld / nftables: "systemctl is-enabled" (unknown without systemd).
        /// </summary>
        public LinuxFirewallStates Enabled { get; internal set; }

        /// <summary>
        /// True if the firewall is running / filtering right now:
        /// ufw: "ufw status" (needs root), firewalld: "firewall-cmd --state" (or "systemctl is-active firewalld"),
        /// nftables: the ruleset contains a native filter chain hooked to input or output (needs root, else "systemctl is-active nftables").
        /// </summary>
        public LinuxFirewallStates Active { get; internal set; }

        /// <summary>
        /// Optional details about how the state was determined (or why it's unknown), null if none.
        /// </summary>
        public String Note { get; internal set; }

        /// <summary>
        /// True if the firewall is active, or might be active (the active state is unknown and it isn't known to be disabled).
        /// </summary>
        public bool LikelyActive => (Active == LinuxFirewallStates.Yes) || ((Active == LinuxFirewallStates.Unknown) && (Enabled != LinuxFirewallStates.No));

        /// <inheritdoc/>
        public override string ToString()
        {
            var s = String.Concat(Kind, Version == null ? "" : (" " + Version), " (active: ", Active, ", enabled: ", Enabled, ")");
            return Note == null ? s : String.Concat(s, " ", Note);
        }
    }

    /// <summary>
    /// The result of <see cref="LinuxFirewallHandler.Detect"/>: the installed firewalls and the one that is used.
    /// </summary>
    public sealed class LinuxFirewallDetection
    {
        /// <summary>
        /// When the detection was made (UTC).
        /// </summary>
        public DateTime Time { get; internal set; }

        /// <summary>
        /// True if the process is running as root (most status commands and all changes need root).
        /// </summary>
        public bool IsRoot { get; internal set; }

        /// <summary>
        /// True if systemd is the init system ("/run/systemd/system" exists and "systemctl" was found).
        /// </summary>
        public bool HasSystemd { get; internal set; }

        /// <summary>
        /// All installed (supported) firewalls, in priority order (firewalld, ufw, nftables), empty if none is installed.
        /// </summary>
        public IReadOnlyList<LinuxFirewallInfo> Installed { get; internal set; } = Array.Empty<LinuxFirewallInfo>();

        /// <summary>
        /// The firewall that is used (rules are added to), null if no firewall is active.
        /// The first active one in priority order (firewalld, ufw, nftables), if none is known to be active, the first one that might be active
        /// (see <see cref="LinuxFirewallInfo.LikelyActive"/>, then <see cref="IsCertain"/> is false).
        /// </summary>
        public LinuxFirewallInfo Used { get; internal set; }

        /// <summary>
        /// The kind of the used firewall, <see cref="LinuxFirewallKinds.None"/> if no firewall is active.
        /// </summary>
        public LinuxFirewallKinds UsedKind => Used?.Kind ?? LinuxFirewallKinds.None;

        /// <summary>
        /// True if the active state of all installed firewalls is known (false if <see cref="Used"/> or the absence of an active firewall is a guess).
        /// </summary>
        public bool IsCertain { get; internal set; }

        /// <summary>
        /// Get the info about an installed firewall.
        /// </summary>
        /// <param name="kind">The kind of firewall</param>
        /// <returns>The info, or null if that firewall isn't installed</returns>
        public LinuxFirewallInfo Get(LinuxFirewallKinds kind) => Installed.FirstOrDefault(x => x.Kind == kind);

        /// <inheritdoc/>
        public override string ToString()
        {
            var used = Used == null ? "No active firewall" : ("Using " + Used.Kind);
            if (!IsCertain)
                used += " (uncertain)";
            var inst = Installed.Count <= 0 ? "none installed" : ("installed: " + String.Join(", ", Installed));
            return String.Concat(used, ", ", inst, IsRoot ? ", root" : ", not root", HasSystemd ? ", systemd" : ", no systemd");
        }
    }

    /// <summary>
    /// The result of running a command with <see cref="ILinuxFirewallSystem.Run"/>.
    /// </summary>
    public readonly struct LinuxCommandResult
    {
        /// <summary>
        /// Create a command result.
        /// </summary>
        /// <param name="exitCode">The exit code of the process, negative if it couldn't be started or timed out</param>
        /// <param name="output">The text written to stdout (null is stored as an empty string)</param>
        /// <param name="error">The text written to stderr (null is stored as an empty string)</param>
        public LinuxCommandResult(int exitCode, String output = null, String error = null)
        {
            ExitCode = exitCode;
            Output = output ?? "";
            Error = error ?? "";
        }

        /// <summary>
        /// The exit code of the process, negative if it couldn't be started (-1) or timed out (-2).
        /// </summary>
        public readonly int ExitCode;

        /// <summary>
        /// The text written to stdout (never null).
        /// </summary>
        public readonly String Output;

        /// <summary>
        /// The text written to stderr (never null), or the reason the process couldn't be started.
        /// </summary>
        public readonly String Error;

        /// <summary>
        /// Stdout and stderr (trimmed, separated by a new line if both are non-empty).
        /// </summary>
        public String AllOutput
        {
            get
            {
                var o = Output.Trim();
                var e = Error.Trim();
                if (o.Length <= 0)
                    return e;
                if (e.Length <= 0)
                    return o;
                return o + "\n" + e;
            }
        }

        /// <inheritdoc/>
        public override string ToString() => String.Concat("Exit code: ", ExitCode, ", output: ", AllOutput);
    }

    /// <summary>
    /// The operating system services used by <see cref="LinuxFirewallHandler"/>: running programs, checking and reading files.
    /// Implemented by <see cref="LinuxFirewallSystem"/>, other implementations can be used to test the handler without a real firewall.
    /// </summary>
    public interface ILinuxFirewallSystem
    {
        /// <summary>
        /// True if the process is running as root (or is otherwise privileged).
        /// </summary>
        bool IsRoot { get; }

        /// <summary>
        /// The program search path (the "PATH" environment variable, folders separated by ':'), may be null.
        /// </summary>
        String SearchPath { get; }

        /// <summary>
        /// Check if a file exists.
        /// </summary>
        /// <param name="path">The full name of the file</param>
        /// <returns>True if the file exists</returns>
        bool FileExists(String path);

        /// <summary>
        /// Check if a directory exists.
        /// </summary>
        /// <param name="path">The full name of the directory</param>
        /// <returns>True if the directory exists</returns>
        bool DirectoryExists(String path);

        /// <summary>
        /// Read a text file.
        /// </summary>
        /// <param name="path">The full name of the file</param>
        /// <returns>The text, or null if the file doesn't exist or can't be read (ex: no access)</returns>
        String TryReadAllText(String path);

        /// <summary>
        /// Run a program (directly, never through a shell) and wait for it to exit.
        /// </summary>
        /// <param name="program">The full name of the program</param>
        /// <param name="args">The arguments, each one is passed as a separate argument (no quoting or escaping is needed)</param>
        /// <returns>The result, never throws</returns>
        LinuxCommandResult Run(String program, IReadOnlyList<String> args);
    }

    /// <summary>
    /// The real implementation of <see cref="ILinuxFirewallSystem"/>.
    /// Programs are started without a shell, with every argument passed separately, stdin closed and "LC_ALL=C" (so that the output isn't localized).
    /// </summary>
    public sealed class LinuxFirewallSystem : ILinuxFirewallSystem
    {
        /// <summary>
        /// The instance (with the default timeout).
        /// </summary>
        public static readonly LinuxFirewallSystem Instance = new LinuxFirewallSystem();

        /// <summary>
        /// The maximum time to wait for a program to exit, the process is killed (and an exit code of -2 is returned) after this time.
        /// </summary>
        public readonly TimeSpan Timeout;

        /// <summary>
        /// Create an instance.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a program to exit, null for the default (60 seconds)</param>
        public LinuxFirewallSystem(TimeSpan? timeout = null)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(60);
        }

        /// <inheritdoc/>
        public bool IsRoot => Environment.IsPrivilegedProcess;

        /// <inheritdoc/>
        public String SearchPath => Environment.GetEnvironmentVariable("PATH");

        /// <inheritdoc/>
        public bool FileExists(String path) => File.Exists(path);

        /// <inheritdoc/>
        public bool DirectoryExists(String path) => Directory.Exists(path);

        /// <inheritdoc/>
        public String TryReadAllText(String path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public LinuxCommandResult Run(String program, IReadOnlyList<String> args)
        {
            try
            {
                using var p = new Process();
                var si = p.StartInfo;
                si.FileName = program;
                foreach (var a in args)
                    si.ArgumentList.Add(a);
                si.UseShellExecute = false;
                si.RedirectStandardOutput = true;
                si.RedirectStandardError = true;
                si.RedirectStandardInput = true;
                si.CreateNoWindow = true;
                si.Environment["LC_ALL"] = "C";
                si.Environment["LANG"] = "C";
                si.Environment["LANGUAGE"] = "C";
                p.Start();
                try
                {
                    p.StandardInput.Close();
                }
                catch
                {
                }
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(Timeout))
                {
                    try
                    {
                        p.Kill(true);
                    }
                    catch
                    {
                    }
                    return new LinuxCommandResult(-2, null, "Timed out after " + Timeout);
                }
                p.WaitForExit();
                Task.WaitAll(o, e);
                return new LinuxCommandResult(p.ExitCode, o.Result, e.Result);
            }
            catch (Exception ex)
            {
                return new LinuxCommandResult(-1, null, ex.Message);
            }
        }
    }
}
