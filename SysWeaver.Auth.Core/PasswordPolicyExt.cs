using System;
using System.Collections.Generic;

namespace SysWeaver.Auth
{
    public static class PasswordPolicyExt
    {
        /// <summary>
        /// Returns a new policy with the lowest common restriction
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>A policy with the lowest common restriction of the inputs</returns>
        public static PasswordPolicy Min(this PasswordPolicy a, PasswordPolicy b)
        {
            return new PasswordPolicy
            {
                MinLength = Math.Min(a.MinLength, b.MinLength),
                MaxLength = Math.Min(a.MaxLength, b.MaxLength),
                MixedCase = a.MixedCase & b.MixedCase,
                MixedNumerical = a.MixedNumerical & b.MixedNumerical,
                MixedSpecial = a.MixedSpecial & b.MixedSpecial,
            };
        }


        /// <summary>
        /// Returns a new policy with the lowest common restriction
        /// </summary>
        /// <param name="policies"></param>
        /// <returns>A policy with the lowest common restriction of the inputs</returns>
        public static PasswordPolicy Min(IEnumerable<PasswordPolicy> policies)
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            bool cs = true;
            bool num = true;
            bool sp = true;
            foreach (var p in policies)
            {
                min = Math.Min(min, p.MinLength);
                max = Math.Max(max, p.MaxLength);
                cs &= p.MixedCase;
                num &= p.MixedNumerical;
                sp &= p.MixedSpecial;
            }
            return new PasswordPolicy
            {
                MinLength = min,
                MaxLength = max,
                MixedCase = cs,
                MixedNumerical = num,
                MixedSpecial = sp,
            };
        }

        /// <summary>
        /// Check a password against a password policy
        /// </summary>
        /// <param name="policy">The policy</param>
        /// <param name="password">The password</param>
        /// <returns>The status of the password</returns>
        public static PasswordStatus Check(this PasswordPolicy policy, String password)
        {
            if (password == null)
                return PasswordStatus.TooShort;
            var minLen = policy.MinLength;
            if (minLen < 1)
                minLen = 1;
            var pl = password.Length;
            if (pl < minLen)
                return PasswordStatus.TooShort;
            var maxLen = policy.MaxLength;
            if (maxLen < minLen)
                maxLen = minLen;
            if (maxLen > 128)
                maxLen = 128;
            if (pl > maxLen)
                return PasswordStatus.TooLong;
            bool letter = false;
            bool lowerCase = false;
            bool upperCase = false;
            bool numeric = false;
            bool special = false;
            for (int i = 0; i < pl; ++ i)
            {
                var c = password[i];
                if (Char.IsLetter(c))
                {
                    letter = true;
                    bool l = Char.ToLower(c) == c;
                    bool u = Char.ToUpper(c) == c;
                    if (l != u)
                    {
                        lowerCase |= l;
                        upperCase |= u;
                    }
                    continue;
                }
                if (Char.IsNumber(c))
                {
                    numeric = true;
                    continue;
                }
                special = true;
            }
            bool needLetter = policy.MixedSpecial | policy.MixedNumerical;
            if (needLetter && (!letter))
                return PasswordStatus.NeedLetter;
            if (policy.MixedCase)
                if (!(lowerCase & upperCase))
                    return lowerCase ? PasswordStatus.NeedUpperCase : PasswordStatus.NeedLowerCase;
            if (policy.MixedNumerical)
                if (!numeric)
                    return PasswordStatus.NeedNumber;
            if (policy.MixedSpecial)
                if (!special)
                    return PasswordStatus.NeedSpecial;
            return PasswordStatus.Ok;
        }


    }

}
