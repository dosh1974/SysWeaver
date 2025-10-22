using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

namespace SysWeaver.Serialization.SwJson.Reader
{
    static class ReadTyped<T>
    {

        public delegate T TypedCreator(JsonParserState state, Func<char, bool> endOn);

        public delegate void TypedAssigner(ref T v, JsonParserState state, Func<char, bool> endOn);


        public static void Set(Expression v)
        {
            Create = Expression.Lambda<TypedCreator>(v, ReadTypeCache.TempCall).Compile();
        }
        //public static Expression CreateExp;
        public static TypedCreator Create;

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
                    New = Expression.Lambda<Func<T>>(Expression.Convert(Expression.Call(ReadTypeCache.MethodGetUninitializedObject, Expression.Constant(t)), t)).Compile();
                }
                var ti = new Dictionary<Utf8Range, TypedAssigner>();
                var o = Expression.Parameter(t.MakeByRefType(), "v");
                foreach (var m in t.GetMembers(BindingFlags.Instance | BindingFlags.Public))
                {
                    {
                        var p = m as PropertyInfo;
                        if (p != null)
                        {
                            if (p.CanRead && p.CanWrite)
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
                I = i;
                obj = New();
                return i;
            }
        }

        static Func<T> New;
        static IMemberLookUp<TypedAssigner> I;

        public delegate void AddDictionaryItem(T dictionary, ReadOnlySpan<byte> key, JsonParserState state, Func<char, bool> endOn);

        static AddDictionaryItem Da;

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

                var keyValueExp = SpanParsers.GetExpression(keyType, keyExp);
                if (keyValueExp == null)
                {
                    if (ReadTypeCache.JsonSpanReaders.TryGetValue(keyType, out var keyBuild))
                        keyValueExp = keyBuild(keyExp);
                }
                if (keyValueExp == null)
                    throw new Exception("Unsupported key type \"" + keyType + "\"");

                var valueExp = ReadTypeCache.Get(valueType).CreateExp;
                var addExp = Expression.Call(dictExp, Helper.SafeGetMethod(t, nameof(Dictionary<int, int>.Add), BindingFlags.Public | BindingFlags.Instance), keyValueExp, valueExp);
                var x = Expression.Lambda<AddDictionaryItem>(addExp, dictExp, keyExp, ReadTypeCache.ParState, ReadTypeCache.ParEndOn);
                i = x.Compile();
            }
            obj = New();
            Da = i;
            return i;
        }

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
                n = Expression.Lambda<Func<T>>(Expression.Convert(Expression.Call(ReadTypeCache.MethodGetUninitializedObject, Expression.Constant(t)), t)).Compile();
            }
            New = n;
            return n();
        }

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
                n = Expression.Lambda<Func<T>>(Expression.Convert(Expression.Call(ReadTypeCache.MethodGetUninitializedObject, Expression.Constant(t)), t)).Compile();
            }
            New = n;
            return n();
        }


    }

}
