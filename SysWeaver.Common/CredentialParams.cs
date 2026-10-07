using System;
using System.IO;


namespace SysWeaver
{
    /// <summary>
    /// Configuration parameters for a user name / password pair, either specified inline (<see cref="User"/>, <see cref="Password"/>) or read from a file (<see cref="CredFile"/>).
    /// Typically used as (part of) service parameters loaded from a config file.
    /// </summary>
    public class CredentialParams
    {

        /// <summary>
        /// Returns the credentials file name, or the user name if no file is used (never the password, so it's safe to log).
        /// </summary>
        /// <returns>A display string.</returns>
        public override string ToString() => String.IsNullOrEmpty(CredFile) ? String.Join(": ", nameof(User), User) : CredFile.ToFilename();

        /// <summary>
        /// Username or key
        /// </summary>
        public String User { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public String Password { get; set; }

        /// <summary>
        /// Filename, if specified the user and password is read from the file (should be single line of text in the user:key format).
        /// The first line that doesn't start with '#' is used, the user name is everything before the first ':' and the password everything after it (surrounding white space is trimmed).
        /// Takes precedence over <see cref="User"/> and <see cref="Password"/>. Relative paths are relative to the executable folder.
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
        /// Get the credentials (may be from the supplied file, no caching is done so don't call frequently)
        /// </summary>
        /// <param name="user">The username</param>
        /// <param name="password">The password</param>
        /// <param name="mustBeValid">If true, throw if the credentials file doesn't exist, contains no non-comment line, or if the user or password is empty.
        /// If false, a missing credentials file or a file without any non-comment line yields empty values (and false) without throwing.</param>
        /// <returns>False if the user or password is empty, else True</returns>
        /// <exception cref="Exception">The first non-comment line of the credentials file has no ':' (regardless of <paramref name="mustBeValid"/>),
        /// or <paramref name="mustBeValid"/> is true and the credentials file doesn't exist, contains no non-comment line, or the user or password is empty.</exception>
        public bool GetUserPassword(out String user, out String password, bool mustBeValid = true)
        {
            var fn = CredFile;
            if (!String.IsNullOrEmpty(fn))
            {
                fn = PathTemplate.Resolve(fn);
                fn = EnvInfo.MakeAbsoulte(fn);
                user = "";
                password = "";
                if (!File.Exists(fn))
                {
                    if (mustBeValid)
                        throw new Exception("Credentials file " + fn.ToFilename() + " must exist!");
                    return false;
                }
                var l = FileExt.ReadLines(fn, null, true, true);
                var lc = l.Length;
                bool found = false;
                for (int i = 0; i < lc; ++ i)
                {
                    var t = l[i];
                    if (t[0] == '#')
                        continue;
                    var f = t.IndexOf(':');
                    if (f < 0)
                        throw new Exception("Credentials file " + fn.ToFilename() + " must only contain a user:password pair!");
                    user = t.Substring(0, f).TrimEnd();
                    password = t.Substring(f + 1).TrimStart();
                    found = true;
                    if (mustBeValid)
                    {
                        if (user.Length <= 0)
                            throw new Exception("Credentials file " + fn.ToFilename() + " must contain a non-empty user name!");
                        if (password.Length <= 0)
                            throw new Exception("Credentials file " + fn.ToFilename() + " must contain a non-empty password!");
                    }
                    break;
                }
                if (mustBeValid && !found)
                    throw new Exception("Credentials file " + fn.ToFilename() + " must contain at least one line of text!");
            }else
            {
                user = User ?? "";
                password = Password ?? "";
                if (mustBeValid)
                {
                    if (user.Length <= 0)
                        throw new Exception(nameof(User) + " parameter may not be empty!");
                    if (password.Length <= 0)
                        throw new Exception(nameof(Password) + " parameter may not be empty!");
                }
            }
            return (user.Length > 0) && (password.Length > 0);
        }

    }

}
