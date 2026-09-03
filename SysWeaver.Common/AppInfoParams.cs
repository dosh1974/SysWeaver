using System;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    public sealed class AppInfoParams
    {
        /// <summary>
        /// The name of the application (can be used for file names etc)
        /// </summary>
        public String AppName;

        /// <summary>
        /// The display name of the application (can be used for texts etc, displayed to end users)
        /// </summary>
        public String AppDisplayName;

        /// <summary>
        /// The description of the application
        /// </summary>
        public String AppDescription;

        /// <summary>
        /// A seed (changes the automatically generated logo)
        /// </summary>
        public int AppSeed;

        /// <summary>
        /// The default language to use, system should try to localize according to this.
        /// The two letter ISO 639-1 language code of the language, ex: "en", "es", "de".
        /// Can optionally have an ISO 3166 Alpha 2 country code appended, ex: "en-GB", "en-US", "es-MX", "es-ES".
        /// </summary>
        public String AppLanguage = "en-US";



        /// <summary>
        /// Specify the number of worker threads.
        /// 0 = Use default.
        /// greater than 0 = Use exactly this many.
        /// less than  0 = Use the maximum of the default and the number of CPU cores multiplied by the absolute value of this number as a percentage.
        /// Ex: -200 = Max(default, (coreCount * 200) / 100)
        /// </summary>
        public int ThreadPoolWorkerThreads = -200;


        /// <summary>
        /// Specify the number of IO threads.
        /// 0 = Use default.
        /// greater than 0 = Use exactly this many.
        /// less than  0 = Use the maximum of the default and the number of CPU cores multiplied by the absolute value of this number as a percentage.
        /// Ex: -200 = Max(default, (coreCount * 200) / 100)
        /// </summary>
        public int ThreadPoolIoThreads = -50;
    }


}
