using System;
using System.Collections.Generic;

namespace SysWeaver.Auth
{
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
        /// A user guid
        /// </summary>
        public readonly String Guid;

        /// <summary>
        /// Optional information for the user
        /// </summary>
        public readonly String NickName;

        /// <summary>
        /// If true, the nick name was auto selected
        /// </summary>
        public readonly bool AutoNickName;


        /// <summary>
        /// Use this to generate a nick name based of a users guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static String GetRandomName(string guid)
            => NameGen.GetRandomName(AuhorizationLimits.MaxNickNameLength, NameGen.Genus.Male, new Random((int)QuickHash.Hash(guid)));


        /// <summary>
        /// If a nick contains any of these it's invalid (email and phone).
        /// </summary>
        static readonly Char[] InvalidNick = "@+".ToCharArray();
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
