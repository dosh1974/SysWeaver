using System;
using System.Collections.Generic;
using System.Net;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace SysWeaver
{

    /// <summary>
    /// Class that takes some data and represents it as a compact string.
    /// </summary>
    public sealed class CompactAsciiString
    {

        public static IReadOnlySet<Char> InvalidDefaults = ReadOnlyData.Set("\\/'\"´`|%_".ToCharArray());

        /// <summary>
        /// Should contain chars that doesn't expand "in transit" (such as serialized json strings, sql requests etc).
        /// </summary>
        public static CompactAsciiString Default = new CompactAsciiString();

        /// <summary>
        /// Should only contain chars that can be used "everywhere", uri's, xml/html attributes and values, js strings etc.
        /// </summary>
        public static CompactAsciiString Secure = new CompactAsciiString(GetSuperSafe());

        static HashSet<Char> GetSuperSafe()
        {
            var c = new HashSet<char>();
            c.Add(' ');
            var a = new XAttribute("n", "a");
            var e = new XElement("n", "a");
            for (int i = 33; i < 128; ++ i)
            {
                var t = (Char)i;
                var ts = "" + t;
                c.Add(t);
                if (Uri.EscapeDataString(ts) != ts)
                    continue;
                if (WebUtility.HtmlEncode(ts) != ts)
                    continue;
                if (WebUtility.UrlEncode(ts) != ts)
                    continue;
                if (SecurityElement.Escape(ts) != ts)
                    continue;
                a.SetValue(ts);
                if (a.Value != ts)
                    continue;
                e.SetValue(ts);
                if (e.Value != ts)
                    continue;
                c.Remove(t);
            }
            foreach (var x in InvalidDefaults)
                c.Add(x);
            return c;
        }

        public CompactAsciiString(IReadOnlySet<Char> invalid = null)
        {
            invalid = invalid ?? InvalidDefaults;
            List<Char> valid = new List<char>(128);
            Dictionary<Char, uint> vals = new Dictionary<char, uint>();
            for (int i = 33; i < 128; ++ i)
            {
                var c = (Char)i;
                if (invalid.Contains(c))
                    continue;
                vals.Add(c, (uint)valid.Count);
                valid.Add(c);
            }
            Valid = valid.ToArray();
            Values = vals.Freeze();
        }

#if DEBUG

        static CompactAsciiString()
        {
            var t = Default;
            if (t.DecodeUInt64(t.Encode(UInt64.MinValue)) != UInt64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt64(t.Encode(UInt64.MaxValue)) != UInt64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MinValue)) != UInt32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MaxValue)) != UInt32.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MinValue)) != Int64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MaxValue)) != Int64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MinValue)) != Int32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MaxValue)) != Int32.MaxValue)
                throw new Exception("Internal error!");


            t = Secure;
            if (t.DecodeUInt64(t.Encode(UInt64.MinValue)) != UInt64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt64(t.Encode(UInt64.MaxValue)) != UInt64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MinValue)) != UInt32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeUInt32(t.Encode(UInt32.MaxValue)) != UInt32.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MinValue)) != Int64.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt64(t.Encode(Int64.MaxValue)) != Int64.MaxValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MinValue)) != Int32.MinValue)
                throw new Exception("Internal error!");
            if (t.DecodeInt32(t.Encode(Int32.MaxValue)) != Int32.MaxValue)
                throw new Exception("Internal error!");

        }




#endif//DEBUG

        /// <summary>
        /// Encode an 64-bit signed integer.
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>The compact string that represents it</returns>
        public String Encode(Int64 value)
            => Encode((UInt64)value);

        /// <summary>
        /// Encode an 64-bit unsiged integer.
        /// </summary>
        /// <param name="value">The value to encode</param>
        /// <returns>The compact string that represents it</returns>
        public String Encode(UInt64 value)
        {
            var v = Valid;
            var vl = (UInt32)Valid.Count;
            Span<Char> temp = stackalloc char[32];
            for (int i = 0; ;)
            {
                var n = value / vl;
                var vi = value - (n * vl);
                temp[i] = v[(int)vi];
                ++i;
                if (n == 0)
                    return new string(temp.Slice(0, i));
                value = n;
            }
        }

        /// <summary>
        /// Decode a compact string to the 64-bit signed integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        public Int64 DecodeInt64(String compactString)
            => (Int64)DecodeUInt64(compactString);

        /// <summary>
        /// Decode a compact string to the 32-bit signed integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        public Int32 DecodeInt32(String compactString)
            => (Int32)DecodeUInt32(compactString);

        /// <summary>
        /// Decode a compact string to the 64-bit unsigned integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        public UInt64 DecodeUInt64(String compactString)
        {
            var v = Values;
            var vl = (UInt32)v.Count;
            var l = compactString.Length;
            UInt64 r = 0;
            while (l > 0)
            {
                --l;
                r *= vl;
                r += v[compactString[l]];
            }
            return r;
        }

        /// <summary>
        /// Decode a compact string to the 32-bit unsigned integer that it represents.
        /// </summary>
        /// <param name="compactString">The compact value representation</param>
        /// <returns>The value that was represented by the string</returns>
        public UInt64 DecodeUInt32(String compactString)
        {
            var v = Values;
            var vl = (UInt32)v.Count;
            var l = compactString.Length;
            UInt32 r = 0;
            while (l > 0)
            {
                --l;
                r *= vl;
                r += v[compactString[l]];
            }
            return r;
        }


        public readonly IReadOnlyList<Char> Valid;
        public readonly IReadOnlyDictionary<Char, uint> Values;


    }



}
