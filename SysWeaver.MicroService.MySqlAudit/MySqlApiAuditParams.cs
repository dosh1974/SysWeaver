using SysWeaver.Db;

namespace SysWeaver.MicroService
{
    public sealed class MySqlApiAuditParams : MySqlDbParams
    {
        public MySqlApiAuditParams()
        {
            Schema = "Audit";
        }
    }
}
