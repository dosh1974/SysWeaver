// https://github.com/SimpleStack/simplestack.orm

using SimpleStack.Orm;
using SimpleStack.Orm.Attributes;
using SimpleStack.Orm.Expressions.Statements.Typed;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SysWeaver.Data;

namespace SysWeaver.Db
{



    public static class DbDataType<T>
    {
        public static readonly IReadOnlyDictionary<String, String[]> FullTextIndexes;
        

        static DbDataType()
        {
            var type = typeof(T);
            var cols = TableDataTools.GetCols<T>();
            var cl = cols.Length;
            for (int i = 0; i < cl; ++i)
            {
                var t = cols[i];
                var n = t.Name;
                var fi = type.GetField(n);
                if (fi != null)
                {
                    if (fi.GetCustomAttribute<IgnoreAttribute>(true) != null)
                        t.Props = TableDataColumnProps.IsComputed | (t.Props & TableDataColumnProps.Hide);
                    continue;
                }
                var pi = type.GetProperty(n);
                if (pi != null)
                {
                    if (pi.GetCustomAttribute<IgnoreAttribute>(true) != null)
                        t.Props = TableDataColumnProps.IsComputed | (t.Props & TableDataColumnProps.Hide);
                    continue;
                }
            }
            var dbType = ModelDefinition<T>.Definition;

            Dictionary<String, FieldDefinition> fs = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
            Dictionary<String, FieldDefinition> propInfos = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
            foreach (var f in dbType.FieldDefinitions)
            {
                fs.Add(f.Name, f);
                propInfos.Add(f.FieldName, f);
            }
            var order = new Action<TypedSelectStatement<T>>[cl];
            var orderDesc = new Action<TypedSelectStatement<T>>[cl];
            var then = new Action<TypedSelectStatement<T>>[cl];
            var thenDesc = new Action<TypedSelectStatement<T>>[cl];
            var filters = new Dictionary<string, Action<TypedSelectStatement<T>, string>[]>(StringComparer.Ordinal);

            var qType = typeof(TypedSelectStatement<T>);
            var expT = Expression.Parameter(type, "t");
            var expQ = Expression.Parameter(qType, "q");
            var methodOrderBy = qType.GetMethod(nameof(TypedSelectStatement<int>.OrderBy));
            var methodOrderByDescending = qType.GetMethod(nameof(TypedSelectStatement<int>.OrderByDescending));
            var methodThenBy = qType.GetMethod(nameof(TypedSelectStatement<int>.ThenBy));
            var methodThenByDescending = qType.GetMethod(nameof(TypedSelectStatement<int>.ThenByDescending));


            var methodWhere = qType.GetMethod(nameof(TypedSelectStatement<int>.Where), [typeof(Expression<Func<T, bool>>)]);
            var filterType = typeof(Func<,>).MakeGenericType(type, typeof(bool));
            var valueExp = DbData.ValueExp;
            var zeroExp = DbData.ZeroExp;
            var containsMethod = DbData.ContainsMethod;
            var startMethod = DbData.StartsWithMethod;
            var endMethod = DbData.EndsWithMethod;
            var compareTypes = DbData.CompareTypes;
            var nameToCol = new Dictionary<string, int>(StringComparer.Ordinal);
            void DoIt(bool forceIndex)
            {
                int primKeyCount = cols.Count(x => (x.Props & TableDataColumnProps.AnyPrimaryKey) != 0);
                for (int i = 0; i < cl; ++i)
                {
                    var col = cols[i];
                    nameToCol[col.Name] = i;
                    var props = col.Props;
                    props &= ~(TableDataColumnProps.IsKey | TableDataColumnProps.CanChart);
                    if ((props & TableDataColumnProps.IsComputed) == 0)
                    {
                        var m = type.GetMemberByName(col.Name);
                        bool haveIndex = false;
                        if (fs.TryGetValue(col.Name, out var d))
                            haveIndex = forceIndex || d.IsPrimaryKey || d.IsIndexed;
                        if (d?.IsPrimaryKey ?? false)
                        {
                            if ((props & TableDataColumnProps.AnyPrimaryKey) == 0)
                            {
                                if (primKeyCount < 7)
                                {
                                    ++primKeyCount;
                                    TableDataColumnProps primKey = (TableDataColumnProps)(primKeyCount * (int)TableDataColumnProps.IsPrimaryKey1);
                                    props |= primKey;
                                }
                            }
                        }else
                        {
                            if (haveIndex)
                                props |= TableDataColumnProps.IsKey;
                        }
                        if (!haveIndex)
                            props &= ~(TableDataColumnProps.CanSort | TableDataColumnProps.AnyFilters);
                        var ptype = d?.FieldType ?? TypeFinder.Get(col.Type);
                        if (d?.IsNullable ?? false)
                            if (ptype.IsPrimitive || ptype.IsValueType)
                                ptype = typeof(Nullable<>).MakeGenericType(ptype);

                        var src = Expression.PropertyOrField(expT, col.Name);

                        var srcType = src.Type;
                        if ((props & TableDataColumnProps.CanSort) != 0)
                        {
                            var ltype = typeof(Func<,>).MakeGenericType(type, ptype);
                            var l = Expression.Lambda(ltype, src, expT);

                            var ce = Expression.Call(expQ, methodOrderBy.MakeGenericMethod(ptype), l);
                            var cel = Expression.Lambda<Action<TypedSelectStatement<T>>>(ce, expQ);
                            order[i] = cel.Compile();

                            ce = Expression.Call(expQ, methodOrderByDescending.MakeGenericMethod(ptype), l);
                            cel = Expression.Lambda<Action<TypedSelectStatement<T>>>(ce, expQ);
                            orderDesc[i] = cel.Compile();

                            ce = Expression.Call(expQ, methodThenBy.MakeGenericMethod(ptype), l);
                            cel = Expression.Lambda<Action<TypedSelectStatement<T>>>(ce, expQ);
                            then[i] = cel.Compile();

                            ce = Expression.Call(expQ, methodThenByDescending.MakeGenericMethod(ptype), l);
                            cel = Expression.Lambda<Action<TypedSelectStatement<T>>>(ce, expQ);
                            thenDesc[i] = cel.Compile();
                        }



                        var value = StringConverterExp.FromString(srcType, valueExp);
                        if (value == null)
                            props &= ~TableDataColumnProps.AnyFilters;

                        if (srcType != typeof(String))
                            props &= ~TableDataColumnProps.TextFilter;


                        if ((props & TableDataColumnProps.AnyFilters) != 0)
                        {
                            Action<TypedSelectStatement<T>, String>[] cmps = new Action<TypedSelectStatement<T>, string>[TableDataFilterOpsProps.Count * 2];
                            filters.Add(col.Name, cmps);

                            void set(TableDataFilterOps op, Expression exp)
                            {
                                var index = ((int)op) * 2;
                                Expression l, ce;
                                Expression<Action<TypedSelectStatement<T>, String>> cel;

                                l = Expression.Lambda(filterType, exp, expT);
                                ce = Expression.Call(expQ, methodWhere, l);
                                cel = Expression.Lambda<Action<TypedSelectStatement<T>, String>>(ce, expQ, valueExp);
                                cmps[index] = cel.Compile();

                                l = Expression.Lambda(filterType, Expression.Not(exp), expT);
                                ce = Expression.Call(expQ, methodWhere, l);
                                cel = Expression.Lambda<Action<TypedSelectStatement<T>, String>>(ce, expQ, valueExp);
                                cmps[index + 1] = cel.Compile();
                            }

                            var enumValues = TableDataTools.GetEnumerableTypeFromStringArray(valueExp, srcType);

                            if ((props & TableDataColumnProps.Filter) != 0)
                            {
                                try
                                {
                                    var valueHashSet = TableDataTools.GetHashSetFromEnum(enumValues);
                                    set(TableDataFilterOps.Equals, Expression.Equal(src, value));
                                    set(TableDataFilterOps.NotEqual, Expression.NotEqual(src, value));
                                    var ce = TableDataTools.HashSetContains(valueHashSet, src);
                                    set(TableDataFilterOps.AnyOf, ce);
                                    set(TableDataFilterOps.NoneOf, Expression.Not(ce));
                                }
                                catch
                                {
                                    props &= ~TableDataColumnProps.Filter;
                                }
                            }
                            if ((props & TableDataColumnProps.OrderFilter) != 0)
                            {
                                Expression minExpression = TableDataTools.GetMinFromEnum(enumValues);
                                Expression maxExpression = TableDataTools.GetMaxFromEnum(enumValues);
                                bool haveCompare = false;
                                if (!compareTypes.Contains(srcType))
                                {
                                    try
                                    {
                                        var valInfo = value;
                                        Expression srcInfo = src;
                                        if (srcType.IsEnum)
                                        {
                                            var et = srcType.GetEnumUnderlyingType();
                                            srcInfo = Expression.Convert(srcInfo, et);
                                            valInfo = Expression.Convert(valInfo, et);
                                            minExpression = Expression.Convert(minExpression, et);
                                            maxExpression = Expression.Convert(maxExpression, et);
                                        }
                                        set(TableDataFilterOps.LessThan, Expression.LessThan(srcInfo, valInfo));
                                        set(TableDataFilterOps.GreaterThan, Expression.GreaterThan(srcInfo, valInfo));
                                        set(TableDataFilterOps.LessEqual, Expression.LessThanOrEqual(srcInfo, valInfo));
                                        set(TableDataFilterOps.GreaterEqual, Expression.GreaterThanOrEqual(srcInfo, valInfo));
                                        set(TableDataFilterOps.InRange,
                                            Expression.And(Expression.GreaterThanOrEqual(srcInfo, minExpression), Expression.LessThan(srcInfo, maxExpression)));
                                        set(TableDataFilterOps.OutsideRange,
                                            Expression.Or(Expression.LessThan(srcInfo, minExpression), Expression.GreaterThan(srcInfo, maxExpression)));
                                        haveCompare = true;
                                    }
                                    catch
                                    {
                                    }
                                }
                                if (!haveCompare)
                                {
                                    if (typeof(IComparable<>).MakeGenericType(srcType).IsAssignableFrom(srcType))
                                    {
                                        var cm = src.Type.GetMethod(nameof(IComparable.CompareTo), BindingFlags.Instance | BindingFlags.Public, [srcType]);
                                        if (cm != null)
                                        {
                                            var cmpExp = Expression.Call(src, cm, value);
                                            set(TableDataFilterOps.LessThan, Expression.LessThan(cmpExp, zeroExp));
                                            set(TableDataFilterOps.GreaterThan, Expression.GreaterThan(cmpExp, zeroExp));
                                            set(TableDataFilterOps.LessEqual, Expression.LessThanOrEqual(cmpExp, zeroExp));
                                            set(TableDataFilterOps.GreaterEqual, Expression.GreaterThanOrEqual(cmpExp, zeroExp));
                                            minExpression = Expression.Call(src, cm, minExpression);
                                            maxExpression = Expression.Call(src, cm, maxExpression);
                                            set(TableDataFilterOps.InRange,
                                                Expression.And(Expression.GreaterThanOrEqual(minExpression, zeroExp), Expression.LessThan(maxExpression, zeroExp)));
                                            set(TableDataFilterOps.OutsideRange,
                                                Expression.Or(Expression.LessThan(minExpression, zeroExp), Expression.GreaterThan(maxExpression, zeroExp)));
                                            haveCompare = true;
                                        }
                                    }
                                }
                                if (!haveCompare)
                                    props &= ~TableDataColumnProps.OrderFilter;
                            }
                            if ((props & TableDataColumnProps.TextFilter) != 0)
                            {
                                try
                                {
                                    set(TableDataFilterOps.Contains, Expression.Call(src, containsMethod, value));
                                    set(TableDataFilterOps.StartsWith, Expression.Call(src, startMethod, value));
                                    set(TableDataFilterOps.EndsWith, Expression.Call(src, endMethod, value));
                                }
                                catch
                                {
                                    props &= ~TableDataColumnProps.TextFilter;
                                }
                            }
                        }
                    }
                    
                    cols[i] = new TableDataColumn
                    {
                        Desc = col.Desc,
                        Format = col.Format,
                        Name = col.Name,
                        Props = props,
                        Title = col.Title,
                        Type = col.Type,
                    };

                }
                
                if (primKeyCount > 7)
                {
                    foreach (var x in cols)
                        x.Props &= ~TableDataColumnProps.AnyPrimaryKey;
                }
                else
                {

                    if (primKeyCount > 0)
                    {
                        var ct = TableDataTools.ChartTypes;
                        foreach (var x in cols)
                        {
                            if (!ct.Contains(x.Type))
                                continue;
                            if ((x.Props & TableDataColumnProps.AnyPrimaryKey) != 0)
                                continue;
                            x.Props |= TableDataColumnProps.CanChart;
                        }
                    }
                }
            }

            DoIt(true);

            MemberNameToColumn = nameToCol.Freeze();

            NoIndexOrder = order;
            NoIndexOrderDesc = orderDesc;
            NoIndexThen = then;
            NoIndexThenDesc = thenDesc;
            NoIndexColFilters = filters.Freeze();

            order = new Action<TypedSelectStatement<T>>[cl];
            orderDesc = new Action<TypedSelectStatement<T>>[cl];
            then = new Action<TypedSelectStatement<T>>[cl];
            thenDesc = new Action<TypedSelectStatement<T>>[cl];
            filters = new Dictionary<string, Action<TypedSelectStatement<T>, string>[]>(StringComparer.Ordinal);
            DoIt(false);
            Order = order;
            OrderDesc = orderDesc;
            Then = then;
            ThenDesc = thenDesc;
            ColFilters = filters.Freeze();
            Cols = cols;
            var ftis = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var fti in type.GetCustomAttributes<FullTextSearchIndexAttribute>(true))
                ftis.Add(fti.Name, fti.Props.Select(x => propInfos[x].Name).ToArray());
            FullTextIndexes = ftis.Freeze();
        }



        /// <summary>
        /// Apply sorting to a select query
        /// </summary>
        /// <param name="q">The query</param>
        /// <param name="order">The sort order by column (member) names.
        /// The first string is the primary sort column.
        /// Prefix the member with a '-' to sort descending, examples:
        /// ["Name"]
        /// ["-Date", "-Price"]  // Sort by latest date, highest price first.
        /// </param>
        /// <param name="first">If first is true, start with "order by" else start with "then by"</param>
        /// <param name="allowNonIndexed">If true, allow using non-index db columns</param>
        /// <returns>The query (to support chaining)</returns>
        public static TypedSelectStatement<T> Sort(TypedSelectStatement<T> q, String[] order, bool first = true, bool allowNonIndexed = false)
        {
            if (order != null)
            {
                var l = order.Length;
                if (l > 0)
                {
                    for (int i = 0; i < l; ++i)
                    {
                        var o = order[i];
                        if (String.IsNullOrEmpty(o))
                            continue;
                        bool desc = o[0] == '-';
                        if (desc)
                            o = o.Substring(1);
                        if (MemberNameToColumn.TryGetValue(o, out var ci))
                        {
                            if (allowNonIndexed)
                            {
                                if (first)
                                {
                                    (desc ? NoIndexOrderDesc : NoIndexOrder)[ci]?.Invoke(q);
                                }
                                else
                                {
                                    (desc ? NoIndexThenDesc : NoIndexThen)[ci]?.Invoke(q);
                                }

                            }
                            else
                            {
                                if (first)
                                {
                                    (desc ? OrderDesc : Order)[ci]?.Invoke(q);
                                }
                                else
                                {
                                    (desc ? ThenDesc : Then)[ci]?.Invoke(q);
                                }
                            }
                            first = false;
                        }
                    }
                }
            }
            return q;
        }


        public static readonly IReadOnlyDictionary<String, int> MemberNameToColumn;

        public static readonly IReadOnlyDictionary<String, Action<TypedSelectStatement<T>, String>[]> ColFilters;
        public static readonly Action<TypedSelectStatement<T>>[] Order;
        public static readonly Action<TypedSelectStatement<T>>[] OrderDesc;
        public static readonly Action<TypedSelectStatement<T>>[] Then;
        public static readonly Action<TypedSelectStatement<T>>[] ThenDesc;

        public static readonly IReadOnlyDictionary<String, Action<TypedSelectStatement<T>, String>[]> NoIndexColFilters;
        public static readonly Action<TypedSelectStatement<T>>[] NoIndexOrder;
        public static readonly Action<TypedSelectStatement<T>>[] NoIndexOrderDesc;
        public static readonly Action<TypedSelectStatement<T>>[] NoIndexThen;
        public static readonly Action<TypedSelectStatement<T>>[] NoIndexThenDesc;



        public static TableDataColumn[] Cols;

    }

    public static class DbDataTypeExt
    {

        /// <summary>
        /// Apply sorting to a select query
        /// </summary>
        /// <param name="q">The query</param>
        /// <param name="order">The sort order by column (member) names.
        /// The first string is the primary sort column.
        /// Prefix the member with a '-' to sort descending, examples:
        /// ["Name"]
        /// ["-Date", "-Price"]  // Sort by latest date, highest price first.
        /// </param>
        /// <param name="first">If first is true, start with "order by" else start with "then by"</param>
        /// <returns>The query (to support chaining)</returns>
        public static TypedSelectStatement<T> Sort<T>(this TypedSelectStatement<T> q, String[] order, bool first = true) => DbDataType<T>.Sort(q, order, first);

    }



}
