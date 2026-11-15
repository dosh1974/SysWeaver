using System;
using System.Data.Common;

// https://github.com/SimpleStack/simplestack.orm


namespace SysWeaver.Db
{
    /// <summary>
    /// Extension methods for making ADO.NET commands
    /// </summary>
    public static class DbSimpleStackExt
    {
        /// <summary>
        /// Add a parameter to a command
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="cmd">The command</param>
        /// <param name="name">The parameter name</param>
        /// <param name="value">The parameter value</param>
        /// <returns>The same command (for chaining)</returns>
        public static DbCommand AddParamater<T>(this DbCommand cmd, String name, T value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
            return cmd;
        }

        /// <summary>
        /// Create a command
        /// </summary>
        /// <param name="con">The connection</param>
        /// <param name="sql">The command text</param>
        /// <returns>A new command</returns>
        public static DbCommand CreateCommand(this DbConnection con, String sql)
        {
            var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        /// <summary>
        /// Create a command with one parameter
        /// </summary>
        /// <param name="con">The connection</param>
        /// <param name="sql">The command text</param>
        /// <param name="param0">Name of the parameter</param>
        /// <param name="value0">Value of the parameter</param>
        /// <returns>A new command</returns>
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

        /// <summary>
        /// Create a command with two or more parameters
        /// </summary>
        /// <param name="con">The connection</param>
        /// <param name="sql">The command text</param>
        /// <param name="param0">Name of the first parameter</param>
        /// <param name="value0">Value of the first parameter</param>
        /// <param name="param1">Name of the second parameter</param>
        /// <param name="value1">Value of the second parameter</param>
        /// <param name="additionalParams">Additional parameters as alternating name (String) and value pairs</param>
        /// <returns>A new command</returns>
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
