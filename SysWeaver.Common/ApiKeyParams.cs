using System;
using System.IO;


namespace SysWeaver
{
    /// <summary>
    /// Configuration parameters for an API key, either specified inline (<see cref="ApiKey"/>) or read from a file (<see cref="CredFile"/>).
    /// Typically used as (part of) service parameters loaded from a config file.
    /// </summary>
    public class ApiKeyParams
    {

        /// <summary>
        /// Returns the credentials file name (never the key itself, so it's safe to log).
        /// </summary>
        /// <returns>The quoted file name, or "null" if no file is specified.</returns>
        public override string ToString() => CredFile.ToFilename();

        /// <summary>
        /// The Api Key, optionally use the CredFile instead to read it from a file.
        /// </summary>
        public String ApiKey { get; set; }

        /// <summary>
        /// Filename, if specified the API key is read from the file (should be single line of text, lines starting with '#' is considered a comment and not read).
        /// Takes precedence over <see cref="ApiKey"/>. Relative paths are relative to the executable folder.
        /// Variables can be used, they start with "$(" and end with ")", see <see cref="PathTemplate"/>.
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
        ///             $(AppGuid) = A guid derived from the application assembly name (stable between runs)
        ///             $(AppDisplayName) = Friendly application name (defaults to de-camel cased exe app name, can be changed in config), ex: "My service".
        ///             $(MachineName) = Machine name, ex: "DESKTOP-324VHA".
        ///             $(KeyFolder) = The folder where keys are stored. ex: "C:\Keys".
        /// </summary>
        public String CredFile { get; set; }

        /// <summary>
        /// Get the api key (may be from the supplied file, no caching is done so don't call frequently)
        /// </summary>
        /// <param name="mustBeValid">If true, throw if the key file doesn't exist or if the inline key is empty</param>
        /// <returns>The API key (trimmed), or null / empty if no key is available and <paramref name="mustBeValid"/> is false</returns>
        /// <exception cref="Exception">The key file doesn't exist (only if <paramref name="mustBeValid"/> is true), the key file contains no non-comment line (regardless of <paramref name="mustBeValid"/>),
        /// or no file is specified, the inline key is empty and <paramref name="mustBeValid"/> is true.</exception>
        public String GetApiKey(bool mustBeValid = true)
        {
            var fn = CredFile;
            if (!String.IsNullOrEmpty(fn))
            {
                fn = PathTemplate.Resolve(fn);
                fn = EnvInfo.MakeAbsoulte(fn);
                if (!File.Exists(fn))
                {
                    if (mustBeValid)
                        throw new Exception("Credentials file " + fn.ToFilename() + " must exist!");
                    return null;
                }
                var t = FileExt.ReadNonCommentString(fn);
                if (t == null)
                    throw new Exception("Credentials file " + fn.ToFilename() + " must contain at least one line of text!");
                return t;
            }
            else
            {
                var r = ApiKey?.Trim();
                if (mustBeValid)
                {
                    if (String.IsNullOrEmpty(r))
                        throw new Exception(nameof(ApiKey) + " parameter may not be empty!");
                }
                return r;
            }
        }

    }

}
