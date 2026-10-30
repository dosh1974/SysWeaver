using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SysWeaver.OsServices
{
    /// <summary>
    /// <see cref="IStorage"/> for Linux: the disks and partitions from "lsblk -J -b", the volumes are the mounted file systems (<see cref="DriveInfo"/>, pseudo file systems skipped).
    /// </summary>
    sealed class UnixStorage : IStorage
    {
        public UnixStorage(String lsblk)
        {
            Lsblk = lsblk;
        }

        readonly String Lsblk;

        public String Name => "lsblk";

        /// <summary>
        /// A node from lsblk (a disk, partition, crypt, lvm etc)
        /// </summary>
        sealed class Node
        {
            public readonly Dictionary<String, String> Values = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            public readonly List<Node> Children = new List<Node>();
            public Node Parent;

            public String this[String key] => Values.TryGetValue(key, out var v) ? v : "";

            public long Long(String key) => long.TryParse(this[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

            public bool Bool(String key) => this[key] is "1" or "true" or "True";

            public IEnumerable<Node> All()
            {
                yield return this;
                foreach (var c in Children)
                    foreach (var x in c.All())
                        yield return x;
            }

            public String Device => this["path"].Length > 0 ? this["path"] : ("/dev/" + (this["kname"].Length > 0 ? this["kname"] : this["name"]));
        }

        static readonly String[] FullColumns = ["NAME", "KNAME", "PATH", "TYPE", "SIZE", "MODEL", "SERIAL", "REV", "TRAN", "ROTA", "RM", "RO", "FSTYPE", "LABEL", "UUID", "MOUNTPOINT", "PTTYPE", "PARTTYPENAME", "STATE", "START", "VENDOR"];
        static readonly String[] BasicColumns = ["NAME", "KNAME", "TYPE", "SIZE", "MODEL", "SERIAL", "TRAN", "ROTA", "RM", "RO", "FSTYPE", "LABEL", "UUID", "MOUNTPOINT"];

        static Node Parse(JsonElement e, Node parent)
        {
            var n = new Node { Parent = parent };
            foreach (var p in e.EnumerateObject())
            {
                if (p.NameEquals("children"))
                {
                    if (p.Value.ValueKind == JsonValueKind.Array)
                        foreach (var c in p.Value.EnumerateArray())
                            n.Children.Add(Parse(c, n));
                    continue;
                }
                var v = p.Value;
                n.Values[p.Name] = v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString()?.Trim() ?? "",
                    JsonValueKind.Number => v.GetRawText(),
                    JsonValueKind.True => "1",
                    JsonValueKind.False => "0",
                    _ => "",
                };
            }
            return n;
        }

        /// <summary>
        /// Run lsblk (with fewer columns on old versions that doesn't know all columns)
        /// </summary>
        List<Node> Read()
        {
            var r = UnixCommand.Run(out var o, out _, Lsblk, ["-J", "-b", "-o", String.Join(',', FullColumns)]);
            if (r != 0)
                o = UnixCommand.Get(Lsblk, "-J", "-b", "-o", String.Join(',', BasicColumns));
            using var doc = JsonDocument.Parse(o);
            var res = new List<Node>();
            if (doc.RootElement.TryGetProperty("blockdevices", out var arr) && (arr.ValueKind == JsonValueKind.Array))
                foreach (var e in arr.EnumerateArray())
                    res.Add(Parse(e, null));
            return res;
        }

        static KeyValuePair<String, String> P(String k, String v) => new KeyValuePair<String, String>(k, v);

        static String Root(Node n)
        {
            while (n.Parent != null)
                n = n.Parent;
            return n["name"];
        }

        /// <summary>
        /// File systems that aren't volumes
        /// </summary>
        static readonly HashSet<String> SkipFs = new(StringComparer.Ordinal)
        {
            "squashfs", "overlay", "tmpfs", "devtmpfs", "proc", "sysfs", "cgroup", "cgroup2", "devpts", "mqueue", "debugfs", "tracefs", "securityfs",
            "pstore", "bpf", "configfs", "fusectl", "hugetlbfs", "autofs", "binfmt_misc", "efivarfs", "rpc_pipefs", "nsfs", "ramfs", "fuse.portal", "fuse.gvfsd-fuse",
        };

        public IReadOnlyList<OsVolume> GetVolumes()
        {
            List<Node> nodes;
            try
            {
                nodes = Read();
            }
            catch
            {
                nodes = new List<Node>();
            }
            var byMount = new Dictionary<String, Node>(StringComparer.Ordinal);
            foreach (var n in nodes.SelectMany(x => x.All()))
            {
                var m = n["mountpoint"];
                if (m.Length > 0)
                    byMount[m] = n;
            }
            var res = new List<OsVolume>();
            foreach (var d in DriveInfo.GetDrives())
            {
                String fs = "";
                bool ready = false;
                try
                {
                    ready = d.IsReady;
                    fs = d.DriveFormat ?? "";
                }
                catch
                {
                }
                var type = d.DriveType;
                if (SkipFs.Contains(fs) || (type == DriveType.Ram) || (type == DriveType.Unknown))
                    continue;
                var root = d.RootDirectory.FullName;
                if (root.StartsWith("/proc", StringComparison.Ordinal) || root.StartsWith("/sys", StringComparison.Ordinal) || root.StartsWith("/run", StringComparison.Ordinal) || (root == "/dev") || root.StartsWith("/dev/", StringComparison.Ordinal) || root.StartsWith("/snap/", StringComparison.Ordinal))
                    continue;
                var x = new OsVolume
                {
                    MountPoint = root,
                    Label = "",
                    Device = "",
                    FileSystem = fs,
                    Type = type.ToString(),
                    Ready = ready,
                    Encryption = "",
                    Health = "",
                    DiskId = "",
                };
                if (ready)
                {
                    try
                    {
                        x.Size = d.TotalSize;
                        x.Free = d.AvailableFreeSpace;
                    }
                    catch
                    {
                    }
                }
                var props = new List<KeyValuePair<String, String>>();
                if (byMount.TryGetValue(root.Length > 1 ? root.TrimEnd('/') : root, out var n))
                {
                    x.Device = n.Device;
                    x.Label = n["label"];
                    x.DiskId = Root(n);
                    for (var p = n; p != null; p = p.Parent)
                    {
                        var t = p["type"];
                        if (t == "crypt")
                            x.Encryption = "LUKS";
                        else if (t == "lvm")
                            props.Add(P("LVM volume", p["name"]));
                        else if (t.StartsWith("raid", StringComparison.Ordinal))
                            props.Add(P("RAID", p["name"] + " (" + t + ")"));
                    }
                    var uuid = n["uuid"];
                    if (uuid.Length > 0)
                        props.Add(P("UUID", uuid));
                    if (n.Bool("ro"))
                        props.Add(P("Read only", "Yes"));
                }
                x.Properties = props.ToArray();
                res.Add(x);
            }
            return res;
        }

        public IReadOnlyList<OsDisk> GetDisks()
        {
            var res = new List<OsDisk>();
            foreach (var d in Read())
            {
                if (d["type"] != "disk")
                    continue;
                var name = d["name"];
                var tran = d["tran"];
                var model = d["model"];
                var vendor = d["vendor"];
                if ((vendor.Length > 0) && !model.StartsWith(vendor, StringComparison.OrdinalIgnoreCase))
                    model = (vendor + " " + model).Trim();
                var x = new OsDisk
                {
                    Id = name,
                    Model = model,
                    Serial = d["serial"],
                    Firmware = d["rev"],
                    Size = d.Long("size"),
                    Bus = tran.Length > 0 ? tran.ToUpperInvariant() : (name.StartsWith("nvme", StringComparison.Ordinal) ? "NVME" : (name.StartsWith("mmcblk", StringComparison.Ordinal) ? "MMC" : (name.StartsWith("vd", StringComparison.Ordinal) ? "Virtual" : ""))),
                    MediaType = d.Values.ContainsKey("rota") ? (d.Bool("rota") ? "HDD" : "SSD") : "",
                    PartitionStyle = d["pttype"] switch
                    {
                        "gpt" => "GPT",
                        "dos" => "MBR",
                        "" => d.Children.Count > 0 ? "" : "RAW",
                        var s => s,
                    },
                    Health = "",
                    Status = d["state"].Length > 0 ? d["state"] : "",
                    Removable = d.Bool("rm"),
                };
                if (x.Bus == "NVME")
                    x.Bus = "NVMe";
                var props = new List<KeyValuePair<String, String>>
                {
                    P("Device", d.Device),
                };
                if (d.Bool("ro"))
                    props.Add(P("Read only", "Yes"));
                var fsd = d["fstype"];
                if (fsd.Length > 0)
                    props.Add(P("File system (whole disk)", fsd));
                x.Properties = props.ToArray();
                var parts = new List<OsPartition>();
                foreach (var p in d.Children)
                {
                    var mounts = p.All().Select(c => c["mountpoint"]).Where(m => m.Length > 0).Distinct().ToArray();
                    var fs = p["fstype"];
                    var inner = p.Children.Select(c => c["fstype"]).FirstOrDefault(f => f.Length > 0);
                    if ((fs == "crypto_LUKS") && !String.IsNullOrEmpty(inner))
                        fs = fs + " (" + inner + ")";
                    parts.Add(new OsPartition
                    {
                        Name = p["name"],
                        Size = p.Long("size"),
                        Offset = p.Long("start") * 512,
                        Type = p["parttypename"].Length > 0 ? p["parttypename"] : p["type"],
                        FileSystem = fs,
                        MountPoints = mounts,
                    });
                }
                x.Partitions = parts.ToArray();
                res.Add(x);
            }
            return res;
        }
    }
}
