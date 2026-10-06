using System;

// https://github.com/SimpleStack/simplestack.orm



namespace SysWeaver
{
    /// <summary>
    /// Application wide settings (name, description, language and thread pool sizes), typically loaded from the application config file
    /// and applied once at startup (see <see cref="AppInfo"/>).
    /// </summary>
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
        /// A seed (changes the automatically generated logo), stored in <see cref="EnvInfo.AppSeed"/>
        /// </summary>
        public int AppSeed;

        /// <summary>
        /// The default language to use, system should try to localize according to this.
        /// The two letter ISO 639-1 language code of the language, ex: "en", "es", "de".
        /// Can optionally have an ISO 3166 Alpha 2 country code appended, ex: "en-GB", "en-US", "es-MX", "es-ES".
        /// </summary>
        public String AppLanguage = "en-US";



        /// <summary>
        /// Specify the minimum number of thread pool worker threads (see <see cref="System.Threading.ThreadPool.SetMinThreads(int, int)"/>).
        /// 0 = Use default.
        /// greater than 0 = Use the maximum of the default and this value (the minimum is never lowered below the default).
        /// less than  0 = Use the maximum of the default and the number of CPU cores multiplied by the absolute value of this number as a percentage.
        /// Ex: -200 = Max(default, (coreCount * 200) / 100)
        /// </summary>
        public int ThreadPoolWorkerThreads = -200;


        /// <summary>
        /// Specify the minimum number of thread pool IO completion threads (see <see cref="System.Threading.ThreadPool.SetMinThreads(int, int)"/>).
        /// 0 = Use default.
        /// greater than 0 = Use the maximum of the default and this value (the minimum is never lowered below the default).
        /// less than  0 = Use the maximum of the default and the number of CPU cores multiplied by the absolute value of this number as a percentage.
        /// Ex: -200 = Max(default, (coreCount * 200) / 100)
        /// </summary>
        public int ThreadPoolIoThreads = -50;
    }


}
