using System;
using System.Diagnostics;
using System.Threading;

namespace SysWeaver.MicroService
{



    /// <summary>
    /// Service that wait for the computer to get a LAN ip.
    /// </summary>
    [IsMicroService]
    public sealed class CpuService : IDisposable
    {
        const String Prefix = "[CPU] ";

        public CpuService(ServiceManager manager, CpuServiceParams p = null)
        {
            p = p ?? new CpuServiceParams();
            Manager = manager;
            var process = Process.GetCurrentProcess();
            var coreCount = Environment.ProcessorCount;
            manager.AddMessage(Prefix + "The system have " + coreCount + " logical cores");
            var coreMask = (1UL << coreCount) - 1;
            if (coreMask == 0)
                coreMask = ~0UL;
            ulong affinity = coreMask;
            if (!String.IsNullOrEmpty(p.AffinityMask))
            {
                try
                {
                    affinity = Convert.ToUInt64(p.AffinityMask, 2);
                }
                catch
                {
                    throw new Exception(String.Concat("The string ", p.AffinityMask.ToQuoted(), " is not a valid binary number, only '0' and '1' may be present, and at most 64 is possible!"));
                }
            }
            else
            {
                var sliceCount = p.SliceCount;
                if (sliceCount > 1)
                {
                    var sliceIndex = p.SliceIndex % sliceCount;
                    manager.AddMessage(Prefix + "Core slicing is used, slice affinity masks:", MessageLevels.Debug);
                    for (int i = 0; i < sliceCount; ++ i)
                        manager.AddMessage(Prefix + (i == sliceIndex ? "==> " : "    ") + i.ToString().PadLeft(2) + ": " + GetSliceAffinity(i, sliceCount, coreCount).AsBinary(coreCount), MessageLevels.Debug);
                    affinity = GetSliceAffinity(sliceIndex, sliceCount, coreCount);
                }
            }
            affinity &= coreMask;
            if (affinity != coreMask)
            {
                manager.AddMessage(Prefix + "Setting process affinity mask to " + affinity.AsBinary(coreCount).ToQuoted());
                try
                {
#pragma warning disable CA1416
                    var old = process.ProcessorAffinity.ToInt64();
                    process.ProcessorAffinity = new IntPtr((long)affinity);
#pragma warning restore CA1416
                    PrevAffinity = old;
                }
                catch (Exception ex)
                {
                    manager.AddMessage(Prefix + "Failed to set process affinity mask to " + affinity.AsBinary(coreCount).ToQuoted() + ", ignoring!", ex, MessageLevels.Warning);
                }
            }
            if (!String.IsNullOrEmpty(p.PriorityClass))
            {
                if (!Enum.TryParse<ProcessPriorityClass>(p.PriorityClass, out var prio))
                    throw new Exception(String.Concat(p.PriorityClass.ToQuoted(), " is not a valid priority class! Valid values are: Normal, Idle, High, RealTime, BelowNormal, AboveNormal"));
                var oldPrio = process.PriorityClass;
                if (oldPrio != prio)
                {
                    manager.AddMessage(Prefix + "Setting process priority class to " + prio.ToString().ToQuoted());
                    try
                    {
                        process.PriorityClass = prio;
                        PrevPritority = oldPrio;
                    }
                    catch (Exception ex)
                    {
                        manager.AddMessage(Prefix + "Failed to set process priority class to " + prio.ToString().ToQuoted() + ", ignoring!", ex, MessageLevels.Warning);
                    }
                }
            }
            var currentBoost = process.PriorityBoostEnabled;
            var newBoost = p.PriorityBoost;
            PrevBoost = currentBoost;
            if (currentBoost != newBoost)
            {
                manager.AddMessage(Prefix + (newBoost ?  "Enabling priority boost for the current process" : "Disabling priority boost for the current process"));
                try
                {
                    process.PriorityBoostEnabled = p.PriorityBoost;
                    ChangedBoost = 1;
                }
                catch (Exception ex)
                {
                    manager.AddMessage(Prefix + (newBoost ? "Failed to enable priority boost for the current process, ignoring!" : "Failed to disable priority boost for the current process, ignoring!"), ex, MessageLevels.Warning);
                }
            }
        }

        public void Dispose()
        {
            var manager = Manager;
            var process = Process.GetCurrentProcess();
            if (Interlocked.Exchange(ref ChangedBoost, 0) != 0)
            {
                var val = PrevBoost;
                manager.AddMessage(Prefix + (val ? "Restoring priority boost for the current process to enabled" : "Restoring priority boost for the current process to disabled"));
                try
                {
                    process.PriorityBoostEnabled = val;
                }
                catch
                {
                }
            }
            var old = PrevPritority;
            PrevPritority = null;
            if (old != null)
            {
                manager.AddMessage(Prefix + "Restoring process priorty class to " + old.ToString().ToQuoted());
                try
                {
                    process.PriorityClass = old.Value;
                }
                catch
                {
                }
            }
            var t = Interlocked.Exchange(ref PrevAffinity, 0);
            if (t != 0)
            {
                manager.AddMessage(Prefix + "Restoring process affinity mask to " + t.AsBinary(Environment.ProcessorCount).ToQuoted());
                try
                {
#pragma warning disable CA1416
                    process.ProcessorAffinity = new IntPtr(t);
#pragma warning restore CA1416
                }
                catch
                {
                }
            }
        }

        readonly ServiceManager Manager;
       
        int ChangedBoost;
        bool PrevBoost;
        long PrevAffinity;
        ProcessPriorityClass? PrevPritority;

        static ulong GetSliceAffinity(int sliceIndex, int sliceCount, int coreCount)
        {
            sliceCount = Math.Max(1, sliceCount);
            coreCount = Math.Max(1, coreCount);
            sliceIndex = Math.Max(0, Math.Min(sliceCount - 1, sliceIndex));
            int uniquePerSlice = coreCount / sliceCount;
            if (uniquePerSlice <= 0)
                return 1UL << ((sliceIndex * coreCount) / sliceCount);
            sliceIndex = sliceIndex % sliceCount;
            int sliceStart = sliceIndex * uniquePerSlice;
            var affinity = 0UL;
            for (int i = 0; i < uniquePerSlice; ++i)
                affinity |= (1UL << (i + sliceStart));
            int reminderStart = (uniquePerSlice * sliceCount);
            int reminder = coreCount - reminderStart;
            if (reminder > 0)
            {
                int reminderIndex = (sliceIndex % reminder) + reminderStart;
                affinity |= (1UL << reminderIndex);
            }
            return affinity;
        }


    }


}
