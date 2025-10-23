using System;
using System.Collections.Generic;

namespace SysWeaver.Db
{
    public abstract class DbParams : CredentialParams
    {
        protected DbParams(String connectionString, String connectionStringNoScehma)
        {
            ConnectionString = connectionString;
            ConnectionStringNoSchema = connectionStringNoScehma;
            User = "root"; 
        }

        /// <summary>
        /// The connection string to use when connecting to a schema, [PropertyName] will be replaced with the value of that property
        /// </summary>
        public String ConnectionString { get; set; }
        
        /// <summary>
        /// The connection string to use when connecting to a database without a schema (useful for auto creation of schemas), [PropertyName] will be replaced with the value of that property
        /// </summary>
        public String ConnectionStringNoSchema { get; set; }

        /// <summary>
        /// The server IP or DNS name, [Server] in connections strings is replaced with this value
        /// </summary>
        public String Server { get; set; }

        /// <summary>
        /// The server port, [Port] in connections strings is replaced with this value
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The schema to connect to, [Schema] in connections strings is replaced with this value
        /// </summary>
        [ConfigIgnore]
        public String Schema { get; set; }

        /// <summary>
        /// Default command time out in seconds
        /// </summary>
        public int TimeOut { get; set; } = 30;

        /// <summary>
        /// The serializer to use for making blobs (object to binary data)
        /// </summary>
        [ConfigIgnore]
        public String BlobSer { get; set; } = "json";

        /// <summary>
        /// The compression type to use for making blobs (object to binary data)
        /// </summary>
        [ConfigIgnore]
        public String BlobComp { get; set; } = "br";

        /// <summary>
        /// If true, try to block as many write operations as possible
        /// </summary>
        [ConfigIgnore]
        public bool ReadOnly {  get; set; }

        /// <summary>
        /// Builds a connection string using the current property values
        /// </summary>
        /// <param name="useSchema">If true the ConnectionString is used, else the ConnectionStringNoSchema is used</param>
        /// <returns>A connection string</returns>
        /// <exception cref="Exception">If there are any parser errors</exception>
        public virtual String BuildConnectionString(bool useSchema = true)
        {
            GetUserPassword(out var user, out var password);
            var args = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            args[nameof(Server)] = Server;
            args[nameof(Port)] = Port.ToString();
            args[nameof(Schema)] = Schema;
            args[nameof(User)] = user;
            args[nameof(Password)] = password;
            SetArgs(args, true);
            var argsFixed = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in args)
                argsFixed[String.Join(k.Key, '[', ']')] = k.Value;
            var cs = TextTemplate.SearchAndReplace(useSchema ? ConnectionString : ConnectionStringNoSchema, argsFixed, true);
            return cs;
        }

        /// <summary>
        /// Derived classes should set the args for all properties that goes into connection string like: args[nameof(PropertyName)] = PropertyName;
        /// </summary>
        /// <param name="args">A dictionary of args</param>
        /// <param name="isSecure">True to set sensitive parameters to their real value, if false set sensitive paramaters to ??? or simular bogus values</param>
        protected virtual void SetArgs(Dictionary<String, String> args, bool isSecure)
        {
        }

        public override string ToString()
        {
            var args = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var s = Server;
            args[nameof(Server)] = String.IsNullOrEmpty(s) ? "localhost" : s;
            args[nameof(Port)] = Port.ToString();
            args[nameof(Schema)] = Schema;
            args[nameof(User)] = "???";
            args[nameof(Password)] = "***";
            SetArgs(args, false);
            args[nameof(User)] = "???";
            args[nameof(Password)] = "***";
            var argsFixed = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in args)
                argsFixed[String.Join(k.Key, '[', ']')] = k.Value;
            var cs = TextTemplate.SearchAndReplace(String.IsNullOrEmpty(Schema) ? ConnectionStringNoSchema : ConnectionString, argsFixed, true);
            return cs;
        }



    }


}
