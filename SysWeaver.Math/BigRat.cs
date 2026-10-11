using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;


namespace SysWeaver
{


    /// <summary>
    /// Extension methods for <see cref="BigRat"/>.
    /// </summary>
    public static class BigRatExt
    {
        /// <summary>
        /// Randomly pass a test based on the probability (this value).
        /// The probability is converted to a 64 bit fraction (numerator and denominator halved until the denominator is below 2^62, the halved values are truncated to integers),
        /// and the test passes if a random value in [0, denominator) is below the numerator.
        /// </summary>
        /// <param name="probability">A probability [0, 1]</param>
        /// <param name="rng">The rng to use</param>
        /// <param name="usedProb">The actual probability used (may be a truncated value that differs from the desired probability)</param>
        /// <returns>True if the test passed (always false for a zero probability, always true for a probability of one or more)</returns>
        public static bool PassProbability64(this BigRat probability, SecureRng rng, out BigRat usedProb)
        {
            var x = probability.Normalize();
            var var = (BigRat)(Int64.MaxValue >> 1);
            var num = x.Numerator();
            var denom = x.Denominator();
            while (denom >= var)
            {
                denom /= 2;
                num /= 2;
            }
            var inum = (UInt64)num;
            var idenom = (UInt64)denom;
            var val = rng.GetUInt64Max(idenom);
            usedProb = new BigRat((long)inum, idenom);
            return val < inum;
        }

    }

    /// <summary>
    /// An arbitrary precision rational number (a big integer numerator and denominator), implementing the .NET generic math interfaces.
    /// The "new" version from:
    /// https://github.com/c-ohle/RationalNumerics?tab=readme-ov-file
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is immutable, <c>default(BigRat)</c> is zero. Arithmetic (+, -, *, /, %) is exact, but results are not automatically reduced
    /// (ex: 2/4 stays 2/4, see <see cref="Normalize"/>), equality and comparisons are by value.
    /// There are no NaN or infinity values, operations that have no rational result throw instead (ex: division by zero).
    /// </para>
    /// <para>
    /// The functions with a <c>digits</c> parameter (<see cref="Sqrt(BigRat, int)"/>, <see cref="Log(BigRat, int)"/>, <see cref="Sin(BigRat, int)"/> etc) compute an approximation
    /// and round the result to that number of decimal digits (after the decimal point; <see cref="Exp(BigRat, int)"/> counts from the most significant digit instead).
    /// A negative value of <c>digits</c> computes with the same precision but returns the unrounded value.
    /// The overloads without <c>digits</c> use 32 digits.
    /// </para>
    /// <para>
    /// Note that <c>Log(x, 2)</c> binds to <see cref="Log(BigRat, int)"/> (natural logarithm with 2 digits), use <c>Log(x, (BigRat)2)</c> for a base 2 logarithm.
    /// </para>
    /// <para>
    /// The implicit conversions from <see cref="double"/>, <see cref="float"/> and <see cref="Half"/> are exact (the binary value, ex: 0.1 becomes 3602879701896397/36028797018963968,
    /// same as the <see cref="BigRat(double)"/> constructor), so they round trip; NaN and infinity throw an <see cref="OverflowException"/>.
    /// Parse the text of a value (ex: <c>BigRat.Parse("0.1")</c>) to get the decimal value.
    /// The explicit conversions to integer types truncate towards zero, the conversions to the 32 bit (and smaller) integer types go through <see cref="double"/>.
    /// The unchecked conversions don't throw if the value doesn't fit (the result is then undefined, it may wrap or saturate), the checked ones throw an <see cref="OverflowException"/>.
    /// </para>
    /// </remarks>
    [Serializable, SkipLocalsInit, DebuggerDisplay("{ToString(\"\"),nq}")]
    public unsafe readonly struct BigRat :
#if NET7_0_OR_GREATER
      INumber<BigRat>, ISignedNumber<BigRat>, IPowerFunctions<BigRat>, IRootFunctions<BigRat>,
      IExponentialFunctions<BigRat>, ILogarithmicFunctions<BigRat>, ISpanParsable<BigRat>,
      ITrigonometricFunctions<BigRat>, IHyperbolicFunctions<BigRat>,
#endif
      IEquatable<BigRat>, IComparable<BigRat>, IComparable, IFormattable, ISpanFormattable
    {
        #region Add on's
        /// <summary>
        /// Create a rational number from a numerator and a denominator (exact, not reduced).
        /// </summary>
        /// <param name="nom">The numerator.</param>
        /// <param name="denom">The denominator.</param>
        /// <exception cref="DivideByZeroException"><paramref name="denom"/> is zero.</exception>
        public BigRat(long nom, ulong denom) : this(((BigRat)nom).p)
        {
            if (denom == 0)
                throw new DivideByZeroException();
            if (denom > 1)
                this /= denom;
        }

        /// <summary>
        /// The value zero (same as <c>default(BigRat)</c>).
        /// </summary>
        public static readonly BigRat Zero = new BigRat(0);
        /// <summary>
        /// The value one.
        /// </summary>
        public static readonly BigRat One = new BigRat(1);

        #endregion//Add on's



        public static implicit operator BigRat(byte value)
        {
            return (uint)value;
        }
        public static implicit operator BigRat(sbyte value)
        {
            return (int)value;
        }
        public static implicit operator BigRat(char value)
        {
            return (uint)value;
        }
        public static implicit operator BigRat(ushort value)
        {
            return (uint)value;
        }
        public static implicit operator BigRat(short value)
        {
            return (int)value;
        }
        public static implicit operator BigRat(int value)
        {
            return new BigRat(value != 0 ? new uint[4] {
        unchecked((uint)value & 0x80000000) | 1, uabs(value), 1, 1 } : null);
        }
        public static implicit operator BigRat(uint value)
        {
            return new BigRat(value != 0 ? new uint[4] { 1, value, 1, 1 } : null);
        }
        public static implicit operator BigRat(long value)
        {
            var t = value >> 63; var p = (BigRat)unchecked((ulong)((value ^ t) - t));
            if (p.p != null) p.p[0] |= unchecked((uint)(value >> 32)) & 0x80000000; return p;
        }
        public static implicit operator BigRat(ulong value)
        {
            return (value >> 32) == 0 ? (uint)value :
              new BigRat(new uint[] { 2, (uint)value, (uint)(value >> 32), 1, 1 });
        }
        public static implicit operator BigRat(nint value)
        {
            return (long)value;
        }
        public static implicit operator BigRat(nuint value)
        {
            return (ulong)value;
        }
        public static implicit operator BigRat(Half value)
        {
            return (double)value;
        }
        public static implicit operator BigRat(float value)
        {
            return (double)value;
        }
        /// <summary>
        /// Convert a double exactly (the binary value, same as <see cref="BigRat(double)"/>, ex: 0.1 becomes 3602879701896397/36028797018963968),
        /// so converting back to a double gives the same value. Use <c>BigRat.Parse(value.ToString("R"))</c> or <see cref="Round(BigRat, int)"/> to get a "decimal" value (0.1 = 1/10).
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <exception cref="OverflowException"><paramref name="value"/> is NaN or infinity.</exception>
        public static implicit operator BigRat(double value)
        {
            return new BigRat(value);
        }
        public static implicit operator BigRat(decimal value)
        {
            var b = (uint*)&value; uint f = b[0], e = (f >> 16) & 0xff; //0..28 
            uint* p = stackalloc uint[9];
            *(ulong*)&p[1] = *(ulong*)&b[2]; p[3] = b[1]; p[0] = p[3] != 0 ? 3u : p[2] != 0 ? 2u : 1;
            if (*(ulong*)p == 1) return default; // zero
            else
            {
                var s = p + (p[0] + 1);
                if (e == 0) { *(ulong*)s = 0x100000001; }
                else
                {
                    var t = (ulong*)(s + 1); var h = e < 19 ? e : 19;
                    t[0] = (ulong)Math.Pow(10, h);
                    t[1] = h == e ? 0 : Math.BigMul(t[0], (ulong)Math.Pow(10, e - h), out t[0]);
                    s[0] = s[3] != 0 ? 3u : s[2] != 0 ? 2u : 1;
                }
                p[0] |= f & 0x80000000;
            }
            return new BigRat(p, 2 + (f = p[0] & 0x3fffffff) + p[f + 1]);
        }
        public static implicit operator BigRat(BigInteger value)
        {
            var si = value.Sign; if (si == 0) return default;
            if (si < 0) value = -value; int n = value.GetByteCount(true), c = ((n - 1) >> 2) + 1;
            Span<uint> s = c + 3 <= maxstack ? stackalloc uint[c + 3] : heap(c + 3);
            fixed (uint* p = s)
            {
                p[c] = 0; var ok = value.TryWriteBytes(new Span<byte>(p + 1, n), out _, true);
                if (p[c] == 0) c--; Debug.Assert(ok && p[c] != 0); //todo: report BigInteger bug p[c] == 0
                p[0] = unchecked((uint)c) | ((uint)si & 0x80000000); *(ulong*)(p + c + 1) = 0x100000001;
                return new BigRat(p, unchecked((uint)c) + 3);
            }
        }

        public static explicit operator byte(BigRat value)
        {
            return (byte)(double)value;
        }
        public static explicit operator sbyte(BigRat value)
        {
            return (sbyte)(double)value;
        }
        public static explicit operator char(BigRat value)
        {
            return (char)(double)value;
        }
        public static explicit operator short(BigRat value)
        {
            return (short)(double)value;
        }
        public static explicit operator ushort(BigRat value)
        {
            return (ushort)(double)value;
        }
        public static explicit operator int(BigRat value)
        {
            return (int)(double)value; //todo opt.
        }
        public static explicit operator uint(BigRat value)
        {
            return (uint)(double)value;
        }
        public static explicit operator long(BigRat value)
        {
            long t; toi(value, (uint*)&t, 2); return t;
        }
        public static explicit operator ulong(BigRat value)
        {
            ulong t; toi(value, (uint*)&t, 2 | 0x10000); return t;
        }
        public static explicit operator nint(BigRat value)
        {
            return (nint)(long)value;
        }
        public static explicit operator nuint(BigRat value)
        {
            return (nuint)(ulong)value;
        }
        public static explicit operator Half(BigRat value)
        {
            return (Half)(double)value;
        }
        public static explicit operator float(BigRat value)
        {
            return (float)(double)value;
        }
        /// <summary>
        /// Convert to the nearest double (approximately, the last bit may differ), infinity if the value is too large.
        /// Values converted from a double (or float / Half) convert back to exactly the same value (incl. subnormals and <see cref="double.MaxValue"/>).
        /// </summary>
        /// <param name="value">The value.</param>
        public static explicit operator double(BigRat value)
        {
            if (value.p == null) return 0;
            fixed (uint* a = value.p)
            {
                var na = a[0] & 0x3fffffff; var b = a + na + 1; var nb = b[0];
                var ca = BitOperations.LeadingZeroCount(a[na]);
                var cb = BitOperations.LeadingZeroCount(b[nb]);
                var va = ((ulong)a[na] << 32 + ca) | (na < 2 ? 0 : ((ulong)a[na - 1] << ca) | (na < 3 ? 0 : (ulong)a[na - 2] >> 32 - ca));
                var vb = ((ulong)b[nb] << 32 + cb) | (nb < 2 ? 0 : ((ulong)b[nb - 1] << cb) | (nb < 3 ? 0 : (ulong)b[nb - 2] >> 32 - cb));
                var e = ((na << 5) - ca) - ((nb << 5) - cb); Debug.Assert(vb != 0);
                var r = (double)(va >> 11) / (vb >> 11);
                if (e < -1021) { if (e >= -1074) *(ulong*)&r = (ulong)(r * (1L << ((int)e + 1074))) + (((*(ulong*)&r >> 52) & 1) ^ 1); else r = 0; }
                else if (e > +1023) r = double.PositiveInfinity;
                else { var p = (0x3ff + e) << 52; r *= *(double*)&p; }
              ((uint*)&r)[1] |= a[0] & 0x80000000; return r;
            }
        }
        public static explicit operator decimal(BigRat value)
        {
            return tom(value, false);
        }
        public static explicit operator BigInteger(BigRat value)
        {
            if (value.p == null) return default;
            var s = new ReadOnlySpan<uint>(Truncate(value).p);
            var a = MemoryMarshal.AsBytes(s.Slice(1, unchecked((int)(s[0] & 0x3fffffff))));
            var r = new BigInteger(a, true, false); if (value < 0) r = -r; return r;
        }

        public static explicit operator checked byte(BigRat value)
        {
            return checked((byte)(double)value);
        }
        public static explicit operator checked sbyte(BigRat value)
        {
            return checked((sbyte)(double)value);
        }
        public static explicit operator checked char(BigRat value)
        {
            return checked((char)(double)value);
        }
        public static explicit operator checked short(BigRat value)
        {
            return checked((short)(double)value);
        }
        public static explicit operator checked ushort(BigRat value)
        {
            return checked((ushort)(double)value);
        }
        public static explicit operator checked int(BigRat value)
        {
            return checked((int)(double)value);
        }
        public static explicit operator checked uint(BigRat value)
        {
            return checked((uint)(double)value);
        }
        public static explicit operator checked long(BigRat value)
        {
            long t; toi(value, (uint*)&t, 2 | 0x80000); return t;
        }
        public static explicit operator checked ulong(BigRat value)
        {
            ulong t; toi(value, (uint*)&t, 2 | 0x10000 | 0x80000); return t;
        }
        public static explicit operator checked nint(BigRat value)
        {
            nint t; toi(value, (uint*)&t, ((uint)sizeof(nint) >> 2) | 0x80000); return t;
        }
        public static explicit operator checked nuint(BigRat value)
        {
            nuint t; toi(value, (uint*)&t, ((uint)sizeof(nuint) >> 2) | 0x10000 | 0x80000); return t;
        }

        public static BigRat operator +(BigRat a)
        {
            return a;
        }
        public static BigRat operator -(BigRat a)
        {
            if (a.p == null) return default;
            fixed (uint* p = a.p) a = new BigRat(p, unchecked((uint)a.p.Length));
            a.p![0] ^= 0x80000000; return a;
        }
        public static BigRat operator +(BigRat a, BigRat b)
        {
            if (a.p == null) return b;
            if (b.p == null) return a;
            var n = (a.p.Length + b.p.Length) * 3; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = b.p, w = s) return add(u, v, w, 0);
        }
        public static BigRat operator -(BigRat a, BigRat b)
        {
            if (a.p == null) return -b;
            if (b.p == null) return a;
            var n = (a.p.Length + b.p.Length) * 3; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = b.p, w = s) return add(u, v, w, 0x80000000);
        }
        public static BigRat operator *(BigRat a, BigRat b)
        {
            if (a.p == null || b.p == null) return default;
            var n = a.p.Length + b.p.Length; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = b.p, w = s) return mul(u, v, w);
        }
        public static BigRat operator /(BigRat a, BigRat b)
        {
            if (b.p == null) throw new DivideByZeroException();
            if (a.p == null) return default;
            var n = a.p.Length + b.p.Length; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = b.p, w = s) return div(u, v, w);
        }
        public static BigRat operator %(BigRat a, BigRat b)
        {
            return a - Truncate(a / b) * b;
        }
        public static BigRat operator +(BigRat a, int b)
        {
            if (a.p == null) return b;
            if (b == 0) return a;
            var n = 4 + (a.p.Length + 4) * 3; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = s) { copy(v, b); return add(u, v, v + 4, 0); }
        }
        public static BigRat operator -(BigRat a, int b)
        {
            return b != int.MinValue ? a + -b : a - (BigRat)b;
        }
        public static BigRat operator *(BigRat a, int b)
        {
            if (a.p == null || b == 0) return default;
            var n = a.p.Length + 8; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = s) { copy(v, b); return mul(u, v, v + 4); }
        }
        public static BigRat operator /(BigRat a, int b)
        {
            if (b == 0) throw new DivideByZeroException();
            if (a.p == null) return default;
            var n = a.p.Length + 8; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = a.p, v = s) { copy(v, b); return div(u, v, v + 4); }
        }
        public static BigRat operator %(BigRat a, int b)
        {
            return a - Truncate(a / b) * b;
        }
        public static BigRat operator +(int a, BigRat b)
        {
            if (a == 0) return b;
            if (b.p == null) return a;
            var n = 4 + (4 + b.p.Length) * 3; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = s, v = b.p) { copy(u, a); return add(u, v, u + 4, 0); }
        }
        public static BigRat operator -(int a, BigRat b)
        {
            if (a == 0) return -b;
            if (b.p == null) return a;
            var n = 4 + (4 + b.p.Length) * 3; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = s, v = b.p) { copy(u, a); return add(u, v, u + 4, 0x80000000); }
        }
        public static BigRat operator *(int a, BigRat b)
        {
            if (a == 0 || b.p == null) return default;
            var n = 8 + b.p.Length; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = s, v = b.p) { copy(u, a); return mul(u, v, u + 4); }
        }
        public static BigRat operator /(int a, BigRat b)
        {
            if (b.p == null) throw new DivideByZeroException();
            if (a == 0) return default;
            var l = 8 + b.p.Length; Span<uint> s = l <= maxstack ? stackalloc uint[l] : heap(l);
            fixed (uint* u = s, v = b.p)
            {
                if (a == 1) // fast inv 1 / x
                {
                    var n = unchecked((uint)b.p.Length);
                    copy(u, v, n); inv(u); return new BigRat(u, n);
                }
                copy(u, a); return div(u, v, u + 4);
            }
        }
        public static BigRat operator ++(BigRat a)
        {
            return a + 1;
        }
        public static BigRat operator --(BigRat a)
        {
            return a - 1;
        }

        public static bool operator ==(BigRat a, BigRat b)
        {
            return a.CompareTo(b) == 0;
        }
        public static bool operator !=(BigRat a, BigRat b)
        {
            return a.CompareTo(b) != 0;
        }
        public static bool operator <=(BigRat a, BigRat b)
        {
            return a.CompareTo(b) <= 0;
        }
        public static bool operator >=(BigRat a, BigRat b)
        {
            return a.CompareTo(b) >= 0;
        }
        public static bool operator <(BigRat a, BigRat b)
        {
            return a.CompareTo(b) < 0;
        }
        public static bool operator >(BigRat a, BigRat b)
        {
            return a.CompareTo(b) > 0;
        }
        public static bool operator ==(BigRat a, int b)
        {
            return a.CompareTo(b) == 0;
        }
        public static bool operator !=(BigRat a, int b)
        {
            return a.CompareTo(b) != 0;
        }
        public static bool operator <=(BigRat a, int b)
        {
            return a.CompareTo(b) <= 0;
        }
        public static bool operator >=(BigRat a, int b)
        {
            return a.CompareTo(b) >= 0;
        }
        public static bool operator <(BigRat a, int b)
        {
            return a.CompareTo(b) < 0;
        }
        public static bool operator >(BigRat a, int b)
        {
            return a.CompareTo(b) > 0;
        }
        public static bool operator ==(int a, BigRat b)
        {
            return b.CompareTo(a) == 0;
        }
        public static bool operator !=(int a, BigRat b)
        {
            return b.CompareTo(a) != 0;
        }
        public static bool operator <=(int a, BigRat b)
        {
            return b.CompareTo(a) >= 0;
        }
        public static bool operator >=(int a, BigRat b)
        {
            return b.CompareTo(a) <= 0;
        }
        public static bool operator <(int a, BigRat b)
        {
            return b.CompareTo(a) > 0;
        }
        public static bool operator >(int a, BigRat b)
        {
            return b.CompareTo(a) < 0;
        }

        public static int Sign(BigRat value)
        {
            return value.p == null ? 0 : (value.p[0] & 0x80000000) != 0 ? -1 : +1;
        }
        /// <summary>
        /// Fast estimate of the base 2 exponent: the bit length of the numerator minus the bit length of the denominator.
        /// The result is floor(log2(|value|)) or one more.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The exponent estimate, -int.MaxValue for zero.</returns>
        public static int ILogB(BigRat value)
        {
            if (value.p == null) return -int.MaxValue; // int.MinValue; //-2147483648; //infinity
            fixed (uint* p = value.p)
            {
                var h = p[0]; var a = h & 0x3fffffff; var q = p + a + 1; var b = q[0];
                var u = (a << 5) - unchecked((uint)BitOperations.LeadingZeroCount(p[a]));
                var v = (b << 5) - unchecked((uint)BitOperations.LeadingZeroCount(q[b]));
                return unchecked((int)u - (int)v);
            }
        }
        /// <summary>
        /// Get the base 10 exponent, floor(log10(|value|)), ex: 999 =&gt; 2, 0.001 =&gt; -3.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The base 10 exponent, 0 for zero.</returns>
        public static int ILog10(BigRat value)
        {
            if (value.p == null) return 0;
            fixed (uint* p = value.p)
            {
                var np = p[0] & 0x3fffffff; var nq = p[np + 1]; var q = p + np + 1; // est log(2) / log(10)
                var mp = ((((long)np << 5) - BitOperations.LeadingZeroCount(p[np])) * 1292913986) >> 32;
                var mq = ((((long)nq << 5) - BitOperations.LeadingZeroCount(q[nq])) * 1292913986) >> 32;
                var ex = unchecked((int)(mp - mq)); var ne = 5 + (abs(ex) >> 3);
                var l = (ne + value.p.Length) << 1; Span<uint> s = l <= maxstack ? stackalloc uint[l] : heap(l);
                fixed (uint* u = s)
                {
                    uint* w = u + ne, z;
                    for (; ; )
                    {
                        pow10(u, uabs(ex));
                        if (ex < 0) { w[0] = mul(p + 1, np, u + 1, u[0], w + 1); mod(w, q, u); }
                        else { w[0] = mul(q + 1, nq, u + 1, u[0], w + 1); copy(z = w + w[0] + 1, p, np + 1); z[0] &= 0x3fffffff; mod(z, w, u); }
                        Debug.Assert(u[0] == 1);
                        if (u[1] == 00) { ex--; continue; }
                        if (u[1] >= 10) { ex++; continue; }
                        return ex;
                    }
                }
            }
        }
        public static BigRat Abs(BigRat a)
        {
            return Sign(a) >= 0 ? a : -a;
        }
        public static BigRat Min(BigRat x, BigRat y)
        {
            return x.CompareTo(y) <= 0 ? x : y;
        }
        public static BigRat Max(BigRat x, BigRat y)
        {
            return x.CompareTo(y) >= 0 ? x : y;
        }
        public static BigRat MaxMagnitude(BigRat x, BigRat y)
        {
            return Abs(x).CompareTo(Abs(y)) >= 0 ? x : y; //todo: acmp
        }
        public static BigRat MinMagnitude(BigRat x, BigRat y)
        {
            return Abs(x).CompareTo(Abs(y)) <= 0 ? x : y; //todo: acmp
        }
        public static BigRat MaxMagnitudeNumber(BigRat x, BigRat y)
        {
            return MaxMagnitude(x, y); // IEEE 754:2019 does not propagate NaN inputs back 
        }
        public static BigRat MinMagnitudeNumber(BigRat x, BigRat y)
        {
            return MinMagnitude(x, y); // IEEE 754:2019 does not propagate NaN inputs back 
        }
        public static BigRat CopySign(BigRat value, BigRat sign)
        {
            int a, b; return (a = Sign(value)) != 0 && (b = Sign(sign) < 0 ? -1 : +1) != 0 && a != b ? -value : value;
        }
        public static BigRat Clamp(BigRat value, BigRat min, BigRat max)
        {
            if (min > max) throw new ArgumentException("min must be less than or equal to max", nameof(min));
            return value < min ? min : value > max ? max : value;
        }
        /// <summary>
        /// Round towards zero to an integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The integer part of the value.</returns>
        public static BigRat Truncate(BigRat value)
        {
            return mod(value, 0);
        }
        /// <summary>
        /// Round down (towards negative infinity) to an integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The largest integer less than or equal to the value.</returns>
        public static BigRat Floor(BigRat value)
        {
            return mod(value, 1);
        }
        /// <summary>
        /// Round up (towards positive infinity) to an integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The smallest integer greater than or equal to the value.</returns>
        public static BigRat Ceiling(BigRat value)
        {
            return mod(value, 2);
        }
        /// <summary>
        /// Round to the nearest integer, midpoints are rounded to the nearest even integer (banker's rounding, ex: 2.5 =&gt; 2, 3.5 =&gt; 4).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The rounded value.</returns>
        public static BigRat Round(BigRat value)
        {
            return mod(value, 3);
        }
        /// <summary>
        /// Round to a number of decimal digits, midpoints are rounded to even (banker's rounding).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">The number of decimal digits after the decimal point, may be negative (ex: -2 rounds to hundreds).</param>
        /// <returns>The rounded value.</returns>
        public static BigRat Round(BigRat value, int digits)
        {
            //todo: inline see BigRat(double value)
            // if (value.p == null) return value; if(digits == 0) return Truncate(value);
            var e = Pow10(digits); value = Round(value * e) / e; return value;
        }
        public static BigRat Round(BigRat value, MidpointRounding mode)
        {
            return Round(value, 0, mode);
        }
        public static BigRat Round(BigRat value, int digits, MidpointRounding mode)
        {
            var p = Pow10(digits); value *= p;
            switch (mode)
            {
                case MidpointRounding.ToEven: value = Round(value); break;
                case MidpointRounding.ToZero: value = Truncate(value); break;
                case MidpointRounding.ToNegativeInfinity: value = Floor(value); break;
                case MidpointRounding.ToPositiveInfinity: value = Ceiling(value); break;
                case MidpointRounding.AwayFromZero:
                    var u = Truncate(value); var v = value - u;
                    if (Abs(v) >= new BigRat(1, 2)) u += Sign(v); value = u; // >= 0.5
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown rounding mode");
            }
            value /= p; return value;
        }
        /// <summary>
        /// Reduce the size of a value: if both the numerator and the denominator have more than <paramref name="bits"/> bits, both are shifted right
        /// until the smaller of them has <paramref name="bits"/> bits (an approximation that keeps about <paramref name="bits"/> significant bits).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="bits">The number of bits to keep.</param>
        /// <returns>The approximated value, zero if <paramref name="bits"/> is zero or negative.</returns>
        public static BigRat RoundB(BigRat value, int bits)
        {
            if (value.p == null || bits <= 0) return default;
            fixed (uint* p = value.p)
            {
                var h = p[0]; var a = h & 0x3fffffff; var q = p + a + 1; var b = q[0];
                var u = (a << 5) - unchecked((uint)BitOperations.LeadingZeroCount(p[a]));
                var v = (b << 5) - unchecked((uint)BitOperations.LeadingZeroCount(q[b]));
                if (u > v) u = v; if (u <= bits) return value;
                var n = value.p.Length; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
                fixed (uint* w = s)
                {
                    copy(w, p, unchecked((uint)n));
                    var t = (int)(u - bits); shr(w, t); shr(q = w + a + 1, t);
                    if (w[0] != a) copy(w + w[0] + 1, q, q[0] + 1);
                    var l = 2 + w[0] + w[w[0] + 1]; w[0] |= h & 0x80000000;
                    return new BigRat(w, l);
                }
            }
        }
        /// <summary>
        /// Raise a value to an integer power (exact).
        /// </summary>
        /// <param name="x">The value.</param>
        /// <param name="y">The exponent, may be negative.</param>
        /// <returns>x^y (1 if y is 0, also for x = 0).</returns>
        /// <exception cref="DivideByZeroException"><paramref name="x"/> is zero and <paramref name="y"/> is negative.</exception>
        public static BigRat Pow(BigRat x, int y)
        {
            if ((y < 0) && (x.p == null))
                throw new DivideByZeroException();
            var r = (BigRat)1u;
            for (var e = uabs(y); ; e >>= 1) { if ((e & 1) != 0) r *= x; if (e <= 1) break; x *= x; }
            if (y < 0) fixed (uint* p = r.p) inv(p); // if (y < 0) r = 1 / r;
            return r;
        }
        /// <summary>
        /// Raise a value to a rational power (approximation, computed as exp(y * log(x))).
        /// </summary>
        /// <param name="x">The value, must be zero or positive (use <see cref="Pow(BigRat, int)"/> for integer powers of negative values).</param>
        /// <param name="y">The exponent.</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>x^y, if <paramref name="x"/> is zero: 0 if y is positive and 1 if y is zero.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative.</exception>
        /// <exception cref="DivideByZeroException"><paramref name="x"/> is zero and <paramref name="y"/> is negative.</exception>
        public static BigRat Pow(BigRat x, BigRat y, int digits)
        {
            if (x.p == null)
            {
                var ys = Sign(y);
                if (ys < 0)
                    throw new DivideByZeroException();
                return ys == 0 ? 1u : default;
            }
            if (IsNegative(x))
                throw new ArgumentOutOfRangeException(nameof(x), "Negative values can't be raised to a rational power");
            var p = base2(abs(digits)); x = exp(y * log(x, p), p);
            return digits >= 0 ? Round(x, digits) : x;
        }
        /// <summary>
        /// Raise a value to a rational power (approximation with 32 decimal digits), see <see cref="Pow(BigRat, BigRat, int)"/>.
        /// </summary>
        /// <param name="x">The value, must be zero or positive.</param>
        /// <param name="y">The exponent.</param>
        /// <returns>x^y.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative.</exception>
        /// <exception cref="DivideByZeroException"><paramref name="x"/> is zero and <paramref name="y"/> is negative.</exception>
        public static BigRat Pow(BigRat x, BigRat y)
        {
            return Pow(x, y, 032);
        }
        /// <summary>
        /// Get 2^y (exact).
        /// </summary>
        /// <param name="y">The exponent, may be negative.</param>
        /// <returns>2 raised to the power of <paramref name="y"/>.</returns>
        public static BigRat Pow2(int y)
        {
            var z = abs(y); uint a = (unchecked((uint)z) >> 5) + 1, b = 1u << (z & 31);
            var p = new uint[a + 3]; if (y >= 0) { p[0] = a; p[a] = b; p[a + 1] = p[a + 2] = 1; }
            else { p[2] = a; p[a + 2] = b; p[0] = p[1] = 1; }
            return new BigRat(p);
        }
        /// <summary>
        /// Get 10^y (exact).
        /// </summary>
        /// <param name="y">The exponent, may be negative.</param>
        /// <returns>10 raised to the power of <paramref name="y"/>.</returns>
        public static BigRat Pow10(int y)
        {
            uint e = uabs(y); var n = 5 + unchecked((int)(e >> 3)); Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* u = s)
            {
                uint* v;
                pow10(v = y >= 0 ? u : u + 2, e); *(ulong*)(u + (y >= 0 ? u[0] + 1 : 0)) = 0x100000001;
                return new BigRat(u, 3 + v[0]);
            }
        }
        public static BigRat Sqrt(BigRat x)
        {
            return Sqrt(x, 032);
        }
        /// <summary>
        /// Compute the square root (approximation).
        /// </summary>
        /// <param name="x">The value, must not be negative.</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>The square root of <paramref name="x"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative.</exception>
        public static BigRat Sqrt(BigRat x, int digits)
        {
            if (x <= 0) { if (x == 0) return default; throw new ArgumentOutOfRangeException(nameof(x), "The value must not be negative"); }
            x = sqrt(x, base2(abs(digits))); return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Cbrt(BigRat x)
        {
            return Cbrt(x, 032);
        }
        /// <summary>
        /// Compute the cube root (approximation), negative values give a negative result.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>The cube root of <paramref name="x"/>.</returns>
        public static BigRat Cbrt(BigRat x, int digits)
        {
            return RootN(x, 3, digits);
        }
        public static BigRat RootN(BigRat x, int n)
        {
            return RootN(x, n, 032);
        }
        /// <summary>
        /// Compute the n:th root (approximation, computed using <see cref="Pow(BigRat, BigRat, int)"/>).
        /// </summary>
        /// <param name="x">The value, may only be negative if <paramref name="n"/> is odd (the result is then negative).</param>
        /// <param name="n">The root, must not be zero (a negative value computes 1 / root).</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>The n:th root of <paramref name="x"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is zero, or <paramref name="x"/> is negative and <paramref name="n"/> is even.</exception>
        /// <exception cref="DivideByZeroException"><paramref name="x"/> is zero and <paramref name="n"/> is negative.</exception>
        public static BigRat RootN(BigRat x, int n, int digits)
        {
            if (n == 0)
                throw new ArgumentOutOfRangeException(nameof(n), "The root must not be zero");
            if (IsNegative(x) && ((n & 1) != 0))
                return -Pow(-x, new BigRat(1, n), digits);
            return Pow(x, new BigRat(1, n), digits);
        }
        public static BigRat Hypot(BigRat x, BigRat y)
        {
            return Hypot(x, y, 032);
        }
        public static BigRat Hypot(BigRat x, BigRat y, int digits)
        {
            return Sqrt(x * x + y * y, digits);
        }
        public static BigRat Exp(BigRat x)
        {
            return Exp(x, 032);
        }
        /// <summary>
        /// Compute e^x (approximation).
        /// </summary>
        /// <param name="x">The exponent.</param>
        /// <param name="digits">Unlike the other functions, the result is rounded to <c>digits - floor(log10(result))</c> decimals, i.e. <c>digits + 1</c> significant digits
        /// (ex: Exp(1, 3) = 2.718, Exp(10, 5) = 22026.5); a negative value doesn't round.</param>
        /// <returns>e raised to the power of <paramref name="x"/>.</returns>
        public static BigRat Exp(BigRat x, int digits)
        {
            x = exp(x, base2(abs(digits)));
            return digits >= 0 ? Round(x, digits - ILog10(x)) : x;
        }
        public static BigRat Exp10(BigRat x)
        {
            return Exp10(x, 032);
        }
        public static BigRat Exp10(BigRat x, int digits)
        {
            return Pow(10, x, digits); //todo: test
        }
        public static BigRat Exp2(BigRat x)
        {
            return Exp2(x, 032);
        }
        public static BigRat Exp2(BigRat x, int digits)
        {
            return Pow(2, x, digits); //todo: test
        }
        public static BigRat Log(BigRat x)
        {
            return Log(x, 032);
        }
        /// <summary>
        /// Compute the natural logarithm (approximation).
        /// Note that the second parameter is the number of digits, not the base, use <see cref="Log(BigRat, BigRat)"/> with a BigRat base for other bases.
        /// </summary>
        /// <param name="x">The value, must be positive.</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>The natural logarithm of <paramref name="x"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is zero or negative.</exception>
        public static BigRat Log(BigRat x, int digits)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "The value must be positive");
            x = log(x, base2(abs(digits))); return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Log(BigRat x, BigRat newBase)
        {
            return Log(x, newBase, 032);
        }
        /// <summary>
        /// Compute the logarithm in a base (approximation, log(x) / log(newBase)).
        /// </summary>
        /// <param name="x">The value, must be positive (except for the special cases of the base).</param>
        /// <param name="newBase">The base, must not be negative or one.</param>
        /// <param name="digits">The number of decimal digits to round the result to, see <see cref="BigRat"/>.</param>
        /// <returns>The logarithm of <paramref name="x"/> in the base <paramref name="newBase"/>, zero if the base is zero and <paramref name="x"/> is one.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="newBase"/> is negative or one, or zero (unless <paramref name="x"/> is one), or <paramref name="x"/> is negative or zero.</exception>
        public static BigRat Log(BigRat x, BigRat newBase, int digits)
        {
            if (newBase < 0 || newBase == 1 || newBase == 0 && x != 1) throw new ArgumentOutOfRangeException(nameof(newBase), "Invalid logarithm base");
            if (x < 0 || x == 0 && (newBase > 1 || newBase > 0 && newBase < 1)) throw new ArgumentOutOfRangeException(nameof(x), "The value must be positive");
            if (newBase == 0 && x == 1) return default;
            var p = base2(abs(digits)); x = log(x, p) / log(newBase, p);
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Log2(BigRat x)
        {
            return Log2(x, 032);
        }
        public static BigRat Log2(BigRat x, int digits)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "The value must be positive");
            x = log2(x, base2(abs(digits)));
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Log10(BigRat x)
        {
            return Log10(x, 032);
        }
        public static BigRat Log10(BigRat x, int digits)
        {
            if (x <= 0) throw new ArgumentOutOfRangeException(nameof(x), "The value must be positive");
            var b = base2(abs(digits)); x = log2(x, b) / log2(10, b); //todo: test 
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Pi()
        {
            return Pi(032);
        }
        public static BigRat Pi(int digits)
        {
            var x = pi(base2(abs(digits)));
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Tau()
        {
            return Tau(032);
        }
        public static BigRat Tau(int digits)
        {
            var x = pi(base2(abs(digits))) * 2;
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat E()
        {
            return E(32);
        }
        public static BigRat E(int digits)
        {
            var x = exp(1, base2(abs(digits)));
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Sin(BigRat x)
        {
            return Sin(x, 032);
        }
        public static BigRat Sin(BigRat x, int digits)
        {
            x = sin(x, base2(abs(digits)), false);
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Cos(BigRat x)
        {
            return Cos(x, 032);
        }
        public static BigRat Cos(BigRat x, int digits)
        {
            x = sin(x, base2(abs(digits)), true);
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Tan(BigRat x)
        {
            return Tan(x, 032);
        }
        public static BigRat Tan(BigRat x, int digits)
        {
            var d = -abs(digits); x = Sin(x, d) / Cos(x, d); //todo: test alg 
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Asin(BigRat x)
        {
            return Asin(x, 032);
        }
        public static BigRat Asin(BigRat x, int digits)
        {
            var t = base2(abs(digits)); var a = 1 - x * x;
            if (a > 0) x = atan(x / sqrt(a, t), t);
            else if (a == 0) { var b = pi(t) / 2; x = x < 0 ? -b : b; }
            else throw new ArgumentOutOfRangeException(nameof(x), "The value must be in the [-1, 1] range");
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Acos(BigRat x)
        {
            return Acos(x, 032);
        }
        public static BigRat Acos(BigRat x, int digits)
        {
            var t = base2(abs(digits)); var a = 1 - x; var b = 1 + x;
            if (a < 0 || b < 0) throw new ArgumentOutOfRangeException(nameof(x), "The value must be in the [-1, 1] range");
            if (b > 0) x = 2 * atan(sqrt(a / b, t), t); else x = pi(t);
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Atan(BigRat x)
        {
            return Atan(x, 032);
        }
        public static BigRat Atan(BigRat x, int digits)
        {
            x = atan(x, base2(abs(digits)));
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Atan2(BigRat y, BigRat x)
        {
            return Atan2(y, x, 032);
        }
        public static BigRat Atan2(BigRat y, BigRat x, int digits)
        {
            x = atan2(y, x, base2(abs(digits)));
            return digits >= 0 ? Round(x, digits) : x;
            static BigRat atan2(BigRat y, BigRat x, int bits) //todo: inline
            {
                if (x > 0) return 2 * atan(y / (sqrt(x * x + y * y, bits) + x), bits);
                else if (y != 0) return 2 * atan((sqrt(x * x + y * y, bits) - x) / y, bits);
                else if (x < 0 && y == 0) return pi(bits);
                else throw new ArgumentException("Atan2 is undefined when both x and y are zero"); //x == y == 0
            }
        }
        public static BigRat Acosh(BigRat x)
        {
            return Acosh(x, 032);
        }
        public static BigRat Acosh(BigRat x, int digits)
        {
            var d = -abs(digits); x = Log(x + Sqrt(x * x - 1, d), d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Asinh(BigRat x)
        {
            return Asinh(x, 032);
        }
        public static BigRat Asinh(BigRat x, int digits)
        {
            var d = -abs(digits); x = Log(x + Sqrt(x * x + 1, d), d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Atanh(BigRat x)
        {
            return Atanh(x, 032);
        }
        public static BigRat Atanh(BigRat x, int digits)
        {
            var d = -abs(digits); x = Log((1 + x) / (1 - x), d) / 2; //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Cosh(BigRat x)
        {
            return Cosh(x, 032);
        }
        public static BigRat Cosh(BigRat x, int digits)
        {
            var d = -abs(digits); x = (Exp(x, d) + Exp(-x, d)) / 2; //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Sinh(BigRat x, int digits)
        {
            var d = -abs(digits); x = (Exp(x, d) - Exp(-x, d)) / 2; //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat Sinh(BigRat x)
        {
            return Sinh(x, 032);
        }
        public static BigRat Tanh(BigRat x)
        {
            return Tanh(x, 032);
        }
        public static BigRat Tanh(BigRat x, int digits)
        {
            var d = -abs(digits); x = 1 - 2 / (Exp(x * 2, d) + 1); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat AcosPi(BigRat x)
        {
            return AcosPi(x, 032);
        }
        public static BigRat AcosPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Acos(x, d) / Pi(d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat AsinPi(BigRat x)
        {
            return AsinPi(x, 032);
        }
        public static BigRat AsinPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Asin(x, d) / Pi(d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat AtanPi(BigRat x)
        {
            return AtanPi(x, 032);
        }
        public static BigRat AtanPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Atan(x, d) / Pi(d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat CosPi(BigRat x)
        {
            return CosPi(x, 032);
        }
        public static BigRat CosPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Cos(x * Pi(d), d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat SinPi(BigRat x)
        {
            return SinPi(x, 032);
        }
        public static BigRat SinPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Sin(x * Pi(d), d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static BigRat TanPi(BigRat x)
        {
            return TanPi(x, 032);
        }
        public static BigRat TanPi(BigRat x, int digits)
        {
            var d = -abs(digits); x = Tan(x * Pi(d), d); //todo: test
            return digits >= 0 ? Round(x, digits) : x;
        }
        public static (BigRat Sin, BigRat Cos) SinCos(BigRat x)
        {
            return SinCos(x, 032);
        }
        public static (BigRat Sin, BigRat Cos) SinCos(BigRat x, int digits)
        {
            return (Sin(x, digits), Cos(x, digits));
        }
        public static (BigRat SinPi, BigRat CosPi) SinCosPi(BigRat x)
        {
            return SinCosPi(x, 032);
        }
        public static (BigRat SinPi, BigRat CosPi) SinCosPi(BigRat x, int digits)
        {
            return (SinPi(x, digits), CosPi(x, digits));
        }

        public static bool IsZero(BigRat value)
        {
            return value.p == null;
        }
        public static bool IsNegative(BigRat value)
        {
            return value.p != null && (value.p[0] & 0x80000000) != 0;
        }
        public static bool IsPositive(BigRat value)
        {
            return value.p == null || (value.p[0] & 0x80000000) == 0;
        }
        public static bool IsInteger(BigRat value)
        {
            // return Truncate(value) == value;
            return value.p == null || mod(value, 4).p != null;
        }
        public static bool IsEvenInteger(BigRat value)
        {
            //return value.p == null || IsInteger(value) && (Normalize(value).p![1] & 1) == 0;
            if (value.p == null) return true;
            var a = mod(value, 5); return a.p != null ? (a.p[1] & 1) == 0 : false;
        }
        public static bool IsOddInteger(BigRat value)
        {
            //return value.p != null && IsInteger(value) && (Normalize(value).p![1] & 1) == 1;
            var a = mod(value, 5); return a.p != null ? (a.p[1] & 1) != 0 : false;
        }
        /// <summary>
        /// Check if the numerator and denominator have no common factor (other than 1).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>True if the value is already reduced (ex: 1/2), false if not (ex: 2/4).</returns>
        public static bool IsNormalized(BigRat value)
        {
            return value.Normalize().p == value.p;
        }

        /// <summary>
        /// Reduce the fraction by dividing the numerator and the denominator by their greatest common divisor (ex: 2/4 =&gt; 1/2).
        /// </summary>
        /// <returns>An equal value with the smallest possible numerator and denominator (this value if it's already reduced).</returns>
        public BigRat Normalize()
        {
            if (this.p == null) return this;
            fixed (uint* p = this.p)
            {
                var d = p + (p[0] & 0x3fffffff) + 1; if (*(ulong*)d == 0x100000001) return this;
                var l = this.p.Length; Span<uint> ss = l * 3 <= maxstack ? stackalloc uint[l * 3] : heap(l * 3); uint x;
                fixed (uint* s = ss)
                {
                    copy(s, p, unchecked((uint)l)); s[0] &= 0x3fffffff;
                    var e = gcd(s, s + s[0] + 1); if (*(ulong*)e == 0x100000001) return this;
                    var t = s + l; copy(t, p, unchecked((uint)l)); t[0] &= 0x3fffffff;
                    var r = t + l; d = t + t[0] + 1; mod(t, e, r); mod(d, e, r + r[0] + 1);
                    x = r[0]; r[0] |= p[0] & 0x80000000;
                    return new BigRat(r, 2 + x + r[x + 1]);
                }
            }
        }
        /// <summary>
        /// Get the numerator (with the sign of this value), as stored (not reduced, ex: 2/4 =&gt; 2, use <see cref="Normalize"/> first if needed).
        /// </summary>
        /// <returns>The numerator, zero for zero.</returns>
        public BigRat Numerator()
        {
            if (this.p == null) return default;
            var s = new ReadOnlySpan<uint>(this.p);
            var n = (int)(s[0] & 0x3fffffff); var a = new uint[n + 3];
            s.Slice(0, n + 1).CopyTo(a); a[n + 1] = a[n + 2] = 1;
            return new BigRat(a);
        }
        /// <summary>
        /// Get the denominator (always positive), as stored (not reduced, ex: 2/4 =&gt; 4, use <see cref="Normalize"/> first if needed).
        /// </summary>
        /// <returns>The denominator, one for zero.</returns>
        public BigRat Denominator()
        {
            if (this.p == null) return 1u;
            var s = new ReadOnlySpan<uint>(this.p);
            int n = (int)(s[0] & 0x3fffffff), m = (int)s[n + 1]; var a = new uint[m + 3];
            new ReadOnlySpan<uint>(this.p).Slice(n + 1, m + 1).CopyTo(a); a[m + 1] = a[m + 2] = 1;
            return new BigRat(a);
        }

        /// <summary>
        /// Get a hash code, equal values (ex: 1/2 and 2/4) have the same hash code (the hash is computed from the <see cref="Normalize"/>d value).
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            if (p == null) return 0;
            var norm = Normalize().p;
            fixed (uint* p = norm)
            {
                uint h = 0, n = unchecked((uint)norm.Length); // n = p[(n = p[0] & 0x3fffffff) + 1] + n + 2;
                for (uint i = 0; i < n; i++) h = ((h << 7) | (h >> 25)) ^ p[i];
                return unchecked((int)h);
            }
        }
        public override bool Equals(object value)
        {
            return value is BigRat a ? Equals(a) : false;
        }
        public bool Equals(BigRat value)
        {
            if (((ReadOnlySpan<uint>)this.p).SequenceEqual(((ReadOnlySpan<uint>)value.p))) return true; //todo: perf test
            return CompareTo(value) == 0;
        }
        public int CompareTo(BigRat value)
        {
            fixed (uint* u = this.p, v = value.p) return cmp(u, v);
        }
        public int CompareTo(int value)
        {
            if (value == 0) return Sign(this);
            var v = stackalloc uint[4]; copy(v, value);
            fixed (uint* u = this.p) return cmp(u, v);
        }
        public int CompareTo(object value)
        {
            if (value == null) return 1;
            if (value is BigRat d) return CompareTo(d);
            throw new ArgumentException("Object must be of type BigRat", nameof(value));
        }

        /// <summary>
        /// Format the value.
        /// Formats: "Q" (the default, decimal notation with up to 32 digits, repeating decimals are marked with a ' (ex: 1/3 =&gt; "0.'3"), "…" is appended when truncated),
        /// "Qn" (same as "Q" with n digits, the digits are truncated, not rounded, a truncated value with more than n integer digits uses exponent notation (ex: 1234567.89 with "Q5" =&gt; "1.2345…E+06"), "Q0" formats as numerator/denominator), "R" (same as "Q" but formats as numerator/denominator instead of truncating, so it always round trips through <see cref="Parse(string)"/>),
        /// "En" (scientific), "Gn" (general), "Fn" (fixed point) and "Nn" (fixed point with group separators).
        /// </summary>
        /// <param name="s">The destination.</param>
        /// <param name="charsWritten">The number of chars written.</param>
        /// <param name="format">The format, see above (empty for "Q").</param>
        /// <param name="provider">The format provider for the decimal and group separators, null for the current culture.</param>
        /// <returns>True if the value was written, false if the destination is too small.</returns>
        /// <exception cref="FormatException">The format is not supported.</exception>
        public bool TryFormat(Span<char> s, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider provider = null)
        {
            var a = this; char f = 'Q', g = f; int d = 32; var info = NumberFormatInfo.GetInstance(provider);
            if (format.Length != 0)
            {
                f = (char)((g = format[0]) & ~0x20); var l = format.Length; if (l > 10) throw new FormatException("Invalid format string, the precision is too long");
                if (l > 1) { d = 0; for (int j = 1; j < l; j++) { var y = format[j] - '0'; if ((uint)y > 9) throw new FormatException("Invalid format string, the precision must be a number"); d = d * 10 + y; } }
                switch (f)
                {
                    case 'Q': if (d == 0 && a.p != null) goto fmq; break;
                    case 'E': if (l == 1) d = 6; break;
                    case 'G': if (d == 0) d = 32; break;
                    case 'F': case 'N': if (l == 1) d = info.NumberDecimalDigits; g = f; f = 'F'; break;
                    case 'R': f = 'Q'; if (d == 0 && a.p != null) goto fmq; break;
                    //todo: case 'P': case 'C':  
                    default: throw new FormatException("Unsupported format string, supported formats are Q, R, E, G, F and N");
                }
            }
            int e = ILog10(a), x = e + 1, t = 16 + d + (f == 'F' && e > 0 ? e * 3 / 2 : 0);
            if (t > s.Length) goto err;
            if (f != 'Q') a = Round(a, f == 'G' ? d - (e + 1) : f == 'E' ? d - e : d);
            if (a.p == null) { if (f != 'E' && f != 'F') { s[0] = '0'; charsWritten = 1; return true; } }
            else if (e != 0) a = a / BigRat.Pow10(e);
            int n = tos(s.Slice(0, f == 'F' ? Math.Max(d + e, 0) + 2 : d + 1), a, f == 'Q', out var r), tr = 0;
            if (n == d + 1) { if (f != 'E' && f != 'F') n--; if (f == 'Q') { if ((g & ~0x20) == 'R') goto fmq; s[n++] = '…'; tr = 1; } }
            if (r != -1) { s.Slice(r, n - r).CopyTo(s.Slice(r + 1)); s[r] = '\''; n++; if (r == 0) { s.Slice(0, n).CopyTo(s.Slice(1)); s[0] = '0'; n++; e++; x++; } }
            if (f == 'F') { e = 0; if (a.p == null) x = 1; }
            // Truncated "Q" with more integer digits than digits written: exponent notation (padding zeros after the '…' would misrepresent the value)
            if ((e <= -5 || e >= 17) || ((f == 'G' || tr != 0) && x > d) || f == 'E' || (r != -1 && e >= r)) x = 1; else e = 0;
            if (x <= 0) { t = 1 - x; s.Slice(0, n).CopyTo(s.Slice(t)); s.Slice(0, t).Fill('0'); n += t; x += t; }
            if (x > n) { s.Slice(n, x - n).Fill('0'); n = x; }
            if (f == 'E' && (t = d - n + 1) > 0) { s.Slice(n, t).Fill('0'); n += t; }
            if (f == 'F' && (t = d - (n - x)) > 0) { s.Slice(n, t).Fill('0'); n += t; }
            if (x > 0 && x < n - tr) { s.Slice(x, n++ - x).CopyTo(s.Slice(x + 1)); s[x] = info.NumberDecimalSeparator[0]; }
            if (g == 'N')
            {
                var l = info.NumberGroupSizes[0]; var c = info.NumberGroupSeparator[0];
                for (t = (x < n ? x : n); (t -= l) > 0;) { s.Slice(t, n - t).CopyTo(s.Slice(t + 1)); s[t] = c; n++; }
            }
            if (this < 0) { s.Slice(0, n).CopyTo(s.Slice(1)); s[0] = '-'; n++; }
            if (e != 0 || f == 'E')
            {
                s[n++] = (char)('E' | (g & 0x20)); s[n++] = e >= 0 ? '+' : '-';
                n += tos(s.Slice(n), unchecked((uint)(((e ^ (e >>= 31)) - e))), f == 'E' ? 3 : 2);
            }
            charsWritten = n; return true; fmq:
            a = this.Numerator(); var b = this.Denominator(); //todo: "123/1E+100", "123/1" -> "123" ...
            e = ILog10(a); x = ILog10(b); if ((t = e + x + 4) > s.Length) goto err;
            n = 0; if (this < 0) s[n++] = '-';
            r = tos(s.Slice(n), a / Pow10(e), false, out _); s.Slice(n + r, e + 1 - r).Fill('0'); n += e + 1; if (b == 1) goto ifm; s[n++] = '/';
            r = tos(s.Slice(n), b / Pow10(x), false, out _); s.Slice(n + r, x + 1 - r).Fill('0'); n += x + 1; ifm:
            charsWritten = n; return true; err:
            new Span<char>(&t, 2).TryCopyTo(s); charsWritten = 0; return false;
        }
        public string ToString(string format, IFormatProvider provider)
        {
            if (format != default && format.Length == 0) provider = NumberFormatInfo.InvariantInfo; // "" : dbg
            Span<char> s = stackalloc char[1024];
            if (TryFormat(s, out var charsWritten, format, provider)) return s.Slice(0, charsWritten).ToString();
            int need; fixed (char* p = s) need = *(int*)p; char* pa = null;
            try
            {
                char* p; if (need <= (maxstack << 1)) { var t = stackalloc char[need]; p = t; }
                else p = pa = (char*)alloc(need << 1);
                var ok = TryFormat(s = new Span<char>(p, need), out charsWritten, format, provider); Debug.Assert(ok);
                return s.Slice(0, charsWritten).ToString();
            }
            finally { free(pa); }
        }
        public string ToString(string format)
        {
            return ToString(format, default(IFormatProvider));
        }
        public string ToString(IFormatProvider provider)
        {
            return ToString(default(string), provider);
        }
        /// <summary>
        /// Format the value using the "Q" format (up to 32 digits, repeating decimals marked with a ', ex: "0.'3") and the current culture, see <see cref="TryFormat"/>.
        /// </summary>
        /// <returns>The value as text.</returns>
        public override string ToString()
        {
            return ToString(default(string), default(IFormatProvider));
        }

        /// <summary>
        /// Parse a number: decimal notation (ex: "-1.25", "2.5E-3"), repeating decimals as formatted by <see cref="TryFormat"/> (ex: "0.'3" = 1/3)
        /// and fractions (ex: "1/3", "-2.5/7").
        /// </summary>
        /// <param name="s">The text.</param>
        /// <param name="style">The allowed styles (hex is not supported): <see cref="NumberStyles.AllowLeadingWhite"/>, <see cref="NumberStyles.AllowTrailingWhite"/> (of the whole text),
        /// <see cref="NumberStyles.AllowLeadingSign"/> (of the numerator and of the denominator), <see cref="NumberStyles.AllowDecimalPoint"/>, <see cref="NumberStyles.AllowExponent"/>
        /// and <see cref="NumberStyles.AllowThousands"/> (only used with a <paramref name="provider"/>).</param>
        /// <param name="provider">The provider of the decimal separator, group separator and signs.
        /// If null (culture independent): '.' and ',' are both accepted, if both are used the last one is the decimal separator and the other one the group separator ("1,234.5" and "1.234,5" = 1234.5),
        /// if only one of them is used it's the decimal separator if used once ("1,5" = 1.5), else the group separator ("1,234,567").</param>
        /// <param name="result">The parsed value, zero if parsing failed.</param>
        /// <returns>True if the text could be parsed.</returns>
        /// <remarks>
        /// The syntax is: [sign] digits [decimal separator [digits] ['&#39;' repeating digits]] ['…'] [('e' | 'E') [sign] digits], optionally followed by '/' and a denominator with the same syntax.
        /// At least one digit is required before the exponent, group separators and '_' are only accepted between digits of the integer part ('_' also in the fraction),
        /// the '…' that <see cref="TryFormat"/> appends to truncated values is accepted (and ignored) at the end of the digits.
        /// A sign is '+', '-', '−' or the <see cref="NumberFormatInfo.PositiveSign"/> / <see cref="NumberFormatInfo.NegativeSign"/> of the <paramref name="provider"/>.
        /// No white space is allowed inside the text (ex: around the '/'). The magnitude of the exponent must be at most 100000 (computing 10^exponent for a huge exponent would take "forever").
        /// </remarks>
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out BigRat result)
        {
            if ((style & NumberStyles.AllowHexSpecifier) != 0) goto err;
            if ((style & NumberStyles.AllowLeadingWhite) != 0) s = s.TrimStart(" \u0009\u000A\u000B\u000C\u000D");
            if ((style & NumberStyles.AllowTrailingWhite) != 0) s = s.TrimEnd(" \u0009\u000A\u000B\u000C\u000D");
            var info = provider != null ? NumberFormatInfo.GetInstance(provider) : null;
            var i = s.IndexOf('/');
            if (i < 0) { if (parse(s, style, info, out result)) return true; goto err; }
            if (!parse(s.Slice(0, i), style, info, out var n) || !parse(s.Slice(i + 1), style, info, out var d) || d.p == null) goto err;
            result = n / d; return true; err: result = default; return false;
        }
        /// <summary>
        /// The max magnitude of an exponent in a parsed text (10^exponent is computed, a huge exponent would take "forever").
        /// </summary>
        const int maxparseexp = 100000;
        static bool parse(ReadOnlySpan<char> s, NumberStyles style, NumberFormatInfo info, out BigRat result)
        {
            result = default; int l = s.Length, i = 0, nd = 0, f = 0, k = -1, j;
            if (l == 0) return false;
            // Decimal and group separators
            ReadOnlySpan<char> dec, grp = default;
            if (info != null)
            {
                dec = info.NumberDecimalSeparator;
                if ((style & NumberStyles.AllowThousands) != 0) grp = info.NumberGroupSeparator;
            }
            else
            {
                int a = s.LastIndexOf('.'), b = s.LastIndexOf(',');
                if (a >= 0 && b >= 0) { dec = a > b ? "." : ","; grp = a > b ? "," : "."; }
                else if (a >= 0 || b >= 0) { var c = a >= 0 ? "." : ","; if (s.IndexOf(c[0]) == (a >= 0 ? a : b)) dec = c; else { dec = default; grp = c; } }
                else dec = default;
            }
            // Sign
            var neg = false;
            if ((j = sign(s, info, out neg)) > 0) { if ((style & NumberStyles.AllowLeadingSign) == 0) return false; i = j; }
            // Digits, collected in a buffer (without separators)
            Span<char> buf = l <= 256 ? stackalloc char[l] : new char[l];
            bool point = false, digit = false;
            while (i < l)
            {
                var c = s[i];
                if ((uint)(c - '0') <= 9) { buf[nd++] = c; if (point) f++; digit = true; i++; continue; }
                var next = i + 1 < l && (uint)(s[i + 1] - '0') <= 9;
                if (c == '_' && digit && next) { i++; continue; }
                if (c == '\'' && point && k < 0 && next) { k = nd; digit = false; i++; continue; }
                if (!point && dec.Length > 0 && s.Slice(i).StartsWith(dec))
                {
                    if ((style & NumberStyles.AllowDecimalPoint) == 0) return false;
                    point = true; digit = false; i += dec.Length; continue;
                }
                if (!point && digit && grp.Length > 0)
                {
                    j = s.Slice(i).StartsWith(grp) ? grp.Length : c == ' ' && (grp.SequenceEqual(" ") || grp.SequenceEqual(" ")) ? 1 : 0;
                    if (j > 0 && i + j < l && (uint)(s[i + j] - '0') <= 9) { i += j; continue; }
                }
                break;
            }
            if (nd == 0) return false;
            if (i < l && s[i] == '…') i++;
            // Exponent
            long e = 0;
            if (i < l && (s[i] | 0x20) == 'e')
            {
                if ((style & NumberStyles.AllowExponent) == 0) return false;
                ++i; j = sign(s.Slice(i), info, out var en); i += j;
                var ed = 0;
                for (; i < l && (uint)(s[i] - '0') <= 9; i++, ed++) if ((e = e * 10 + (s[i] - '0')) > maxparseexp) return false;
                if (ed == 0) return false;
                if (en) e = -e;
            }
            if (i != l) return false;
            e -= f; if (e < int.MinValue || e > int.MaxValue) return false;
            // Value: digits * 10^e, with repeating digits: (A * (10^r - 1) + R) * 10^(e + r) / (10^r - 1) where A are the digits before the "'" and R the r digits after it
            var digits = buf.Slice(0, nd);
            if (k < 0) result = num(digits);
            else
            {
                var r = nd - k; var p = Pow10(r) - 1;
                result = (num(digits.Slice(0, k)) * p + num(digits.Slice(k))) / p; e += r;
                if (e > int.MaxValue) return false;
            }
            if (result.p != null)
            {
                if (e != 0) result *= Pow10(unchecked((int)e));
                if (neg) result = -result;
            }
            return true;
            static int sign(ReadOnlySpan<char> s, NumberFormatInfo info, out bool neg)
            {
                neg = false; if (s.Length == 0) return 0;
                switch (s[0]) { case '+': return 1; case '-' or '−': neg = true; return 1; }
                if (info == null) return 0;
                var t = info.NegativeSign; if (t.Length > 0 && s.StartsWith(t)) { neg = true; return t.Length; }
                t = info.PositiveSign; return t.Length > 0 && s.StartsWith(t) ? t.Length : 0;
            }
            static BigRat num(ReadOnlySpan<char> d)
            {
                if (d.Length <= 19) { ulong v = 0; foreach (var c in d) v = v * 10 + unchecked((uint)(c - '0')); return v; }
                return BigInteger.Parse(d, NumberStyles.None, CultureInfo.InvariantCulture);
            }
        }
        public static bool TryParse([NotNullWhen(true)] string s, out BigRat result)
        {
            return TryParse(s.AsSpan(), NumberStyles.Float, null, out result);
        }
        public static bool TryParse(ReadOnlySpan<char> s, out BigRat result)
        {
            return TryParse(s, NumberStyles.Float, null, out result);
        }
        public static bool TryParse([NotNullWhen(true)] string s, NumberStyles style, IFormatProvider provider, out BigRat result)
        {
            return TryParse(s.AsSpan(), style, provider, out result);
        }
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out BigRat result)
        {
            return TryParse(s, NumberStyles.Float, provider, out result);
        }
        public static bool TryParse([NotNullWhen(true)] string s, IFormatProvider provider, out BigRat result)
        {
            return TryParse(s.AsSpan(), NumberStyles.Float, provider, out result);
        }

        /// <summary>
        /// Parse a number, see <see cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider, out BigRat)"/>.
        /// </summary>
        /// <param name="s">The text.</param>
        /// <param name="style">The allowed styles (hex is not supported).</param>
        /// <param name="provider">The format provider, see <see cref="TryParse(ReadOnlySpan{char}, NumberStyles, IFormatProvider, out BigRat)"/>.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentException"><paramref name="style"/> includes <see cref="NumberStyles.AllowHexSpecifier"/>.</exception>
        /// <exception cref="FormatException">The text is not a valid number.</exception>
        public static BigRat Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider provider = null)
        {
            if ((style & NumberStyles.AllowHexSpecifier) != 0) throw new ArgumentException("Hex numbers are not supported", nameof(style));
            if (!TryParse(s, style, provider, out var result)) throw new FormatException("The input is not a valid rational number"); return result;
        }
        public static BigRat Parse(string s)
        {
            return Parse(s.AsSpan(), NumberStyles.Float | NumberStyles.AllowThousands, null);
        }
        public static BigRat Parse(string s, NumberStyles style)
        {
            return Parse(s.AsSpan(), style, null);
        }
        public static BigRat Parse(ReadOnlySpan<char> s, IFormatProvider provider)
        {
            return Parse(s, NumberStyles.Float, provider);
        }
        public static BigRat Parse(string s, NumberStyles style, IFormatProvider provider)
        {
            return Parse(s.AsSpan(), style, provider);
        }
        public static BigRat Parse(string s, IFormatProvider provider)
        {
            return Parse(s.AsSpan(), NumberStyles.Float, provider);
        }


        // fast bit precise conversion
        /// <summary>
        /// Create a rational number with exactly the same value as a double (ex: 0.1 becomes 3602879701896397/36028797018963968).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="OverflowException"><paramref name="value"/> is NaN or infinity.</exception>
        public BigRat(double value)
        {
            if (value == 0) return; int h = ((int*)&value)[1], e = ((h >> 20) & 0x7FF) - 1075;
            if (e == 0x3cc) throw new OverflowException("NaN and infinity can't be converted to a BigRat"); // NaN
            var p = stackalloc uint[64]; Debug.Assert((abs(e) >> 5) + 8 <= 41);
            p[0] = 2; p[1] = *(uint*)&value; p[2] = (unchecked((uint)h) & 0x000FFFFF) | 0x100000;
            if (e == -1075) { if ((p[2] &= 0xfffff) == 0 && p[p[0] = 1] == 0) { *(ulong*)(p + 2) = 0x100000001; goto m1; } e++; } // denormalized
            if (e > 0) shl(p, e); var q = p + (p[0] + 1); *(ulong*)q = 0x100000001; if (e < 0) shl(q, -e); m1:
            var n = p[0]; p[0] |= unchecked((uint)h) & 0x80000000; this.p = new BigRat(p, 2 + n + p[n + 1]).p;
        }

        // fast bitlevel access, binary serialization etc.
        /// <summary>
        /// Get the internal representation (for binary serialization), empty for zero.
        /// Layout: [sign bit | numerator length], numerator words (least significant first), denominator length, denominator words.
        /// </summary>
        /// <returns>The internal data, can be used to create an equal value using <see cref="BigRat(ReadOnlySpan{uint})"/>.</returns>
        public ReadOnlySpan<uint> AsSpan()
        {
            return p;
        }
        /// <summary>
        /// Create a rational number from data returned by <see cref="AsSpan"/> (the data is copied, trailing data is ignored).
        /// </summary>
        /// <param name="span">The internal representation, empty for zero.</param>
        /// <exception cref="ArgumentException">The data is not a valid representation (ex: lengths out of range, zero denominator).</exception>
        public BigRat(ReadOnlySpan<uint> span)
        {
            uint n = unchecked((uint)span.Length), u, v, l; if (n == 0) return; // zero
            fixed (uint* p = span)
            {
                if ((u = p[0] & 0x7fffffff) == 0 || u + 3 > n || u != 1 && p[u] == 0) throw new ArgumentException("Invalid numerator", nameof(span));
                if ((v = p[u + 1]) == 0 || (l = u + v + 2) > n || p[u + v + 1] == 0) throw new ArgumentException("Invalid denominator", nameof(span));
                this.p = p[u] != 0 ? span.Slice(0, unchecked((int)l)).ToArray() : null;
            }
        }

        readonly uint[] p;

        BigRat(uint[] p)
        {
            this.p = p;
        }
        BigRat(uint* p, uint n)
        {
            //this.p = new uint[n]; for (uint i = 0; i < n; i++) this.p[i] = p[i]; return;
            if (p[1] == 0 && p[(p[0] & 0x3fffffff) + 2] == 0) // zero rem 
            {
                uint d = p[0] & 0x3fffffff, c = 1; var v = p + d + 1; var e = v[0];
                for (; p[c + 1] == 0 && v[c + 1] == 0; c++) ;
                copy(p + 1, p + 1 + c, d -= c); p[0] = (p[0] & 0x80000000) | d;
                copy(v - c + 1, v + c + 1, v[-c] = e - c); n -= c << 1;
            }
            fixed (uint* t = this.p = new uint[n]) copy(t, p, n);
        }
        BigRat(int n, int d)
        {
            fixed (uint* t = this.p = new uint[4])
            {
                p[0] = 1 | unchecked(((uint)n ^ (uint)d) & 0x80000000);
                p[1] = uabs(n); p[2] = 1; p[3] = uabs(d);
            }
        }

        static BigRat mul(uint* u, uint* v, uint* w)
        {
            if (u == v) return sqr(u, w);
            uint nu = u[0] & 0x3fffffff, nv = v[0] & 0x3fffffff;
            uint na = mul(u + 1, nu, v + 1, nv, w + 1);
            uint nb = mul(u + nu + 2, u[nu + 1], v + nv + 2, v[nv + 1], w + 2 + na);
            w[0] = (u[0] ^ v[0]) & 0x80000000 | na; w[na + 1] = nb;
            return new BigRat(w, na + nb + 2);
        }
        static BigRat sqr(uint* u, uint* w)
        {
            uint nu = u[0] & 0x3fffffff;
            uint na = sqr(u + 1, nu, w + 1);
            uint nb = sqr(u + nu + 2, u[nu + 1], w + 2 + na);
            w[0] = na; w[na + 1] = nb;
            return new BigRat(w, na + nb + 2);
        }
        static BigRat div(uint* u, uint* v, uint* w)
        {
            uint nu = u[0] & 0x3fffffff, nv = v[0] & 0x3fffffff;
            uint na = mul(u + 1, nu, v + nv + 2, v[nv + 1], w + 1);
            uint nb = mul(u + nu + 2, u[nu + 1], v + 1, nv, w + 2 + na);
            w[0] = (u[0] ^ v[0]) & 0x80000000 | na; w[na + 1] = nb;
            return new BigRat(w, na + nb + 2);
        }
        static BigRat add(uint* u, uint* v, uint* w, uint f)
        {
            uint nu = u[0] & 0x3fffffff, nv = v[0] & 0x3fffffff;
            uint* wa = w + 1, wb, vv, uu;
            uint na = mul(u + 1, nu, vv = v + nv + 2, v[nv + 1], wa);
            uint nb = mul(uu = u + nu + 2, u[nu + 1], v + 1, nv, wb = wa + na), si = 0;
            if (((u[0] ^ v[0] ^ f) & 0x80000000) != 0)
            {
                si = na == nb ? 0 : na > nb ? 1 : 0x80000000;
                if (si == 0) for (uint i = na; i != 0;) if (wa[--i] != wb[i]) { si = wa[i] > wb[i] ? 1 : 0x80000000; break; }
                if (si == 0) return default;
            }
            na = si == 0 ? add(wa, na, wb, nb, wa) : si == 1 ? sub(wa, na, wb, nb, wa) : sub(wb, nb, wa, na, wa);
            nb = mul(uu, u[nu + 1], vv, v[nv + 1], w + 2 + na);
            w[0] = ((u[0] ^ si) & 0x80000000) | na; w[na + 1] = nb;
            return new BigRat(w, na + nb + 2);
        }
        static BigRat mod(BigRat a, int f)
        {
            if (a.p == null) return a;
            fixed (uint* p = a.p)
            {
                var n = p[0] & 0x3fffffff; if (*(ulong*)(p + n + 1) == 0x100000001) return a;
                var sl = a.p.Length << 1; Span<uint> s = sl <= maxstack ? stackalloc uint[sl] : heap(sl);
                fixed (uint* u = s)
                {
                    var v = u + a.p.Length; // n + 1;
                    copy(u, p, n + 1); u[0] = n; mod(u, p + n + 1, v); var inc = false;
                    switch (f)
                    {
                        case 1: inc = (p[0] & 0x80000000) != 0 && *(ulong*)u != 1; break; // Floor
                        case 2: inc = (p[0] & 0x80000000) == 0 && *(ulong*)u != 1; break; // Ceiling
                        case 3: // Round
                            {
                                var t = u + n + 1; copy(t, p + n + 1, p[n + 1] + 1);
                                shr(t, 1); var x = cms(u, t); if (x == 0 && (v[1] & 1) != 0 && (p[n + 2] & 1) == 0) x = 1; inc = x > 0; // a tie (remainder = denominator / 2) is only possible with an even denominator
                            }
                            break;
                        case 4: return *(ulong*)u != 1 ? default : a; // IsInteger // case 6: v = u; break;
                        case 5: if (*(ulong*)u != 1) return default; break; // return int's only IsEvenInt, IsOddInt
                    }
                    if (inc) { var w = 1u; v[0] = add(v + 1, v[0], &w, 1, v + 1); }
                    if (*(ulong*)v == 1) return default;
                    var l = v[0]; v[0] |= p[0] & 0x80000000; *(ulong*)(v + l + 1) = 0x100000001;
                    return new BigRat(v, l + 3);
                }
            }
        }

        static BigRat log2(BigRat x, int bits)
        {
            var r = bits + 16; x = RoundB(x, r); Debug.Assert(x > 0);
            var a = ILogB(x); if (a != 0) x /= Pow2(a);
            var e = x.CompareTo(1); if (e == 0) return a; if (e < 0) { x *= 2; a--; }
            var b = default(BigRat); Debug.Assert(x > 1 && x < 2);
            for (int i = 1; i <= r; i++)
            {
                x = x * x; x = RoundB(x, r); if (x <= 2) continue;
                x = x / 2; var p = Pow2(-i); b += p; b = RoundB(b, r);
            }
            return a + b;
        }
        static BigRat log(BigRat x, int bits)
        {
            return log2(x, bits) / log2(exp(1, bits), bits); //todo: const log2(E)
        }
        static BigRat exp(BigRat x, int bits)
        {
            var s = x < 0; if (s) x = -x; if (bits < 16) bits = 16;
            int p = ILogB(x) + 1; if (p > 0) x = x / Pow2(p); else p = 0; // 0..0.5
            BigRat a = x + 1, b = x, c = 1, d; int i = 2, e = -bits - base2(p) - 16, r = bits + 32;
            for (; ; i++) { b *= x; c *= i; a += d = b / c; if (ILogB(d) < e) break; a = RoundB(a, r); }
            for (i = 0; i < p; i++) a = RoundB(a * a, r);
            if (s) a = 1 / a; return a;
        }
        static BigRat pi(int bits)
        {
            Debug.Assert(bits >= 0);
            bits += 16; BigRat c = 0, a = 1; //, t1 = -32, t2 = 1, t3 = 256, t4 = 64, t5 = 4, a = 1;
            for (int n = 0, l = (((bits - 1) >> 5) + 2) << 5; ; n++)
            {
                int t = 10 * n, s = n << 2; //var b = t1 / (s + 1) - t2 / (s + 3) + t3 / (t + 1) - t4 / (t + 3) - t5 / (t + 5) - t5 / (t + 7) + t2 / (t + 9);
                var b = new BigRat(-32, s + 1) - new BigRat(1, s + 3) + new BigRat(256, t + 1) - new BigRat(64, t + 3) - new BigRat(4, t + 5) - new BigRat(4, t + 7) + new BigRat(1, t + 9);
                var d = a * b; var i = -ILogB(d); if (i > bits) break;
                c += d; c = RoundB(c, l); a /= -1024;
            }
            c /= 64; return c;
        }
        static BigRat sin(BigRat x, int bits, bool cos)
        {
            int c = bits + 32, e = ILogB(x);
            var pih = pi(c + Math.Max(0, e)); pih /= 2; if (cos) x += pih;
            var u = Truncate(x / pih); var s = u.p != null ? unchecked((int)u.p[1]) : 0; if (u < 0) s = -s;
            if (s != 0) { x -= u * pih; if ((s & 1) != 0) x = pih - x; if ((s & 2) != 0) x = -x; }
            if (x.p == null) return default;
            //if ((e = ILogB(u = x < 0 ? pih + x : pih - x)) < -27) { if (e < -bits) return Sign(x); c += 64; } // < 1e-8 near 90°
            BigRat f = 6, a = x, b = x * x, d; b = RoundB(b, c);
            for (int i = 1, k = 4; ; i++, k += 2)
            {
                a *= b; a = RoundB(a, c); d = a / f; var t = -ILogB(d); if (t > c) break;
                if ((i & 1) != 1) x += d; else x -= d; f *= k * (k + 1); x = RoundB(x, c);
            }
            return x;
        }
        static BigRat sqrt(BigRat x, int bits)
        {
            Debug.Assert(x >= 0 && bits >= 0); if (x.p == null) return default;
            var a = est(x); var e = abs(ILog10(x)); bits += 16; if (e > bits) bits = e;
            for (int i = 0; ; i++)
            {
                var f = a * a - x; var d = -ILogB(f); if (d > bits) break;
                a = RoundB(a, bits); a = (x / a + a) / 2;
            }
            return a;
            static BigRat est(BigRat x)
            {
                var n = x.p!.Length; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
                fixed (uint* u = s, t = x.p)
                {
                    copy(u, t, unchecked((uint)n));
                    uint nu = u[0], nv = u[nu + 1]; var v = u + nu + 1;
                    var mu = (nu << 5) - unchecked((uint)BitOperations.LeadingZeroCount(u[nu]));
                    var mv = (nv << 5) - unchecked((uint)BitOperations.LeadingZeroCount(v[nv]));
                    if (mu > 1) shr(u, unchecked((int)(mu >> 1))); var un = u[0];
                    if (mv > 1) shr(v, unchecked((int)(mv >> 1))); var vn = v[0];
                    if (un != nu) copy(u + un + 1, v, vn + 1); return new BigRat(u, 2 + un + vn);
                }
            }
        }
        static BigRat atan(BigRat _x, int bits)
        {
            var x = _x; var s = Sign(x); if (s == 0) return default; // atan(0) = 0
            if (s < 0) x = -x; // atan(-x) = -atan(x)
            var td = x > 1; if (td) x = 1 / x; // atan(1/x) = pi/2 - atan(x)
            var sh = 0; bits += 32; // atan(x) = atan(c) + atan((x - c) / (1 + c * x)) -> c == (x - c) / (1 + c * x) -> c <= 0.1
            for (var d = new BigRat(1, 10); x > d;) { var y = 1 / x; x = sqrt(y * y + 1, bits) - y; x = RoundB(x, bits); sh++; }
            BigRat zsqr = x * x, zsqr1 = zsqr + 1, a = 1, b = 1;
            for (int n = 2; ; n += 2) // n < 20
            {
                b *= n * zsqr / ((n + 1) * zsqr1);
                a += b; a = RoundB(a, bits); var l = -ILogB(b); if (l > bits) break;
            }
            x = (x * a) / zsqr1; if (sh != 0) x *= Pow2(sh); a = b = x;//rem
            if (td) x = pi(bits) / 2 - x;
            if (s < 0) x.p![0] ^= 0x80000000;
            return x;
        }

        static int tos(Span<char> s, BigRat v, bool reps, out int rep)
        {
            rep = -1; if (v.p == null) { s[0] = '0'; return 1; }
            uint nv = unchecked((uint)v.p.Length), np = v.p[0] & 0x3fffffff, ten = 10, nr = 0; int ex = 0;
            if (reps) { nr = v.p[np + 1] + 1; var uex = (ulong)nr * (uint)s.Length; if (uex > (500000 >> 2)) uex = 0; ex = unchecked((int)uex); }
            var need = checked(v.p.Length + 8 + ex); uint* pa = default;
            try
            {
                uint* p; if (need <= maxstack) { var t = stackalloc uint[need]; p = t; }
                else p = pa = (uint*)alloc(need << 2);
                uint* d = p + np + 1 + 4;
                fixed (uint* t = v.p) { copy(p, t, np + 1); copy(d, t + np + 1, t[np + 1] + 1); p[0] &= 0x3fffffff; }
                uint* w = p + nv + 4, r = w + 4;
                for (int i = 0; i < s.Length; i++)
                {
                    mod(p, d, w); var c = (char)('0' + w[1]); // Debug.Assert(c >= '0' && c <= '9');
                    if (ex != 0 && c != '0')
                    {
                        for (int t = 0; t < i; t++)
                        {
                            if (s[t] != c || cms(p, r + t * nr) != 0) continue;
                            for (; t != 0 && s[i - 1] == '0' && s[t - 1] == '0'; i--, t--) ;
                            rep = t; return i;
                        }
                        copy(r + i * nr, p, p[0] + 1); Debug.Assert(p[0] < nr);
                    }
                    s[i] = c; if (*(ulong*)p == 1) return i + 1;
                    p[0] = mul(p + 1, p[0], &ten, 1, p + 1);
                }
            }
            finally { free(pa); }
            return s.Length;
        }
        static int tos(Span<char> s, uint u, int m)
        {
            int i = 0; for (; u != 0 || i < m; u /= 10u) s[i++] = unchecked((char)('0' + u % 10));
            s.Slice(0, i).Reverse(); return i;
        }
        static void toi(BigRat x, uint* d, uint f)
        {
            uint i, c, l = f & 0xffff; if (x.p == null) { for (i = 0; i < l; i++) d[i] = 0; return; }
            fixed (uint* p = Truncate(x).p)
            {
                var n = p[0] & 0x3fffffff; var s = (p[0] & 0x80000000) != 0;
                if ((f & 0x10000) != 0) // unsigned
                {
                    if (s || n > l)
                    {
                        if ((f & 0x80000) != 0) throw new OverflowException();
                        if ((f & 0x20000) != 0) { c = s ? 0 : 0xffffffff; for (i = 0; i < l; i++) d[i] = c; return; }
                        if (!s) { for (i = 0; i < l; i++) d[i] = 0; return; }
                        c = n < l ? n : l; for (i = 0; i < c; d[i] = p[++i]) ; neg(d, c);
                        for (; i < l; d[i++] = 0xffffffff) ; return;
                    }
                }
                else
                {
                    i = 0; if (s && n == l && p[n] == 0x80000000) for (i = 1; i < n && p[i] == 0; i++) ; //if (i == n) { } //MinValue
                    if (n > l || i != n && n == l && (p[n] & 0x80000000) != 0)
                    {
                        if ((f & 0x80000) != 0) throw new OverflowException();
                        c = (f & 0x20000) != 0 && !s ? 0xffffffff : 0;
                        for (i = 0; i < l; i++) d[i] = c; d[l - 1] ^= 0x80000000; return;
                    }
                }
                for (i = 0; i < n; d[i] = p[++i]) ; for (; i < l; d[i++] = 0) ;
                if (s) neg(d, l); static void neg(uint* d, uint l) { for (uint i = 0, c = 1; i < l; i++) { var t = (ulong)(d[i] ^ 0xffffffff) + c; d[i] = (uint)t; c = (uint)(t >> 32); } }
            }
        }
        static decimal tom(BigRat x, bool sat)
        {
            if (x.p == null) return default;
            fixed (uint* p = x.p)
            {
                var u = p; uint nu = u[0] & 0x3ffffff, nv = u[nu + 1]; var v = u + nu + 1;
                var mu = unchecked((int)nu << 5) - BitOperations.LeadingZeroCount(u[nu]);
                var mv = unchecked((int)nv << 5) - BitOperations.LeadingZeroCount(v[nv]);
                var sh = Math.Max(mu - 96, mv - 96);
                var n = sh > 0 ? x.p.Length : 0; Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
                fixed (uint* t = s)
                {
                    if (sh > 0)
                    {
                        copy(t, u, unchecked((uint)n));
                        v = (u = t) + nu + 1; shr(u, sh); shr(v, sh); nu = u[0]; nv = v[0];
                        if (*(ulong*)u == 1) return default;
                        if (*(ulong*)v == 1)
                        {
                            if (sat) return (p[0] & 0x80000000) != 0 ? decimal.MinValue : decimal.MaxValue;
                            throw new OverflowException("The value is too large for a decimal");
                        }
                    }
                    var du = new decimal(((int*)u)[1], nu >= 2 ? ((int*)u)[2] : 0, nu >= 3 ? ((int*)u)[3] : 0, (x.p[0] & 0x80000000) != 0, 0);
                    var dv = new decimal(((int*)v)[1], nv >= 2 ? ((int*)v)[2] : 0, nv >= 3 ? ((int*)v)[3] : 0, false, 0);
                    return du / dv;
                }
            }
        }
        static void pow10(uint* p, uint e)
        {
            uint n = e / 19, r = e - n * 19; ulong a = 1, b = 10;
            for (; ; r >>= 1) { if ((r & 1) != 0) a *= b; if (r <= 1) break; b *= b; }
            *(ulong*)(p + 1) = a; p[0] = p[2] != 0 ? 2u : 1u; if (n == 0) return;
            var l = 4 + (int)(e >> 3); Span<uint> s = (l << 1) <= maxstack ? stackalloc uint[l << 1] : heap(l << 1);
            fixed (uint* sv = s)
            {
                uint* v = sv, w = v + l, t;
                v[0] = 2; *(ulong*)(v + 1) = 10000000000000000000;
                for (; ; n >>= 1)
                {
                    if ((n & 1) != 0) { copy(w, p, p[0] + 1); p[0] = mul(v + 1, v[0], w + 1, w[0], p + 1); } // p *= v;
                    if (n <= 1) break; w[0] = sqr(v + 1, v[0], w + 1); t = v; v = w; w = t; // v *= v;
                }
            }
        }

        static int cms(uint* a, uint* b)
        {
            uint na = a[0], nb = b[0]; if (na != nb) return na > nb ? +1 : -1;
            for (var i = na + 1; --i != 0;) if (a[i] != b[i]) return a[i] > b[i] ? +1 : -1;
            return 0;
        }
        static int cmp(uint* a, uint* b)
        {
            if (a == b) return 0;
            var su = a == null ? 0 : (a[0] & 0x80000000) != 0 ? -1 : +1;
            var sv = b == null ? 0 : (b[0] & 0x80000000) != 0 ? -1 : +1;
            if (su != sv) return su > sv ? +1 : -1;
            uint nu = a[0] & 0x3fffffff, du = a[nu + 1], nv = b[0] & 0x3fffffff, dv = b[nv + 1];
            var n = unchecked((int)(nu + du + nv + dv + 8)); Span<uint> s = n <= maxstack ? stackalloc uint[n] : heap(n);
            fixed (uint* w = s)
            {
                uint* p;
                uint cu = mul(a + 1, nu, b + nv + 2, dv, w);
                uint cv = mul(b + 1, nv, a + nu + 2, du, p = w + cu);
                if (cu != cv) return cu > cv ? su : -su;
                for (uint i = cu; i != 0;) if (w[--i] != p[i]) return w[i] > p[i] ? su : -su;
                return 0;
            }
        }
        static uint mul(uint* a, uint na, uint* b, uint nb, uint* r)
        {
            //todo: enable optis
            //if ((na < nb ? na : nb) >= 32 && na + nb < 25000) { kmu(a, na, b, nb, r); if (r[(na = na + nb) - 1] == 0) na--; return na; }
            //if (na < nb) { var u = na; na = nb; nb = u; var v = a; a = b; b = v; }
            //if (na == 1) { *(ulong*)r = (ulong)a[0] * b[0]; goto ret; }
            //if (na == 2 && Bmi2.X64.IsSupported) { *(ulong*)(r + 2) = Bmi2.X64.MultiplyNoFlags(*(ulong*)a, nb == 2 ? *(ulong*)b: b[0], (ulong*)r); goto ret; } 
            ulong c = 0, d;
            for (uint k = 0; k < na; k++, c = d >> 32)
                r[k] = unchecked((uint)(d = c + (ulong)a[k] * b[0]));
            r[na] = (uint)c;
            for (uint i = 1, k; i < nb; i++)
            {
                for (k = 0, c = 0; k < na; k++, c = d >> 32)
                    r[i + k] = unchecked((uint)(d = r[i + k] + c + (ulong)a[k] * b[i]));
                r[i + na] = (uint)c;
            } //ret:
            if (r[(na = na + nb) - 1] == 0) na--; return na;
        }
        static uint sqr(uint* a, uint n, uint* r)
        {
            if (n <= 2)
            {
                if (n < 2) { *(ulong*)r = (ulong)a[0] * a[0]; goto m1; }
                if (Bmi2.X64.IsSupported) { ((ulong*)r)[1] = Bmi2.X64.MultiplyNoFlags(*(ulong*)a, *(ulong*)a, ((ulong*)r)); goto m1; }
                var t1 = (ulong)a[0] * a[0];
                var t2 = (ulong)a[0] * a[1];
                var t3 = (ulong)a[1] * a[1];
                var t4 = t2 + (t1 >> 32);
                var t5 = t2 + (uint)t4;
                ((ulong*)r)[0] = t5 << 32 | (uint)t1;
                ((ulong*)r)[1] = t3 + (t4 >> 32) + (t5 >> 32); goto m1;
            }
            for (uint i = 0; i < n; i++)
            {
                ulong c = 0, e, f;
                for (uint j = 0; j < i; j++, c = (f + (e >> 1)) >> 31)
                    r[i + j] = unchecked((uint)((e = r[i + j] + c) + ((f = (ulong)a[j] * a[i]) << 1)));
                *(ulong*)(r + (i << 1)) = (ulong)a[i] * a[i] + c;
            }
        m1: if (r[(n = n << 1) - 1] == 0) n--; Debug.Assert(r[n - 1] != 0); return n;
        }
        static uint add(uint* a, uint na, uint* b, uint nb, uint* r)
        {
            if (na < nb) { var p = a; a = b; b = p; var t = na; na = nb; nb = t; }
            uint i = 0; ulong c = 0, d;
            for (; i < nb; i++, c = d >> 32) r[i] = unchecked((uint)(d = (a[i] + c) + b[i]));
            for (; i < na; i++, c = d >> 32) r[i] = unchecked((uint)(d = a[i] + c));
            return (r[i] = unchecked((uint)c)) != 0 ? i + 1 : i;
        }
        static uint sub(uint* a, uint na, uint* b, uint nb, uint* r)
        {
            uint i = 0; var c = 0L; Debug.Assert(na >= nb);
            for (; i < nb; i++) { var u = a[i] + c - b[i]; r[i] = unchecked((uint)u); c = u >> 32; }
            for (; i < na; i++) { var u = a[i] + c; /*  */ r[i] = unchecked((uint)u); c = u >> 32; }
            for (; i > 1 && r[i - 1] == 0; i--) ; Debug.Assert(c == 0); return i;
        }
        static void mod(uint* a, uint* b, uint* r)
        {
            uint na = a[0], nb = b[0] & 0x3fffffff; if (r != null) *(ulong*)r = 1;
            if (na < nb) return;
            if (na == 1)
            {
                if (b[1] == 0) return;
                uint q = a[1] / b[1], m = a[1] - q * b[1];
                a[a[0] = 1] = m; if (r != null) r[1] = q; return;
            }
            if (na == 2)
            {
                ulong x = *(ulong*)(a + 1), y = nb != 1 ? *(ulong*)(b + 1) : b[1];
                ulong q = x / y, m = x - (q * y);
                *(ulong*)(a + 1) = m; a[0] = a[2] != 0 ? 2u : 1u; if (r == null) return;
                *(ulong*)(r + 1) = q; r[0] = r[2] != 0 ? 2u : 1u; return;
            }
            if (nb == 1)
            {
                ulong x = 0; uint y = b[1];
                for (uint i = na; i != 0; i--)
                {
                    ulong q = (x = (x << 32) | a[i]) / y, m = x - (q * y);
                    if (r != null) r[i] = unchecked((uint)q); x = m;
                }
                a[0] = 1; a[1] = unchecked((uint)x); if (r == null) return;
                for (; na > 1 && r[na] == 0; na--) ; r[0] = na; return;
            }
            uint dh = b[nb], dl = nb > 1 ? b[nb - 1] : 0;
            int sh = BitOperations.LeadingZeroCount(dh), sb = 32 - sh;
            if (sh > 0)
            {
                uint db = nb > 2 ? b[nb - 2] : 0;
                dh = (dh << sh) | (dl >> sb);
                dl = (dl << sh) | (db >> sb);
            }
            for (uint i = na, d; i >= nb; i--)
            {
                uint n = i - nb, t = i < na ? a[i + 1] : 0;
                ulong vh = ((ulong)t << 32) | a[i];
                uint vl = i > 1 ? a[i - 1] : 0;
                if (sh > 0)
                {
                    uint va = i > 2 ? a[i - 2] : 0;
                    vh = (vh << sh) | (vl >> sb);
                    vl = (vl << sh) | (va >> sb);
                }
                ulong di = vh / dh; if (di > 0xffffffff) di = 0xffffffff;
                for (; ; di--)
                {
                    ulong th = dh * di, tl = dl * di; th += tl >> 32;
                    if (th < vh) break; if (th > vh) continue;
                    if ((tl & 0xffffffff) > vl) continue; break;
                }
                if (di != 0)
                {
                    ulong c = 0; var p = a + n;
                    for (uint k = 1; k <= nb; k++)
                    {
                        c += b[k] * di; d = unchecked((uint)c); c >>= 32;
                        if (p[k] < d) c++; p[k] = p[k] - d; // ryujit
                    }
                    if (unchecked((uint)c) != t)
                    {
                        c = 0; p = a + n; di--; ulong g;
                        for (uint k = 1; k <= nb; k++, c = g >> 32) p[k] = unchecked((uint)(g = (p[k] + c) + b[k]));
                    }
                }
                if (di != 0 && r != null)
                {
                    var x = n + 1; for (var j = r[0] + 1; j < x; j++) r[j] = 0;
                    if (r[0] < x) r[0] = x; r[x] = unchecked((uint)di);
                }
                if (i < na) a[i + 1] = 0;
            }
            for (; a[0] > 1 && a[a[0]] == 0; a[0]--) ; //todo: nb == 1 case, test and rem
        }
        static void inv(uint* p)
        {
            uint n = (p[0] & 0x3fffffff) + 1, m = p[n] + 1, t, a, b;
            p[n] |= p[0] & 0x80000000; p[0] &= 0x3fffffff;
            if (n == m) { for (a = 0; a < n; a++) { t = p[a]; p[a] = p[b = a + n]; p[b] = t; } }
            else
            {
                uint c = n - 1, d = m + c;
                for (a = 0, b = c; a < b; a++, b--) { t = p[a]; p[a] = p[b]; p[b] = t; }
                for (a = n, b = d; a < b; a++, b--) { t = p[a]; p[a] = p[b]; p[b] = t; }
                for (a = 0, b = d; a < b; a++, b--) { t = p[a]; p[a] = p[b]; p[b] = t; }
            }
        }
        static void shr(uint* p, int c)
        {
            int s = c & 31, r = 32 - s; uint n = p[0] & 0x3fffffff, i = 0, k = 1 + unchecked((uint)c >> 5), l = k <= n ? p[k++] >> s : 0;
            while (k <= n) { var t = p[k++]; p[++i] = l | unchecked((uint)((ulong)t << r)); l = t >> s; }
            if (l != 0 || i == 0) p[++i] = l; p[0] = i;
        }
        static void shl(uint* p, int c)
        {
            int s = c & 31, r = 32 - s; uint d = unchecked((uint)(c >> 5)), n = p[0] & 0x3fffffff; p[0] = p[n + 1] = 0;
            for (uint i = n + 1; i > 0; i--) p[i + d] = (p[i] << s) | unchecked((uint)((ulong)p[i - 1] >> r));
            for (uint i = 1; i <= d; i++) p[i] = 0;
            n += d; p[0] = p[n + 1] != 0 ? n + 1 : n;
        }
        static uint* gcd(uint* u, uint* v)
        {
            if (cms(u, v) < 0) { var t = u; u = v; v = t; }
            while (v[0] > 2)
            {
                uint e = u[0] - v[0];
                if (e <= 2)
                {
                    ulong xh = u[u[0]], xm = u[u[0] - 1], xl = u[u[0] - 2];
                    ulong yh = e == 0 ? v[v[0]] : 0, ym = e <= 1 ? v[v[0] - (1 - e)] : 0, yl = v[v[0] - (2 - e)];
                    int z = BitOperations.LeadingZeroCount(unchecked((uint)xh));
                    ulong x = ((xh << 32 + z) | (xm << z) | (xl >> 32 - z)) >> 1;
                    ulong y = ((yh << 32 + z) | (ym << z) | (yl >> 32 - z)) >> 1; Debug.Assert(x >= y);
                    uint a = 1, b = 0, c = 0, d = 1, k = 0;
                    while (y != 0)
                    {
                        ulong q, r, s, t;
                        q = x / y; if (q > 0xffffffff) break;
                        r = a + q * c; if (r > 0x7fffffff) break;
                        s = b + q * d; if (s > 0x7fffffff) break;
                        t = x - q * y; if (t < s || t + r > y - c) break;
                        a = unchecked((uint)r);
                        b = unchecked((uint)s); k++;
                        x = t; if (x == b) break;
                        q = y / x; if (q > 0xffffffff) break;
                        r = d + q * b; if (r > 0x7fffffff) break;
                        s = c + q * a; if (s > 0x7fffffff) break;
                        t = y - q * x; if (t < s || t + r > x - b) break;
                        d = unchecked((uint)r);
                        c = unchecked((uint)s); k++;
                        y = t; if (y == c) break;
                    }
                    if (b != 0)
                    {
                        long cx = 0, cy = 0;
                        for (uint i = 1, l = v[0]; i <= l; i++)
                        {
                            long ui = u[i], vi = v[i];
                            long dx = a * ui - b * vi + cx; u[i] = unchecked((uint)dx); cx = dx >> 32;
                            long dy = d * vi - c * ui + cy; v[i] = unchecked((uint)dy); cy = dy >> 32;
                        }
                        u[0] = v[0];
                        for (; u[0] > 1 && u[u[0]] == 0; u[0]--) ;
                        for (; v[0] > 1 && v[v[0]] == 0; v[0]--) ;
                        if ((k & 1) != 0) { var t = u; u = v; v = t; }
                        continue;
                    }
                }
                mod(u, v, null); var p = u; u = v; v = p;
            }
            if (*(ulong*)v != 1)
            {
                if (u[0] > 2) { mod(u, v, null); var t = u; u = v; v = t; if (*(ulong*)v == 1) goto m1; }
                ulong x = u[0] != 1 ? *(ulong*)(u + 1) : u[1];
                ulong y = v[0] != 1 ? *(ulong*)(v + 1) : v[1];
                while (y != 0 && (x | y) > 0xffffffff) { var t = x % y; x = y; y = t; }
                while (y != 0) { var t = unchecked((uint)x) % unchecked((uint)y); x = y; y = t; }
                *(ulong*)(u + 1) = x; u[0] = u[2] != 0 ? 2u : 1u; m1:;
            }
            return u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int abs(int x)
        {
            return (x ^ (x >> 31)) - (x >> 31);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint uabs(int x)
        {
            return unchecked((uint)abs(x));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int base2(int digits)
        {
            //return (int)Math.Ceiling(digits * 3.321928094887362);
            return unchecked((int)(((long)digits * 14267572527) >> 32)) + 1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void copy(uint* d, uint* s, uint n)
        {
            //for (uint i = 0; i < n; i++) d[i] = s[i];
            Unsafe.CopyBlock(d, s, n << 2);
        }
        static void copy(uint* d, int v)
        {
            d[0] = unchecked((uint)v & 0x80000000) | 1;
            d[1] = uabs(v); ((ulong*)d)[1] = 0x100000001;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void* alloc(int n)
        {
            return Marshal.AllocCoTaskMem(n).ToPointer();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void free(void* p)
        {
            if (p != null) Marshal.FreeCoTaskMem((IntPtr)p);
        }
        /// <summary>
        /// The max number of uint's of a temporary buffer on the stack, larger buffers are allocated on the heap (huge values would overflow the stack).
        /// Usage: Span&lt;uint&gt; s = n &lt;= maxstack ? stackalloc uint[n] : heap(n); fixed (uint* p = s) ...
        /// </summary>
        const int maxstack = 0x4000;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static uint[] heap(int n)
        {
            return GC.AllocateUninitializedArray<uint>(n);
        }

#if NET7_0_OR_GREATER
        public static implicit operator BigRat(Int128 value)
        {
            var p = (uint*)&value; var s = (p[3] & 0x80000000) != 0; if (s) value = -value;
            var a = (BigRat)(*(UInt128*)p); if (s) a.p![0] |= 0x80000000; return a;
        }
        public static implicit operator BigRat(UInt128 value)
        {
            var p = (ulong*)&value; return p[1] == 0 ? p[0] : p[0] + p[1] * Pow2(64); //todo: opt
        }
        public static explicit operator Int128(BigRat value)
        {
            Int128 t; toi(value, (uint*)&t, 4); return t;
        }
        public static explicit operator UInt128(BigRat value)
        {
            UInt128 t; toi(value, (uint*)&t, 4 | 0x10000); return t;
        }
        public static explicit operator checked Int128(BigRat value)
        {
            Int128 t; toi(value, (uint*)&t, 4 | 0x80000); return t;
        }
        public static explicit operator checked UInt128(BigRat value)
        {
            UInt128 t; toi(value, (uint*)&t, 4 | 0x10000 | 0x80000); return t;
        }

        public static bool IsNaN(BigRat value)
        {
            return false;
        }
        public static bool IsNormal(BigRat value)
        {
            return value.p != null;
        }
        public static bool IsFinite(BigRat value)
        {
            return true;
        }
        public static bool IsInfinity(BigRat value)
        {
            return false;
        }
        public static bool IsNegativeInfinity(BigRat value)
        {
            return false;
        }
        public static bool IsPositiveInfinity(BigRat value)
        {
            return false;
        }
        public static bool IsRealNumber(BigRat value)
        {
            return true;
        }
        public static bool IsSubnormal(BigRat value)
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigRat CreateChecked<TOther>(TOther value) where TOther : INumberBase<TOther>
        {
            if (TryConvertFrom(value, out var a) || TOther.TryConvertToChecked(value, out a)) return a;
            throw new NotSupportedException();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigRat CreateSaturating<TOther>(TOther value) where TOther : INumberBase<TOther>
        {
            if (TryConvertFrom(value, out var a) || TOther.TryConvertToSaturating(value, out a)) return a;
            throw new NotSupportedException();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BigRat CreateTruncating<TOther>(TOther value) where TOther : INumberBase<TOther>
        {
            if (TryConvertFrom(value, out var a) || TOther.TryConvertToTruncating(value, out a)) return a;
            throw new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertFromChecked<TOther>(TOther value, out BigRat result)
        {
            return TryConvertFrom(value, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertFromSaturating<TOther>(TOther value, out BigRat result)
        {
            return TryConvertFrom(value, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertFromTruncating<TOther>(TOther value, out BigRat result)
        {
            return TryConvertFrom(value, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertToSaturating<T>(BigRat value, out T result)
        {
            return TryConvertTo<T>(value, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertToTruncating<T>(BigRat value, out T result)
        {
            return TryConvertTo<T>(value, out result);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool INumberBase<BigRat>.TryConvertToChecked<T>(BigRat value, out T result)
        {
            if (typeof(T) == typeof(BigRat)) { result = (T)(object)(BigRat)value; return true; }
            else if (typeof(T) == typeof(double)) { result = (T)(object)checked((double)value); return true; }
            else if (typeof(T) == typeof(long)) { result = (T)(object)checked((long)value); return true; }
            else if (typeof(T) == typeof(ulong)) { result = (T)(object)checked((ulong)value); return true; }
            else if (typeof(T) == typeof(nint)) { result = (T)(object)checked((nint)value); return true; }
            else if (typeof(T) == typeof(nuint)) { result = (T)(object)checked((nuint)value); return true; }
            else if (typeof(T) == typeof(decimal)) { result = (T)(object)checked((decimal)value); return true; }
            else if (typeof(T) == typeof(Int128)) { result = (T)(object)checked((Int128)value); return true; }
            else if (typeof(T) == typeof(UInt128)) { result = (T)(object)checked((UInt128)value); return true; }
            else if (typeof(T) == typeof(BigInteger)) { result = (T)(object)checked((BigInteger)value); return true; }
            else return T.TryConvertFromChecked((double)value, out result!);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryConvertTo<T>(BigRat value, out T result) where T : INumberBase<T>
        {
            if (typeof(T) == typeof(BigRat)) { result = (T)(object)value; return true; }
            else if (typeof(T) == typeof(double)) { result = (T)(object)(double)value; return true; }
            else if (typeof(T) == typeof(long)) { long t; toi(value, (uint*)&t, 2 | 0x20000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(ulong)) { ulong t; toi(value, (uint*)&t, 2 | 0x20000 | 0x10000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(nint)) { nint t; toi(value, (uint*)&t, ((uint)sizeof(nint) >> 2) | 0x20000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(nuint)) { nuint t; toi(value, (uint*)&t, ((uint)sizeof(nuint) >> 2) | 0x20000 | 0x10000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(Int128)) { Int128 t; toi(value, (uint*)&t, 4 | 0x20000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(UInt128)) { UInt128 t; toi(value, (uint*)&t, 4 | 0x20000 | 0x10000); result = (T)(object)t; return true; }
            else if (typeof(T) == typeof(decimal)) { result = (T)(object)tom(value, true); return true; }
            else if (typeof(T) == typeof(BigInteger)) { result = (T)(object)(BigInteger)value; return true; }
            else return T.TryConvertFromSaturating((double)value, out result!);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryConvertFrom<T>(T value, out BigRat result) where T : INumberBase<T>
        {
            if (typeof(T) == typeof(BigRat)) { result = (BigRat)(object)value; return true; }
            else if (typeof(T) == typeof(double)) { result = (BigRat)(double)(object)value; return true; }
            else if (typeof(T) == typeof(float)) { result = (BigRat)(float)(object)value; return true; }
            else if (typeof(T) == typeof(Half)) { result = (BigRat)(Half)(object)value; return true; }
            else if (typeof(T) == typeof(byte)) { result = (BigRat)(byte)(object)value; return true; }
            else if (typeof(T) == typeof(sbyte)) { result = (BigRat)(sbyte)(object)value; return true; }
            else if (typeof(T) == typeof(char)) { result = (BigRat)(char)(object)value; return true; }
            else if (typeof(T) == typeof(ushort)) { result = (BigRat)(ushort)(object)value; return true; }
            else if (typeof(T) == typeof(short)) { result = (BigRat)(short)(object)value; return true; }
            else if (typeof(T) == typeof(int)) { result = (BigRat)(int)(object)value; return true; }
            else if (typeof(T) == typeof(uint)) { result = (BigRat)(uint)(object)value; return true; }
            else if (typeof(T) == typeof(long)) { result = (BigRat)(long)(object)value; return true; }
            else if (typeof(T) == typeof(ulong)) { result = (BigRat)(ulong)(object)value; return true; }
            else if (typeof(T) == typeof(nint)) { result = (BigRat)(nint)(object)value; return true; }
            else if (typeof(T) == typeof(nuint)) { result = (BigRat)(nuint)(object)value; return true; }
            else if (typeof(T) == typeof(decimal)) { result = (BigRat)(decimal)(object)value; return true; }
            else if (typeof(T) == typeof(Int128)) { result = (BigRat)(Int128)(object)value; return true; }
            else if (typeof(T) == typeof(UInt128)) { result = (BigRat)(UInt128)(object)value; return true; }
            else if (typeof(T) == typeof(BigInteger)) { result = (BigRat)(BigInteger)(object)value; return true; }
            else { result = default; return false; }
        }

        static bool INumberBase<BigRat>.IsCanonical(BigRat value)
        {
            return true;
        }
        static bool INumberBase<BigRat>.IsComplexNumber(BigRat value)
        {
            return false;
        }
        static bool INumberBase<BigRat>.IsImaginaryNumber(BigRat value)
        {
            return false;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static int INumberBase<BigRat>.Radix
        {
            get { return 1; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat INumberBase<BigRat>.Zero
        {
            get { return default; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat INumberBase<BigRat>.One
        {
            get { return 1u; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat IAdditiveIdentity<BigRat, BigRat>.AdditiveIdentity
        {
            get { return default; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat IMultiplicativeIdentity<BigRat, BigRat>.MultiplicativeIdentity
        {
            get { return 1u; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat ISignedNumber<BigRat>.NegativeOne
        {
            get { return -1; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat IFloatingPointConstants<BigRat>.E
        {
            get { return Exp(1, 032); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat IFloatingPointConstants<BigRat>.Pi
        {
            get { return Pi(032); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        static BigRat IFloatingPointConstants<BigRat>.Tau
        {
            get { return Tau(032); }
        }
#endif
    }
}
