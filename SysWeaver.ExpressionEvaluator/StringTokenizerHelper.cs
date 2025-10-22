using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    public static class StringTokenizerHelper
    {

        static readonly IReadOnlySet<Char> ExtraValidFirst = ReadOnlyData.Set("@_".ToCharArray());
        static readonly IReadOnlySet<Char> ExtraValid = ReadOnlyData.Set("_".ToCharArray());


        public static bool IsValidIdentiferCharFirst(Char c)
        {
            return Char.IsLetter(c) || ExtraValidFirst.Contains(c);
        }

        public static bool IsValidIdentiferCharRest(Char c)
        {
            return Char.IsLetterOrDigit(c) || ExtraValid.Contains(c);
        }
        public static bool IsValidOperatorChar(Char c)
        {
            return !(IsValidIdentiferCharRest(c) || Char.IsWhiteSpace(c));
        }

        public static void ValidateIdentifier(String value, String argumentNameN = null)
        {
            var argumentName = argumentNameN ?? "value";
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("Identifier can't be null or empty!", argumentName);
            char c = value[0];
            if (!IsValidIdentiferCharFirst(c))
                throw new ArgumentException("Character '" + c + "' (" + (int)c + ") at position 0 is invalid for an identifier!\nIdentifier name: \"" + value + "\"", argumentName);
            for (int i = 1; i < value.Length; ++i)
            {
                c = value[i];
                if (!IsValidIdentiferCharRest(c))
                    throw new ArgumentException("Character '" + c + "' (" + (int)c + ") at position " + i + " is invalid for an identifier!\nIdentifier name: \"" + value + "\"", argumentName);
            }
        }

        public static void ValidateOperator(String value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("Operator can't be null or empty!", "value");
            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];
                if (!IsValidOperatorChar(c))
                    throw new ArgumentException("Character '" + c + "' (" + (int)c + ") at position " + i + " is invalid for an operator!\nOperator name: \"" + value + "\"", "value");
            }
        }

    }

}
