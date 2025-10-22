using System;

namespace SysWeaver.Auth
{

    /// <summary>
    /// Represents a policy to use for a password
    /// </summary>
    public sealed class PasswordPolicy
    {
        public override string ToString() => String.Concat('[', MinLength, ", ", MaxLength, "]",
            MixedCase ? ", mixed case" : "",
            MixedNumerical ? ", mixed numerical" : "",
            MixedSpecial ? ", mixed special" : "");

        /// <summary>
        /// The minimum length of a valid password
        /// </summary>
        public int MinLength = 8;
        /// <summary>
        /// The maximum length of a valid password
        /// </summary>
        public int MaxLength = 64;

        /// <summary>
        /// At least one lower. and one upper-case letter must be present
        /// </summary>
        public bool MixedCase = true;

        /// <summary>
        /// At least one numerical in addition to letters must be present
        /// </summary>
        public bool MixedNumerical = true;

        /// <summary>
        /// At least one non-letter, non-numerical char must be present
        /// </summary>
        public bool MixedSpecial;


#if DEBUG
        static PasswordPolicy()
        {
            var p = new PasswordPolicy
            {
                MinLength = 1,
                MixedCase = true,
                MixedNumerical = false,
                MixedSpecial = false,
            };
            String e = "Password failed";
            if (p.Check("test") != PasswordStatus.NeedUpperCase)
                throw new Exception(e);
            if (p.Check("TEST") != PasswordStatus.NeedLowerCase)
                throw new Exception(e);
            if (p.Check("Test") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("tEST") != PasswordStatus.Ok)
                throw new Exception(e);
            p = new PasswordPolicy
            {
                MinLength = 1,
                MixedNumerical = true,
                MixedCase = false,
                MixedSpecial = false,
            };
            if (p.Check("test") != PasswordStatus.NeedNumber)
                throw new Exception(e);
            if (p.Check("123") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("test123") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("TEST123") != PasswordStatus.Ok)
                throw new Exception(e);

            p = new PasswordPolicy
            {
                MinLength = 1,
                MixedSpecial = true,
                MixedNumerical = false,
                MixedCase = false,
            };
            if (p.Check("test") != PasswordStatus.NeedSpecial)
                throw new Exception(e);
            if (p.Check("!?!") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("123!?!") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("test!") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("TEST!") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("T12!") != PasswordStatus.Ok)
                throw new Exception(e);

            p = new PasswordPolicy
            {
                MinLength = 1,
                MixedNumerical = true,
                MixedCase = true,
                MixedSpecial = false,
            };
            if (p.Check("test123") != PasswordStatus.NeedUpperCase)
                throw new Exception(e);
            if (p.Check("TEST123") != PasswordStatus.NeedLowerCase)
                throw new Exception(e);
            if (p.Check("123!?!") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("Test123") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("Test123!") != PasswordStatus.Ok)
                throw new Exception(e);

            p = new PasswordPolicy
            {
                MinLength = 1,
                MixedNumerical = true,
                MixedSpecial = true,
                MixedCase = false,
            };
            if (p.Check("test123") != PasswordStatus.NeedSpecial)
                throw new Exception(e);
            if (p.Check("TEST!") != PasswordStatus.NeedNumber)
                throw new Exception(e);
            if (p.Check("123!?!") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("test123!") != PasswordStatus.Ok)
                throw new Exception(e);
            if (p.Check("TEST123!") != PasswordStatus.Ok)
                throw new Exception(e);


            p = new PasswordPolicy
            {
                MinLength = 1,
                MixedNumerical = true,
                MixedCase = true,
                MixedSpecial = true,
            };
            if (p.Check("test123") != PasswordStatus.NeedUpperCase)
                throw new Exception(e);
            if (p.Check("Test123") != PasswordStatus.NeedSpecial)
                throw new Exception(e);
            if (p.Check("123!?!") != PasswordStatus.NeedLetter)
                throw new Exception(e);
            if (p.Check("test123!") != PasswordStatus.NeedUpperCase)
                throw new Exception(e);
            if (p.Check("TEST123!") != PasswordStatus.NeedLowerCase)
                throw new Exception(e);
            if (p.Check("Test123!") != PasswordStatus.Ok)
                throw new Exception(e);
        }
#endif//DEBUG

    }

}
