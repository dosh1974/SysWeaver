using System;

namespace SysWeaver.FileDatabase
{
    /// <summary>
    /// A search request data type (note: not used by <see cref="FileDatabase{T}"/> itself, it uses table data requests)
    /// </summary>
    public class FindInFileDatabase
    {
        /// <summary>
        /// Optional filter on the file name
        /// </summary>
        public FindFilterText Name;
        /// <summary>
        /// Optional filter on the file extension
        /// </summary>
        public FindFilterText Ext;
        /// <summary>
        /// Optional filter on the last modified time
        /// </summary>
        public FindFilterDateTime LastModified;
        /// <summary>
        /// Optional filter on the file size
        /// </summary>
        public FindFilterValue Size;
        /// <summary>
        /// Column names, first entry is the primary order, prefix with a - for descending order, ex:
        /// ["Name"]
        /// ["-Size"]
        /// ["Ext", "Name"]
        /// </summary>
        public String[] Order;
        /// <summary>
        /// The number of matches to skip
        /// </summary>
        public int Offset;
        /// <summary>
        /// The maximum number of matches to return
        /// </summary>
        public int MaxCount;
    }




    /// <summary>
    /// Text compare operations
    /// </summary>
    public enum FindStringOps
    {
        /// <summary>
        /// The value contains the text
        /// </summary>
        Contains,
        /// <summary>
        /// The value ends with the text
        /// </summary>
        EndsWith,
        /// <summary>
        /// The value starts with the text
        /// </summary>
        StartsWith,
        /// <summary>
        /// The value equals the text
        /// </summary>
        Equals,
    }




    /// <summary>
    /// A text filter
    /// </summary>
    public class FindFilterText
    {
        /// <summary>
        /// The compare operation
        /// </summary>
        public FindStringOps Op;
        /// <summary>
        /// True to match the values that do NOT match
        /// </summary>
        public bool Invert;
        /// <summary>
        /// The text to compare with
        /// </summary>
        public String Text;
    }

    /// <summary>
    /// A date / time range filter
    /// </summary>
    public class FindFilterDateTime
    {
        /// <summary>
        /// True to match the values that are NOT in the range
        /// </summary>
        public bool Invert;
        /// <summary>
        /// True if the minimum is exclusive
        /// </summary>
        public bool MinExclusive;
        /// <summary>
        /// The minimum, null for no minimum
        /// </summary>
        public DateTime? Min;
        /// <summary>
        /// True if the maximum is exclusive
        /// </summary>
        public bool MaxExclusive;
        /// <summary>
        /// The maximum, null for no maximum
        /// </summary>
        public DateTime? Max;
    }

    /// <summary>
    /// A value range filter
    /// </summary>
    public class FindFilterValue
    {
        /// <summary>
        /// True to match the values that are NOT in the range
        /// </summary>
        public bool Invert;
        /// <summary>
        /// True if the minimum is exclusive
        /// </summary>
        public bool MinExclusive;
        /// <summary>
        /// The minimum, null for no minimum
        /// </summary>
        public long? Min;
        /// <summary>
        /// True if the maximum is exclusive
        /// </summary>
        public bool MaxExclusive;
        /// <summary>
        /// The maximum, null for no maximum
        /// </summary>
        public long? Max;
    }

    /// <summary>
    /// A search response
    /// </summary>
    public class FindInFileDatabaseResponse
    {
        /// <summary>
        /// The offset to use to get the next matches
        /// </summary>
        public int NextOffset;
    }

    /// <summary>
    /// A search response with the matching rows
    /// </summary>
    /// <typeparam name="T">The row type</typeparam>
    public class FindInFileDatabaseResponseT<T> : FindInFileDatabaseResponse where T : DbFileData
    {
        /// <summary>
        /// The matching rows
        /// </summary>
        public T[] Files;
    }


}
