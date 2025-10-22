using System;

namespace SysWeaver.FileDatabase
{
    public class FindInFileDatabase
    {
        public FindFilterText Name;
        public FindFilterText Ext;
        public FindFilterDateTime LastModified;
        public FindFilterValue Size;
        /// <summary>
        /// Column names, first entry is the primary order, prefix with a - for descending order, ex:
        /// ["Name"]
        /// ["-Size"]
        /// ["Ext", "Name"]
        /// </summary>
        public String[] Order;
        public int Offset;
        public int MaxCount;
    }




    public enum FindStringOps
    {
        Contains,
        EndsWith,
        StartsWith,
        Equals,
    }




    public class FindFilterText
    {
        public FindStringOps Op;
        public bool Invert;
        public String Text;
    }

    public class FindFilterDateTime
    {
        public bool Invert;
        public bool MinExclusive;
        public DateTime? Min;
        public bool MaxExclusive;
        public DateTime? Max;
    }

    public class FindFilterValue
    {
        public bool Invert;
        public bool MinExclusive;
        public long? Min;
        public bool MaxExclusive;
        public long? Max;
    }

    public class FindInFileDatabaseResponse
    {
        public int NextOffset;
    }

    public class FindInFileDatabaseResponseT<T> : FindInFileDatabaseResponse where T : DbFileData
    {
        public T[] Files;
    }


}
