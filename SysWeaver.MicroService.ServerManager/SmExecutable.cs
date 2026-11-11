using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// An executable found in the active folder of a managed service (a candidate for the service executable).
    /// </summary>
    public sealed class SmExecutable
    {
        public override string ToString() => Name;

        /// <summary>
        /// The file name.
        /// </summary>
        public String Name;

        /// <summary>
        /// The kind: "Console", "Gui" or "Script".
        /// </summary>
        public String Kind;

        /// <summary>
        /// A short description of the format, ex: "PE32+ x64 console", "ELF64 x86-64 PIE".
        /// </summary>
        public String Format;

        /// <summary>
        /// When the file was last modified.
        /// </summary>
        public DateTime LastModified;

        /// <summary>
        /// The size in bytes.
        /// </summary>
        public long Size;

        /// <summary>
        /// False if the file lacks the execute permission (Linux).
        /// </summary>
        public bool CanExecute;

        /// <summary>
        /// True if this is a known bundled tool (ex: "createdump").
        /// </summary>
        public bool IsKnownTool;

        /// <summary>
        /// True if this looks like a .NET app host.
        /// </summary>
        public bool IsDotNetHost;

        /// <summary>
        /// True if this is the executable that is used for the service.
        /// </summary>
        public bool IsCurrent;

        /// <summary>
        /// True if this is the best guess (the one that is used when the selection is automatic).
        /// </summary>
        public bool IsBestGuess;

        public SmExecutable()
        {
        }

        internal SmExecutable(ExecutableFile f, String current)
        {
            Name = f.Name;
            Kind = f.Kind.ToString();
            Format = f.Format;
            LastModified = f.LastModifiedUtc;
            Size = f.Size;
            CanExecute = f.CanExecute;
            IsKnownTool = f.IsKnownTool;
            IsDotNetHost = f.IsDotNetHost;
            IsCurrent = String.Equals(f.Name, current, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            IsBestGuess = f.Rank == 0;
        }
    }

    /// <summary>
    /// A request to select the executable of a managed service.
    /// </summary>
    public sealed class SmSelectExeRequest
    {
        /// <summary>
        /// The name of the managed service.
        /// </summary>
        public String ServiceName;

        /// <summary>
        /// The file name of the executable to use (one of <see cref="SmServiceDetail.Executables"/>), null or empty to select automatically (the best guess).
        /// </summary>
        public String ExeName;
    }

    /// <summary>
    /// The persisted executable of a managed service (in the app key value store).
    /// </summary>
    public sealed class SmExeSelection
    {
        /// <summary>
        /// The file name of the executable.
        /// </summary>
        public String Name;

        /// <summary>
        /// True if selected by a user (never replaced automatically), false if detected (replaced if it disappears or doesn't respond to the service verbs as a service).
        /// </summary>
        public bool Manual;
    }
}
