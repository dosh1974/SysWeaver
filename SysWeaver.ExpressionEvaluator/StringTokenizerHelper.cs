using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SysWeaver.Parser
{

    /// <summary>
    /// Character classes of the <see cref="StringTokenizer{T}"/>
    /// </summary>
    public static class StringTokenizerHelper
    {

        static readonly IReadOnlySet<Char> ExtraValidFirst = ReadOnlyData.Set("@_".ToCharArray());
        static readonly IReadOnlySet<Char> ExtraValid = ReadOnlyData.Set("_".ToCharArray());


        /// <summary>
        /// Check if a character can start an identifier: a letter, '@' or '_'
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>True if valid</returns>
        public static bool IsValidIdentiferCharFirst(Char c)
        {
            return Char.IsLetter(c) || ExtraValidFirst.Contains(c);
        }

        /// <summary>
        /// Check if a character can be part of an identifier (after the first character): a letter, a digit or '_'
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>True if valid</returns>
        public static bool IsValidIdentiferCharRest(Char c)
        {
            return Char.IsLetterOrDigit(c) || ExtraValid.Contains(c);
        }
        /// <summary>
        /// Check if a character can be part of an operator: anything except letters, digits, '_' and whitespaces
        /// </summary>
        /// <param name="c">The character</param>
        /// <returns>True if valid</returns>
        public static bool IsValidOperatorChar(Char c)
        {
            return !(IsValidIdentiferCharRest(c) || Char.IsWhiteSpace(c));
        }

        /// <summary>
        /// Validate an identifier name
        /// </summary>
        /// <param name="value">The identifier</param>
        /// <param name="argumentNameN">The parameter name to use in the exception, null for "value"</param>
        /// <exception cref="ArgumentException">The identifier is null, empty or contains an invalid character</exception>
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

        /// <summary>
        /// Validate an operator
        /// </summary>
        /// <param name="value">The operator</param>
        /// <exception cref="ArgumentException">The operator is null, empty or contains an invalid character</exception>
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
