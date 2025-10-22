using System;
using System.Globalization;
using System.Text;

namespace SysWeaver
{

    public enum DomainTypes
    {
        ComputerName,
        DnsName,
        IPv4,
        IPv6,
    }


    public static class StringValidate
    {

        static readonly Char[] InvalidComputerNameChars = "\\/:*?\"<>|.".ToCharArray();

        /// <summary>
        /// Validate that the input is valid for a NetBIOS computer name (windows)
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception"></exception>
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
        /// Validate that the input is valid for a DNS domain name 
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception"></exception>
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
        /// Validate that the input is valid for an IPv4 address
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception"></exception>
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
        /// Validate that the input is valid for an IPv6 address
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <exception cref="Exception"></exception>
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
        /// </summary>
        /// <param name="name">The string to test</param>
        /// <returns>The type of domain</returns>
        /// <exception cref="Exception"></exception>
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
        /// Validate that the input is valid for an email (name@domainname)
        /// </summary>
        /// <param name="email">The string to test</param>
        /// <returns>The type of domain</returns>
        /// <exception cref="Exception"></exception>
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
        /// Validate that a string only contains numeric and optionally is within some interval
        /// </summary>
        /// <param name="s">The string to test</param>
        /// <param name="errPrefix">A prefix to add to any exception texts</param>
        /// <param name="min">An optional minimum allowed value</param>
        /// <param name="max">An optional maximum allowed value</param>
        /// <exception cref="Exception"></exception>
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
        /// Validate that a string only contains numeric and optionally is within some interval
        /// </summary>
        /// <param name="s">The string to test</param>
        /// <param name="errPrefix">A prefix to add to any exception texts</param>
        /// <param name="min">An optional minimum allowed value</param>
        /// <param name="max">An optional maximum allowed value</param>
        /// <exception cref="Exception"></exception>
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
