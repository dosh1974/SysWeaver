using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format values as:
    /// Format as a decimal number.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataNumberAttribute : TableDataRawFormatAttribute
    {
        /// <summary>
        /// Format as a decimal number.
        /// </summary>
        /// <param name="decimals">
        /// The number of decimals to display.
        /// If less than zero, integer number will have zero decimals and floating point values will have -decimals decimal values. 
        /// For example if decimals=-2, value=42, results will be:
        /// For an Int32: "42". 
        /// For a Single: "42.00".
        /// </param>
        /// <param name="textFormat">
        /// {0} = Formatted value.
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataNumberAttribute(int decimals = -2, String textFormat = "{0}", String titleFormat = "Raw: {2}", bool copyOnClick = true)
            : base(TableDataFormats.Number, decimals, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClick ? "True" : "")
        {
        }

        /// <summary>
        /// Predefined percentage attribute (2 decimals for floating point types)
        /// </summary>
        public static readonly TableDataNumberAttribute Percentage = new TableDataNumberAttribute(-2, "{0}%");

        /// <summary>
        /// Predefined multiplier attribute (2 decimals for floating point types)
        /// </summary>
        public static readonly TableDataNumberAttribute Multiplier = new TableDataNumberAttribute(-2, "{0}x");


    }


    /// <summary>
    /// Format values as a percentage, ex: "23.50%".
    /// Defaults to 2 decimals for decimal point numbers nad zero for integers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataPercentageAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format as a decimal number.
        /// </summary>
        /// <param name="decimals">
        /// The number of decimals to display.
        /// If less than zero, integer number will have zero decimals and floating point values will have -decimals decimal values. 
        /// For example if decimals=-2, value=42, results will be:
        /// For an Int32: "42". 
        /// For a Single: "42.00".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataPercentageAttribute(int decimals = -2, String titleFormat = "Raw: {2}", bool copyOnClick = true)
            : base(decimals, "{0} %", titleFormat, copyOnClick)
        {
        }
    }

    /// <summary>
    /// Format values as a multiplier, ex: "3.6x".
    /// Defaults to 1 decimals for decimal point numbers nad zero for integers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataMultiplierAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format as a decimal number.
        /// </summary>
        /// <param name="decimals">
        /// The number of decimals to display.
        /// If less than zero, integer number will have zero decimals and floating point values will have -decimals decimal values. 
        /// For example if decimals=-2, value=42, results will be:
        /// For an Int32: "42". 
        /// For a Single: "42.00".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataMultiplierAttribute(int decimals = -1, String titleFormat = "Raw: {2}", bool copyOnClick = true)
            : base(decimals, "{0} x", titleFormat, copyOnClick)
        {
        }
    }


    /// <summary>
    /// Use this attribute to mark a data table "column" as the primary key (used by default when creating a graph from a column)
    /// </summary>
    /// <remarks>
    /// Put on the row type, at most 7 columns can be part of the primary key.
    /// Building the table data type information throws if a name doesn't match a column name.
    /// If no primary key is specified, the first column with a <see cref="TableDataKeyAttribute"/> becomes the primary key.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false)]
    public class TableDataPrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// Specify the primary key columns.
        /// </summary>
        /// <param name="primaryKeyNames">The column names (in order) that make up the primary key</param>
        public TableDataPrimaryKeyAttribute(params String[] primaryKeyNames)
        {
            PrimaryKeyNames = primaryKeyNames;
        }

        /// <summary>
        /// The column names (in order) that make up the primary key
        /// </summary>
        public readonly String[] PrimaryKeyNames;
    }


    /// <summary>
    /// Use this attribute to mark a data table "column" as potential key (optionally used by default when creating a graph from a column)
    /// </summary>
    /// <remarks>
    /// Sets <see cref="TableDataColumnProps.IsKey"/> on the column (unless it becomes the primary key).
    /// If the type has no primary key, the first column with this attribute is used as the primary key instead.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class TableDataKeyAttribute : Attribute
    {
        /// <summary>
        /// Mark (or explicitly unmark) a column as a potential key.
        /// </summary>
        /// <param name="isKey">True if the column is a potential key</param>
        public TableDataKeyAttribute(bool isKey = true)
        {
            IsKey = isKey;
        }

        /// <summary>
        /// True if the column is a potential key
        /// </summary>
        public readonly bool IsKey;
    }




    /// <summary>
    /// Format values as an amount in USD dollars.
    /// Ex: "$ 22.50"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataAmountUSDAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format valus as an amount in USD dollars.
        /// Ex: "$ 22.50"
        /// </summary>
        /// <param name="titleFormat">
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataAmountUSDAttribute(String titleFormat = "{0} USD.\nRaw: {2}", bool copyOnClick = true)
            : base(2, "$ {0}", titleFormat ?? "{0} USD.\nRaw: {2}", copyOnClick)
        {
        }
    }


    /// <summary>
    /// Format values as an amount in an unknown currency using 2 decimals.
    /// Ex: "22.50💰"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataAmountAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format values as an amount in an unknown currency using 2 decimals.
        /// Ex: "22.50💰"
        /// </summary>
        /// <param name="titleFormat">
        /// {0} = Formatted value (decimals and thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataAmountAttribute(String titleFormat = "{0} of some unknown currency.\nRaw: {2}", bool copyOnClick = true)
            : base(2, "{0}💰", titleFormat ?? "{0} of some unknown currency.\nRaw: {2}", copyOnClick)
        {
        }
    }

    /// <summary>
    /// Format values as XP points.
    /// Ex: "22⚡"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataXpAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format values as XP points.
        /// Ex: "22⚡"
        /// </summary>
        /// <param name="titleFormat">
        /// {0} = Formatted value (thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataXpAttribute(String titleFormat = "{0} Xp (experience points)", bool copyOnClick = true)
            : base(0, "{0}⚡", titleFormat ?? "{0} Xp (experience points)", copyOnClick)
        {
        }
    }


    /// <summary>
    /// Format values as number of stars
    /// Ex: "22⭐"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataStarAttribute : TableDataNumberAttribute
    {
        /// <summary>
        /// Format values as XP points.
        /// Ex: "22⭐"
        /// </summary>
        /// <param name="decimals">
        /// The number of decimals to display.
        /// If less than zero, integer number will have zero decimals and floating point values will have -decimals decimal values. 
        /// For example if decimals=-2, value=42, results will be:
        /// For an Int32: "42⭐". 
        /// For a Single: "42.00⭐".
        /// </param>
        /// <param name="titleFormat">
        /// {0} = Formatted value (thousands separator applied)
        /// {1} = Next value (must exist). 
        /// {2} = Value before formatting.
        /// {3} = The text (after formatting). 
        /// </param>
        /// <param name="copyOnClick">Copy the value (before formatting) to the clipboard on click.</param>
        public TableDataStarAttribute(int decimals = -1, String titleFormat = "{0} stars", bool copyOnClick = true)
            : base(decimals, "{0}⭐", titleFormat ?? "{0} stars", copyOnClick)
        {
        }
    }

}



