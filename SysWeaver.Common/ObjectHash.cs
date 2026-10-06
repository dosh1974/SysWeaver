
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SysWeaver
{
    /// <summary>
    /// Contains methods for getting and mixing hashcodes.
    /// These are generated with maximum performance and quality in mind.
    /// The hash codes are only meant for in-process use (hash tables etc), <see cref="object.GetHashCode"/> of strings etc is randomized per process.
    /// </summary>
    public static class ObjectHash
    {
        /// <summary>
        /// Get the hash code of a value
        /// </summary>
        /// <typeparam name="T">The type of the value</typeparam>
        /// <param name="value">The value, may be null</param>
        /// <returns>The hash code of the value (<see cref="object.GetHashCode"/>), 0 if the value is null</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Get<T>(T value)
        {
            return value?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Mix 2 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2)
        {
            return (h1 * -1640531535) ^ h2;
        }

        /// <summary>
        /// Mix 3 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3)
        {
            h1 = (h1 * -1640531535) ^ h2;
            return (h3 * 524287) ^ h1;
        }

        /// <summary>
        /// Mix 4 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            return (h1 * 31) ^ h2;
        }

        /// <summary>
        /// Mix 5 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h1 = (h5 * 31) ^ h1;
            return (h2 * 131071) ^ h1;
        }

        /// <summary>
        /// Mix 6 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h1 = (h1 * 131071) ^ h2;
            return (h3 * 127) ^ h1;
        }

        /// <summary>
        /// Mix 7 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h1 = (h7 * 131071) ^ h1;
            h1 = (h1 * 127) ^ h2;
            return (h3 * 65537) ^ h1;
        }

        /// <summary>
        /// Mix 8 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h1 = (h1 * 127) ^ h2;
            h2 = (h3 * 65537) ^ h4;
            return (h1 * 257) ^ h2;
        }

        /// <summary>
        /// Mix 9 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h1 = (h9 * 127) ^ h1;
            h1 = (h1 * 65537) ^ h2;
            h2 = (h3 * 257) ^ h4;
            return (h1 * 8191) ^ h2;
        }

        /// <summary>
        /// Mix 10 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h1 = (h1 * 65537) ^ h2;
            h2 = (h3 * 257) ^ h4;
            h1 = (h5 * 8191) ^ h1;
            return (h2 * 5) ^ h1;
        }

        /// <summary>
        /// Mix 11 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h1 = (h11 * 65537) ^ h1;
            h1 = (h1 * 257) ^ h2;
            h2 = (h3 * 8191) ^ h4;
            h1 = (h5 * 5) ^ h1;
            return (h2 * 7) ^ h1;
        }

        /// <summary>
        /// Mix 12 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <param name="h12">Hash code 12 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h1 = (h1 * 257) ^ h2;
            h2 = (h3 * 8191) ^ h4;
            h3 = (h5 * 5) ^ h6;
            h1 = (h1 * 7) ^ h2;
            return (h3 * 17) ^ h1;
        }

        /// <summary>
        /// Mix 13 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <param name="h12">Hash code 12 to mix</param>
        /// <param name="h13">Hash code 13 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h1 = (h13 * 257) ^ h1;
            h1 = (h1 * 8191) ^ h2;
            h2 = (h3 * 5) ^ h4;
            h3 = (h5 * 7) ^ h6;
            h1 = (h1 * -1640531535) ^ h2;
            return (h3 * 524287) ^ h1;
        }

        /// <summary>
        /// Mix 14 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <param name="h12">Hash code 12 to mix</param>
        /// <param name="h13">Hash code 13 to mix</param>
        /// <param name="h14">Hash code 14 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h1 = (h1 * 8191) ^ h2;
            h2 = (h3 * 5) ^ h4;
            h3 = (h5 * 7) ^ h6;
            h1 = (h7 * 17) ^ h1;
            h1 = (h1 * 524287) ^ h2;
            return (h3 * 31) ^ h1;
        }

        /// <summary>
        /// Mix 15 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <param name="h12">Hash code 12 to mix</param>
        /// <param name="h13">Hash code 13 to mix</param>
        /// <param name="h14">Hash code 14 to mix</param>
        /// <param name="h15">Hash code 15 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14, int h15)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h1 = (h15 * 8191) ^ h1;
            h1 = (h1 * 5) ^ h2;
            h2 = (h3 * 7) ^ h4;
            h3 = (h5 * 17) ^ h6;
            h1 = (h7 * 524287) ^ h1;
            h1 = (h1 * 31) ^ h2;
            return (h3 * 131071) ^ h1;
        }

        /// <summary>
        /// Mix 16 hash codes into one hash code (the order of the hash codes matters).
        /// </summary>
        /// <param name="h1">Hash code 1 to mix</param>
        /// <param name="h2">Hash code 2 to mix</param>
        /// <param name="h3">Hash code 3 to mix</param>
        /// <param name="h4">Hash code 4 to mix</param>
        /// <param name="h5">Hash code 5 to mix</param>
        /// <param name="h6">Hash code 6 to mix</param>
        /// <param name="h7">Hash code 7 to mix</param>
        /// <param name="h8">Hash code 8 to mix</param>
        /// <param name="h9">Hash code 9 to mix</param>
        /// <param name="h10">Hash code 10 to mix</param>
        /// <param name="h11">Hash code 11 to mix</param>
        /// <param name="h12">Hash code 12 to mix</param>
        /// <param name="h13">Hash code 13 to mix</param>
        /// <param name="h14">Hash code 14 to mix</param>
        /// <param name="h15">Hash code 15 to mix</param>
        /// <param name="h16">Hash code 16 to mix</param>
        /// <returns>The mixed hash code</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mix(int h1, int h2, int h3, int h4, int h5, int h6, int h7, int h8, int h9, int h10, int h11, int h12, int h13, int h14, int h15, int h16)
        {
            h1 = (h1 * -1640531535) ^ h2;
            h2 = (h3 * 524287) ^ h4;
            h3 = (h5 * 31) ^ h6;
            h4 = (h7 * 131071) ^ h8;
            h5 = (h9 * 127) ^ h10;
            h6 = (h11 * 65537) ^ h12;
            h7 = (h13 * 257) ^ h14;
            h8 = (h15 * 8191) ^ h16;
            h1 = (h1 * 5) ^ h2;
            h2 = (h3 * 7) ^ h4;
            h3 = (h5 * 17) ^ h6;
            h4 = (h7 * 524287) ^ h8;
            h1 = (h1 * 31) ^ h2;
            h2 = (h3 * 131071) ^ h4;
            return (h1 * 127) ^ h2;
        }

        /// <summary>
        /// Mix a sequence of hash codes into one hash code (the order of the hash codes matters).
        /// The result is the same as for <see cref="Mix(IEnumerable)"/> and <see cref="Mix(object[])"/> with the same values.
        /// </summary>
        /// <param name="p">The hash codes to mix</param>
        /// <param name="index">The index of the first hash code in <paramref name="p"/> to mix</param>
        /// <param name="len">The number of hash codes to mix, a negative value mixes all hash codes from <paramref name="index"/> to the end of <paramref name="p"/></param>
        /// <returns>The mixed hash code, 42 if no hash codes are mixed</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="p"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is negative or larger than the number of elements, or if <paramref name="index"/> + <paramref name="len"/> is larger than the number of elements in <paramref name="p"/></exception>
        public static int Mix(IReadOnlyList<int> p, int index = 0, int len = -1)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(p);
#endif//DEBUG
            var count = p.Count;
            if ((uint)index > (uint)count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within the list!");
            if (len < 0)
                len = count - index;
            else if (len > (count - index))
                throw new ArgumentOutOfRangeException(nameof(len), len, "Index + length must be within the list!");
            // Fast paths, no interface calls
            if (p is int[] a)
                return MixSpan(new ReadOnlySpan<int>(a, index, len));
            if (p is List<int> l)
                return MixSpan(CollectionsMarshal.AsSpan(l).Slice(index, len));
            return MixList(p, index, len);
        }

        static int MixSpan(ReadOnlySpan<int> p)
        {
            int hash = 42;
            while (p.Length >= 15)
            {
                hash = Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]);
                p = p.Slice(15);
            }
            switch (p.Length)
            {
                case 1:
                    return Mix(hash, p[0]);
                case 2:
                    return Mix(hash, p[0], p[1]);
                case 3:
                    return Mix(hash, p[0], p[1], p[2]);
                case 4:
                    return Mix(hash, p[0], p[1], p[2], p[3]);
                case 5:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4]);
                case 6:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5]);
                case 7:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
                case 8:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
                case 9:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8]);
                case 10:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
                case 11:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10]);
                case 12:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]);
                case 13:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12]);
                case 14:
                    return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13]);
            }
            return hash;
        }

        static int MixList(IReadOnlyList<int> p, int index, int len)
        {
            int hash = 42;
            while (len >= 15)
            {
                hash = Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12], p[index + 13], p[index + 14]);
                index += 15;
                len -= 15;
            }
            switch (len)
            {
                case 1:
                    return Mix(hash, p[index + 0]);
                case 2:
                    return Mix(hash, p[index + 0], p[index + 1]);
                case 3:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2]);
                case 4:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3]);
                case 5:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4]);
                case 6:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5]);
                case 7:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6]);
                case 8:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7]);
                case 9:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8]);
                case 10:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9]);
                case 11:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10]);
                case 12:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11]);
                case 13:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12]);
                case 14:
                    return Mix(hash, p[index + 0], p[index + 1], p[index + 2], p[index + 3], p[index + 4], p[index + 5], p[index + 6], p[index + 7], p[index + 8], p[index + 9], p[index + 10], p[index + 11], p[index + 12], p[index + 13]);
            }
            return hash;
        }

        /// <summary>
        /// Mix the hash codes of some objects into one hash code (the order of the objects matters).
        /// The hash code of every object is computed using <see cref="Get{T}(T)"/> (null objects have a hash code of 0).
        /// The result is the same as for <see cref="Mix(IReadOnlyList{int}, int, int)"/> with the hash codes of the objects.
        /// </summary>
        /// <param name="p">The objects to mix the hash codes of</param>
        /// <returns>The mixed hash code, 42 if there are no objects</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="p"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>Value types are boxed by the caller, prefer the fixed arity overloads if possible</remarks>
        public static int Mix(params Object[] p)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(p);
#endif//DEBUG
            int hash = 42;
            int index = 0;
            int len = p.Length;
            while (len >= 15)
            {
                hash = Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]), Get(p[index + 13]), Get(p[index + 14]));
                index += 15;
                len -= 15;
            }
            switch (len)
            {
                case 1:
                    return Mix(hash, Get(p[index + 0]));
                case 2:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]));
                case 3:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]));
                case 4:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]));
                case 5:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]));
                case 6:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]));
                case 7:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]));
                case 8:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]));
                case 9:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]));
                case 10:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]));
                case 11:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]));
                case 12:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]));
                case 13:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]));
                case 14:
                    return Mix(hash, Get(p[index + 0]), Get(p[index + 1]), Get(p[index + 2]), Get(p[index + 3]), Get(p[index + 4]), Get(p[index + 5]), Get(p[index + 6]), Get(p[index + 7]), Get(p[index + 8]), Get(p[index + 9]), Get(p[index + 10]), Get(p[index + 11]), Get(p[index + 12]), Get(p[index + 13]));
            }
            return hash;
        }


        /// <summary>
        /// Mix the hash codes of all elements in a sequence into one hash code (the order of the elements matters).
        /// The hash code of every element is computed using <see cref="Get{T}(T)"/> (null elements have a hash code of 0).
        /// The result is the same as for <see cref="Mix(IReadOnlyList{int}, int, int)"/> with the hash codes of the elements.
        /// </summary>
        /// <param name="obj">The sequence of elements</param>
        /// <returns>The mixed hash code, 42 if the sequence is empty</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="obj"/> is null (debug builds only; release builds throw a <see cref="NullReferenceException"/>)</exception>
        /// <remarks>
        /// Arrays and lists of int and arrays of objects are handled without enumerating (no boxing), other sequences box value type elements.
        /// Note that a single string argument binds to this overload (a string is a sequence of chars), use <see cref="Get{T}(T)"/> to get the hash code of the string itself.
        /// </remarks>
        public static int Mix(IEnumerable obj)
        {
#if DEBUG
            ArgumentNullException.ThrowIfNull(obj);
#endif//DEBUG
            // Fast paths (same result), the hash code of an int is the value itself
            if (obj is int[] ia)
                return MixSpan(ia);
            if (obj is List<int> il)
                return MixSpan(CollectionsMarshal.AsSpan(il));
            if (obj is IReadOnlyList<int> rl)
                return MixList(rl, 0, rl.Count);
            if (obj is Object[] oa)
                return Mix(oa);
            return MixEnumerable(obj);
        }

        [SkipLocalsInit]
        static int MixEnumerable(IEnumerable obj)
        {
            int hash = 42;
            Span<int> p = stackalloc int[15];
            var it = obj.GetEnumerator();
            try
            {
                for (; ; )
                {
                    int len;
                    for (len = 0; (len < 15) && it.MoveNext(); ++len)
                        p[len] = Get(it.Current);
                    switch (len)
                    {
                        case 0:
                            return hash;
                        case 1:
                            return Mix(hash, p[0]);
                        case 2:
                            return Mix(hash, p[0], p[1]);
                        case 3:
                            return Mix(hash, p[0], p[1], p[2]);
                        case 4:
                            return Mix(hash, p[0], p[1], p[2], p[3]);
                        case 5:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4]);
                        case 6:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5]);
                        case 7:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
                        case 8:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
                        case 9:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8]);
                        case 10:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
                        case 11:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10]);
                        case 12:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11]);
                        case 13:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12]);
                        case 14:
                            return Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13]);
                        case 15:
                            hash = Mix(hash, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], p[12], p[13], p[14]);
                            break;
                    }
                }
            }
            finally
            {
                (it as IDisposable)?.Dispose();
            }
        }

    }

}  //namespace SysWeaver
