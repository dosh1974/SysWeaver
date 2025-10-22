using System;
using System.IO;


namespace SysWeaver
{
    public class CredentialParams
    {

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
        /// Filename, if specified the user and password is read from the file (should be single line of text in the user:key format)
        /// </summary>
        public String CredFile { get; set; }

        /// <summary>
        /// Get the credentials (may be from the supplied file, no caching is done so don't call frequently)
        /// </summary>
        /// <param name="user">The username</param>
        /// <param name="password">The password</param>
        /// <param name="mustBeValid">Throw if the user or password is empty</param>
        /// <returns>False if the user or password is empty, else True</returns>
        public bool GetUserPassword(out String user, out String password, bool mustBeValid = true)
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
                user = "";
                password = "";
                for (int i = 0; i < lc; ++ i)
                {
                    var t = l[i].Trim();
                    if (t.Length == 0)
                        continue;
                    if (t[0] == '#')
                        continue;
                    var f = t.IndexOf(':');
                    if (f < 0)
                        throw new Exception("Credentials file " + fn.ToFilename() + " must only contain a user:password pair!");
                    user = t.Substring(0, f).TrimEnd();
                    password = t.Substring(f + 1).TrimStart();
                    if (mustBeValid)
                    {
                        if (user.Length <= 0)
                            throw new Exception("Credentials file " + fn.ToFilename() + " must contain a non-empty user name!");
                        if (password.Length <= 0)
                            throw new Exception("Credentials file " + fn.ToFilename() + " must contain a non-empty password!");
                    }
                    break;
                }
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
