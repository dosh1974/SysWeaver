using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IStorage"/> for Windows, using the Storage Management WMI classes (root\Microsoft\Windows\Storage) and BitLocker (Win32_EncryptableVolume, requires admin).
    /// </summary>
    sealed class Win32Storage : IStorage
    {
        public String Name => "Windows Storage Management";

        const String StorageScope = @"root\Microsoft\Windows\Storage";
        const String BitLockerScope = @"root\CIMV2\Security\MicrosoftVolumeEncryption";

        static String Health(long h) => h switch
        {
            0 => "Healthy",
            1 => "Warning",
            2 => "Unhealthy",
            _ => "",
        };

        static String Bus(long b) => b switch
        {
            1 => "SCSI",
            2 => "ATAPI",
            3 => "ATA",
            4 => "1394",
            5 => "SSA",
            6 => "Fibre Channel",
            7 => "USB",
            8 => "RAID",
            9 => "iSCSI",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            14 => "Virtual",
            15 => "File Backed Virtual",
            16 => "Storage Spaces",
            17 => "NVMe",
            _ => "",
        };

        static String Media(long m) => m switch
        {
            3 => "HDD",
            4 => "SSD",
            5 => "SCM",
            _ => "",
        };

        static readonly Dictionary<String, String> GptTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}"] = "System (EFI)",
            ["{e3c9e316-0b5c-4db8-817d-f92df00215ae}"] = "Reserved (MSR)",
            ["{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}"] = "Basic",
            ["{de94bba4-06d1-4d40-a16a-bfd50179d6ac}"] = "Recovery",
            ["{5808c8aa-7e8f-42e0-85d2-e1e90434cfb3}"] = "LDM metadata",
            ["{af9b60a0-1431-4f62-bc68-3311714a69ad}"] = "LDM data",
            ["{0fc63daf-8483-4772-8e79-3d69d8477de4}"] = "Linux filesystem",
            ["{0657fd6d-a4ab-43c4-84e5-0933c84b4f4f}"] = "Linux swap",
        };

        static String MbrType(long t) => t switch
        {
            0x07 => "IFS (NTFS / exFAT)",
            0x06 => "FAT16",
            0x0B or 0x0C => "FAT32",
            0x27 => "Recovery",
            0x83 => "Linux",
            0x82 => "Linux swap",
            0xEE => "GPT protective",
            0 => "",
            _ => "0x" + t.ToString("X2"),
        };

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        /// <summary>
        /// Get the BitLocker state by drive letter (ex: "C:"), empty if not available (requires admin).
        /// </summary>
        static Dictionary<String, String> BitLocker()
        {
            var res = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var objs = Win32Wmi.TryQuery(BitLockerScope, "SELECT DriveLetter, ProtectionStatus, ConversionStatus FROM Win32_EncryptableVolume");
            try
            {
                foreach (var o in objs)
                {
                    var letter = Win32Wmi.Str(o, "DriveLetter");
                    if (letter.Length <= 0)
                        continue;
                    var prot = Win32Wmi.Long(o, "ProtectionStatus");
                    var conv = Win32Wmi.Get(o, "ConversionStatus") == null ? -1 : Win32Wmi.Long(o, "ConversionStatus");
                    if ((prot == 0) && (conv <= 0))
                        continue;
                    var c = conv switch
                    {
                        0 => "decrypted",
                        1 => "fully encrypted",
                        2 => "encrypting",
                        3 => "decrypting",
                        4 => "encryption paused",
                        5 => "decryption paused",
                        _ => "",
                    };
                    var p = prot switch
                    {
                        1 => "on",
                        0 => "suspended",
                        _ => "unknown",
                    };
                    res[letter] = "BitLocker: " + p + (c.Length > 0 ? (" (" + c + ")") : "");
                }
            }
            finally
            {
                Win32Wmi.Dispose(objs);
            }
            return res;
        }

        sealed class PartInfo
        {
            public String Disk;
            public String[] Paths;
        }

        public IReadOnlyList<OsVolume> GetVolumes()
        {
            var vols = Win32Wmi.TryQuery(StorageScope, "SELECT * FROM MSFT_Volume");
            var parts = Win32Wmi.TryQuery(StorageScope, "SELECT DiskNumber, AccessPaths FROM MSFT_Partition");
            try
            {
                var byLetter = new Dictionary<String, ManagementObject>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in vols)
                {
                    var l = Win32Wmi.Str(v, "DriveLetter");
                    if (l.Length == 1)
                        byLetter[l + ":"] = v;
                }
                var partInfo = parts.Select(p => new PartInfo { Disk = Win32Wmi.Long(p, "DiskNumber").ToString(), Paths = Win32Wmi.Strs(p, "AccessPaths") }).ToList();
                var bl = BitLocker();
                var res = new List<OsVolume>();
                foreach (var d in DriveInfo.GetDrives())
                {
                    var root = d.RootDirectory.FullName;
                    var letter = root.TrimEnd('\\');
                    bool ready = false;
                    try
                    {
                        ready = d.IsReady;
                    }
                    catch
                    {
                    }
                    var x = new OsVolume
                    {
                        MountPoint = root,
                        Label = "",
                        Device = "",
                        FileSystem = "",
                        Type = d.DriveType.ToString(),
                        Ready = ready,
                        Encryption = bl.TryGetValue(letter, out var enc) ? enc : "",
                        Health = "",
                        DiskId = "",
                        Properties = [],
                    };
                    if (ready)
                    {
                        try
                        {
                            x.Label = d.VolumeLabel ?? "";
                            x.FileSystem = d.DriveFormat ?? "";
                            x.Size = d.TotalSize;
                            x.Free = d.AvailableFreeSpace;
                        }
                        catch
                        {
                        }
                    }
                    var props = new List<KeyValuePair<String, String>>();
                    if (byLetter.TryGetValue(letter, out var v))
                    {
                        var path = Win32Wmi.Str(v, "Path");
                        x.Device = path;
                        x.Health = Health(Win32Wmi.Long(v, "HealthStatus"));
                        if (x.FileSystem.Length <= 0)
                            x.FileSystem = Win32Wmi.Str(v, "FileSystem");
                        if (x.Label.Length <= 0)
                            x.Label = Win32Wmi.Str(v, "FileSystemLabel");
                        var au = Win32Wmi.Long(v, "AllocationUnitSize");
                        if (au > 0)
                            props.Add(P("Allocation unit size", au + " bytes"));
                        var free = Win32Wmi.Long(v, "SizeRemaining");
                        if (free > 0)
                            props.Add(P("Free space (all users)", free.ToString("#,0") + " bytes"));
                        var uid = Win32Wmi.Str(v, "UniqueId");
                        if (uid.Length > 0)
                            props.Add(P("Unique id", uid));
                        var disks = partInfo.Where(p => p.Paths.Any(a => String.Equals(a, path, StringComparison.OrdinalIgnoreCase))).Select(p => p.Disk).Distinct().ToList();
                        if (disks.Count == 1)
                            x.DiskId = disks[0];
                        else if (disks.Count > 1)
                            props.Add(P("Disks", String.Join(", ", disks)));
                    }
                    x.Properties = props.ToArray();
                    res.Add(x);
                }
                return res;
            }
            finally
            {
                Win32Wmi.Dispose(vols);
                Win32Wmi.Dispose(parts);
            }
        }

        public IReadOnlyList<OsDisk> GetDisks()
        {
            var disks = Win32Wmi.Query(StorageScope, "SELECT * FROM MSFT_Disk");
            var phys = Win32Wmi.TryQuery(StorageScope, "SELECT * FROM MSFT_PhysicalDisk");
            var parts = Win32Wmi.TryQuery(StorageScope, "SELECT * FROM MSFT_Partition");
            var vols = Win32Wmi.TryQuery(StorageScope, "SELECT Path, FileSystem, DriveLetter FROM MSFT_Volume");
            try
            {
                var volFs = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in vols)
                    volFs[Win32Wmi.Str(v, "Path")] = Win32Wmi.Str(v, "FileSystem");
                var res = new List<OsDisk>();
                foreach (var d in disks)
                {
                    var num = Win32Wmi.Long(d, "Number").ToString();
                    var p = phys.FirstOrDefault(x => Win32Wmi.Str(x, "DeviceId") == num);
                    var style = Win32Wmi.Long(d, "PartitionStyle") switch
                    {
                        1 => "MBR",
                        2 => "GPT",
                        _ => "RAW",
                    };
                    var offline = Win32Wmi.Bool(d, "IsOffline");
                    var bus = Bus(Win32Wmi.Long(d, "BusType"));
                    var x = new OsDisk
                    {
                        Id = num,
                        Model = p != null ? Win32Wmi.Str(p, "FriendlyName") : Win32Wmi.Str(d, "FriendlyName"),
                        Serial = Win32Wmi.Str(d, "SerialNumber"),
                        Firmware = Win32Wmi.Str(d, "FirmwareVersion"),
                        Size = Win32Wmi.Long(d, "Size"),
                        Bus = bus,
                        MediaType = p != null ? Media(Win32Wmi.Long(p, "MediaType")) : "",
                        PartitionStyle = style,
                        Health = Health(Win32Wmi.Long(d, "HealthStatus")),
                        Status = offline ? "Offline" : "Online",
                        Removable = bus is "USB" or "SD" or "MMC",
                    };
                    if (x.Model.Length <= 0)
                        x.Model = Win32Wmi.Str(d, "Model");
                    if ((x.Serial.Length <= 0) && (p != null))
                        x.Serial = Win32Wmi.Str(p, "SerialNumber");
                    var props = new List<KeyValuePair<String, String>>();
                    if (Win32Wmi.Bool(d, "IsBoot"))
                        props.Add(P("Boot disk", "Yes"));
                    if (Win32Wmi.Bool(d, "IsSystem"))
                        props.Add(P("System disk", "Yes"));
                    if (Win32Wmi.Bool(d, "IsReadOnly"))
                        props.Add(P("Read only", "Yes"));
                    var loc = Win32Wmi.Str(d, "Location");
                    if (loc.Length > 0)
                        props.Add(P("Location", loc));
                    if (p != null)
                    {
                        var rpm = Win32Wmi.Long(p, "SpindleSpeed");
                        if ((rpm > 0) && (rpm < 100000))
                            props.Add(P("Spindle speed", rpm + " rpm"));
                        var usage = Win32Wmi.Long(p, "Usage") switch
                        {
                            1 => "Auto-Select",
                            2 => "Manual-Select",
                            3 => "Hot Spare",
                            4 => "Retired",
                            5 => "Journal",
                            _ => "",
                        };
                        if (usage.Length > 0)
                            props.Add(P("Usage", usage));
                        var lss = Win32Wmi.Long(p, "LogicalSectorSize");
                        var pss = Win32Wmi.Long(p, "PhysicalSectorSize");
                        if (lss > 0)
                            props.Add(P("Sector size", lss + " bytes logical, " + pss + " bytes physical"));
                    }
                    var uid = Win32Wmi.Str(d, "UniqueId");
                    if (uid.Length > 0)
                        props.Add(P("Unique id", uid));
                    x.Properties = props.ToArray();
                    var pl = new List<OsPartition>();
                    foreach (var pa in parts.Where(q => Win32Wmi.Long(q, "DiskNumber").ToString() == num).OrderBy(q => Win32Wmi.Long(q, "Offset")))
                    {
                        var gpt = Win32Wmi.Str(pa, "GptType");
                        var type = gpt.Length > 0 ? (GptTypes.TryGetValue(gpt, out var t) ? t : gpt) : MbrType(Win32Wmi.Long(pa, "MbrType"));
                        var paths = Win32Wmi.Strs(pa, "AccessPaths");
                        String fs = "";
                        foreach (var a in paths)
                            if (volFs.TryGetValue(a, out var f) && (f.Length > 0))
                                fs = f;
                        pl.Add(new OsPartition
                        {
                            Name = Win32Wmi.Long(pa, "PartitionNumber").ToString(),
                            Size = Win32Wmi.Long(pa, "Size"),
                            Offset = Win32Wmi.Long(pa, "Offset"),
                            Type = type,
                            FileSystem = fs,
                            MountPoints = paths.Where(a => !a.StartsWith(@"\\?\", StringComparison.Ordinal)).ToArray(),
                        });
                    }
                    x.Partitions = pl.ToArray();
                    res.Add(x);
                }
                res.Sort((a, b) => String.CompareOrdinal(a.Id.PadLeft(4), b.Id.PadLeft(4)));
                return res;
            }
            finally
            {
                Win32Wmi.Dispose(disks);
                Win32Wmi.Dispose(phys);
                Win32Wmi.Dispose(parts);
                Win32Wmi.Dispose(vols);
            }
        }
    }
}
