using System;
using System.Collections.Generic;

namespace SysWeaver.Auth
{
    /// <summary>
    /// Public information about a user (identity, tokens and profile data), without any authorizer specific state.
    /// </summary>
    public class AuthorizationInfo
    {
        /// <summary>
        /// The name of the user
        /// </summary>
        public readonly String Username;

        /// <summary>
        /// The set of security tokens that this user have (all lowercase)
        /// </summary>
        public readonly IReadOnlySet<String> Tokens;

        /// <summary>
        /// Preferred language of the user
        /// </summary>
        public readonly String Language;

        /// <summary>
        /// A domain for a user, the meaning of a domain is application specific
        /// </summary>
        public readonly String Domain;

        /// <summary>
        /// Optional email for the user
        /// </summary>
        public readonly String Email;

        /// <summary>
        /// A user guid, ASCII only, starting with the authorizer's <see cref="AuthorizerBase.GuidPrefix"/> and a ':'
        /// </summary>
        public readonly String Guid;

        /// <summary>
        /// The display name of the user, defaults to the user name.
        /// If the name contains '@' or '+' (could be an email or phone number) a random name is generated from the guid instead.
        /// </summary>
        public readonly String NickName;

        /// <summary>
        /// If true, the nick name was auto selected
        /// </summary>
        public readonly bool AutoNickName;


        /// <summary>
        /// Generate a deterministic random nick name based of a users guid (the same guid always gives the same name)
        /// </summary>
        /// <param name="guid">The user guid</param>
        /// <returns>A name with at most <see cref="AuhorizationLimits.MaxNickNameLength"/> chars</returns>
        public static String GetRandomName(string guid)
            => NameGen.GetRandomName(AuhorizationLimits.MaxNickNameLength, NameGen.Genus.Male, new Random((int)QuickHash.Hash(guid)));


        /// <summary>
        /// If a nick contains any of these it's invalid (email and phone).
        /// </summary>
        static readonly Char[] InvalidNick = "@+".ToCharArray();
        /// <summary>
        /// Create user information, validating the lengths of the values.
        /// </summary>
        /// <param name="username">The user name (at most <see cref="AuhorizationLimits.MaxUserNameLength"/> chars)</param>
        /// <param name="tokens">The security tokens (lower case), null means no tokens. The set is frozen</param>
        /// <param name="language">Preferred language, may be null</param>
        /// <param name="domain">Application specific domain (at most <see cref="AuhorizationLimits.MaxDomainName"/> chars), may be null</param>
        /// <param name="email">Email (at most <see cref="AuhorizationLimits.MaxEmailLength"/> chars), may be null</param>
        /// <param name="guid">The user guid (ASCII, at most <see cref="AuhorizationLimits.MaxGuidLength"/> chars)</param>
        /// <param name="nickName">The nick name, null uses the user name</param>
        /// <exception cref="Exception">A value is too long or the guid contains non ASCII chars.</exception>
        public AuthorizationInfo(string username, IReadOnlySet<string> tokens, string language, string domain, string email, string guid, string nickName)
        {
            AuthTools.ValidateUserGuid(guid);
            if (username.Length > AuhorizationLimits.MaxUserNameLength)
                throw new Exception("User name to long!");
            if ((email?.Length ?? 0) > AuhorizationLimits.MaxEmailLength)
                throw new Exception("Email to long!");
            if ((domain?.Length ?? 0) > AuhorizationLimits.MaxDomainName)
                throw new Exception("Domain to long!");
            var l = System.Text.Encoding.UTF8.GetBytes(guid);
            if (l.Length > 64)
                throw new Exception("Guid to long!");
            Guid = guid;
            Username = username;
            Tokens = tokens?.Freeze() ?? AuthTools.EmptyTokens;
            Email = email;
            var nick = nickName ?? username;
            if (nick.IndexOfAny(InvalidNick) >= 0)
            {
                AutoNickName = true;
                nick = GetRandomName(guid);
            }
            NickName = nick;
            Domain = domain;
            Language = language;
        }

        /// <summary>
        /// Create a copy of the public user information (ex: to strip the authorizer state from an <see cref="Authorization"/>).
        /// </summary>
        /// <param name="from">The instance to copy from</param>
        public AuthorizationInfo(AuthorizationInfo from)
        {
            Guid = from.Guid;
            Username = from.Username;
            Tokens = from.Tokens;
            Email = from.Email;
            NickName = from.NickName;
            Domain = from.Domain;
            Language = from.Language;
            AutoNickName = from.AutoNickName;

        }

    }

}
