using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SysWeaver.Serialization.SwJson.Writer;

namespace SysWeaver.Serialization.SwJson
{
    /// <summary>
    /// Building blocks for the generated writers (expression trees), that avoids delegate calls, boxing and interface calls
    /// </summary>
    unsafe partial class JsonWriter
    {
        #region Constants

        /// <summary>
        /// Constants up to this size are written using 8 byte stores of immediate values (else they are copied from an array)
        /// </summary>
        const int MaxImmediateConstant = 32;

        //  These write 8 bytes at a time, so up to 7 bytes after the constant are overwritten (the 64 byte slack of every Ensure covers that)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteConst8(ref BufferWriter w, ulong a, int length)
        {
            var o = w.Offset;
            var d = w.DataPtr + o;
            Unsafe.WriteUnaligned(d, a);
            w.Offset = o + length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteConst16(ref BufferWriter w, ulong a, ulong b, int length)
        {
            var o = w.Offset;
            var d = w.DataPtr + o;
            Unsafe.WriteUnaligned(d, a);
            Unsafe.WriteUnaligned(d + 8, b);
            w.Offset = o + length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteConst24(ref BufferWriter w, ulong a, ulong b, ulong c, int length)
        {
            var o = w.Offset;
            var d = w.DataPtr + o;
            Unsafe.WriteUnaligned(d, a);
            Unsafe.WriteUnaligned(d + 8, b);
            Unsafe.WriteUnaligned(d + 16, c);
            w.Offset = o + length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteConst32(ref BufferWriter w, ulong a, ulong b, ulong c, ulong e, int length)
        {
            var o = w.Offset;
            var d = w.DataPtr + o;
            Unsafe.WriteUnaligned(d, a);
            Unsafe.WriteUnaligned(d + 8, b);
            Unsafe.WriteUnaligned(d + 16, c);
            Unsafe.WriteUnaligned(d + 24, e);
            w.Offset = o + length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void WriteConstBytes(ref BufferWriter w, Byte[] data)
        {
            var o = w.Offset;
            var l = data.Length;
            Unsafe.CopyBlockUnaligned(ref *(w.DataPtr + o), ref MemoryMarshal.GetArrayDataReference(data), (uint)l);
            w.Offset = o + l;
        }

        /// <summary>
        /// The methods used by the generated code (in a separate class, the static fields of JsonWriter are initialized in an undefined order across the partial files)
        /// </summary>
        static class FastMethods
        {
            static MethodInfo Get(String name) => Helper.SafeGetMethod(typeof(JsonWriter), name, BindingFlags.NonPublic | BindingFlags.Static);

            public static readonly MethodInfo WriteConst8 = Get(nameof(JsonWriter.WriteConst8));
            public static readonly MethodInfo WriteConst16 = Get(nameof(JsonWriter.WriteConst16));
            public static readonly MethodInfo WriteConst24 = Get(nameof(JsonWriter.WriteConst24));
            public static readonly MethodInfo WriteConst32 = Get(nameof(JsonWriter.WriteConst32));
            public static readonly MethodInfo WriteConstBytes = Get(nameof(JsonWriter.WriteConstBytes));
            public static readonly MethodInfo WriteStringOrNull = Get(nameof(JsonWriter.WriteStringOrNull));
            public static readonly MethodInfo WriteByteArrayEnsure = Get(nameof(JsonWriter.WriteByteArrayEnsure));
        }

        /// <summary>
        /// Get 8 bytes of a constant (zero padded) as a little endian ulong
        /// </summary>
        static ulong GetConstPart(Byte[] data, int offset)
        {
            ulong v = 0;
            for (int i = 7; i >= 0; --i)
            {
                v <<= 8;
                var p = offset + i;
                if (p < data.Length)
                    v |= data[p];
            }
            return v;
        }

        /// <summary>
        /// An expression that writes a constant (the space must be ensured, including 7 extra bytes)
        /// </summary>
        static Expression GetWriteConstExp(Byte[] data)
        {
            var l = data.Length;
            var w = WriterExp;
            var len = GetInt32Exp(l);
            if (l <= 8)
                return Expression.Call(FastMethods.WriteConst8, w, Expression.Constant(GetConstPart(data, 0)), len);
            if (l <= 16)
                return Expression.Call(FastMethods.WriteConst16, w, Expression.Constant(GetConstPart(data, 0)), Expression.Constant(GetConstPart(data, 8)), len);
            if (l <= 24)
                return Expression.Call(FastMethods.WriteConst24, w, Expression.Constant(GetConstPart(data, 0)), Expression.Constant(GetConstPart(data, 8)), Expression.Constant(GetConstPart(data, 16)), len);
            if (l <= MaxImmediateConstant)
                return Expression.Call(FastMethods.WriteConst32, w, Expression.Constant(GetConstPart(data, 0)), Expression.Constant(GetConstPart(data, 8)), Expression.Constant(GetConstPart(data, 16)), Expression.Constant(GetConstPart(data, 24)), len);
            return Expression.Call(FastMethods.WriteConstBytes, w, Expression.Constant(data));
        }

        #endregion//Constants

        #region Values

        /// <summary>
        /// Write a string or null (like InternalMaybeNull&lt;String&gt; without the delegate calls)
        /// </summary>
        static void WriteStringOrNull(ref BufferWriter w, String value)
        {
            if (value == null)
            {
                w.Ensure(64);
                var o = w.Offset;
                //  "null"
                Unsafe.WriteUnaligned(w.DataPtr + o, 0x6c6c756eu);
                w.Offset = o + 4;
                return;
            }
            WriteString(ref w, value);
        }


        /// <summary>
        /// An expression that writes a value of a type that isn't bounded (the writer ensures the space it needs).
        /// Same rules as for members: value types are written with Internal&lt;T&gt;, sealed types with InternalMaybeNull&lt;T&gt; (string and byte[] without delegates)
        /// and other types with InternalMaybeBoxed&lt;T&gt; (polymorphism).
        /// </summary>
        static Expression GetWriteUnboundedExp(Type type, Expression value)
        {
            var w = WriterExp;
            if (type == typeof(String))
                return Expression.Call(FastMethods.WriteStringOrNull, w, value);
            if (type == typeof(Byte[]))
                return Expression.Call(FastMethods.WriteByteArrayEnsure, w, value);
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
            {
                //  A nullable of a bounded type: inline the code of the Nullable<T> writer (avoids boxing the value)
                var inner = CacheWriter(type.GetGenericArguments()[0]);
                if (inner.BoundedSize > 0)
                {
                    var size = Math.Max(inner.BoundedSize, 32);
                    var v = Expression.Variable(type, "n");
                    return Expression.Block([v],
                        Expression.Assign(v, value),
                        Expression.Call(w, MethodBufferWriterEnsure, GetInt32Exp(size)),
                        Expression.IfThenElse(
                            Expression.Property(v, nameof(Nullable<int>.HasValue)),
                            inner.WriteExp(Expression.Property(v, nameof(Nullable<int>.Value))),
                            GetWriteConstExp(NullData)));
                }
            }
            if (type.IsPrimitive || type.IsValueType)
                return Expression.Call(MethodInternal.MakeGenericMethod(type), w, value);
            if (type.IsSealed)
                return Expression.Call(MethodInternalMaybeNull.MakeGenericMethod(type), w, value);
            return Expression.Call(MethodInternalMaybeBoxed.MakeGenericMethod(type), w, value);
        }

        /// <summary>
        /// An expression that writes a value of any type, including the Ensure needed for bounded types
        /// </summary>
        static Expression GetWriteValueExp(Type type, TypeInfo typeWriter, Expression value)
        {
            var size = typeWriter.BoundedSize;
            if (size > 0)
                return Expression.Block(Expression.Call(WriterExp, MethodBufferWriterEnsure, GetInt32Exp(size + 8)), typeWriter.WriteExp(value));
            return GetWriteUnboundedExp(type, value);
        }

        #endregion//Values

        #region Collections

        /// <summary>
        /// Find a public GetEnumerator() (like foreach does), that returns an enumerator with MoveNext() and Current.
        /// Most collections have a struct enumerator (no allocation and no interface calls).
        /// </summary>
        static bool TryGetEnumeratorPattern(Type type, out MethodInfo getEnumerator, out MethodInfo moveNext, out PropertyInfo current)
        {
            moveNext = null;
            current = null;
            getEnumerator = type.GetMethod(nameof(IEnumerable<int>.GetEnumerator), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (getEnumerator == null)
                return false;
            var et = getEnumerator.ReturnType;
            if (et.IsInterface)
                return false;
            moveNext = et.GetMethod(nameof(IEnumerator<int>.MoveNext), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            current = et.GetProperty(nameof(IEnumerator<int>.Current), BindingFlags.Public | BindingFlags.Instance);
            return (moveNext != null) && (moveNext.ReturnType == typeof(bool)) && (current != null);
        }

        /// <summary>
        /// Build an expression that loops over all items of a collection, calling writeItem(item, isFirst) for every item
        /// </summary>
        /// <param name="type">The collection type</param>
        /// <param name="itemType">The item type</param>
        /// <param name="collection">The collection (of type "type")</param>
        /// <param name="writeItem">Builds the expression that writes an item, the bool is true for the first item (no comma)</param>
        /// <param name="vars">Variables used by the expression</param>
        static Expression GetLoopExp(Type type, Type itemType, Expression collection, Func<Expression, bool, Expression> writeItem, List<ParameterExpression> vars)
        {
            var exit = Expression.Label("exit");
            //  Arrays and lists: index loop
            bool isList = type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>));
            if ((type.IsArray && (type.GetArrayRank() == 1) && (type.GetElementType() == itemType)) || isList)
            {
                var i = Expression.Variable(typeof(int), "i");
                var len = Expression.Variable(typeof(int), "l");
                vars.Add(i);
                vars.Add(len);
                Func<Expression, Expression> item;
                Expression count;
                if (isList)
                {
                    var indexer = type.GetProperty("Item", [typeof(int)]);
                    item = idx => Expression.Property(collection, indexer, idx);
                    count = Expression.Property(collection, nameof(List<int>.Count));
                }
                else
                {
                    item = idx => Expression.ArrayIndex(collection, idx);
                    count = Expression.ArrayLength(collection);
                }
                return Expression.Block(
                    Expression.Assign(len, count),
                    Expression.IfThen(Expression.GreaterThan(len, GetInt32Exp(0)),
                        Expression.Block(
                            writeItem(item(GetInt32Exp(0)), true),
                            Expression.Assign(i, GetInt32Exp(1)),
                            Expression.Loop(
                                Expression.Block(
                                    Expression.IfThen(Expression.GreaterThanOrEqual(i, len), Expression.Break(exit)),
                                    writeItem(item(i), false),
                                    Expression.PreIncrementAssign(i)
                                ), exit))));
            }
            //  Pattern based enumerator (struct enumerators) or the IEnumerable<T> interface
            Expression enumerable;
            MethodInfo getEnum;
            MethodInfo moveNext;
            PropertyInfo current;
            if (!TryGetEnumeratorPattern(type, out getEnum, out moveNext, out current) || (current.PropertyType != itemType))
            {
                var et = typeof(IEnumerable<>).MakeGenericType(itemType);
                var ett = typeof(IEnumerator<>).MakeGenericType(itemType);
                getEnum = Helper.SafeGetMethod(et, nameof(IEnumerable<int>.GetEnumerator), BindingFlags.Instance | BindingFlags.Public);
                moveNext = MethodMoveNext;
                current = ett.GetProperty(nameof(IEnumerator<int>.Current), BindingFlags.Instance | BindingFlags.Public);
                enumerable = Expression.Convert(collection, et);
            }
            else
            {
                enumerable = collection;
            }
            var enumerator = Expression.Variable(getEnum.ReturnType, "e");
            vars.Add(enumerator);
            return Expression.Block(
                Expression.Assign(enumerator, Expression.Call(enumerable, getEnum)),
                Expression.IfThen(Expression.Call(enumerator, moveNext),
                    Expression.Block(
                        writeItem(Expression.Property(enumerator, current), true),
                        Expression.Loop(
                            Expression.Block(
                                Expression.IfThen(Expression.Not(Expression.Call(enumerator, moveNext)), Expression.Break(exit)),
                                writeItem(Expression.Property(enumerator, current), false)
                            ), exit))));
        }

        #endregion//Collections
    }
}
