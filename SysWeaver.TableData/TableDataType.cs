using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using SysWeaver.Docs;
using SysWeaver.Translation;

namespace SysWeaver.Data
{


    sealed class MemberInfoComparer : IEqualityComparer<MemberInfo>
    {

        public static readonly MemberInfoComparer Inst = new MemberInfoComparer();

        public bool Equals(MemberInfo x, MemberInfo y)
        {
            if (!x.Name.FastEquals(y.Name))
                return false;
            return x.DeclaringType.FullName.FastEquals(y.DeclaringType.FullName);
        }

        public int GetHashCode([DisallowNull] MemberInfo obj)
        {
            int h0 = String.GetHashCode(obj.Name);
            int h1 = String.GetHashCode(obj.DeclaringType.FullName);
            return h0 ^ (h1 * 37);
        }
    }

    static class TableDataType<T>
    {

        public static readonly IReadOnlyDictionary<String, int> NameToColumnIndex;
        public static readonly IReadOnlyList<String> PrimaryKey;
        public static readonly IReadOnlyList<String> Keys;

        public static readonly Func<ITranslator, String, Object[], TranslationEffort, TranslationCacheRetention, Task> TranslateRow;
     
        static TableDataType()
        {
            var t = typeof(T);
            var ot = typeof(Object);
            var et = typeof(IEnumerable<T>);
            var eot = typeof(IOrderedEnumerable<T>);
            var op = Expression.Parameter(t, "o");
            var ep = Expression.Parameter(et, "e");

            List<Expression> extract = new();
            List<TableDataColumn> cols = new();
            List<Type> colTypes = new();
            List<Func<IEnumerable<T>, IEnumerable<T>>> sort = new();
            List<Func<IEnumerable<T>, IEnumerable<T>>> sortDesc = new();
            List<Func<IEnumerable<T>, IEnumerable<T>>> thenBy = new();
            List<Func<IEnumerable<T>, IEnumerable<T>>> thenByDesc = new();

            List<Func<IEnumerable<T>, String, IEnumerable<T>>[]> filters = new();

            Dictionary<String, int> nameToColumnIndex = new(StringComparer.Ordinal);
            var valid = TableDataTools.ValidDataTypes;
            var cmpValueExp = TableDataTools.CmpValueExp;


            HashSet<String> keys = new HashSet<string>(StringComparer.Ordinal);
            List<String> key = new List<string>();

            List<String> primaryKey = new List<string>();
            HashSet<String> primaryKeys = new HashSet<string>(StringComparer.Ordinal);
            var pk = t.GetCustomAttribute<TableDataPrimaryKeyAttribute>(true);
            if (pk != null)
            {
                var x = pk.PrimaryKeyNames;
                if (x != null)
                {
                    foreach (var y in x)
                        if (primaryKeys.Add(y))
                            primaryKey.Add(y);
                    if (primaryKey.Count > 7)
                        throw new Exception("At most 7 columns can be part of the primary key! Found in type " + t.FullName.ToQuoted());
                }
            }
            Dictionary<MemberInfo, int> memberCols = new (MemberInfoComparer.Inst);
            foreach (var x in TableDataTools.GetPublicInstanceMembers(t))//t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
            {
                GetColumn(x, memberCols, cols, colTypes, filters, nameToColumnIndex, extract, op, op, ep, keys, key, primaryKey, primaryKeys, sort, sortDesc, thenBy, thenByDesc);
            }
            //  Have invalid names in primary key
            if (primaryKeys.Count > 0)
                throw new Exception("The following table data column-names are not available in " + t.FullName.ToQuoted() + ": \"" + String.Join("\", \"", primaryKeys) + "\"");
            var pc = primaryKey.Count;
            for (int i = 0; i < pc; ++i)
            {
                var keyN = primaryKey[i];
                cols[nameToColumnIndex[keyN]].Props |= (TableDataColumnProps)((1 + i) << 8);
            }

            NameToColumnIndex = nameToColumnIndex.Freeze();
            PrimaryKey = primaryKey.ToArray();
            Keys = key.ToArray();

            if (pc > 0)
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

            var newArray = Expression.NewArrayInit(ot, extract);
            Extract = Expression.Lambda<Func<T, Object[]>>(newArray, op).Compile();

            ColTypes = colTypes.ToArray();
            Cols = cols.ToArray();
            Sort = sort.ToArray();
            SortDesc = sortDesc.ToArray();

            ThenBy = thenBy.ToArray();
            ThenByDesc = thenByDesc.ToArray();


            Filters = filters.ToArray();
            //  Translation
            List<Expression> translateCols = new List<Expression>();
            foreach (var kv in memberCols.OrderBy(x => x.Value))
            {
                var exp = GetTranslateColumnExp(kv.Key, kv.Value, memberCols);
                if (exp == null)
                    continue;
                translateCols.Add(exp);
            }
            var tc = translateCols.Count;
            if (tc > 0)
            {
                if (tc == 1)
                {
                    var prog = Expression.Lambda<Func<ITranslator, String, Object[], TranslationEffort, TranslationCacheRetention, Task>>(translateCols[0], TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, ValuesParam, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
                    TranslateRow = prog.Compile();
                }
                else
                {
                    var taskArray = Expression.NewArrayInit(typeof(Task), translateCols);
                    var whenAll = Expression.Call(TypeTranslator.TaskWhenAllArrayMethod, taskArray);
                    var prog = Expression.Lambda<Func<ITranslator, String, Object[], TranslationEffort, TranslationCacheRetention, Task>>(whenAll, TypeTranslator.ParamTranslator, TypeTranslator.ParamTo, ValuesParam, TypeTranslator.ParamEffort, TypeTranslator.ParamRetention);
                    TranslateRow = prog.Compile();


                }

            }



        }


        static readonly ParameterExpression ValuesParam = Expression.Parameter(typeof(Object[]), "values");
        

        static Expression GetTranslateColumnExp(MemberInfo mi, int colIndex, IReadOnlyDictionary<MemberInfo, int> memberCols)
        {
            var attr = mi.GetCustomAttribute<AutoTranslateAttribute>(true);
            if (attr == null)
                return null;
            var p = ValuesParam;
            var context = attr.NoContext ? null : mi.XmlDoc();
            //  Get the context expression
            List<String> staticContexts = new List<string>(10);
            if (context != null)
            {
                var sum = context.Summary;
                if (!String.IsNullOrEmpty(sum))
                {
                    if (!attr.NoContext)
                        staticContexts.Add(String.Concat("The description is \"", sum, "\"."));
                }
            }
            var typeContext = TypeTranslator.TypeContexts[(int)(mi.GetCustomAttribute<AutoTranslateTypeAttribute>(true)?.Type ?? TranslatorTypes.Text)];
            if (!String.IsNullOrEmpty(typeContext))
                staticContexts.Add(typeContext);


            List<Expression> dynContexts = new List<Expression>();
            var tempVal = TypeTranslator.TempVal;
            var fmt = TypeTranslator.StringFmt;
            var ns = TypeTranslator.NullString;
            var type = typeof(T);
            foreach (var c in mi.GetCustomAttributes<AutoTranslateContextAttribute>(true))
            {
                var x = c.ContextText.Trim().TrimEnd('.');
                if (String.IsNullOrEmpty(x))
                    continue;
                x += '.';
                if (x.IndexOf('{') < 0)
                {
                    staticContexts.Add(x);
                    continue;
                }
                var t = c.MemberNames;
                if (t == null)
                {
                    staticContexts.Add(x);
                    continue;
                }
                var tl = t.Length;
                if (tl <= 0)
                {
                    staticContexts.Add(x);
                    continue;
                }

                List<Expression> reads = new (tl);
                for (int i = 0; i < tl; ++i)
                {
                    var name = t[i];
                    var fi = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    int cIndex = -1;
                    if (fi != null)
                    {
                        if (!memberCols.TryGetValue(fi, out cIndex))
                            cIndex = -1;
                    }
                    else
                    {
                        var pi = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi != null)
                        {
                            if (!memberCols.TryGetValue(pi, out cIndex))
                                cIndex = -1;
                        }
                    }
                    if (cIndex >= 0)
                        reads.Add(Expression.ArrayAccess(p, Expression.Constant(cIndex)));
                }
                tl = reads.Count;
                List<ParameterExpression> dynP = new List<ParameterExpression>(tl);
                List<Expression> dynProg = new List<Expression>(tl + 1);
                for (int i = 0; i < tl; ++i)
                {
                    var r = reads[i];
                    var tp = Expression.Variable(typeof(Object));
                    dynP.Add(tp);
                    dynProg.Add(Expression.Assign(tp, r));
                    reads[i] = tp;
                }
                Expression dynE = Expression.Call(fmt, Expression.Constant(x), Expression.NewArrayInit(typeof(Object), reads));
                var dynPC = dynP.Count;
                while (dynPC > 0)
                {
                    --dynPC;
                    var dynV = dynP[dynPC];
                    dynE = Expression.Condition(Expression.Equal(dynV, Expression.Constant(null, dynV.Type)), ns, dynE);
                }
                if (dynP.Count > 0)
                {
                    dynProg.Add(dynE);
                    dynE = Expression.Block(dynP, dynProg);
                }
                dynContexts.Add(dynE);
            }
            Expression con = ns;
            if (dynContexts.Count > 0)
            {
                if (staticContexts.Count > 0)
                    dynContexts.Add(Expression.Constant(String.Join('\n', staticContexts)));
                con = Expression.Call(TypeTranslator.MergeContextsMethod, Expression.NewArrayInit(typeof(String), dynContexts));
            }
            else
            {
                if (staticContexts.Count > 0)
                    con = Expression.Constant(String.Join('\n', staticContexts));
            }
            Expression value = Expression.ArrayAccess(p, Expression.Constant(colIndex));
            var strParams = TypeTranslator.ParamString;
            var save = Expression.Lambda<Action<String>>(Expression.Assign(value, strParams), strParams);
            value = Expression.Convert(value, typeof(String));

            var from = attr.FromLanguage;
            if (String.IsNullOrEmpty(from))
                from = "en";
            var fnc = Expression.Call(
                TypeTranslator.TranslateOneMethod,
                TypeTranslator.ParamTranslator,
                Expression.Constant(from),
                TypeTranslator.ParamTo,
                value,
                con,
                save,
                TypeTranslator.ParamEffort, TypeTranslator.ParamRetention
                );
            return fnc;
        }

        static internal bool GetColumn(MemberInfo x,
            Dictionary<MemberInfo, int> members,
            List<TableDataColumn> cols,
            List<Type> colTypes,
            List<Func<IEnumerable<T>, String, IEnumerable<T>>[]> filters,
            Dictionary<String, int> nameToColumnIndex,
            List<Expression> extract,
            Expression currentObj,
            ParameterExpression op,
            ParameterExpression ep,
            HashSet<String> keys,
            List<String> key,
            List<String> primaryKey,
            HashSet<String> primaryKeys,
            List<Func<IEnumerable<T>, IEnumerable<T>>> sort,
            List<Func<IEnumerable<T>, IEnumerable<T>>> sortDesc,
            List<Func<IEnumerable<T>, IEnumerable<T>>> thenBy,
            List<Func<IEnumerable<T>, IEnumerable<T>>> thenByDesc,
            String namePrefix = "",
            String titlePrefix = ""
            )
        {
            var t = typeof(T);
            if (x.GetCustomAttribute<TableDataIgnoreAttribute>() != null)
                return false;
            bool isField = x.MemberType == MemberTypes.Field;
            bool isProperty = x.MemberType == MemberTypes.Property;
            bool isOk = isField | isProperty;
            if (!isOk)
                return false;

            var name = (x.GetCustomAttribute<TableDataNameAttribute>()?.Name ?? x.Name);
            var title = x.GetCustomAttribute<TableDataTitleAttribute>()?.Value ?? StringTools.RemoveCamelCase(name);
            if (titlePrefix.Length > 0)
                title = titlePrefix + title.MakeFirstLowercase();
            if (namePrefix.Length > 0)
                name = namePrefix + name;

            String desc;
            String format = null;
            Type mt;
            Expression ee;
            if (isField)
            {
                var m = x as FieldInfo;
                mt = m.FieldType;
                ee = Expression.Field(currentObj, m);
                desc = m.XmlDoc().ToTitle();
            }
            else
            {
                var m = x as PropertyInfo;
                mt = m.PropertyType;
                ee = Expression.Property(currentObj, m);
                desc = m.XmlDoc().ToTitle();
            }
            Func<String, Object> textToObj = null;
            Func<Expression, Expression> convert = null;
            bool isNullable = false;
            if (mt.IsGenericType)
                isNullable = mt.GetGenericTypeDefinition() == typeof(Nullable<>);
            var baseType = isNullable ? mt.GetGenericArguments()[0] : mt;
            if (!baseType.IsEnum)
            {
                if (!TableDataTools.ValidDataTypes.TryGetValue(baseType, out var tid))
                {
                    var exp = mt.GetCustomAttribute<TableDataExpandAttribute>(true) ?? x.GetCustomAttribute<TableDataExpandAttribute>(true);
                    if (exp != null)
                    {
                        var newCurrentObj = ee;
                        if (mt.IsClass)
                        {
                            var def = TableDataTools.GetStaticObject(mt);
                            if (def == null)
                                return false;
                            newCurrentObj = Expression.Coalesce(ee, def);
                        }
                        var newNamePrefix = String.Format(exp.MemberNamePrefix ?? "{0}_", name);
                        var newTitlePrefix = String.Format(exp.TitlePrefix ?? "{1} ", name, title);
                        foreach (var newX in TableDataTools.GetPublicInstanceMembers(mt))
                        {
                            GetColumn(newX, members, cols, colTypes, filters, nameToColumnIndex, extract, newCurrentObj, op, ep, keys, key, primaryKey, primaryKeys, sort, sortDesc, thenBy, thenByDesc, newNamePrefix, newTitlePrefix);
                        }
                    }
                    return false;
                }
                convert = tid.TypeToData;
                textToObj = tid.StringToData;
            }
            var vt = ee.Type;
            bool canSort = x.GetCustomAttribute<TableDataNoSortAttribute>() == null;
            if (canSort)
                canSort &= typeof(IComparable<>).MakeGenericType(vt).IsAssignableFrom(vt);
            bool sortRev = canSort && (x.GetCustomAttribute<TableDataSortDescAttribute>() != null);
            bool hide = x.GetCustomAttribute<TableDataHideAttribute>() != null;
            nameToColumnIndex[name] = cols.Count;

            TableDataColumnProps props = 0;

            primaryKeys.Remove(name);
            var k = x.GetCustomAttribute<TableDataKeyAttribute>()?.IsKey ?? false;
            if (k)
            {
                if (primaryKey.Count <= 0)
                    primaryKey.Add(name);
                else
                {
                    if (keys.Add(name))
                    {
                        key.Add(name);
                        props |= TableDataColumnProps.IsKey;
                    }
                }
            }
            var da = x.GetCustomAttribute<TableDataDescAttribute>();
            if (da != null)
                desc = da.Value;
            var fa = x.GetCustomAttribute<TableDataRawFormatAttribute>();
            if (fa != null)
                format = fa.Value;
            colTypes.Add(mt);
            members.Add(x, cols.Count);
            cols.Add(new TableDataColumn
            {
                Name = name,
                Title = title,
                Desc = desc,
                Type = mt.FullName,
                Format = format,
                Props = props
                    | TableDataColumnProps.CanSort
                    | (sortRev ? TableDataColumnProps.SortedDesc : 0)
                    | (hide ? TableDataColumnProps.Hide : 0)
                    | TableDataColumnProps.Filter
                    | TableDataColumnProps.TextFilter
                    | TableDataColumnProps.OrderFilter
            });
            //  String value
            Expression stringValue;
            if (mt.IsValueType)
            {
                stringValue = Expression.Call(ee, nameof(Object.ToString), []);
            }
            else
            {
                stringValue = Expression.Condition(Expression.Equal(ee, Expression.Constant(null, mt)), Expression.Constant(""), Expression.Call(ee, nameof(Object.ToString), []));
            }
            //  Sorting
            var sortKey = canSort ? ee : stringValue;
            {
                var sorType = sortKey.Type;
                var ft = typeof(Func<,>).MakeGenericType(t, sorType);
                var l = Expression.Lambda(ft, sortKey, op);
                var prog = Expression.Lambda<Func<IEnumerable<T>, IEnumerable<T>>>(Expression.Call(null, TableDataTools.Order.MakeGenericMethod(t, sorType), ep, l), ep);
                var s = prog.Compile();
                prog = Expression.Lambda<Func<IEnumerable<T>, IEnumerable<T>>>(Expression.Call(null, TableDataTools.OrderDesc.MakeGenericMethod(t, sorType), ep, l), ep);
                var sd = prog.Compile();
                if (sortRev)
                {
                    sort.Add(sd);
                    sortDesc.Add(s);
                }
                else
                {
                    sort.Add(s);
                    sortDesc.Add(sd);
                }

                var eot = typeof(IOrderedEnumerable<T>);
                prog = Expression.Lambda<Func<IEnumerable<T>, IEnumerable<T>>>(Expression.Call(null, TableDataTools.ThenBy.MakeGenericMethod(t, sorType), Expression.Convert(ep, eot), l), ep);
                s = prog.Compile();
                prog = Expression.Lambda<Func<IEnumerable<T>, IEnumerable<T>>>(Expression.Call(null, TableDataTools.ThenByDesc.MakeGenericMethod(t, sorType), Expression.Convert(ep, eot), l), ep);
                sd = prog.Compile();
                if (sortRev)
                {
                    thenBy.Add(sd);
                    thenByDesc.Add(s);
                }
                else
                {
                    thenBy.Add(s);
                    thenByDesc.Add(sd);
                }

            }
            //  Filter
            Expression cmpGetValue = TableDataTools.CmpValueExp;
            if (textToObj != null)
                cmpGetValue = Expression.Call(Expression.Constant(textToObj.Target), textToObj.Method, cmpGetValue);
            GetFilters(out var fs, cmpGetValue, sortKey, stringValue, TableDataTools.CmpValueExp, op, ep);
            filters.Add(fs.ToArray());

            //  Convert
            if (convert != null)
            {
                ee = convert(ee);
                vt = ee.Type;
            }
            var ot = typeof(Object);
            if (vt != ot)
                ee = Expression.Convert(ee, ot);
            extract.Add(ee);
            return true;
        }

        

        static void GetFilters(out List<Func<IEnumerable<T>, String, IEnumerable<T>>> filters, Expression getCompareValueExp, Expression value, Expression valueString, ParameterExpression compareValueObject, ParameterExpression op, ParameterExpression ep)
        {
            filters = new();
            ParameterExpression[] ps = null;
            Expression assignVar = null;
            var getComapreStringExp = getCompareValueExp;
            if (getCompareValueExp.Type != typeof(String))
            {
                var cmpVar = Expression.Variable(value.Type, "cmpValue");
                ps =
                [
                    cmpVar,
                ];
                assignVar = Expression.Assign(cmpVar, Expression.Convert(getCompareValueExp, value.Type));
                getCompareValueExp = cmpVar;
            }
            var ft = typeof(Func<,>).MakeGenericType(typeof(T), typeof(Boolean));

            void addOne(List<Func<IEnumerable<T>, String, IEnumerable<T>>> to, Expression cmpExp)
            {
                var l = Expression.Lambda(ft, cmpExp, op);
                Expression prog = Expression.Call(null, TableDataTools.Where.MakeGenericMethod(typeof(T)), ep, l);
                if (ps != null)
                    prog = Expression.Block(ps, assignVar, prog);
                var p = Expression.Lambda<Func<IEnumerable<T>, String, IEnumerable<T>>>(prog, ep, compareValueObject);
                var e = p.Compile();
                to.Add(e);
            }




            foreach (var cmp in TableDataTools.CmpFuncs)
            {
                var cmpExp = cmp(value, getCompareValueExp, false);
                addOne(filters, cmpExp);
                cmpExp = Expression.Not(cmpExp);
                addOne(filters, cmpExp);
                cmpExp = cmp(value, getCompareValueExp, true);
                addOne(filters, cmpExp);
                cmpExp = Expression.Not(cmpExp);
                addOne(filters, cmpExp);
            }

            void addOneStr(List<Func<IEnumerable<T>, String, IEnumerable<T>>> to, Expression cmpExp)
            {
                var l = Expression.Lambda(ft, cmpExp, op);
                Expression prog = Expression.Call(null, TableDataTools.Where.MakeGenericMethod(typeof(T)), ep, l);
                var p = Expression.Lambda<Func<IEnumerable<T>, String, IEnumerable<T>>>(prog, ep, compareValueObject);
                var e = p.Compile();
                to.Add(e);
            }
            foreach (var cmp in TableDataTools.StringCmpFuncs)
            {
                var cmpExp = cmp(valueString, compareValueObject, false);
                addOneStr(filters, cmpExp);
                cmpExp = Expression.Not(cmpExp);
                addOneStr(filters, cmpExp);
                cmpExp = cmp(valueString, compareValueObject, true);
                addOneStr(filters, cmpExp);
                cmpExp = Expression.Not(cmpExp);
                addOneStr(filters, cmpExp);
            }

            var getVal = Expression.Lambda(value, op);

            var enumValues = TableDataTools.GetEnumerableTypeFromStringArray(compareValueObject, value.Type);
            var valueHashSetTrue = TableDataTools.GetHashSetFromEnum(enumValues, true);
            var valueHashSetFalse = TableDataTools.GetHashSetFromEnum(enumValues, false);
           
            void addHash(List<Func<IEnumerable<T>, String, IEnumerable<T>>> to, bool not, bool caseSensitive)
            {
                Expression valueHelper = Expression.Call(
                    (not ? HashSetHelper.CheckNot : HashSetHelper.Check).MakeGenericMethod(typeof(T), value.Type),
                    caseSensitive ? valueHashSetTrue : valueHashSetFalse, getVal);
                var tempVar = Expression.Variable(valueHelper.Type, "cache");
                var assignTemp = Expression.Assign(tempVar, valueHelper);
                var l = tempVar;
                Expression prog = Expression.Call(null, TableDataTools.Where.MakeGenericMethod(typeof(T)), ep, l);
                prog = Expression.Block([tempVar], [assignTemp, prog]);
                var p = Expression.Lambda<Func<IEnumerable<T>, String, IEnumerable<T>>>(prog, ep, compareValueObject);
                var e = p.Compile();
                to.Add(e);
            }



            addHash(filters, false, false);
            addHash(filters, true, false);
            addHash(filters, false, true);
            addHash(filters, true, true);
            addHash(filters, true, false);
            addHash(filters, false, false);
            addHash(filters, true, true);
            addHash(filters, false, true);






            Expression minMax = TableDataTools.GetMinMaxFromEnum(enumValues);

            //Expression minExpression = TableDataTools.GetMinFromEnum(enumValues);
            //Expression maxExpression = TableDataTools.GetMaxFromEnum(enumValues);

            var paramMin = Expression.Parameter(value.Type, "min");
            var paramMax = Expression.Parameter(value.Type, "max");
            var paramVal = Expression.Parameter(value.Type, "val");


            void addMinMax(List<Func<IEnumerable<T>, String, IEnumerable<T>>> to, bool not, Expression cmpExp)
            {
                var lam = Expression.Lambda(cmpExp, [paramMin, paramMax, paramVal]);
                //        static Func<T, bool> DoIt<T, M>(M min, M max, Func<T, M> getM, Func<M, M, M, bool> func)

                Expression valueHelper = Expression.Call(
                    (not ? MinMaxHelper.CheckNot : MinMaxHelper.Check).MakeGenericMethod(typeof(T), value.Type),
                    minMax, getVal, lam);
                var tempVar = Expression.Variable(valueHelper.Type, "cache");
                var assignTemp = Expression.Assign(tempVar, valueHelper);
                var l = tempVar;
                Expression prog = Expression.Call(null, TableDataTools.Where.MakeGenericMethod(typeof(T)), ep, l);
                prog = Expression.Block([tempVar], [assignTemp, prog]);
                var p = Expression.Lambda<Func<IEnumerable<T>, String, IEnumerable<T>>>(prog, ep, compareValueObject);
                var e = p.Compile();
                to.Add(e);
            }

            foreach (var cmp in TableDataTools.MinMaxCmpFuncs)
            {
                var cmpExp = cmp(paramVal, paramMin, paramMax, false);
                addMinMax(filters, false, cmpExp);
                addMinMax(filters, true, cmpExp);
                cmpExp = cmp(paramVal, paramMin, paramMax, true);
                addMinMax(filters, false, cmpExp);
                addMinMax(filters, true, cmpExp);
            }

            /*
        var stringCol = Expression.Call(valueExp, DbData.StringSplitMethod, commaArrayExp, stringSplitOptionsExp);
        if (srcType != typeof(String))
        {
            var selM = linqSelectMethod.MakeGenericMethod(typeof(String), srcType);
            stringCol = Expression.Call(selM, stringCol, StringConverterExp.GetFromStringLambdaExp(srcType));
        }
        */

            // TODO: Add the four new methods!
        }

        internal static readonly TableDataColumn[] Cols;
        internal static readonly Type[] ColTypes;

        internal static readonly Func<T, Object[]> Extract;

        static readonly Func<IEnumerable<T>, IEnumerable<T>>[] Sort;
        static readonly Func<IEnumerable<T>, IEnumerable<T>>[] SortDesc;
        static readonly Func<IEnumerable<T>, IEnumerable<T>>[] ThenBy;
        static readonly Func<IEnumerable<T>, IEnumerable<T>>[] ThenByDesc;

        /// <summary>
        /// [Column index][(FilerOp * 4) | (CaseSensitive ? 2 : 0) | (Reverse ? 1 : 0)]
        /// </summary>
        static readonly Func<IEnumerable<T>, String, IEnumerable<T>>[][] Filters;


        public static IEnumerable<T> Filter(TableDataFilter[] fs, IEnumerable<T> data)
        {
            if (data == null)
                return null;
            if (fs == null)
                return data;
            var fl = fs.Length;
            if (fl <= 0)
                return data;
            var nameToCol = NameToColumnIndex;
            var filters = Filters;
            for (int i = 0; i < fl; ++i)
            {
                var f = fs[i];
                var filterVal = f.Value;
                if (filterVal == null)
                    continue;
                if (!nameToCol.TryGetValue(f.ColName ?? "", out var colIndex))
                    continue;
                var findex = ((int)f.Op) << 2;
                if (f.Invert)
                    findex |= 1;
                if (f.CaseSensitive)
                    findex |= 2;
                var filter = filters[colIndex][findex];
                data = filter(data, filterVal);
            }
            return data;
        }

        public static IEnumerable<T> SortAndFilter(TableDataRequest request, IEnumerable<T> data)
        {
            if (data == null)
                return null;
            //  Filter
            var nameToCol = NameToColumnIndex;
            var fs = request.Filters;
            if (fs != null)
            {
                var fl = fs.Length;
                if (fl > 0)
                {
                    var filters = Filters;
                    for (int i = 0; i < fl; ++i)
                    {
                        var f = fs[i];
                        var filterVal = f.Value;
                        if (filterVal == null)
                            continue;
                        if (!nameToCol.TryGetValue(f.ColName ?? "", out var colIndex))
                            continue;
                        var findex = ((int)f.Op) << 2;
                        if (f.Invert)
                            findex |= 1;
                        if (f.CaseSensitive)
                            findex |= 2;
                        var filter = filters[colIndex][findex];
                        data = filter(data, filterVal);
                    }
                }
            }
            //  Sorting
            var o = request.Order;
            if (o != null)
            {
                bool first = true;
                foreach (var x in o)
                {
                    var t = x?.Trim();
                    if (String.IsNullOrEmpty(t))
                        continue;
                    var desc = t[0] == '-';
                    if (desc)
                        t = t.Substring(1);
                    if (!nameToCol.TryGetValue(t ?? "", out var colIndex))
                        continue;
                    var ss = first ? (desc ? SortDesc : Sort) : (desc ? ThenByDesc : ThenBy);
                    var s = ss[colIndex];
                    if (s != null)
                    {
                        data = s(data);
                        first = false;
                    }
                }
            }
/*            if (o >= 0)
            {
                var sorter = request.SortReverse ? SortDesc : Sort;
                var s = sorter[o];
                if (s != null)
                    data = s(data);
            }
*/            return data;
        }

        public static IEnumerable<T> SortAndFilterAndLimit(TableDataRequest request, IEnumerable<T> data, long maxAllowedRows= 10000)
        {
            if (data == null)
                return null;
            data = SortAndFilter(request, data);
            //  Skip
            var skip = request.Row;
            while (skip > 0)
            {
                var s = skip;
                if (s > 0x7ffffff)
                    s = 0x7ffffff;
                skip -= s;
                data = data.Skip((int)s);
            }
            //  Limit
            var limit = request.MaxRowCount;
            if (limit <= 0)
                return data;
            if (limit > maxAllowedRows)
                limit = maxAllowedRows;
            data = data.Take((int)limit + 1);
            return data;
        }

        public static TableDataRow[] ExtractGet(out long count, IEnumerable<T> data, long limit = long.MaxValue, long lookAhead = 0)
        {
            count = 0;
            if (data == null)
                return Array.Empty<TableDataRow>();
            if (limit <= 0)
                limit = long.MaxValue;
            List<TableDataRow> rows = (limit <= (1L << 16)) ? new((int)limit) : new();
            var e = Extract;
            var it = data.GetEnumerator();
            while ((limit > 0) && it.MoveNext())
            {
                rows.Add(new TableDataRow
                {
                    Values = e(it.Current),
                });
                --limit;
            }
            var rc = rows.Count;
            count += rc;
            if ((limit == 0) && (lookAhead > 0))
            {
                while ((lookAhead > 0) && it.MoveNext())
                {
                    ++count;
                    --lookAhead;
                }
            }
            return rc > 0 ? rows.ToArray() : Empty;
        }


        static readonly TableDataRow[] Empty = [];

        public static TableData GetAll(IEnumerable<T> data)
        {
            var rows = ExtractGet(out var _, data);
            return new TableData
            {
                Rows = rows,
                Cols = Cols,
            };
        }

        public static TableData Get(TableDataRequest request, IEnumerable<T> data, String title)
        {
            long count = 0;
            TableDataRow[] rows = Empty;
            try
            {
                //  Filter and sort
                data = SortAndFilter(request, data);
                //  Skip
                var skip = request.Row;
                while (skip > 0)
                {
                    var s = skip;
                    if (s > 0x7ffffff)
                        s = 0x7ffffff;
                    skip -= s;
                    data = data.Skip((int)s);
                }
                rows = ExtractGet(out count, data, request.MaxRowCount, request.LookAheadCount);
                count += skip;
            }
            catch
            {
            }
            var cc = EnvInfo.Cc;
            bool isNew = request.Cc != cc;
            return new TableData
            {
                Rows = rows,
                Cols = isNew ? Cols : null,
                Title = isNew ? title : null,
                RowCount = count,
                Cc = cc,
            };
        }
    }




}
