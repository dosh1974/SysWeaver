using System;

namespace SysWeaver.Data
{
    public sealed class TableDataRow
    {
#if DEBUG
        public override string ToString() => Values == null ? "null" : String.Join('|', Values);
#endif//DEBUG
        /// <summary>
        /// The column values
        /// </summary>
        public Object[] Values;
    }

}
