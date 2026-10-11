using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver
{
    /// <summary>
    /// Contains a static cache that can merge identical immutable objects
    /// </summary>
    /// <remarks>
    /// Two objects are considered identical if they are of the exact same type and all their public readable instance properties (excluding indexers) are equal.
    /// Property values that are enumerable (strings, arrays, lists etc) are compared element wise (recursively), other property values are compared using their Equals method.
    /// If the object itself is enumerable, it's elements are also part of the identity.
    /// Public fields, non-public properties and static properties are not part of the identity.
    /// The cache is a least recently used cache (a <see cref="FastMemCache{K, V}"/>): an object that isn't used for <see cref="Timeout"/> (5 minutes) is removed,
    /// so the memory used is bounded by the objects used recently (expr-cov-15: objects were kept forever). An identical object passed after that becomes the new shared instance.
    /// </remarks>
    public static class ObjectMerger
    {
        /// <summary>
        /// The properties of a type that makes up the identity of an instance (cached per type, reflection is slow)
        /// </summary>
        static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypeProperties = new ConcurrentDictionary<Type, PropertyInfo[]>();

        static PropertyInfo[] GetTypeProperties(Type type)
        {
            if (TypeProperties.TryGetValue(type, out var p))
                return p;
            return TypeProperties.GetOrAdd(type, static t =>
            {
                var props = t.GetTypeInfo().FindProperties(ReflectionFlags.IsPublic, ReflectionFlags.IsStatic);
                int count = 0;
                var r = new PropertyInfo[8];
                foreach (var x in props)
                {
                    //  Indexer properties can't be read without arguments
                    if (x.GetIndexParameters().Length != 0)
                        continue;
                    //  Write only properties can't be read
                    if (x.GetGetMethod() == null)
                        continue;
                    if (count == r.Length)
                        Array.Resize(ref r, count * 2);
                    r[count] = x;
                    ++count;
                }
                if (count != r.Length)
                    Array.Resize(ref r, count);
                return r;
            });
        }

        private sealed class Key : IEquatable<Key>
        {
            private static int ExtendedHashCode(Object o)
            {
                if (o == null)
                    return 0;
                //  Strings are IEnumerable (of boxed chars), but the string hash code is computed on the content (and is consistent with an element wise compare)
                if (o is String s)
                    return s.GetHashCode();
                var x = o as IEnumerable;
                if (x == null)
                    return o.GetHashCode();
                int h = 42;
                int count = 0;
                foreach (var oo in x)
                {
                    h = (h * -1640531535) ^ ExtendedHashCode(oo);
                    ++count;
                }
                return h ^ count;
            }

            private static bool ExtendedEquals(Object a, Object b)
            {
                if (a == b)
                    return true;
                if (a == null)
                    return false;
                if (b == null)
                    return false;
                //  Element wise compare of strings is the same as an ordinal compare (if b isn't a string the types are different)
                if (a is String s)
                    return s.Equals(b as String);
                var aa = a as IEnumerable;
                if (aa == null)
                    return a.Equals(b);
                if (a.GetType() != b.GetType())
                    return false;
                var bb = b as IEnumerable;
                if (bb == null)
                    return false;
                var itb = bb.GetEnumerator();
                try
                {
                    foreach (var va in aa)
                    {
                        if (!itb.MoveNext())
                            return false;
                        if (!ExtendedEquals(va, itb.Current))
                            return false;
                    }
                    return !itb.MoveNext();
                }
                finally
                {
                    (itb as IDisposable)?.Dispose();
                }
            }

            public Key(Object exp)
            {
                var type = exp.GetType();
                Type = type;
                var p = GetTypeProperties(type);
                //  The content of an enumerable (strings, lists etc) is part of the key (compared element wise)
                var self = exp is IEnumerable;
                var pc = p.Length;
                var pp = GC.AllocateUninitializedArray<Object>(pc + (self ? 1 : 0));
                int h = type.GetHashCode();
                for (int i = 0; i < pc; ++i)
                {
                    h = (h * 31) ^ (h >> 24);
                    var obj = p[i].GetValue(exp, null);
                    pp[i] = obj;
                    h ^= ExtendedHashCode(obj);
                }
                if (self)
                {
                    h = (h * 31) ^ (h >> 24);
                    pp[pc] = exp;
                    h ^= ExtendedHashCode(exp);
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
                var a = Properties;
                var b = other.Properties;
                var l = a.Length;
                if (l != b.Length)
                    return false;
                for (int i = 0; i < l; ++i)
                {
                    if (!ExtendedEquals(a[i], b[i]))
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Try to merge this immutable object
        /// </summary>
        /// <typeparam name="T">Type of the object, only use immutable types!</typeparam>
        /// <param name="obj">The object that should be merged with any other objects representing the same thing</param>
        /// <returns>The input object or an object representing the same thing (the cached instance)</returns>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> is null</exception>
        /// <exception cref="TargetInvocationException">A property getter of <paramref name="obj"/> threw an exception</exception>
        /// <remarks>
        /// The cache is global and thread safe, the first instance (with a given identity) that is passed to this method is returned for all subsequent calls with an identical object,
        /// as long as it's used: an instance that isn't used for <see cref="Timeout"/> is removed (and the next identical object passed becomes the shared instance).
        /// See <see cref="ObjectMerger"/> for a description of what makes two objects identical.
        /// Mutating an object after it have been merged is undefined behavior.
        /// </remarks>
        public static T GetShared<T>(T obj) where T : notnull
        {
            Object o = obj;
            ArgumentNullException.ThrowIfNull(o, nameof(obj));
            var key = new Key(o);
            var c = Cache;
            var e = c.Cache.GetOrUpdate(key, static (_, x) => new Entry(x), o);
            //  Least recently used: an entry that is used is kept for another timeout (the expiration time is only extended when half of it has passed, to limit the writes)
            var now = Environment.TickCount64;
            if ((now - Volatile.Read(ref e.Used)) >= c.RefreshMs)
            {
                Volatile.Write(ref e.Used, now);
                c.Cache.Set(key, e);
            }
            var shared = e.Obj;
            //  Value types are copied, so no point in counting them
            if ((!ReferenceEquals(shared, o)) && (!typeof(T).IsValueType))
                Interlocked.Increment(ref InternalMergeCounter);
            return (T)shared;
        }

        /// <summary>
        /// Number of merged objects (the number of times <see cref="GetShared{T}(T)"/> returned a different instance than the one passed in)
        /// </summary>
        /// <remarks>
        /// Calls where the type argument is a value type are never counted.
        /// </remarks>
        public static long MergeCounter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Interlocked.Read(ref InternalMergeCounter);
            }
        }
        private static long InternalMergeCounter;

        /// <summary>
        /// The time an object is kept in the cache after it was last used (the default is 5 minutes).
        /// An object is removed when it hasn't been used for this time, an object used within the last half of it is always kept.
        /// Setting it replaces the cache (all cached objects are dropped); zero or negative: nothing is cached (no objects are merged), <see cref="TimeSpan.MaxValue"/>: objects are kept forever.
        /// </summary>
        public static TimeSpan Timeout
        {
            get => Cache.Timeout;
            set => Cache = new SharedCache(value);
        }

        /// <summary>
        /// The number of objects in the cache (expired objects are removed on the next cache miss)
        /// </summary>
        public static int Count => Cache.Cache.GetCount();

        /// <summary>
        /// A cached object
        /// </summary>
        sealed class Entry
        {
            public Entry(Object obj)
            {
                Obj = obj;
                Used = Environment.TickCount64;
            }

            /// <summary>
            /// The shared instance
            /// </summary>
            public readonly Object Obj;

            /// <summary>
            /// The time (<see cref="Environment.TickCount64"/>) the expiration time of the entry was last set
            /// </summary>
            public long Used;
        }

        sealed class SharedCache
        {
            public SharedCache(TimeSpan timeout)
            {
                Timeout = timeout;
                Cache = new FastMemCache<Key, Entry>(timeout);
                //  Extend the expiration time when half of the timeout has passed (never for a zero / negative timeout: nothing is cached)
                RefreshMs = timeout <= TimeSpan.Zero ? long.MaxValue : (long)(timeout.TotalMilliseconds * 0.5);
            }
            public readonly TimeSpan Timeout;
            public readonly FastMemCache<Key, Entry> Cache;
            public readonly long RefreshMs;
        }

        static SharedCache Cache = new SharedCache(TimeSpan.FromMinutes(5));

    }
}
