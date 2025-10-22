using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using System.Reflection;

namespace SysWeaver
{
    /// <summary>
    /// Contains a static cache that can merge identical immutable objects
    /// </summary>
    public static class ObjectMerger
    {
        private sealed class Key : IEquatable<Key>
        {
            private static int ExtendedHashCode(Object o)
            {
                if (o == null)
                    return 0;
                var x = o as IEnumerable;
                if (x == null)
                    return o.GetHashCode();
                List<int> hashCodes = new List<int>();
                foreach (var oo in x)
                    hashCodes.Add(ExtendedHashCode(oo));
                return ObjectHash.Mix(hashCodes);
            }

            private static bool ExtendedEquals(Object a, Object b)
            {
                if (a == b)
                    return true;
                if (a == null)
                    return false;
                if (b == null)
                    return false;
                var aa = a as IEnumerable;
                if (aa == null)
                    return a.Equals(b);
                if (a.GetType() != b.GetType())
                    return false;
                var bb = b as IEnumerable;
                if (bb == null)
                    return false;
                var itb = bb.GetEnumerator();
                foreach (var va in aa)
                {
                    if (!itb.MoveNext())
                        return false;
                    if (!ExtendedEquals(va, itb.Current))
                        return false;
                }
                return !itb.MoveNext();
            }

            public Key(Object exp)
            {
                Type = exp.GetType();
                var p = Type.GetTypeInfo().FindProperties(ReflectionFlags.IsPublic, ReflectionFlags.IsStatic).ToArray();
                var pp = GC.AllocateUninitializedArray<Object>(p.Length);
                int h = Type.GetHashCode();
                for (int i = 0; i < p.Length; ++i)
                {
                    h = (h * 31) ^ (h >> 24);
                    var obj = p[i].GetValue(exp, null);
                    pp[i] = obj;
                    h ^= ExtendedHashCode(obj);
                }
                Properties = pp;
                HashCode = h;
            }
            private readonly Type Type;
            private readonly Object[] Properties;
            private readonly int HashCode;

            public override int GetHashCode()
            {
                return HashCode;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Key);
            }

            public bool Equals(Key other)
            {
                if (other == null)
                    return false;
                if (HashCode != other.HashCode)
                    return false;
                if (Type != other.Type)
                    return false;
                if (Properties.Length != other.Properties.Length)
                    return false;
                return ExtendedEquals(Properties, other.Properties);
            }
        }

        /// <summary>
        /// Try to merge this immutable object
        /// </summary>
        /// <typeparam name="T">Type of the object, only use immutable types!</typeparam>
        /// <param name="obj">The object that should be merged with any other objects representing the same thing</param>
        /// <returns>The input object or an object representing the same thing</returns>
        public static T GetShared<T>(T obj) where T : notnull
        {
            var key = new Key(obj);
            if (SharedObjects.TryGetValue(key, out var shared))
            {
                if (!shared.Equals(obj))
                    Interlocked.Increment(ref InternalMergeCounter);
                return (T)shared;
            }
            if (!SharedObjects.TryAdd(key, obj))
            {
                shared = SharedObjects[key];
                if (!shared.Equals(obj))
                    Interlocked.Increment(ref InternalMergeCounter);
                return (T)shared;
            }
            return obj;
        }

        /// <summary>
        /// Number of merged objects
        /// </summary>
        public static long MergeCounter
        {
            get
            {
                return Interlocked.Read(ref InternalMergeCounter);
            }
        }
        private static long InternalMergeCounter;

        private static readonly ConcurrentDictionary<Key, Object> SharedObjects = new ConcurrentDictionary<Key, Object>();

    }
}
