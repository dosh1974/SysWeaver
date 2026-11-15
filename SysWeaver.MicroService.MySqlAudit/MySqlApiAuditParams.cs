using SysWeaver.Db;

namespace SysWeaver.MicroService
{
    /// <summary>
    /// Parameters for the <see cref="MySqlApiAuditService"/>
    /// </summary>
    public sealed class MySqlApiAuditParams : MySqlDbParams
    {
        /// <summary>
        /// Sets the default schema name ("Audit")
        /// </summary>
        public MySqlApiAuditParams()
        {
            Schema = "Audit";
        }
    }
}
