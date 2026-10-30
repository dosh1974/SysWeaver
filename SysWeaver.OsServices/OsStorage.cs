using System;
using System.Collections.Generic;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// A mounted volume (a drive letter on Windows, a mount point on Linux).
    /// </summary>
    public sealed class OsVolume
    {
        /// <summary>
        /// The mount point (ex: "C:\" or "/home"), used as the id of the volume
        /// </summary>
        public String MountPoint;

        /// <summary>
        /// The label (volume name)
        /// </summary>
        public String Label;

        /// <summary>
        /// The device (ex: "\\?\Volume{..}\" or "/dev/sda1")
        /// </summary>
        public String Device;

        /// <summary>
        /// The file system, ex: "NTFS" or "ext4"
        /// </summary>
        public String FileSystem;

        /// <summary>
        /// The type, ex: "Fixed", "Removable", "Network", "CDRom"
        /// </summary>
        public String Type;

        /// <summary>
        /// True if the volume is ready (ex: false for an empty card reader)
        /// </summary>
        public bool Ready;

        /// <summary>
        /// The total size in bytes
        /// </summary>
        public long Size;

        /// <summary>
        /// The free space in bytes (available to the current user)
        /// </summary>
        public long Free;

        /// <summary>
        /// The encryption, ex: "BitLocker: on", "BitLocker: off", "LUKS" or empty if not encrypted (or unknown)
        /// </summary>
        public String Encryption;

        /// <summary>
        /// The health, ex: "Healthy" (empty if unknown)
        /// </summary>
        public String Health;

        /// <summary>
        /// The id of the physical disk (see <see cref="OsDisk.Id"/>) that the volume is on, empty if unknown (or more than one)
        /// </summary>
        public String DiskId;

        /// <summary>
        /// More details (platform specific)
        /// </summary>
        public KeyValuePair<String, String>[] Properties;
    }


    /// <summary>
    /// A partition of a disk.
    /// </summary>
    public sealed class OsPartition
    {
        /// <summary>
        /// The partition number (or name on Linux, ex: "sda1")
        /// </summary>
        public String Name;

        /// <summary>
        /// The size in bytes
        /// </summary>
        public long Size;

        /// <summary>
        /// The offset in bytes (0 if unknown)
        /// </summary>
        public long Offset;

        /// <summary>
        /// The type, ex: "Basic", "System", "Reserved", "Recovery" or "Linux filesystem"
        /// </summary>
        public String Type;

        /// <summary>
        /// The file system (if known)
        /// </summary>
        public String FileSystem;

        /// <summary>
        /// The mount points (drive letters, folders), empty if not mounted
        /// </summary>
        public String[] MountPoints;
    }


    /// <summary>
    /// A physical disk.
    /// </summary>
    public sealed class OsDisk
    {
        /// <summary>
        /// The id, ex: "0" (the disk number on Windows) or "sda" (the kernel name on Linux)
        /// </summary>
        public String Id;

        /// <summary>
        /// The model (friendly name)
        /// </summary>
        public String Model;

        /// <summary>
        /// The serial number
        /// </summary>
        public String Serial;

        /// <summary>
        /// The firmware version
        /// </summary>
        public String Firmware;

        /// <summary>
        /// The size in bytes
        /// </summary>
        public long Size;

        /// <summary>
        /// The bus, ex: "NVMe", "SATA", "USB"
        /// </summary>
        public String Bus;

        /// <summary>
        /// The media type, ex: "SSD", "HDD" (empty if unknown)
        /// </summary>
        public String MediaType;

        /// <summary>
        /// The partition style, ex: "GPT" or "MBR"
        /// </summary>
        public String PartitionStyle;

        /// <summary>
        /// The health, ex: "Healthy", "Warning", "Unhealthy" (empty if unknown)
        /// </summary>
        public String Health;

        /// <summary>
        /// The status, ex: "Online", "Offline"
        /// </summary>
        public String Status;

        /// <summary>
        /// True if the disk is removable
        /// </summary>
        public bool Removable;

        /// <summary>
        /// The partitions
        /// </summary>
        public OsPartition[] Partitions;

        /// <summary>
        /// More details (platform specific)
        /// </summary>
        public KeyValuePair<String, String>[] Properties;
    }


    /// <summary>
    /// Lists the disks and volumes of the system.
    /// Get an instance using <see cref="OsSystem.Storage"/> (created by <see cref="IServiceHostFactory.CreateStorage"/>).
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        /// The display name of the implementation, ex: "Windows Storage Management" or "lsblk"
        /// </summary>
        String Name { get; }

        /// <summary>
        /// Get all mounted volumes.
        /// </summary>
        /// <returns>The volumes</returns>
        IReadOnlyList<OsVolume> GetVolumes();

        /// <summary>
        /// Get all physical disks (with their partitions).
        /// </summary>
        /// <returns>The disks</returns>
        IReadOnlyList<OsDisk> GetDisks();
    }
}
