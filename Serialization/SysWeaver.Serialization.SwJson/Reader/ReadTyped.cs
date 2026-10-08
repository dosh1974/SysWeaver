using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SysWeaver.Serialization.SwJson.Reader
{
    /// <summary>
    /// Per type (static generic) storage of the compiled readers: the value creator, the object factory, the member assigners and the dictionary item adder.
    /// </summary>
    /// <remarks>
    /// <see cref="Create"/> is set by <see cref="ReadTypeCache"/> when the type is first used. The member look up and dictionary adder are built lazily (under a lock on <see cref="Create"/>, so it must be set first).
    /// All members are thread safe once built.
    /// </remarks>
    /// <typeparam name="T">The type to read</typeparam>
    static class ReadTyped<T>
    {

        /// <summary>
        /// Read a value of type <typeparamref name="T"/> at the current position.
        /// </summary>
        public delegate T TypedCreator(JsonParserState state, Func<char, bool> endOn);

        /// <summary>
        /// Read a member value at the current position and assign it to the instance <paramref name="v"/> (by ref, so it works for value types).
        /// </summary>
        public delegate void TypedAssigner(ref T v, JsonParserState state, Func<char, bool> endOn);


        /// <summary>
        /// Compile and set <see cref="Create"/>.
        /// </summary>
        /// <param name="v">An expression of type <typeparamref name="T"/> using <see cref="ReadTypeCache.ParState"/> and <see cref="ReadTypeCache.ParEndOn"/></param>
        public static void Set(Expression v)
        {
            Create = Expression.Lambda<TypedCreator>(v, ReadTypeCache.TempCall).Compile();
        }
        //public static Expression CreateExp;
        /// <summary>
        /// The compiled reader for <typeparamref name="T"/>, null until <see cref="ReadTypeCache.Get(Type)"/> has been called for the type.
        /// </summary>
        public static TypedCreator Create;

        /// <summary>
        /// Get the member look up (built on first use) and create a new instance to populate.
        /// </summary>
        /// <remarks>
        /// The look up contains all public instance properties with a getter and setter (indexers excluded) and all public non readonly fields, by exact (case sensitive) name.
        /// The instance is created with the public parameterless constructor, or uninitialized (no constructor runs) if there is none.
        /// Throws if <see cref="Create"/> isn't set.
        /// </remarks>
        /// <param name="obj">A new instance</param>
        /// <returns>The member assigners by UTF8 name</returns>
        public static IMemberLookUp<TypedAssigner> GetMembers(out T obj)
        {
            var i = I;
            if (i != null)
            {
                obj = New();
                return i;
            }
            lock (Create)
            {
                i = I;
                if (i != null)
                {
                    obj = New();
                    return i;
                }
                var t = typeof(T);
                if (t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null)
                { 
                    New = Expression.Lambda<Func<T>>(Expression.New(t)).Compile();
                }
                else
                {
                    New = NewUninitialized(t);
                }
                var ti = new Dictionary<Utf8Range, TypedAssigner>();
                var o = Expression.Parameter(t.MakeByRefType(), "v");
                foreach (var m in t.GetMembers(BindingFlags.Instance | BindingFlags.Public))
                {
                    {
                        var p = m as PropertyInfo;
                        if (p != null)
                        {
                            //  Skip indexers (such as the Item property of a dictionary), they can't be assigned from a json member
                            if (p.CanRead && p.CanWrite && (p.GetIndexParameters().Length == 0))
                            {
                                var pi = ReadTypeCache.Get(p.PropertyType);
                                var setProp = Expression.Lambda<TypedAssigner>(Expression.Assign(Expression.Property(o, p), pi.CreateExp), o, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                                ti.Add(Utf8Range.Create(p.Name), setProp.Compile());
                            }
                            continue;
                        }
                    }
                    {
                        var f = m as FieldInfo;
                        if (f != null)
                        {
                            if (!f.IsInitOnly)
                            {
                                var pi = ReadTypeCache.Get(f.FieldType);
                                var setField = Expression.Lambda<TypedAssigner>(Expression.Assign(Expression.Field(o, f), pi.CreateExp), o, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                                ti.Add(Utf8Range.Create(f.Name), setField.Compile());
                            }
                        }
                    }
                }
                //i = new Utf8RangeLookup<TypedAssigner>(ti);
                i = MemberLookUp<TypedAssigner>.Create(ti);
                O = new MemberOrder(ti);
                Volatile.Write(ref I, i);
                obj = New();
                return i;
            }
        }

        /// <summary>
        /// Get the members (built on first use) and create a new instance to populate, see <see cref="GetMembers(out T)"/>.
        /// </summary>
        /// <param name="obj">A new instance</param>
        /// <returns>The member assigners, with the order they are expected in</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MemberOrder GetMemberOrder(out T obj)
        {
            var o = O;
            if (o != null)
            {
                obj = New();
                return o;
            }
            GetMembers(out obj);
            return O;
        }

        static MemberOrder O;

        /// <summary>
        /// The members, null until <see cref="GetMembers(out T)"/> (or <see cref="GetMemberOrder(out T)"/>) has been called
        /// </summary>
        public static MemberOrder Order
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => O;
        }

        /// <summary>
        /// The member assigners (in no particular order) and the order the members are expected in (learned from the data):
        /// a member is first compared to the member that followed the previous member the last time (one compare for json written in the same member order, like all json written by the <see cref="JsonWriter"/>),
        /// the look up is only used when that fails.
        /// </summary>
        /// <remarks>
        /// Thread safe, the learned order (<see cref="Next"/>) is updated without synchronization (an int is written atomically, a stale value just costs a look up).
        /// </remarks>
        public sealed class MemberOrder
        {
            public MemberOrder(Dictionary<Utf8Range, TypedAssigner> members)
            {
                var n = members.Count;
                var keys = new Byte[n][];
                var assigners = new TypedAssigner[n];
                var index = new Dictionary<Utf8Range, int>(n);
                int i = 0;
                foreach (var kv in members)
                {
                    keys[i] = kv.Key.Mem.ToArray();
                    assigners[i] = kv.Value;
                    index.Add(kv.Key, i);
                    ++i;
                }
                var next = new int[n + 1];
                for (i = 0; i < n; ++i)
                    next[i] = i + 1;
                Keys = keys;
                Assigners = assigners;
                Lookup = MemberLookUp<int>.Create(index);
                Next = next;
            }

            /// <summary>
            /// The UTF8 names of the members
            /// </summary>
            public readonly Byte[][] Keys;
            /// <summary>
            /// The assigners of the members (same index as the <see cref="Keys"/>)
            /// </summary>
            public readonly TypedAssigner[] Assigners;
            /// <summary>
            /// The index of a member by its UTF8 name
            /// </summary>
            public readonly IMemberLookUp<int> Lookup;
            /// <summary>
            /// The index of the member expected after a member (the index of the previous member, the number of members for the first member), may be out of range (no expectation)
            /// </summary>
            public readonly int[] Next;
        }

        /// <summary>
        /// Create an instance without calling a constructor (default for a value type, avoids boxing)
        /// </summary>
        static Func<T> NewUninitialized(Type t)
        {
            if (t.IsValueType)
                return Expression.Lambda<Func<T>>(Expression.Default(t)).Compile();
            return Expression.Lambda<Func<T>>(Expression.Convert(Expression.Call(ReadTypeCache.MethodGetUninitializedObject, Expression.Constant(t)), t)).Compile();
        }

        static Func<T> New;
        static IMemberLookUp<TypedAssigner> I;

        /// <summary>
        /// Parse the key (from the UTF8 <paramref name="key"/>), read the value at the current position and add them to the dictionary.
        /// </summary>
        public delegate void AddDictionaryItem(T dictionary, ReadOnlySpan<byte> key, JsonParserState state, Func<char, bool> endOn);

        static AddDictionaryItem Da;

        /// <summary>
        /// Get the dictionary item adder (built on first use) and create a new dictionary.
        /// </summary>
        /// <remarks>
        /// <typeparamref name="T"/> must be a concrete generic type with two type arguments, a public parameterless constructor and a public Add(key, value) method.
        /// Keys can be any type supported by <see cref="SpanParsers"/>, a <see cref="String"/> or an enum.
        /// </remarks>
        /// <param name="obj">A new (empty) dictionary</param>
        /// <returns>The item adder</returns>
        /// <exception cref="Exception">The key type isn't supported</exception>
        public static AddDictionaryItem GetDictionary(out T obj)
        {
            var i = Da;
            if (i != null)
            {
                obj = New();
                return i;
            }
            lock (Create)
            {
                i = Da;
                if (i != null)
                {
                    obj = New();
                    return i;
                }
                var t = typeof(T);
                New = Expression.Lambda<Func<T>>(Expression.New(t)).Compile();
                var dictExp = Expression.Parameter(t, "d");
                var keyExp = Expression.Parameter(typeof(ReadOnlySpan<byte>), "key");

                var types = t.GetGenericArguments();
                var keyType = types[0];
                var valueType = types[1];

                var keyValueExp = DictionaryKey.GetExpression(keyType, keyExp);

                var valueExp = ReadTypeCache.Get(valueType).CreateExp;
                var addExp = Expression.Call(dictExp, Helper.SafeGetMethod(t, nameof(Dictionary<int, int>.Add), BindingFlags.Public | BindingFlags.Instance), keyValueExp, valueExp);
                var x = Expression.Lambda<AddDictionaryItem>(addExp, dictExp, keyExp, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                i = x.Compile();
            }
            obj = New();
            Da = i;
            return i;
        }

        /// <summary>
        /// Create a new instance using the public parameterless constructor, or uninitialized (no constructor runs) if there is none.
        /// </summary>
        public static T CreateNew()
        {
            var n = New;
            if (n != null)
                return n();
            var t = typeof(T);
            if (t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null)
            {
                n = Expression.Lambda<Func<T>>(Expression.New(t)).Compile();
            }
            else
            {
                n = NewUninitialized(t);
            }
            New = n;
            return n();
        }

        /// <summary>
        /// Same as <see cref="CreateNew"/> but boxed.
        /// </summary>
        public static Object CreateNewBoxed()
        {
            var n = New;
            if (n != null)
                return n();
            var t = typeof(T);
            if (t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) != null)
            {
                n = Expression.Lambda<Func<T>>(Expression.New(t)).Compile();
            }
            else
            {
                n = NewUninitialized(t);
            }
            New = n;
            return n();
        }


    }

}
