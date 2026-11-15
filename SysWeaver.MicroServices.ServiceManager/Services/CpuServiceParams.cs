using System;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Controls the cpu usage
    /// </summary>
    public sealed class CpuServiceParams
    {
        /// <inheritdoc />
        public override string ToString() => AffinityMask == null ?
            String.Concat("Priority class: ", PriorityClass, ", boost: ", PriorityBoost, ", core slice: ", SliceIndex, '/', SliceCount)
              :
            String.Concat("Priority class: ", PriorityClass, ", boost: ", PriorityBoost, ", affinity mask: ", AffinityMask.ToQuoted());

        /// <summary>
        /// The process priority class, valid values are: Normal, Idle, High, RealTime, BelowNormal, AboveNormal.
        /// If null or empty the priority class is not changed.
        /// </summary>
        public String PriorityClass;

        /// <summary>
        /// Enable or disable priority boost
        /// </summary>
        public bool PriorityBoost = true;

        /// <summary>
        /// Specifies the affinity mask manually as a binary number (if set, slicing is not used).
        /// Ex: "11110000", to use core 4 to 7, a better solution is to use core slicing:
        /// If multiple services are deployed on the same machine it could be easier to divide the number of cores into "slices" and then assign a slice index per service.
        /// This approach is agnostic to the number of cores available so the same config's should work good on any number of cores.
        /// </summary>
        public String AffinityMask;

        /// <summary>
        /// Specifies how many slices to split the number of available cores into (slicing is only used if this is greater than 1).
        /// If multiple services are deployed on the same machine it could be easier to divide the number of cores into "slices" and then assign a slice index per service.
        /// This approach is agnostic to the number of cores available so the same config's should work good on any number of cores.
        /// Ex (Octa core machine):
        ///   2 slices given 4 cores per slice.
        ///   3 slices gives 3 cores per slice (some overlap).
        /// </summary>
        public int SliceCount;

        /// <summary>
        /// What slice to use for this process (modulo <see cref="SliceCount"/>).
        /// If multiple services are deployed on the same machine it could be easier to divide the number of cores into "slices" and then assign a slice index per service.
        /// This approach is agnostic to the number of cores available so the same config's should work good on any number of cores.
        /// </summary>
        public int SliceIndex;
    }


}
