using System;
using SysWeaver.Data;

namespace SysWeaver.MicroService
{
    public sealed class SmMemoryInfo
    {
        /// <summary>
        /// Used RAM as a percentage
        /// </summary>
        [TableDataNumber(2, "{0}%")]
        public double Used;

        /// <summary>
        /// Number of bytes free
        /// </summary>
        [TableDataByteSize]
        public long Free;

        /// <summary>
        /// Total number of bytes
        /// </summary>
        [TableDataByteSize]
        public long Total;
    }



    [TableDataPrimaryKey(nameof(Drive), nameof(Label))]
    public sealed class SmDriveInfo
    {
        /// <summary>
        /// Index
        /// </summary>
        [TableDataUrl("{0}", "*../chart/chart.html?q=../ServerManager/GetDriveChart?{0}")]
        public int Index;

        /// <summary>
        /// The drive letter
        /// </summary>
        public String Drive;

        /// <summary>
        /// The name (label) of the drive
        /// </summary>
        public String Label;

        /// <summary>
        /// Used disc space as a percentage
        /// </summary>
        [TableDataNumber(2, "{0}%")]
        public double Used;

        /// <summary>
        /// Number of bytes free
        /// </summary>
        [TableDataByteSize]
        public long Free;

        /// <summary>
        /// Total number of bytes for this drive
        /// </summary>
        [TableDataByteSize]
        public long Total;

        /// <summary>
        /// The format of this drive
        /// </summary>
        public String Format;

        /// <summary>
        /// The type of drive
        /// </summary>
        public String Type;

    }

}
