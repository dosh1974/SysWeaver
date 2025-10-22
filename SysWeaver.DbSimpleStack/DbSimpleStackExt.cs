using System;
using System.Data.Common;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    public static class DbSimpleStackExt
    {
        public static DbCommand AddParamater<T>(this DbCommand cmd, String name, T value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
            return cmd;
        }

        public static DbCommand CreateCommand(this DbConnection con, String sql)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        public static DbCommand CreateCommand(this DbConnection con, String sql, String param0, Object value0)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            var p = cmd.CreateParameter();
            p.ParameterName = param0;
            p.Value = value0;
            cmd.Parameters.Add(p);
            return cmd;
        }

        public static DbCommand CreateCommand(this DbConnection con, String sql, String param0, Object value0, String param1, Object value1, params Object[] additionalParams)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            var p = cmd.CreateParameter();
            p.ParameterName = param0;
            p.Value = value0;
            cmd.Parameters.Add(p);

            p = cmd.CreateParameter();
            p.ParameterName = param1;
            p.Value = value1;
            cmd.Parameters.Add(p);

            var l = additionalParams.Length;
            for (int i = 0; i < l; i += 2)
            {
                p = cmd.CreateParameter();
                p.ParameterName = (String)additionalParams[i];
                p.Value = additionalParams[i + 1];
                cmd.Parameters.Add(p);
            }
            return cmd;
        }



    }


}
