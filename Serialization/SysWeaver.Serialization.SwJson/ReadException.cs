using System;
using System.Text;

namespace SysWeaver.Serialization.SwJson
{
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

        public static void ThrowExpectedQuoatedString()
        {
            throw new Exception("Expected start of quoted string, '\"'");
        }

        public static void ThrowUnhandledUknownObject()
        {
            throw new Exception("Unknown objects are not handled ATM");
        }

        public static void ThrowUnhandledUknownArray()
        {
            throw new Exception("Unknown array are not handled ATM");
        }

        public static void ThrowUnexpectedCharacter()
        {
            throw new Exception("Unexpected character");
        }

        public static void ThrowOnlyAsciiInParameter(uint u)
        {
            throw new ArgumentException("Only ascii values are permitted in the until param, found: " + u + " '" + (Char)u + "'", "until");
        }

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

        public static void ThrowExpectedBoolean(ReadOnlySpan<Byte> d)
        {
            var val = Encoding.ASCII.GetString(d);
            throw new Exception("Expected a boolean value, found \"" + val + "\"");
        }

        public static void ThrowInteralError()
        {
            throw new Exception("Internal error!");
        }

    }

}
