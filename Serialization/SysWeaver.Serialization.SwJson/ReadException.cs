using System;
using System.Text;

namespace SysWeaver.Serialization.SwJson
{
    /// <summary>
    /// Throw helpers for json parse errors (kept out of line so that the parsing code stays small).
    /// All methods always throw, an <see cref="Exception"/> with a description of what was expected (<see cref="ArgumentException"/> for <see cref="ThrowOnlyAsciiInParameter(uint)"/>).
    /// <see cref="JsonReader"/> wraps these in an exception that describes the position in the json.
    /// </summary>
    static class ReadException
    {
        public static void ThrowObjectOpener()
        {
            throw new Exception("Expected an object opener, '{'");
        }

        public static void ThrowExpectedObject()
        {
            throw new Exception("Expected object data");
        }

        public static void ThrowExpectedKeyValueSeparator()
        {
            throw new Exception("Expected a key-value separator, ':'");
        }

        public static void ThrowExpectedTypename()
        {
            throw new Exception("Expected a typename string");
        }

        public static void ThrowExpectedValueSeparator()
        {
            throw new Exception("Expected a value separator, ','");
        }

        public static void ThrowExpectedValue()
        {
            throw new Exception("Expected a value");
        }

        public static void ThrowExpectedEndOfObject()
        {
            throw new Exception("Expected end of object, '}'");
        }

        public static void ThrowExpectedArrayFoundObject()
        {
            throw new Exception("Expected an array but an object was found");
        }

        public static void ThrowExpectedBoxedValue()
        {
            throw new Exception("Expected \"$value\" or \"$values\"");
        }

        public static void ThrowArrayOpener()
        {
            throw new Exception("Expected an array opener, '['");
        }

        public static void ThrowExpectedArray()
        {
            throw new Exception("Expected array data");
        }

        public static void ThrowExpectedEndOfArray()
        {
            throw new Exception("Expected end of array, ']'");
        }

        /// <summary>
        /// Throw: a '"' was expected (the name is misspelled, kept for compatibility with callers).
        /// </summary>
        public static void ThrowExpectedQuoatedString()
        {
            throw new Exception("Expected start of quoted string, '\"'");
        }

        /// <summary>
        /// Throw: an unknown member has an object value, skipping those isn't supported.
        /// </summary>
        public static void ThrowUnhandledUknownObject()
        {
            throw new Exception("Unknown objects are not handled ATM");
        }

        /// <summary>
        /// Throw: an unknown member has an array value, skipping those isn't supported.
        /// </summary>
        public static void ThrowUnhandledUknownArray()
        {
            throw new Exception("Unknown array are not handled ATM");
        }

        public static void ThrowUnexpectedCharacter()
        {
            throw new Exception("Unexpected character");
        }

        /// <summary>
        /// Throw an <see cref="ArgumentException"/>: an end char parameter must be ASCII (only checked in DEBUG builds).
        /// </summary>
        public static void ThrowOnlyAsciiInParameter(uint u)
        {
            throw new ArgumentException("Only ascii values are permitted in the until param, found: " + u + " '" + (Char)u + "'", "until");
        }

        /// <summary>
        /// Throw: the end of the data was reached before the expected char <paramref name="u"/>.
        /// </summary>
        public static void ThrowEndOfData(uint u)
        {
            throw new Exception("Unexpected end of data found, expected: " + u + " '" + (Char)u + "'");
        }

        public static void ThrowEndOfData()
        {
            throw new Exception("Unexpected end of data found");
        }

        public static void ThrowEndOfDataUtf8()
        {
            throw new Exception("Unexpected end of data found while parsing Utf8 multi byte char");
        }

        public static void ThrowEndOfDataEscape()
        {
            throw new Exception("Expected more data after escape sequence begun");
        }

        public static void ThrowInvalidBase64Char(uint u)
        {
            throw new Exception("Invalid char in base64 found: " + u + " '" + (Char)u + "'");
        }

        public static void ThrowInvalidBase64Length(int l)
        {
            throw new Exception("Not a valid base64 length: " + l);
        }

        public static void ThrowExpectedEndOfBlockComment()
        {
            throw new Exception("Expected end of block comment \"*/\"");
        }

        public static void ThrowInvalidHexChar(uint u)
        {
            throw new Exception("Invalid char in hex data found: " + u + " '" + (Char)u + "'");
        }

        public static void ThrowInvalidEscapeChar(uint u)
        {
            throw new Exception("Invalid char in escape sequence found: " + u + " '" + (Char)u + "'");
        }

        public static void ThrowExpectedNumberChar(uint u)
        {
            throw new Exception("Invalid char in a number found: " + u + " '" + (Char)u + "'");
        }

        /// <summary>
        /// Throw: the value isn't a boolean, the message includes the value (decoded as ASCII).
        /// </summary>
        public static void ThrowExpectedBoolean(ReadOnlySpan<Byte> d)
        {
            var val = Encoding.ASCII.GetString(d);
            throw new Exception("Expected a boolean value, found \"" + val + "\"");
        }

        /// <summary>
        /// Throw: the type named by "$type" can't be created (a reference type without a public parameterless constructor).
        /// </summary>
        public static void ThrowCantCreate(Type t)
        {
            throw new Exception("The type \"" + t.CleanTypename() + "\" named by \"$type\" can't be created, it has no public parameterless constructor");
        }

        /// <summary>
        /// Throw: an internal error (unexpected state).
        /// </summary>
        public static void ThrowInteralError()
        {
            throw new Exception("Internal error!");
        }

    }

}
