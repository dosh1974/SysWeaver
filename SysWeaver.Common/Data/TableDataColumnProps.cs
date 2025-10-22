using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Column properties (flags)
    /// </summary>
    [Flags]
    public enum TableDataColumnProps
    {
        /// <summary>
        /// Set if the column may be sorted
        /// </summary>
        CanSort = 1,
        
        /// <summary>
        /// Set if the column is sorted in desceding order by default
        /// </summary>
        SortedDesc = 2,
        /// <summary>
        /// Set if the column should be hidden
        /// </summary>
        Hide = 4,

        /// <summary>
        /// Basic filtering is possible (equal, not equal)
        /// </summary>
        Filter = 8,

        /// <summary>
        /// Advanced text filtering (contains, starts with, ends with)
        /// </summary>
        TextFilter = 16,


        /// <summary>
        /// Order filtering is possible (greater than, less than, greater or equal, less or equal)
        /// </summary>
        OrderFilter = 32,

        /// <summary>
        /// The column can be used to create a chart
        /// </summary>
        CanChart = 64,

        /// <summary>
        /// The column can be used as a key (typically for a chart / graph)
        /// </summary>
        IsKey = 128,

        /// <summary>
        /// The column is the first part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey1 = 256,

        /// <summary>
        /// The column is the second part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey2 = 512,

        /// <summary>
        /// The column is the third part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey3 = 256 | 512,

        /// <summary>
        /// The column is the fourth part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey4 = 1024,

        /// <summary>
        /// The column is the fifth part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey5 = 1024 | 256,


        /// <summary>
        /// The column is the sixth part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey6 = 1024 | 512,

        /// <summary>
        /// The column is the seventh part of the primary key (typically for a chart / graph)
        /// </summary>
        IsPrimaryKey7 = 1024 | 512 | 256,

        /// <summary>
        /// If true the column is computed (and can't be searched etc)
        /// </summary>
        IsComputed = 2048,


        //  Computed
        AnyFilters = Filter | TextFilter | OrderFilter,
        AnyPrimaryKey = IsPrimaryKey7,
        AnyKey = AnyPrimaryKey | IsKey,
    }




}
