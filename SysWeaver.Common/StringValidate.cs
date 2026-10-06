using System;
using System.Globalization;
using System.Text;

namespace SysWeaver
{

    /// <summary>
    /// The type of a domain name, as detected by <see cref="StringValidate.DomainName(string)"/>
    /// </summary>
    public enum DomainTypes
    {
        /// <summary>
        /// A NetBIOS (windows) computer name (no '.' or ':')
        /// </summary>
        ComputerName,
        /// <summary>
        /// A DNS domain name (contains a '.' and the last part isn't numeric)
        /// </summary>
        DnsName,
        /// <summary>
        /// An IPv4 address (the part after the last '.' is numeric)
        /// </summary>
        IPv4,
        /// <summary>
        /// An IPv6 address (contains a ':' but no '.')
        /// </summary>
        IPv6,
    }


    /// <summary>
    /// Validation of common string formats (computer names, DNS names, IP addresses, emails and numbers).
    /// All methods throws an <see cref="Exception"/> with a human readable message if the input is invalid.
    /// The validation is a simplified (not RFC complete) syntax check.
    /// </summary>
    public static class StringValidate
    {

        /// <summary>
        /// Chars that are not allowed in a NetBIOS computer name
        /// </summary>
        static readonly Char[] InvalidComputerNameChars = "\\/:*?\"<>|.".ToCharArray();

        /// <summary>
        /// Validate that the input is valid for a NetBIOS computer name (windows): at most 15 chars, no white spaces and none of \ / : * ? " &lt; &gt; | .
        /// </summary>
        /// <param name="name">The string to test (an empty string is accepted)</param>
        /// <exception cref="Exception">The name is null or invalid</exception>
        public static void ComputerName(String name)
        {
            if (name == null)
                throw new Exception("A computer name may not be null");
            var l = name.Length;
            if (name.Trim().Length != l)
                throw new Exception("A computer name may not not start or end with a whitespace");
            if (l > 15)
                throw new Exception("A computer name may not exceed 15 chars in length");
            var ii = name.IndexOfAny(InvalidComputerNameChars);
            if (ii >= 0)
                throw new Exception("A computer name may not contain a '" + name[ii] + "'");
            foreach (var x in name)
                if (Char.IsWhiteSpace(x))
                    throw new Exception("A computer name may not contain whitespaces");
        }

        /// <summary>
        /// Validate that the input is valid for a DNS domain name: 2 - 255 bytes (UTF-8), parts separated by '.', every part is non empty and only contains letters (any unicode letter), digits or '-',
        /// may not start with a '-' or end with a '-' or '.'
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception">The name is null or invalid</exception>
        /// <exception cref="IndexOutOfRangeException">The name contains non ASCII chars (the UTF-8 byte count is used as the char count)</exception>
        public static void DnsName(String name)
        {
            if (name == null)
                throw new Exception("A DNS name may not be null");
            var l = name.Length;
            if (name.Trim().Length != l)
                throw new Exception("A DNS name may not start or end with a whitespace");
            l = Encoding.UTF8.GetByteCount(name);
            if (l < 2)
                throw new Exception("A DNS name must be at least 2 characters in length");
            if (l > 255)
                throw new Exception("A DNS name may not exceed 255 bytes in length");
            if (name[0] == '-')
                throw new Exception("A DNS name may not start with a '-'");
            var last = name[l - 1];
            switch (last)
            {
                case '-':
                case '.':
                    throw new Exception("A DNS name may not end with a '" + last + "'");
            }
            var t = name.Split('.');
            var tl = t.Length;
            for (int i = 0; i < tl; ++i)
            {
                var p = t[i];
                if (p.Length == 0)
                    throw new Exception("A DNS name part may not be empty");
                foreach (var c in p)
                {
                    if (Char.IsLetterOrDigit(c))
                        continue;
                    if (c != '-')
                        throw new Exception("A DNS name may not contain a '" + c + "'");

                }
            }
        }

        /// <summary>
        /// Validate that the input is valid for an IPv4 address (four decimal numbers 0 - 255 separated by '.')
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception">The address is null or invalid</exception>
        public static void IpV4(String name)
        {
            if (name == null)
                throw new Exception("An IPv4 address may not be null");
            var l = name.Length;
            if (name.Trim().Length != l)
                throw new Exception("An IPv4 address may not start or end with a whitespace");
            var t = name.Split('.');
            if (t.Length != 4)
                throw new Exception("An IPv4 address must be of the XX.XX.XX.XX format");
            for (int i = 0; i < 4; ++i)
                Numeric(t[i], "An IPv4 address part ", 0, 255);
        }

        /// <summary>
        /// Validate that the input is valid for an IPv6 address, with an optional "/prefix length" (1 - 128).
        /// The address must have 2 - 8 parts separated by ':', where every non empty part must be a DECIMAL number 0 - 65535
        /// (so addresses with hex digits a - f, like "fe80::1", are rejected).
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception">The address is null or invalid</exception>
        public static void IpV6(String name)
        {
            if (name == null)
                throw new Exception("An IPv6 address may not be null");
            var l = name.Length;
            if (name.Trim().Length != l)
                throw new Exception("An IPv6 address may not start or end with a whitespace");
            var x = name.LastIndexOf('/');
            if (x >= 0)
            {
                if (name.IndexOf('/') != x)
                    throw new Exception("An IPv6 address may only contain one '/'");
                Numeric(name.Substring(x + 1), "The prefix length of an IPv6 address ", 1, 128);
                name = name.Substring(0, x);
            }
            var p = name.Split(':');
            var pl = p.Length;
            if (pl < 2)
                throw new Exception("An IPv6 address must contain at least two ':'");
            if (pl > 8)
                throw new Exception("An IPv6 address must contain at most eight ':'");
            for (int i = 0; i < pl; ++i)
            {
                var part = p[i];
                if (part.Length == 0)
                    continue;
                Numeric(part, "An IPv6 address part ", 0, 65535);
            }
        }

        /// <summary>
        /// Validate that the input is valid for a Domain name (IPv4, IPv6 address, DNS or Computer name).
        /// The type is detected from the format: no '.' and no ':' is a computer name, no '.' but a ':' is an IPv6 address,
        /// a numeric last part (after the last '.') is an IPv4 address, else it's a DNS name. The detected type is then validated.
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <returns>The type of domain</returns>
        /// <exception cref="Exception">The name is null, empty or invalid (for the detected type)</exception>
        public static DomainTypes DomainName(String name)
        {
            if (name == null)
                throw new Exception("A domain name may not be null");
            var l = name.Length;
            if (name.Trim().Length != l)
                throw new Exception("A domain name may not start or end with a whitespace");
            if (l <= 0)
                throw new Exception("A domain name may not be empty");

            var p = name.LastIndexOf('.');
            if (p < 0)
            {
                p = name.IndexOf(':');
                if (p < 0)
                {
                    try
                    {
                        ComputerName(name);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Domain name is a NetBIOS computer name: " + ex.Message);
                    }
                    return DomainTypes.ComputerName;
                }
                try
                {
                    IpV6(name);
                }
                catch (Exception ex)
                {
                    throw new Exception("Domain name is an IPv6 address: " + ex.Message);
                }
                return DomainTypes.IPv6;
            }
            ++p;
            if (p >= l)
                throw new Exception("A domain name may not end in a '.'");
            bool isNumeric = true;
            while (p < l)
            {
                isNumeric &= Char.IsAsciiDigit(name[p]);
                if (!isNumeric)
                    break;
                ++p;
            }
            if (isNumeric)
            {
                try
                {
                    IpV4(name);
                }
                catch (Exception ex)
                {
                    throw new Exception("Domain name is an IPv4 address: " + ex.Message);
                }
                return DomainTypes.IPv4;
            }
            try
            {
                DnsName(name);
            }
            catch (Exception ex)
            {
                throw new Exception("Domain name is a DNS name: " + ex.Message);
            }
            return DomainTypes.DnsName;
        }

        /// <summary>
        /// Validate that the input is valid for an email (name@domainname): 3 - 254 chars, exactly one '@', a non empty name and a valid domain name (see <see cref="DomainName(string)"/>).
        /// The name part is not validated (except that it may not end with a white space).
        /// </summary>
        /// <param name="email">The string to test</param>
        /// <returns>The type of the domain name part</returns>
        /// <exception cref="Exception">The email is null or invalid</exception>
        public static DomainTypes Email(String email)
        {
            if (email == null)
                throw new Exception("An email address may not be null");
            var l = email.Length;
            if (email.Trim().Length != l)
                throw new Exception("A email address may not start or end with a whitespace");
            if (l < 3)
                throw new Exception("An email address must be atleast 3 chars in length");
            if (l > 254)
                throw new Exception("An email address may not be exceed 254 chars in length");
            var i = email.IndexOf('@');
            if (i < 0)
                throw new Exception("An email address must contain an '@' symbol");
            if (i == 0)
                throw new Exception("An email address must have a name before the '@' symbol");
            if (Char.IsWhiteSpace(email[i - 1]))
                throw new Exception("The name part of an email address may not end in a white space");
            ++i;
            if (i >= l)
                throw new Exception("An email address must have a domain name after the '@' symbol");
            if (email.IndexOf('@', i) >= 0)
                throw new Exception("An email may not contain multiple '@' symbols");
            try
            {
                return DomainName(email.Substring(i));
            }
            catch (Exception ex)
            {
                throw new Exception("The domain name part of the email address was invalid: " + ex.Message);
            }
        }


        /// <summary>
        /// Validate that a string only contains decimal digits ('0' - '9') and optionally is within some interval
        /// </summary>
        /// <param name="s">The string to test</param>
        /// <param name="errPrefix">A prefix to add to any exception texts</param>
        /// <param name="min">An optional minimum allowed value (inclusive)</param>
        /// <param name="max">An optional maximum allowed value (inclusive)</param>
        /// <exception cref="Exception">The string is null, empty, contains a non digit or is out of range</exception>
        /// <exception cref="OverflowException">The value doesn't fit in an <see cref="int"/></exception>
        public static void Numeric(String s, String errPrefix, int? min = null, int? max = null)
        {
            if (s == null)
                throw new Exception(errPrefix + "may not be null");
            var l = s.Length;
            if (s.Trim().Length != l)
                throw new Exception(errPrefix + "may not start or end with a whitespace");
            if (l <= 0)
                throw new Exception(errPrefix + "may not be empty");
            if (!s.IsNumeric(false))
                throw new Exception(errPrefix + "may only contain digits");
            var val = int.Parse(s);
            if (min != null)
            {
                var m = min ?? 0;
                if (val < m)
                    throw new Exception(errPrefix + "may not be less than " + m);
            }
            if (max != null)
            {
                var m = max ?? 0;
                if (val > m)
                    throw new Exception(errPrefix + "may not be greater than " + m);
            }
        }


        /// <summary>
        /// Validate that a string only contains hexadecimal digits ('0' - '9', 'a' - 'f', 'A' - 'F') and optionally is within some interval.
        /// Note: the max check is currently broken (it checks val &lt; max), so <paramref name="max"/> only rejects values below it.
        /// </summary>
        /// <param name="s">The string to test</param>
        /// <param name="errPrefix">A prefix to add to any exception texts</param>
        /// <param name="min">An optional minimum allowed value (inclusive)</param>
        /// <param name="max">An optional maximum allowed value (inclusive)</param>
        /// <exception cref="Exception">The string is null, empty, contains a non hex digit or is out of range</exception>
        /// <exception cref="OverflowException">The value doesn't fit in 32 bits (more than 8 significant digits), values with 8 digits and the top bit set are parsed as negative numbers</exception>
        public static void Hex(String s, String errPrefix, int? min = null, int? max = null)
        {
            if (s == null)
                throw new Exception(errPrefix + "may not be null");
            var l = s.Length;
            if (s.Trim().Length != l)
                throw new Exception(errPrefix + "may not start or end with a whitespace");
            if (l <= 0)
                throw new Exception(errPrefix + "may not be empty");
            if (!s.IsHex(false))
                throw new Exception(errPrefix + "may only contain digits");
            var val = int.Parse(s, NumberStyles.HexNumber);
            if (min != null)
            {
                var m = min ?? 0;
                if (val < m)
                    throw new Exception(errPrefix + "may not be less than " + m);
            }
            if (max != null)
            {
                var m = max ?? 0;
                if (val < m)
                    throw new Exception(errPrefix + "may not be greater than " + m);
            }
        }

    }

}
