using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32.SafeHandles;

namespace SysWeaver
{
    /// <summary>
    /// The kind of an executable file (as found by <see cref="ExecutableFinder"/>).
    /// The order is the preference when guessing the main executable of a folder (lower is better).
    /// </summary>
    public enum ExecutableKinds
    {
        /// <summary>
        /// A native console (character mode) executable: a PE image with the console subsystem (Windows), or an ELF executable (Linux).
        /// Services and their command line interface are console programs.
        /// </summary>
        Console = 0,
        /// <summary>
        /// A native graphical executable (a PE image with the Windows GUI subsystem), unlikely to be a service.
        /// </summary>
        Gui = 1,
        /// <summary>
        /// A script with an interpreter line ("#!", Linux).
        /// </summary>
        Script = 2,
        /// <summary>
        /// Not an executable (or an unknown format).
        /// </summary>
        None = 100,
    }

    /// <summary>
    /// An executable file found in a folder by <see cref="ExecutableFinder.Find"/> (or <see cref="IPlatformTools.FindExecutables"/>).
    /// </summary>
    public sealed class ExecutableFile
    {
        public override string ToString() => String.Concat(Name, " [", Format, "]");

        /// <summary>
        /// The full path of the file.
        /// </summary>
        public String FullName;

        /// <summary>
        /// The file name (no folder).
        /// </summary>
        public String Name;

        /// <summary>
        /// The kind of executable.
        /// </summary>
        public ExecutableKinds Kind;

        /// <summary>
        /// A short description of the format, ex: "PE32+ x64 console", "ELF64 x86-64 PIE", "Script (/bin/bash)".
        /// </summary>
        public String Format;

        /// <summary>
        /// The last write time of the file (UTC).
        /// </summary>
        public DateTime LastModifiedUtc;

        /// <summary>
        /// The size of the file in bytes.
        /// </summary>
        public long Size;

        /// <summary>
        /// True if the file can be executed (Linux: at least one execute bit is set, Windows: always true).
        /// </summary>
        public bool CanExecute;

        /// <summary>
        /// True if the name is a known bundled tool (ex: "createdump" of .NET), it's never the main executable unless there is nothing else.
        /// </summary>
        public bool IsKnownTool;

        /// <summary>
        /// True if this looks like a .NET app host (a "[name].dll" and a "[name].runtimeconfig.json" is next to it), informational only.
        /// </summary>
        public bool IsDotNetHost;

        /// <summary>
        /// The position in the list (0 = the best guess for the main executable).
        /// </summary>
        public int Rank;
    }

    /// <summary>
    /// Finds the executables in a folder by inspecting the file headers (not by trusting the name), and guesses the main executable:
    /// <para>1. The kind: console executables, then gui executables, then scripts (see <see cref="ExecutableKinds"/>).</para>
    /// <para>2. Known bundled tools (ex: "createdump") last.</para>
    /// <para>3. The last modified first: bundled tools (copied from a package) are older than the executable that was just built.</para>
    /// <para>4. The name (ordinal), so that the result is stable.</para>
    /// No assumption is made about the language / runtime the executable is written in.
    /// </summary>
    public static class ExecutableFinder
    {
        /// <summary>
        /// Names (lower case, no extension) of tools that are known to be bundled with a runtime / framework, never the main executable unless there is nothing else.
        /// </summary>
        public static readonly IReadOnlySet<String> KnownTools = ReadOnlyData.Set(StringComparer.Ordinal,
            "createdump",
            "crashpad_handler",
            "chrome_crashpad_handler"
        );

        /// <summary>
        /// The maximum number of files that are inspected in a folder (the newest are inspected first).
        /// </summary>
        public const int MaxFiles = 5000;

        /// <summary>
        /// Find the executables in a folder (not recursive), the best guess for the main executable is first.
        /// </summary>
        /// <param name="folder">The folder</param>
        /// <param name="windows">True to find Windows executables (*.exe files that are PE images), false to find Linux executables (any file that is an ELF executable or a script)</param>
        /// <returns>The executables, best guess first, empty if none was found (or the folder doesn't exist)</returns>
        public static IReadOnlyList<ExecutableFile> Find(String folder, bool windows)
        {
            var di = new DirectoryInfo(folder);
            if (!di.Exists)
                return Array.Empty<ExecutableFile>();
            var files = (windows ? di.EnumerateFiles("*.exe") : di.EnumerateFiles())
                .OrderByDescending(x => x.LastWriteTimeUtc)
                .Take(MaxFiles)
                .ToList();
            var res = new List<ExecutableFile>();
            foreach (var f in files)
            {
                try
                {
                    if (!windows && IsSharedLibraryName(f.Name))
                        continue;
                    var kind = Inspect(f.FullName, out var format);
                    if (kind == ExecutableKinds.None)
                        continue;
                    //  A PE image on Linux (or an ELF on Windows) can't be run
                    if (windows != format.FastStartsWith("PE"))
                        continue;
                    var baseName = windows ? Path.GetFileNameWithoutExtension(f.Name) : f.Name;
                    res.Add(new ExecutableFile
                    {
                        FullName = f.FullName,
                        Name = f.Name,
                        Kind = kind,
                        Format = format,
                        LastModifiedUtc = f.LastWriteTimeUtc,
                        Size = f.Length,
                        CanExecute = windows || CanExecute(f),
                        IsKnownTool = KnownTools.Contains(baseName.FastToLower()),
                        IsDotNetHost = File.Exists(Path.Combine(di.FullName, baseName + ".dll")) && File.Exists(Path.Combine(di.FullName, baseName + ".runtimeconfig.json")),
                    });
                }
                catch
                {
                    //  Can't read: not a candidate
                }
            }
            res.Sort(Compare);
            for (int i = 0; i < res.Count; ++i)
                res[i].Rank = i;
            return res;
        }

        /// <summary>
        /// The order of <see cref="Find"/>: kind, not a known tool, last modified (newest first), name.
        /// </summary>
        public static int Compare(ExecutableFile a, ExecutableFile b)
        {
            var c = ((int)a.Kind).CompareTo((int)b.Kind);
            if (c != 0)
                return c;
            c = a.IsKnownTool.CompareTo(b.IsKnownTool);
            if (c != 0)
                return c;
            c = b.LastModifiedUtc.CompareTo(a.LastModifiedUtc);
            if (c != 0)
                return c;
            return String.CompareOrdinal(a.Name, b.Name);
        }

        /// <summary>
        /// Test if a name is a shared library name: "*.so" or "*.so.[version]".
        /// </summary>
        public static bool IsSharedLibraryName(String name)
            => name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) || name.Contains(".so.", StringComparison.OrdinalIgnoreCase);

        static bool CanExecute(FileInfo f)
        {
            if (OperatingSystem.IsWindows())
                return true;
            const UnixFileMode anyX = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (f.UnixFileMode & anyX) != 0;
        }

        /// <summary>
        /// Inspect the header of a file.
        /// </summary>
        /// <param name="file">The file</param>
        /// <param name="format">A short description of the format (see <see cref="ExecutableFile.Format"/>), null if not an executable</param>
        /// <returns>The kind of executable, <see cref="ExecutableKinds.None"/> if not an executable</returns>
        public static ExecutableKinds Inspect(String file, out String format)
        {
            format = null;
            using var h = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var len = RandomAccess.GetLength(h);
            if (len < 4)
                return ExecutableKinds.None;
            Span<Byte> magic = stackalloc Byte[4];
            if (RandomAccess.Read(h, magic, 0) != 4)
                return ExecutableKinds.None;
            if (magic[0] == 'M' && magic[1] == 'Z')
                return InspectPe(h, len, out format);
            if (magic[0] == 0x7f && magic[1] == 'E' && magic[2] == 'L' && magic[3] == 'F')
                return InspectElf(h, len, out format);
            if (magic[0] == '#' && magic[1] == '!')
            {
                Span<Byte> line = stackalloc Byte[128];
                var r = RandomAccess.Read(h, line, 2);
                var s = System.Text.Encoding.UTF8.GetString(line.Slice(0, r));
                var e = s.IndexOfAny(['\n', '\r']);
                if (e >= 0)
                    s = s.Substring(0, e);
                s = s.Trim();
                if (s.Length == 0)
                    return ExecutableKinds.None;
                format = "Script (" + s + ")";
                return ExecutableKinds.Script;
            }
            return ExecutableKinds.None;
        }

        static bool ReadAt(SafeFileHandle h, long len, long offset, Span<Byte> data)
        {
            if ((offset < 0) || ((offset + data.Length) > len))
                return false;
            return RandomAccess.Read(h, data, offset) == data.Length;
        }

        /// <summary>
        /// PE: "MZ", e_lfanew at 0x3c, "PE\0\0", COFF header (machine, characteristics), optional header (magic, subsystem).
        /// </summary>
        static ExecutableKinds InspectPe(SafeFileHandle h, long len, out String format)
        {
            format = null;
            Span<Byte> b = stackalloc Byte[4];
            if (!ReadAt(h, len, 0x3c, b))
                return ExecutableKinds.None;
            long pe = BinaryPrimitives.ReadUInt32LittleEndian(b);
            //  Signature (4) + COFF header (20) + optional header up to the subsystem (70)
            Span<Byte> hdr = stackalloc Byte[24 + 70];
            if (!ReadAt(h, len, pe, hdr))
                return ExecutableKinds.None;
            if (hdr[0] != 'P' || hdr[1] != 'E' || hdr[2] != 0 || hdr[3] != 0)
                return ExecutableKinds.None;
            var machine = BinaryPrimitives.ReadUInt16LittleEndian(hdr.Slice(4));
            var characteristics = BinaryPrimitives.ReadUInt16LittleEndian(hdr.Slice(22));
            const ushort executableImage = 0x0002;
            const ushort dll = 0x2000;
            if ((characteristics & executableImage) == 0 || (characteristics & dll) != 0)
                return ExecutableKinds.None;
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(hdr.Slice(24));
            String bits;
            switch (magic)
            {
                case 0x10b:
                    bits = "PE32";
                    break;
                case 0x20b:
                    bits = "PE32+";
                    break;
                default:
                    return ExecutableKinds.None;
            }
            //  The subsystem is at offset 68 of the optional header (both PE32 and PE32+)
            var subsystem = BinaryPrimitives.ReadUInt16LittleEndian(hdr.Slice(24 + 68));
            var arch = machine switch
            {
                0x14c => "x86",
                0x8664 => "x64",
                0xaa64 => "arm64",
                0x1c4 => "arm",
                _ => "0x" + machine.ToString("x4"),
            };
            switch (subsystem)
            {
                case 3: // IMAGE_SUBSYSTEM_WINDOWS_CUI
                    format = String.Concat(bits, " ", arch, " console");
                    return ExecutableKinds.Console;
                case 2: // IMAGE_SUBSYSTEM_WINDOWS_GUI
                    format = String.Concat(bits, " ", arch, " gui");
                    return ExecutableKinds.Gui;
            }
            //  Drivers, EFI images etc
            return ExecutableKinds.None;
        }

        /// <summary>
        /// ELF: class, data (endian), type (EXEC or DYN), machine; a DYN is an executable (PIE) if it has an interpreter (PT_INTERP),
        /// or if it has no interpreter and isn't named as a shared library (a static PIE).
        /// </summary>
        static ExecutableKinds InspectElf(SafeFileHandle h, long len, out String format)
        {
            format = null;
            Span<Byte> e = stackalloc Byte[64];
            if (!ReadAt(h, len, 0, e.Slice(0, 52)))
                return ExecutableKinds.None;
            var is64 = e[4] == 2;
            if (!is64 && e[4] != 1)
                return ExecutableKinds.None;
            if (is64 && !ReadAt(h, len, 0, e))
                return ExecutableKinds.None;
            var le = e[5] == 1;
            if (!le && e[5] != 2)
                return ExecutableKinds.None;
            ushort U16(ReadOnlySpan<Byte> s) => le ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);
            uint U32(ReadOnlySpan<Byte> s) => le ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
            ulong U64(ReadOnlySpan<Byte> s) => le ? BinaryPrimitives.ReadUInt64LittleEndian(s) : BinaryPrimitives.ReadUInt64BigEndian(s);
            var type = U16(e.Slice(16));
            var machine = U16(e.Slice(18));
            const ushort etExec = 2;
            const ushort etDyn = 3;
            if (type != etExec && type != etDyn)
                return ExecutableKinds.None;
            var arch = machine switch
            {
                0x3e => "x86-64",
                0x03 => "x86",
                0xb7 => "aarch64",
                0x28 => "arm",
                0xf3 => "riscv",
                _ => "0x" + machine.ToString("x"),
            };
            var bits = is64 ? "ELF64 " : "ELF32 ";
            if (type == etExec)
            {
                format = bits + arch;
                return ExecutableKinds.Console;
            }
            //  ET_DYN: look for PT_INTERP
            long phoff = is64 ? (long)U64(e.Slice(32)) : U32(e.Slice(28));
            int phentsize = U16(e.Slice(is64 ? 54 : 42));
            int phnum = U16(e.Slice(is64 ? 56 : 44));
            if (phentsize < 4 || phnum <= 0 || phnum > 512)
                return ExecutableKinds.None;
            Span<Byte> p = stackalloc Byte[4];
            for (int i = 0; i < phnum; ++i)
            {
                if (!ReadAt(h, len, phoff + (long)i * phentsize, p))
                    return ExecutableKinds.None;
                const uint ptInterp = 3;
                if (U32(p) == ptInterp)
                {
                    format = bits + arch + " PIE";
                    return ExecutableKinds.Console;
                }
            }
            //  A shared library or a static PIE (the caller filters out *.so names)
            format = bits + arch + " static PIE";
            return ExecutableKinds.Console;
        }
    }
}
