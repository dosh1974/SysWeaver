using System;

namespace SysWeaver.Db
{

    public class MySqlDbParams : DbParams
    {
        public MySqlDbParams() : base(
            "server=[Server];user=[User];port=[Port];database=[Schema];password=[Password];SSL Mode=None",
            "server=[Server];user=[User];port=[Port];password=[Password];SSL Mode=None"
            )
        {
            Port = 3306;
        }

        /// <summary>
        /// Character set to use for the entire db (default)
        /// </summary>
        public String CharSet { get; set; } = "utf8mb4";

        /// <summary>
        /// The character set collation to use for the entire db (default)
        /// </summary>
        public String CharSetCollate { get; set; } = "utf8mb4_bin";

        /// <summary>
        /// Case insensitive collation (if case insensitive attribute is set for a column)
        /// </summary>
        public String CharSetCollateCI { get; set; } = "utf8mb4_unicode_ci";


        /// <summary>
        /// Case sensitive collation (if case insensitive attribute is set for a column)
        /// </summary>
        public String CharSetCollateC { get; set; } = "utf8mb4_bin";


        /// <summary>
        /// The character set collation to use for the entire db (default)
        /// </summary>
        public String CharSetAsciiCollate { get; set; } = "ascii_bin";


        /// <summary>
        /// Case insensitive ascii collation (if case insensitive attribute is set for a column)
        /// </summary>
        public String CharSetAsciiCollateCI { get; set; } = "ascii_general_ci";


        /// <summary>
        /// Case sensitive ascii collation (if case insensitive attribute is set for a column)
        /// </summary>
        public String CharSetAsciiCollateC { get; set; } = "ascii_bin";

        /// <summary>
        /// Set this to use a specific public key for the connection (safer)
        /// </summary>
        public String ServerRSAPublicKeyFile { get; set; }

        /// <summary>
        /// True to allow the db connection to get the public key of the server
        /// </summary>
        public bool AllowPublicKeyRetrieval { get; set; } = true;

        public override string BuildConnectionString(bool useSchema = true)
        {
            var s = base.BuildConnectionString(useSchema);
            var t = ServerRSAPublicKeyFile;
            if (!String.IsNullOrEmpty(t))
                s = String.Join(";ServerRSAPublicKeyFile=", s, t);
            if (AllowPublicKeyRetrieval)
                s += ";AllowPublicKeyRetrieval=true";
            return s;
        }

        /// <summary>
        /// List of partitions (folders to use) for the tables
        /// </summary>
        public String[] Partitions { get; set; }

    }





}
