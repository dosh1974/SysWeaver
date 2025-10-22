using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Docs;
using SysWeaver.Translation;

namespace SysWeaver.Data
{



    public static class TableDataTools
    {

        /// <summary>
        /// Filter some data using TableDataFilter's
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filters">The filters to apply</param>
        /// <param name="data">Source data</param>
        /// <returns>The resulting data</returns>
        public static IEnumerable<T> Filter<T>(TableDataFilter[] filters, IEnumerable<T> data)
            => TableDataType<T>.Filter(filters, data);


        /// <summary>
        /// Sort and filter some data using a table data request 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="data">Source data</param>
        /// <returns>The resulting data</returns>
        public static IEnumerable<T> SortAndFilter<T>(TableDataRequest request, IEnumerable<T> data)
            => TableDataType<T>.SortAndFilter(request ?? DefRequest, data);

        /// <summary>
        /// Sort, filter and limitsome data using a table data request 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="data">Source data</param>
        /// <param name="maxAllowedRows">Maximum allowed rows (minimum of this and the request is used)</param>
        /// <returns>The resulting data</returns>
        public static IEnumerable<T> SortAndFilterAndLimit<T>(TableDataRequest request, IEnumerable<T> data, long maxAllowedRows = 100000)
                => TableDataType<T>.SortAndFilterAndLimit(request ?? DefRequest, data, maxAllowedRows);


        /// <summary>
        /// Get table data from an enumerable sequence
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static TableData Get<T>(TableDataRequest request, IEnumerable<T> data, String title = null) 
            => TableDataType<T>.Get(request ?? DefRequest, data, title);


        /// <summary>
        /// Get table data from an enumerable sequence without any filtering, sorting or limiting
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">Source data</param>
        /// <returns>Some table data</returns>
        public static TableData GetAll<T>(IEnumerable<T> data)
            => TableDataType<T>.GetAll(data);

        /// <summary>
        /// Get table data from an enumerable sequence
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms to wait befor a new refresh</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static TableData Get<T>(TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null)
        {
            var r = TableDataType<T>.Get(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r;
        }

        /// <summary>
        /// Get table data from an enumerable sequence with any translations applied
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="translationContext">The translator and target language to use</param>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms to wait befor a new refresh</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static Task<TableData> Get<T>(ITranslationContext translationContext, TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null)
        {
            var r = TableDataType<T>.Get(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r.Translate<T>(translationContext);
        }



        static readonly TableDataRequest DefRequest = new ()
        {
            MaxRowCount = 20
        };

        #region Runtime manipulation

        #region Column header


        /// <summary>
        /// If table data columns exist, clone them and return true
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cols">The columns data to modify</param>
        /// <param name="addColumns">Add columns</param>
        /// <returns>True if column information exists, else false</returns>
        public static bool ModifyColumns(this TableData data, out TableDataColumn[] cols, int addColumns = 0)
        {
            var src = data.Cols;
            if (src == null)
            {
                cols = null;
                return false;
            }
            if (addColumns < 0)
                addColumns = 0;
            var l = src.Length;
            cols = GC.AllocateUninitializedArray<TableDataColumn>(l + addColumns);
            data.Cols = cols;
            for (int i = 0; i < l; i++)
                cols[i] = src[i].Clone();
            for (int i = 0; i < addColumns; ++i)
                cols[i + l] = new TableDataColumn();
            return true;
        }


        /// <summary>
        /// Remove a column, based on index
        /// </summary>
        /// <param name="data"></param>
        /// <param name="columnIndex">The columns data to modify</param>
        /// <returns>True if column information exists, else false</returns>
        public static TableData RemoveColumnIndex(this TableData data, int columnIndex)
        {
            var src = data.Cols;
            if (src != null)
                data.Cols = data.Cols.RemoveAt(columnIndex);
            var rows = data.Rows;
            if (rows != null)
                foreach (var x in rows)
                    x.Values = x.Values.RemoveAt(columnIndex);
            return data;
        }


        /// <summary>
        /// Hide a column in some table data
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (memeber name)</param>
        /// <returns>The source table data</returns>
        public static TableData HideColumn(this TableData data, String name)
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Props |= TableDataColumnProps.Hide;
            }
            return data;
        }

        /// <summary>
        /// Set the title of a column
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="title">The new title</param>
        /// <returns>The source table data</returns>
        public static TableData SetColumnTitle(this TableData data, String name, String title)
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Title = title;
            }
            return data;
        }

        /// <summary>
        /// Set the title of a column
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="getTitle">A function that gets a new title</param>
        /// <returns>The source table data</returns>
        public static TableData SetColumnTitle(this TableData data, String name, Func<TableDataColumn, String> getTitle)
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Title = getTitle(x);
            }
            return data;
        }


        /// <summary>
        /// Set the description of a column
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="desc">The new description</param>
        /// <returns>The source table data</returns>
        public static TableData SetColumnDesc(this TableData data, String name, String desc)
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Desc = desc;
            }
            return data;
        }

        /// <summary>
        /// Set the description of a column
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="getDesc">A function that gets a new description</param>
        /// <returns>The source table data</returns>
        public static TableData SetColumnDesc(this TableData data, String name, Func<TableDataColumn, String> getDesc)
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Title = getDesc(x);
            }
            return data;
        }

        #endregion //Column header

        #endregion //Runtime manipulation

        internal static readonly MethodInfo Order = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => (x.Name == nameof(Enumerable.OrderBy)) && (x.GetParameters().Length == 2));
        internal static readonly MethodInfo OrderDesc = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => (x.Name == nameof(Enumerable.OrderByDescending)) && (x.GetParameters().Length == 2));

        internal static readonly MethodInfo ThenBy = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => (x.Name == nameof(Enumerable.ThenBy)) && (x.GetParameters().Length == 2));
        internal static readonly MethodInfo ThenByDesc = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => (x.Name == nameof(Enumerable.ThenByDescending)) && (x.GetParameters().Length == 2));

        internal static readonly MethodInfo Where = typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public).First(x => (x.Name == nameof(Enumerable.Where)) && (x.GetParameters().Length == 2));

        /// <summary>
        /// For a given type and column name (member name), get the column index
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The column name (member name)</param>
        /// <returns>The column index or -1 if not found</returns>
        public static int GetColumnIndex<T>(String name) => TableDataType<T>.NameToColumnIndex.TryGetValue(name, out var index) ? index : -1;


        static readonly PropertyInfo TypeFullNameProp = typeof(Type).GetProperty(nameof(Type.FullName), BindingFlags.Public | BindingFlags.Instance);

        static Expression ConvType(Expression e)
        {
            var isNullExp = Expression.Equal(e, ExpHelper<Type>.Null);
            var propExp = Expression.Property(e, TypeFullNameProp);
            var con = Expression.Condition(isNullExp,
                            ExpHelper<String>.Null,
                            propExp);
            return con;
        }

        static readonly PropertyInfo ExceptionMessageProp = typeof(Exception).GetProperty(nameof(Exception.Message), BindingFlags.Public | BindingFlags.Instance);

        static Expression ConvException(Expression e)
        {
            var isNullExp = Expression.Equal(e, ExpHelper<Exception>.Null);
            var propExp = Expression.Property(e, ExceptionMessageProp);
            var con = Expression.Condition(isNullExp,
                            ExpHelper<String>.Null,
                            propExp);
            return con;
        }

        static readonly MethodInfo ToStringMethod = typeof(Object).GetMethod(nameof(Object.ToString), BindingFlags.Public | BindingFlags.Instance);

        static Expression ConvToString(Expression e)
        {
            var type = e.Type;
            var isNullExp = Expression.Equal(e, Expression.Constant(null, type));
            var propExp = Expression.Call(e, ToStringMethod);
            var con = Expression.Condition(isNullExp,
                            ExpHelper<String>.Null,
                            propExp);
            return con;
        }



        static Expression HandleObject(Expression e) => Expression.Call(ConvObjectMi, e);
        internal static ParameterExpression CmpValueExp = Expression.Parameter(typeof(String), "filterUsing");


        static Object ConvObject(Object o)
        {
            if (o == null)
                return o;
            var t = o.GetType();
            if (t.IsPrimitive)
                return o;
            if (t == typeof(Object))
                return o.ToString();
            return ValidDataTypes.ContainsKey(t) ? o : o.ToString();
        }

        static readonly MethodInfo ConvObjectMi = typeof(TableDataTools).GetMethod(nameof(ConvObject), BindingFlags.NonPublic| BindingFlags.Static);

        internal sealed class FtInfo
        {
            public readonly Func<Expression, Expression> TypeToData;
            public readonly Func<String, Object> StringToData;

            public FtInfo(Func<Expression, Expression> typeToData, Type type)
            {
                TypeToData = typeToData;
                StringToObject.TryGetConverter(type, out var c);
                StringToData = c;
            }
        }


        internal static readonly IReadOnlyDictionary<Type, FtInfo> ValidDataTypes = new Dictionary<Type, FtInfo>()
        {
            {  typeof(SByte), new FtInfo(null, typeof(SByte)) },
            {  typeof(Byte), new FtInfo(null, typeof(Byte)) },
            {  typeof(Int16), new FtInfo(null, typeof(Int16)) },
            {  typeof(UInt16), new FtInfo(null, typeof(UInt16)) },
            {  typeof(Int32), new FtInfo(null, typeof(Int32)) },
            {  typeof(UInt32), new FtInfo(null, typeof(UInt32)) },
            {  typeof(Int64), new FtInfo(null, typeof(Int64)) },
            {  typeof(UInt64), new FtInfo(null, typeof(UInt64)) },
            {  typeof(Single), new FtInfo(null, typeof(Single)) },
            {  typeof(Double), new FtInfo(null, typeof(Double)) },
            {  typeof(Decimal), new FtInfo(null, typeof(Decimal)) },
            {  typeof(Boolean), new FtInfo(null, typeof(Boolean)) },
            {  typeof(TimeSpan), new FtInfo(null, typeof(TimeSpan)) },
            {  typeof(DateTime), new FtInfo(null, typeof(DateTime)) },
            {  typeof(TimeOnly), new FtInfo(null, typeof(TimeOnly)) },
            {  typeof(DateOnly), new FtInfo(null, typeof(DateOnly)) },
            {  typeof(Guid), new FtInfo(null, typeof(Guid)) },
            {  typeof(String), new FtInfo(null, typeof(String)) },
            {  typeof(Type), new FtInfo(ConvType, typeof(Type)) },
            {  typeof(Exception), new FtInfo(ConvException, typeof(Exception)) },
            {  typeof(Object), new FtInfo(HandleObject, typeof(Object)) },
        }.Freeze();



        static readonly MethodInfo StringCompareCase = typeof(String).GetMethod(nameof(String.CompareOrdinal), BindingFlags.Public | BindingFlags.Static, [typeof(String), typeof(String)]);
        static readonly MethodInfo StringCompareNoCase = typeof(String).GetMethod(nameof(String.Compare), BindingFlags.Public | BindingFlags.Static, [typeof(String), typeof(String), typeof(StringComparison)]);


        static readonly MethodInfo StringContains = typeof(String).GetMethod(nameof(String.Contains), BindingFlags.Public | BindingFlags.Instance, [typeof(String), typeof(StringComparison)]);
        static readonly MethodInfo StringStartsWith = typeof(String).GetMethod(nameof(String.StartsWith), BindingFlags.Public | BindingFlags.Instance, [typeof(String), typeof(StringComparison)]);
        static readonly MethodInfo StringEndsWith = typeof(String).GetMethod(nameof(String.EndsWith), BindingFlags.Public | BindingFlags.Instance, [typeof(String), typeof(StringComparison)]);


        static readonly Expression CompCaseExp = Expression.Constant(StringComparison.Ordinal);
        static readonly Expression CompNoCaseExp = Expression.Constant(StringComparison.OrdinalIgnoreCase);
        static readonly Expression ConstInt32_0 = Expression.Constant(0, typeof(Int32));
        static readonly Expression ConstString_null = Expression.Constant(null, typeof(String));
        static readonly Expression ConstBoolean_false = Expression.Constant(false, typeof(Boolean));

        static readonly ConcurrentDictionary<Type, int> CompareTypes = new ConcurrentDictionary<Type, int>();


        static readonly IReadOnlySet<Type> UseIComparable = ReadOnlyData.Set(
            typeof(String),
            typeof(Boolean)
        );

        static int GetCompareType(Type t)
        {
            var cts = CompareTypes;
            if (cts.TryGetValue(t, out var i))
                return i;
            if (!UseIComparable.Contains(t))
            {
                try
                {
                    var c = Expression.Constant(StringToObject.GetDefaultValue(t), t);
                    Expression.GreaterThan(c, c);
                    Expression.LessThan(c, c);
                    Expression.Equal(c, c);
                    Expression.NotEqual(c, c);
                    Expression.GreaterThanOrEqual(c, c);
                    Expression.LessThanOrEqual(c, c);
                    i = 0;
                    cts.TryAdd(t, i);
                    return i;
                }
                catch
                {
                }
            }
            var ict = typeof(IComparable<>).MakeGenericType(t);
            if (ict.IsAssignableFrom(t))
            {
                i = 1;
                cts.TryAdd(t, i);
                return i;
            }
            i = 2;
            cts.TryAdd(t, i);
            return i;
        }

        static void HandleString(ref Expression value, ref Expression cmpValue, bool caseSensitive)
        {
            var vt = value.Type;
            if (vt == typeof(String))
            {
                if (caseSensitive)
                {
                    value = Expression.Call(null, StringCompareCase, value, cmpValue);
                }
                else
                {
                    value = Expression.Call(null, StringCompareNoCase, value, cmpValue, CompNoCaseExp);
                }
                cmpValue = ConstInt32_0;
                return;
            }
            switch (GetCompareType(vt))
            {
                case 1:
                    var ict = typeof(IComparable<>).MakeGenericType(vt);
                    var mi = ict.GetMethod(nameof(IComparable.CompareTo), BindingFlags.Public | BindingFlags.Instance, [vt]);
                    value = Expression.Call(value, mi, cmpValue);
                    cmpValue = ConstInt32_0;
                    break;
                case 2:
                    break;

            }
        }


        internal static readonly Func<Expression, Expression, bool, Expression>[] CmpFuncs = new[]
        {
            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.Equal(value, cmpValue);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.NotEqual(value, cmpValue);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.LessThan(value, cmpValue);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.GreaterThan(value, cmpValue);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.LessThanOrEqual(value, cmpValue);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                HandleString(ref value, ref cmpValue, caseSensitive);
                return Expression.GreaterThanOrEqual(value, cmpValue);
            }),
        };


        internal static readonly Func<Expression, Expression, bool, Expression>[] StringCmpFuncs = new[]
        {
            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                return Expression.Call(value, StringContains, cmpValue, caseSensitive ? CompCaseExp : CompNoCaseExp);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                return Expression.Call(value, StringStartsWith, cmpValue, caseSensitive ? CompCaseExp : CompNoCaseExp);
            }),

            new Func<Expression, Expression, bool, Expression>((value, cmpValue, caseSensitive) =>
            {
                return Expression.Call(value, StringEndsWith, cmpValue, caseSensitive ? CompCaseExp : CompNoCaseExp);
                //return Expression.Condition(Expression.Equal(value, ConstString_null), ConstBoolean_false, c);
            }),
        };


        internal static readonly Func<Expression, Expression, Expression>[] HashSetCmpFuncs = new[]
        {
            new Func<Expression, Expression, Expression>((value, cmpValue) =>
            {
                return TableDataTools.HashSetContains(cmpValue, value);
            }),

            new Func<Expression, Expression, Expression>((value, cmpValue) =>
            {
                return Expression.Not(TableDataTools.HashSetContains(cmpValue, value));
            }),
        };


        internal static readonly Func<Expression, Expression, Expression, bool, Expression>[] MinMaxCmpFuncs = new[]
        {
            new Func<Expression, Expression, Expression, bool, Expression>((value, min, max, caseSensitive) =>
            {
                var vmax = value;
                HandleString(ref value, ref min, caseSensitive);
                HandleString(ref vmax, ref max, caseSensitive);
                return Expression.And(Expression.GreaterThanOrEqual(value, min), Expression.LessThan(vmax, max));
            }),
            
            new Func<Expression, Expression, Expression, bool, Expression>((value, min, max, caseSensitive) =>
            {
                var vmax = value;
                HandleString(ref value, ref min, caseSensitive);
                HandleString(ref vmax, ref max, caseSensitive);
                return Expression.Or(Expression.LessThan(value, min), Expression.GreaterThan(vmax, max));
            }),


        };


        static int GetOrder(MemberInfo mi) => mi.GetCustomAttribute<TableDataOrderAttribute>()?.Order ?? 0;

        static internal IEnumerable<MemberInfo> GetPublicInstanceMembers(Type t)
        {
            Func<MemberInfo, int> orderFn = GetOrder;
            if (t.IsInterface)
            {
                HashSet<String> seen = new HashSet<string>();
                List<MemberInfo> mi = new List<MemberInfo>();
                foreach (var x in t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (x.MemberType != MemberTypes.Method)
                    {
                        if (!seen.Add(x.Name))
                            continue;
                        mi.Add(x);
                    }
                }
                foreach (var i in t.GetInterfaces())
                {
                    foreach (var x in i.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (x.MemberType != MemberTypes.Method)
                        {
                            if (!seen.Add(x.Name))
                                continue;
                            mi.Add(x);
                        }
                    }
                }
                foreach (var x in mi.OrderBy(orderFn))
                    yield return x;
            }else
            {
                foreach (var x in t.GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(i => i.MemberType != MemberTypes.Method).OrderBy(orderFn))
                    yield return x;
            }
        }

        /// <summary>
        /// Get column information for a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static TableDataColumn[] GetCols<T>() => TableDataType<T>.Cols;

        /// <summary>
        /// Get column types for a type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Type[] GetColTypes<T>() => TableDataType<T>.ColTypes;


        /// <summary>
        /// Get column information for a type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static TableDataColumn[] GetCols(Type t)
        {
            var gt = (TableDataColumn[])typeof(TableDataType<>).MakeGenericType(t).GetField(nameof(TableDataType<int>.Cols), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return gt;
        }

        /// <summary>
        /// Get column types for a type
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Type[] GetColTypes(Type t)
        {
            var gt = (Type[])typeof(TableDataType<>).MakeGenericType(t).GetField(nameof(TableDataType<int>.ColTypes), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return gt;
        }

        /// <summary>
        /// Extract data from enumerable (should already be filtered, ordered, offsetted etc)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="count">Count (inluding lookahead)</param>
        /// <param name="data">Enumerable data</param>
        /// <param name="limit">Maximum number of rows to take</param>
        /// <param name="lookAhead">Number of rows to look ahead</param>
        /// <returns>Data rows</returns>
        public static TableDataRow[] ExtractGet<T>(out long count, IEnumerable<T> data, long limit = long.MaxValue, long lookAhead = 0) => TableDataType<T>.ExtractGet(out count, data, limit, lookAhead);


        internal static readonly ConstantExpression CommaArrayCharExp = Expression.Constant(new Char[] { ',' });
        internal static readonly ConstantExpression StringSplitOptionsExp = Expression.Constant(StringSplitOptions.TrimEntries);

        internal static readonly MethodInfo StringSplitMethod = typeof(String).GetMethod(nameof(String.Split), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(Char[]), typeof(StringSplitOptions) });
        internal static readonly MethodInfo LinqSelectMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == nameof(Enumerable.Select)).First(mi => mi.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);

        internal static readonly MethodInfo LinqMinMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsGenericMethod && x.Name == nameof(Enumerable.Min)).First(mi => mi.GetParameters().Length == 1);
        internal static readonly MethodInfo LinqMaxMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsGenericMethod && x.Name == nameof(Enumerable.Max)).First(mi => mi.GetParameters().Length == 1);
        internal static readonly IReadOnlyDictionary<Type, MethodInfo> LinqMinMethods;
        internal static readonly IReadOnlyDictionary<Type, MethodInfo> LinqMaxMethods;

        public static Expression GetEnumerableTypeFromStringArray(Expression stringValueExpression, Type type)
        {
            var stringCol = Expression.Call(stringValueExpression, StringSplitMethod, CommaArrayCharExp, StringSplitOptionsExp);
            if (type != typeof(String))
            {
                var selM = LinqSelectMethod.MakeGenericMethod(typeof(String), type);
                stringCol = Expression.Call(selM, stringCol, StringConverterExp.GetFromStringLambdaExp(type));
            }
            return stringCol;
        }

        public static Expression GetHashSetFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            var hashCon = typeof(HashSet<>).MakeGenericType(elementType).GetConstructor(new Type[] { typeof(IEnumerable<>).MakeGenericType(elementType) });
            var hashSetExpression = Expression.New(hashCon, enumExpression);
            return hashSetExpression;
        }

        public static Expression GetHashSetFromEnum(Expression enumExpression, bool caseSensitive)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            if (elementType != typeof(String))
                return GetHashSetFromEnum(enumExpression);
            var hashCon = typeof(HashSet<>).MakeGenericType(elementType).GetConstructor(new Type[] { typeof(IEnumerable<>).MakeGenericType(elementType), typeof(StringComparer) });
            var hashSetExpression = Expression.New(hashCon, enumExpression, Expression.Constant(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase));
            return hashSetExpression;
        }



        static readonly MethodInfo MinMaxMethod = typeof(EnumerableExt).GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == nameof(EnumerableExt.MinMax) && x.GetParameters().Length == 2);

        public static Expression GetMinMaxFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            if (et.HasElementType)
                enumExpression = Expression.Convert(enumExpression, typeof(IEnumerable<>).MakeGenericType(elementType));
            var cnull = Expression.Constant(null, typeof(IComparer<>).MakeGenericType(elementType));
            var mi = MinMaxMethod.MakeGenericMethod(elementType);
            return Expression.Call(mi, enumExpression, cnull);
        }


        public static Expression GetMinFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            return Expression.Call(LinqMinMethods.TryGetValue(elementType, out var m) ? m : LinqMinMethod.MakeGenericMethod(elementType), enumExpression);
        }

        public static Expression GetMaxFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            return Expression.Call(LinqMaxMethods.TryGetValue(elementType, out var m) ? m : LinqMaxMethod.MakeGenericMethod(elementType), enumExpression);
        }

        public static Expression HashSetContains(Expression hashSetExpression, Expression value)
        {
            var elementType = hashSetExpression.Type.GetGenericArguments()[0];
            var cm = typeof(HashSet<>).MakeGenericType(elementType).GetMethod(nameof(HashSet<int>.Contains), BindingFlags.Public | BindingFlags.Instance);
            var ce = Expression.Call(hashSetExpression, cm, value);
            return ce;
        }



        static TableDataTools()
        {
            var min = new Dictionary<Type, MethodInfo>();
            var max = new Dictionary<Type, MethodInfo>();
            foreach (var x in typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (x.Name == nameof(Enumerable.Min))
                {
                    if (x.GetParameters().Length != 1)
                        continue;
                    if (x.IsGenericMethod)
                    {
                        LinqMinMethod = x;
                        continue;
                    }
                    min[x.ReturnType] = x;
                }
                if (x.Name == nameof(Enumerable.Max))
                {
                    if (x.GetParameters().Length != 1)
                        continue;
                    if (x.IsGenericMethod)
                    {
                        LinqMaxMethod = x;
                        continue;
                    }
                    max[x.ReturnType] = x;

                }
            }
            LinqMinMethods = min.Freeze();
            LinqMaxMethods = max.Freeze();
        }


        /// <summary>
        /// Create a function that returns table data from some static data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">The static data (a copy won't be made)</param>
        /// <param name="columns">Optionally use these column data instead of the ones dervied from the type and attributes, must match order, column type and name etc</param>
        /// <param name="title">Optional title</param>
        /// <returns>A function that can be used to get the static data</returns>
        public static Func<TableDataRequest, TableData> GetStaticTableFn<T>(IEnumerable<T> data, TableDataColumn[] columns = null, String title = null)
        {
            var d = data.ToList();
            if (columns == null)
                return r => Get(r, d);
            var cols = TableDataType<T>.Cols;
            var cl = cols.Length;
            if (columns.Length != cl)
                throw new Exception("Invalid number of columns!");
            TableDataColumn[] destCols = new TableDataColumn[cl];
            for (int i = 0; i < cl; ++ i)
            {
                var dc = new TableDataColumn();
                destCols[i] = dc;
                var def = columns[i];
                var gen = cols[i];
                dc.Name = gen.Name;
                dc.Type = gen.Type;
                dc.Desc = def.Desc ?? gen.Desc;
                dc.Format = def.Format ?? gen.Format;
                dc.Title = String.IsNullOrEmpty(def.Title) ? gen.Title : def.Title;
                dc.Props = gen.Props | (def.Props & (TableDataColumnProps.Hide | TableDataColumnProps.AnyKey | TableDataColumnProps.CanChart));
            }
            var cc = EnvInfo.Cc;
            return r =>
            {
                var t = Get(r, d);
                var isNew = r.Cc != cc;
                var ret = new TableData
                {
                    Cc = cc,
                    Cols = isNew ? destCols : null,
                    Title = isNew ? title : null,
                    RowCount = t.RowCount,
                    RefreshRate = t.RefreshRate,
                    Rows = t.Rows,
                };
                return ret;
            };
        }



        static readonly IReadOnlyDictionary<ValueTuple<Type, Type>, Func<Object, Object>> Converters = new Dictionary<ValueTuple<Type, Type>, Func<Object, Object>>()
        {
            { ValueTuple.Create(typeof(String), typeof(TimeSpan)), new Func<Object,Object>(x => TimeSpan.Parse(x as String)) },
            { ValueTuple.Create(typeof(String), typeof(DateTime)), new Func<Object,Object>(x => DateTime.Parse(x as String)) },
            { ValueTuple.Create(typeof(String), typeof(DateOnly)), new Func<Object,Object>(x => DateOnly.Parse(x as String)) },
            { ValueTuple.Create(typeof(String), typeof(TimeOnly)), new Func<Object,Object>(x => TimeOnly.Parse(x as String)) },
            { ValueTuple.Create(typeof(String), typeof(DateTimeOffset)), new Func<Object,Object>(x => DateTimeOffset.Parse(x as String)) },
            { ValueTuple.Create(typeof(String), typeof(Guid)), new Func<Object,Object>(x => Guid.Parse(x as String)) },

        }.Freeze();

        static Object ChangeType(Object val, Type type)
        {
            if (val == null)
                return val;
            var vt = val.GetType();
            if (type.IsAssignableFrom(vt))
                return val;
            try
            {
                var kv = ValueTuple.Create(vt, type);
                if (Converters.TryGetValue(kv, out var c))
                    return c(val);
                if (type.IsEnum)
                {
                    val = ChangeType(val, type.GetEnumUnderlyingType());
                    return Enum.ToObject(type, val);
                }
                return Convert.ChangeType(val, type);
            }
            catch
            {
                return val;
            }
        }


        static readonly MethodInfo ChangeTypeMethod = typeof(TableDataTools).GetMethod(nameof(ChangeType), BindingFlags.NonPublic| BindingFlags.Static);

        /// <summary>
        /// Create a function that returns table data from some static data
        /// </summary>
        /// <param name="columns">The columns in the table</param>
        /// <param name="rows">Data for each row and column</param>
        /// <param name="title">Optional title</param>
        /// <returns>A function that can be used to get the static data</returns>
        public static Func<TableDataRequest, TableData> GetStaticTableFn(TableDataColumn[] columns, IEnumerable<object[]> rows, String title = null)
        {
            var type = GetDynType(out var createFn, columns);
            return createFn(rows.GetEnumerator(), columns, title);
        }

        static readonly ParameterExpression InputRows = Expression.Parameter(typeof(IEnumerator<object[]>), "rows");
        static readonly ParameterExpression VarCols = Expression.Variable(typeof(object[]), "columnValues");
        static readonly ParameterExpression InputColumns = Expression.Variable(typeof(TableDataColumn[]), "colDefs");
        static readonly ParameterExpression Title = Expression.Variable(typeof(String), "title");


        static readonly MethodInfo MethodMoveNext = typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext));
        static readonly PropertyInfo PropCurrent = typeof(IEnumerator<Object[]>).GetProperties().First(x => (x.Name == nameof(IEnumerator<Object[]>.Current)) && (x.PropertyType != typeof(Object)));
        static readonly MethodInfo MethodGetStaticTableFn = typeof(TableDataTools).GetMethods().First(x => (x.Name == nameof(GetStaticTableFn)) && x.IsGenericMethod);


        static readonly AssemblyName AsmName = new AssemblyName("TableDataDynamicTypes_" + Guid.NewGuid().ToString().Replace('-', '_'));
        static readonly AssemblyBuilder AsmBuilder = AssemblyBuilder.DefineDynamicAssembly(AsmName, AssemblyBuilderAccess.Run);
        static readonly ModuleBuilder ModuleBuilder = AsmBuilder.DefineDynamicModule(AsmName.Name);

        static readonly ConstructorInfo ObjectCon = typeof(Object).GetConstructor([]);


        static long TypeName;

        static Type GetDynType(out Func<IEnumerator<Object[]>, TableDataColumn[], String, Func<TableDataRequest, TableData>> createFn, TableDataColumn[] defs)
        {
            var types = defs.Select(x => TypeFinder.Get(x.Type)).ToArray();
            var names = defs.Select(x => x.Name).ToArray();
            var key = String.Join('|', String.Join('/', types.Select(x => x.FullName)), String.Join("\\", names));
            var cache = TypeCache;
            if (cache.TryGetValue(key, out var c))
            {
                createFn = c.Item1;
                return c.Item2;
            }
            lock (cache)
            {
                if (cache.TryGetValue(key, out c))
                {
                    createFn = c.Item1;
                    return c.Item2;
                }
                var l = types.Length;
                var inpRows = InputRows;
                var varCols = VarCols;

                var typeName = "TableDatatDynType" + Interlocked.Increment(ref TypeName);
                var typeBuilder = ModuleBuilder.DefineType(typeName, TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class);
                var fields = new FieldBuilder[l];
                for (int i = 0; i < l; ++ i)
                    fields[i] = typeBuilder.DefineField(names[i], types[i], FieldAttributes.Public | FieldAttributes.InitOnly);

                var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, types);
                var constructorIl = constructor.GetILGenerator();

                constructorIl.Emit(OpCodes.Ldarg_0);
                constructorIl.Emit(OpCodes.Call, ObjectCon);
                for (int i = 0; i < l; ++ i)
                {
                    constructorIl.Emit(OpCodes.Ldarg_0);
                    constructorIl.Emit(OpCodes.Ldarg, i + 1);
                    constructorIl.Emit(OpCodes.Stfld, fields[i]);
                }
                constructorIl.Emit(OpCodes.Ret);


                var type = typeBuilder.CreateType();





                var listType = typeof(List<>).MakeGenericType(type);
                var MethodListAdd = listType.GetMethods().First(x => (x.Name == nameof(List<Object>.Add)) && (x.GetParameters()[0].ParameterType == type));


                var varList = Expression.Variable(listType, "list");
                List<Expression> program = new List<Expression>(4);
                program.Add(Expression.Assign(varList, Expression.New(listType)));
                List<Expression> innerLoop = new List<Expression>(4);
                var label = Expression.Label("endLoop");
                innerLoop.Add(Expression.IfThenElse(Expression.Call(inpRows, MethodMoveNext), Expression.Default(typeof(void)), Expression.Break(label)));
                innerLoop.Add(Expression.Assign(varCols, Expression.Property(inpRows, PropCurrent)));
                var args = new Expression[l];
                var ctm = ChangeTypeMethod;
                for (int i = 0; i < l; i++)
                {
                    Expression val = Expression.ArrayAccess(varCols, Expression.Constant(i));
                    var valType = types[i];
                    val = Expression.Call(ctm, val, Expression.Constant(valType));
                    args[i] = Expression.Convert(val, valType);
                }
                var ci = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, types);
                var ct = Expression.New(ci, args);
                innerLoop.Add(Expression.Call(varList, MethodListAdd, ct));
                program.Add(Expression.Loop(Expression.Block([varCols], innerLoop), label));
                var m = MethodGetStaticTableFn.MakeGenericMethod(type);
                var inpCols = InputColumns;
                var inpTitle = Title;
                program.Add(Expression.Call(m, varList, inpCols, inpTitle));
                var expProg = Expression.Block([varList], program);
                createFn = Expression.Lambda<Func<IEnumerator<Object[]>, TableDataColumn[], String, Func<TableDataRequest, TableData>>>(expProg, inpRows, inpCols, inpTitle).Compile();
                c = Tuple.Create(createFn, type);
                if (!cache.TryAdd(key, c))
                {
                    c = cache[key];
                    createFn = c.Item1;
                    return c.Item2;
                }
                return type;
            }
        }

        static readonly ConcurrentDictionary<String, Tuple<Func<IEnumerator<Object[]>, TableDataColumn[], String, Func<TableDataRequest, TableData>>, Type>> TypeCache = new (StringComparer.Ordinal);


        public static TableDataFilter[] GetDefaultFilterRow<T>()
        {
            var c = TableDataType<T>.Cols;
            var l = c.Length;
            var f = new TableDataFilter[l];
            for (int i = 0; i < l; ++ i)
            {
                f[i] = new TableDataFilter
                {
                    ColName = c[i].Name
                };
            }
            return f;
        }

        public static TableDataFilter[] RemoveHidden<T>(TableDataFilter[] f)
        {
            var c = TableDataType<T>.Cols;
            var l = f.Length;
            int o = 0;
            for (int i = 0; i < l; ++ i)
            {
                if ((c[i].Props & TableDataColumnProps.Hide) != 0)
                    continue;
                f[o] = f[i];
                ++o;
            }
            if (o != l)
                Array.Resize(ref f, o);
            return f;
        }


        public static TableDataState StateFromRequest<T>(TableDataRequest r)
        {
            var cindex = TableDataType<T>.NameToColumnIndex;
            List<TableDataFilter[]> filters = new List<TableDataFilter[]>(3);
            filters.Add(GetDefaultFilterRow<T>());
            var f = r.Filters;
            var count = f?.Length ?? 0;
            if (count > 0)
            {
                var colCount = filters[0].Length;
                int[] rowIndices = new int[colCount];
                for (int i = 0; i < count; ++ i)
                {
                    var ff = f[i];
                    if (ff.Value == null)
                        continue;
                    if (!cindex.TryGetValue(ff.ColName, out var ci))
                        continue;
                    var p = rowIndices[ci];
                    if (p >= filters.Count)
                        filters.Add(GetDefaultFilterRow<T>());
                    filters[p][ci] = ff;
                    ++p;
                    rowIndices[ci] = p;
                }
            }
            var fc = filters.Count;
            for (int i = 0; i < fc; ++i)
                filters[i] = RemoveHidden<T>(filters[i]);
            return new TableDataState
            {
                Filters = filters.ArrayOrNullIfEmpty(),
                FilterRows = filters.Count,
                RequestParams = r,
            };
        }



        internal static Expression GetStaticObject(Type type)
        {
            var c = DefaultObjects;
            if (c.TryGetValue(type, out var exp))
                return exp;
            var ci = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>());
            if (ci != null)
            {
                try
                {
                    var t = ci.Invoke(null);
                    exp = Expression.Constant(t, type);
                }
                catch
                {
                }
            }
            c.TryAdd(type, exp);
            return exp;
        }

        static readonly ConcurrentDictionary<Type, Expression> DefaultObjects = new ConcurrentDictionary<Type, Expression>();

        /// <summary>
        /// Column types that can be charted (value axis)
        /// </summary>
        public static readonly IReadOnlySet<String> ChartTypes = ReadOnlyData.Set(StringComparer.Ordinal,
            [
                typeof(SByte).FullName,
                typeof(Int16).FullName,
                typeof(Int32).FullName,
                typeof(Int64).FullName,
                typeof(Byte).FullName,
                typeof(UInt16).FullName,
                typeof(UInt32).FullName,
                typeof(UInt64).FullName,

                typeof(Single).FullName,
                typeof(Double).FullName,
                typeof(Decimal).FullName,

                typeof(Boolean).FullName,
                typeof(TimeSpan).FullName,
                typeof(DateTime).FullName,

            ]);



        /// <summary>
        /// Translate the data in a table data.
        /// </summary>
        /// <typeparam name="T">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate</param>
        /// <param name="translator">The translator to use</param>
        /// <param name="to">The target language</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>A task to await for translation completion</returns>
        public static async Task<TableData> Translate<T>(this TableData data, ITranslator translator, String to, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long)
        {
            if ((translator == null) || (data == null))
                return data;
            var tr = TableDataType<T>.TranslateRow;
            if (tr == null)
                return data;
            var rows = data.Rows;
            if (rows == null)
                return data;
            var l = rows.Length;
            if (l <= 0)
                return data;
            if (l == 1)
            {
                await tr(translator, to, rows[0].Values, effort, retention).ConfigureAwait(false);
                return data;
            }
            var tasks = new Task[l];
            for (int i = 0; i < l; ++i)
                tasks[i] = tr(translator, to, rows[i].Values, effort, retention);
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Translate the data in a table data.
        /// </summary>
        /// <typeparam name="T">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate</param>
        /// <param name="translationContext">The translator and target language to use</param>
        /// <returns>A task to await for translation completion</returns>
        public static Task<TableData> Translate<T>(this TableData data, ITranslationContext translationContext)
            => Translate<T>(data, translationContext?.Translator, translationContext?.Language);

    }






}
