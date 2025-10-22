using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SysWeaver.AI;

namespace SysWeaver.Data
{

    /// <summary>
    /// Represent rows of data in a type agnostic manner
    /// </summary>
    public sealed class TableData : BaseTableData
    {
        /// <summary>
        /// A change counter for the column information, if the request Cc is equal to this, no column information is sent
        /// </summary>
        [OpenAiIgnore]
        public long Cc;

        /// <summary>
        /// Number of ms to wait before a new refresh
        /// </summary>
        [EditMin(0)]
        [OpenAiIgnore]
        public long RefreshRate;
    }



}
