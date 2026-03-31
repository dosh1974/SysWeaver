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
        ///             $(Executable) = Full path to the executable, ex: "C:\MyServices\MyService.exe"
        ///             $(ExeAppName) = Name of the executable, ex: "MyService" (this can be different from AppName)
        ///             $(ExecutableDir) = ExecutableDir, ex: "C:\MyServices"
        ///             $(ExecutableBase) = Full path to the executable, excluding it's extensions, ex: "C:\MyServices\MyService"
        ///             $(AppName) = Application name (defaults to exe app name, can be changed in config), ex: "MyService".
        ///             $(AppGuid) = A "unique" id for this process
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
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
                var t = FileExt.ReadNonCommentString(fn);
                if (t == null)
                    throw new Exception("Credentials file " + fn.ToFilename() + " must contain at least one line of text!");
                return t;
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
