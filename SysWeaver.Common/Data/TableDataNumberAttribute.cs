using System;

namespace SysWeaver.Data
{
    /// <summary>
    /// Format valus as:
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
        /// <param name="copyOnClick">Copy the value to the clipboard on click.</param>
        public TableDataNumberAttribute(int decimals = -2, String textFormat = "{0}", String titleFormat = "Raw: {2}", bool copyOnClick = true)
            : base(TableDataFormats.Number, decimals, textFormat ?? "{0}", titleFormat ?? "Raw: {2}", copyOnClick)
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
    /// Use this attribute to mark a data table "column" as the primary key (used by default when creating a graph from a column)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false)]
    public class TableDataPrimaryKeyAttribute : Attribute
    {
        public TableDataPrimaryKeyAttribute(params String[] primaryKeyNames)
        {
            PrimaryKeyNames = primaryKeyNames;
        }

        public readonly String[] PrimaryKeyNames;
    }


    /// <summary>
    /// Use this attribute to mark a data table "column" as potential key (optionally used by default when creating a graph from a column)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class TableDataKeyAttribute : Attribute
    {
        public TableDataKeyAttribute(bool isKey = true)
        {
            IsKey = isKey;
        }

        public readonly bool IsKey;
    }




    /// <summary>
    /// Format valus as an amount in USD dollars.
    /// Ex: "$ 22.50"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TableDataAmountUSDAttribute : TableDataRawFormatAttribute
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
        /// <param name="copyOnClick">Copy the value to the clipboard on click.</param>
        public TableDataAmountUSDAttribute(String titleFormat = "USD {2}", bool copyOnClick = true)
            : base(TableDataFormats.Number, 2, "$ {0}", titleFormat ?? "USD {2}", copyOnClick)
        {
        }
    }






}



