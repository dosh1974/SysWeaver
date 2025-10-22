using System;
using SysWeaver.AI;

namespace SysWeaver.Data
{

    public class TableDataFilterBase
    {
#if DEBUG
        public override string ToString() => String.Concat( Invert ? "Not " : "", Op, ' ', Value, CaseSensitive ? " (case sensitive)" : "");
#endif//DEBUG
        /// <summary>
        /// True to invert the filter result (i.e keep entries that would otherwise be rejected)
        /// </summary>
        [OpenAiOptional]
        public bool Invert;
        /// <summary>
        /// Use case sensitive operations if applicable
        /// </summary>
        [OpenAiOptional]
        public bool CaseSensitive;
        /// <summary>
        /// The filter operation to use
        /// </summary>
        public TableDataFilterOps Op;
        /// <summary>
        /// The value to use for this operation, some ops require more than one, use comma separation in that case
        /// </summary>
        public String Value;


        public void CopyFrom(TableDataFilterBase src)
        {
            Invert = src.Invert;
            CaseSensitive = src.CaseSensitive;
            Op = src.Op;
            Value = src.Value;
        }

    }


    public sealed class TableDataFilter : TableDataFilterBase
    {
#if DEBUG
        public override string ToString() => String.Concat( '"', ColName, "\" ", base.ToString());
#endif//DEBUG
        /// <summary>
        /// Name of the column to apply this filter to
        /// </summary>
        public String ColName;


        public void CopyFrom(TableDataFilter src)
        {
            ColName = src.ColName;
            base.CopyFrom(src);
        }


    }

}
