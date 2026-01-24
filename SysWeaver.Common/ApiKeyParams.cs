using System;
using System.IO;


namespace SysWeaver
{
    public class ApiKeyParams
    {

        public override string ToString() => CredFile.ToFilename();

        /// <summary>
        /// The Api Key, optionally use the CredFile instead to read it from a file.
        /// </summary>
        public String ApiKey { get; set; }

        /// <summary>
        /// Filename, if specified the API key is read from the file (should be single line of text, lines starting with '#' is considered a comment and not read).
        /// Variables can be used and with "$(" and ends with ")".
        /// Variables can be any value of the Environment.SpecialFolder enum, or CLI environment variables plus others.
        /// Ex:
        /// "$(KeyFolder)/SecretService.txt"
        /// Some common folder variables:
        ///             $(CommonApplicationData) = The directory that serves as a common repository for application-specific data that is used by all users.
        ///             $(LocalApplicationData) = The directory that serves as a common repository for application-specific data that is used by the current, non-roaming user.
        ///             $(ApplicationData) = The directory that serves as a common repository for application-specific data for the current roaming user (typically settings that should be shared between systems).
        ///             $(MyPictures) = The My Pictures folder.
        /// Env info variables:
        ///             $(ExeAppName) = Name of the executable.
        ///             $(Executable) = Full path to the executable.
        ///             $(ExecutableDir) = ExecutableDir.
        ///             $(ExecutableBase) = Full path to the folder where the application is.
        ///             $(AppInstance) = A "unique" id for this process
        ///             $(KeyFolder) = The folder where keys are stored.
        /// </summary>
        public String CredFile { get; set; }

        /// <summary>
        /// Get the api key (may be from the supplied file, no caching is done so don't call frequently)
        /// </summary>
        /// <param name="mustBeValid">Throw if the user or password is empty</param>
        /// <returns>False if the user or password is empty, else True</returns>
        public String GetApiKey(bool mustBeValid = true)
        {
            var fn = CredFile;
            if (!String.IsNullOrEmpty(fn))
            {
                fn = PathTemplate.Resolve(fn);
                fn = EnvInfo.MakeAbsoulte(fn);
                if (!File.Exists(fn))
                    throw new Exception("Credentials file " + fn.ToFilename() + " must exist!");
                var l = File.ReadAllLines(fn);
                var lc = l.Length;
                if (lc < 1)
                    throw new Exception("Credentials file " + fn.ToFilename() + " must contain at least one line of text!");
                for (int i = 0; i < lc; ++i)
                {
                    var t = l[i].Trim();
                    if (t.Length == 0)
                        continue;
                    if (t[0] == '#')
                        continue;
                    return t;
                }
            }
            else
            {
                if (mustBeValid)
                {
                    if (String.IsNullOrEmpty(ApiKey))
                        throw new Exception(nameof(ApiKey) + " parameter may not be empty!");
                }
            }
            return ApiKey;
        }

    }

}
