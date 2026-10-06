using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SysWeaver.Docs;
using SysWeaver.Search;
using SysWeaver.Serialization;
using SysWeaver.Translation;

namespace SysWeaver.Data
{



    /// <summary>
    /// The main entry point for producing table data from a sequence of objects.
    /// Columns are derived from the row type (public fields and properties, <c>TableData*</c> attributes and XML documentation),
    /// and requests (<see cref="TableDataRequest"/>) are applied as filters, sort order, free text search, paging and look ahead.
    /// Also contains helpers to manipulate columns, translate table content, build tables from untyped rows and handle the column change counter.
    /// </summary>
    /// <remarks>
    /// Per type metadata and compiled code is built once (on first use of a row type) and cached, all methods are thread safe.
    /// Processing is done in memory using LINQ over the supplied sequence.
    /// </remarks>
    public static class TableDataTools
    {
        /// <summary>
        /// The default text searcher used for <see cref="TableDataRequest.SearchText"/> when none is specified.
        /// </summary>
        public static ITextSearch DefaultSearch = new SimpleTextSearch();


        /// <summary>
        /// Convert from typed table data to generic table data, extracting the values of each row object.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="table">The typed table.</param>
        /// <returns>A new table. If the typed table has columns, all columns of <typeparamref name="T"/> are used (matching the extracted values), else none.</returns>
        public static TableData ToTableData<T>(this TypedTableData<T> table)
            => new TableData
            {
                Cc = table.Cc,
                RefreshRate = table.RefreshRate,
                Rows = table.Rows.Convert(TableDataTools.GetRow),
                Cols = table.Cols == null ? null : TableDataType<T>.Cols,
                RowCount = table.RowCount,
                Title = table.Title,
            };


        /// <summary>
        /// Deserialize a serialized typed table (ex: a <see cref="TypedTableData{T}"/> received from another service) into generic table data.
        /// </summary>
        /// <param name="type">The typed table type, its first generic argument is the row type used to get the columns.</param>
        /// <param name="ser">The deserializer to use.</param>
        /// <param name="data">The serialized data.</param>
        /// <returns>The table data, using the columns of the row type.</returns>
        /// <remarks>Deserialization is done into a dynamically emitted type with one property per column, the conversion function is cached per type and mime type.</remarks>
        public static TableData FromTypedData(Type type, IDeserializer ser, ReadOnlySpan<Byte> data)
        {
            var c = TableConverters;
            var key = String.Concat(type.FullName, '_', ser.Mime);
            var e = type.GetGenericArguments()[0];
            var columns = GetCols(e);
            if (c.TryGetValue(key, out var fn))
                return fn(ser, data, columns);
            e = GetPropertyType(columns);
            type = typeof(TypedTableData<>).MakeGenericType(e);
            var exp = Expression.Lambda<Func<IDeserializer, ReadOnlySpan<Byte>, TableDataColumn[], TableData>>(Expression.Call(MethodToTableData.MakeGenericMethod(e), Expression.Call(ParamSer, MethodSerCreate.MakeGenericMethod(type), ParamData), ParamCols), ParamSer, ParamData, ParamCols);
            fn = exp.Compile();
            c.TryAdd(key, fn);
            return fn(ser, data, columns);
        }


        static TableData InternalToTableData<T>(this TypedTableData<T> table, TableDataColumn[] cols)
            => new TableData
            {
                Cc = table.Cc,
                RefreshRate = table.RefreshRate,
                Rows = table.Rows.Convert(TableDataTools.GetRow),
                Cols = cols,
                RowCount = table.RowCount,
                Title = table.Title,
            };

        static readonly ParameterExpression ParamSer = Expression.Parameter(typeof(IDeserializer), "ser");
        static readonly ParameterExpression ParamData = Expression.Parameter(typeof(ReadOnlySpan<Byte>), "data");
        static readonly ParameterExpression ParamCols = Expression.Parameter(typeof(TableDataColumn[]), "cols");



        static readonly MethodInfo MethodSerCreate = typeof(IDeserializer).GetMethod(nameof(IDeserializer.Create), BindingFlags.Instance | BindingFlags.Public, [typeof(ReadOnlySpan<Byte>)]);

        static readonly MethodInfo MethodToTableData = typeof(TableDataTools).GetMethod(nameof(InternalToTableData), BindingFlags.Static | BindingFlags.NonPublic);
        static readonly ConcurrentDictionary<String, Func<IDeserializer, ReadOnlySpan<Byte>, TableDataColumn[], TableData>> TableConverters = new(StringComparer.Ordinal);


        /// <summary>
        /// Sort some data by column names (lazily, using LINQ).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="order">The column names to sort by in priority order, a '-' prefix reverses the order. Unknown names are ignored.</param>
        /// <param name="data">Source data</param>
        /// <returns>The resulting data, null if <paramref name="data"/> is null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> Sort<T>(String[] order, IEnumerable<T> data)
            => TableDataType<T>.Sort(order, data);

        /// <summary>
        /// Filter some data using <see cref="TableDataFilter"/>'s (lazily, using LINQ).
        /// Filters with a null value or an unknown column name are ignored.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="filters">The filters to apply, may be null.</param>
        /// <param name="data">Source data</param>
        /// <returns>The resulting data, null if <paramref name="data"/> is null.</returns>
        /// <exception cref="IndexOutOfRangeException">A filter has an undefined <see cref="TableDataFilterOps"/> value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> Filter<T>(TableDataFilter[] filters, IEnumerable<T> data)
            => TableDataType<T>.Filter(filters, data);


        /// <summary>
        /// Filter and sort some data using a table data request (lazily).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="data">Source data</param>
        /// <returns>The resulting data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> SortAndFilter<T>(TableDataOrderRequest request, IEnumerable<T> data)
            => TableDataType<T>.SortAndFilter(request ?? DefRequest, data);

        /// <summary>
        /// Filter, sort, skip and limit some data using a table data request (lazily).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="request">What part of the data, sorting etc, if null a default request (first 20 rows) is used.</param>
        /// <param name="data">Source data</param>
        /// <param name="maxAllowedRows">Maximum allowed rows (minimum of this and the requested row count plus look ahead is used).
        /// Not applied if the request has no row limit (<see cref="TableDataOrderRequest.MaxRowCount"/> zero or negative).</param>
        /// <returns>The resulting data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> SortAndFilterAndLimit<T>(TableDataOrderRequest request, IEnumerable<T> data, long maxAllowedRows = 100000)
                => TableDataType<T>.SortAndFilterAndLimit(request ?? DefRequest, data, maxAllowedRows);


        /// <summary>
        /// Sort, skip and limit some data using a table data request (lazily), filters are NOT applied.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="request">What part of the data, sorting etc, if null a default request (first 20 rows) is used.</param>
        /// <param name="data">Source data</param>
        /// <param name="maxAllowedRows">Maximum allowed rows (minimum of this and the requested row count plus look ahead is used).
        /// Not applied if the request has no row limit (<see cref="TableDataOrderRequest.MaxRowCount"/> zero or negative).</param>
        /// <returns>The resulting data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> SortAndLimit<T>(TableDataOrderRequest request, IEnumerable<T> data, long maxAllowedRows = 100000)
                => TableDataType<T>.SortAndLimit(request ?? DefRequest, data, maxAllowedRows);



        #region Boxed versions

        /// <summary>
        /// Get table data from an enumerable sequence: filter, sort, text search, skip, limit (with look ahead) and extract the rows.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="request">What part of the data, sorting etc, if null a default request (first 20 rows) is used.</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data. Columns and title are only included if the request change counter differs from the current one.</returns>
        /// <remarks>
        /// Exceptions while processing the data are swallowed, giving an empty table.
        /// No server side cap is applied to the requested row count (zero or negative returns all rows).
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TableData Get<T>(TableDataRequest request, IEnumerable<T> data, String title = null) 
            => TableDataType<T>.Get(request ?? DefRequest, data, title);


        /// <summary>
        /// Get table data from an enumerable sequence without any filtering, sorting or limiting.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">Source data</param>
        /// <returns>Some table data with all columns, <see cref="CommonTableData.RowCount"/> is not set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TableData GetAll<T>(IEnumerable<T> data)
            => TableDataType<T>.GetAll(data);

        /// <summary>
        /// Get table data from an enumerable sequence, see <see cref="Get{T}(TableDataRequest, IEnumerable{T}, string)"/>.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
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
        /// Get table data from an enumerable sequence with the <see cref="AutoTranslateAttribute"/> columns translated,
        /// see <see cref="Get{T}(TableDataRequest, IEnumerable{T}, string)"/>.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="translationContext">The translator and target language to use, if null (or without translator) no translation is done</param>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
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


        #endregion// Boxed versions


        #region Typed versions

        /// <summary>
        /// Get typed table data (rows are the objects themselves) from an enumerable sequence: filter, sort, text search, skip and limit.
        /// The columns exclude read only and computed members.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <param name="request">What part of the data, sorting etc, if null a default request (first 20 rows) is used.</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        /// <remarks>Exceptions while processing the data are swallowed, giving an empty table.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypedTableData<T> GetTyped<T>(TableDataRequest request, IEnumerable<T> data, String title = null)
            => TableDataType<T>.GetTyped<TypedTableData<T>>(request ?? DefRequest, data, title);


        /// <summary>
        /// Get typed table data from an enumerable sequence without any filtering, sorting or limiting.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <param name="data">Source data</param>
        /// <returns>Some table data, <see cref="CommonTableData.RowCount"/> is not set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypedTableData<T> GetAllTyped<T>(IEnumerable<T> data)
            => TableDataType<T>.GetAllTyped<TypedTableData<T>>(data);

        /// <summary>
        /// Get typed table data from an enumerable sequence, see <see cref="GetTyped{T}(TableDataRequest, IEnumerable{T}, string)"/>.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static TypedTableData<T> GetTyped<T>(TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null)
        {
            var r = TableDataType<T>.GetTyped<TypedTableData<T>>(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r;
        }

        /// <summary>
        /// Get typed table data from an enumerable sequence with any translations applied (the row objects are modified in place).
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <param name="translationContext">The translator and target language to use, if null (or without translator) no translation is done</param>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static Task<TypedTableData<T>> GetTyped<T>(ITranslationContext translationContext, TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null)
        {
            var r = TableDataType<T>.GetTyped<TypedTableData<T>>(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r.Translate<T, TypedTableData<T>>(translationContext);
        }

        #endregion// Typed versions


        #region Typed base versions

        /// <summary>
        /// Get typed table data of a custom table type, see <see cref="GetTyped{T}(TableDataRequest, IEnumerable{T}, string)"/>.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <typeparam name="R">The typed table type to create</typeparam>
        /// <param name="request">What part of the data, sorting etc, if null a default request (first 20 rows) is used.</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R GetTyped<T, R>(TableDataRequest request, IEnumerable<T> data, String title = null) where R : TypedTableData<T>, new()
            => TableDataType<T>.GetTyped<R>(request ?? DefRequest, data, title);


        /// <summary>
        /// Get typed table data of a custom table type without any filtering, sorting or limiting.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <typeparam name="R">The typed table type to create</typeparam>
        /// <param name="data">Source data</param>
        /// <returns>Some table data</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static R GetAllTyped<T, R>(IEnumerable<T> data) where R : TypedTableData<T>, new()
            => TableDataType<T>.GetAllTyped<R>(data);

        /// <summary>
        /// Get typed table data of a custom table type, see <see cref="GetTyped{T}(TableDataRequest, IEnumerable{T}, string)"/>.
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <typeparam name="R">The typed table type to create</typeparam>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static R GetTyped<T, R>(TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null) where R : TypedTableData<T>, new()
        {
            var r = TableDataType<T>.GetTyped<R>(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r;
        }

        /// <summary>
        /// Get typed table data of a custom table type with any translations applied (the row objects are modified in place).
        /// </summary>
        /// <typeparam name="T">The element (row) type</typeparam>
        /// <typeparam name="R">The typed table type to create</typeparam>
        /// <param name="translationContext">The translator and target language to use, if null (or without translator) no translation is done</param>
        /// <param name="request">What part of the data, sorting etc</param>
        /// <param name="refreshRate">Number of ms the client should wait before refreshing the data</param>
        /// <param name="data">Source data</param>
        /// <param name="title">Optional title of the table</param>
        /// <returns>Some table data</returns>
        public static Task<R> GetTyped<T, R>(ITranslationContext translationContext, TableDataRequest request, long refreshRate, IEnumerable<T> data, String title = null) where R : TypedTableData<T>, new()
        {
            var r = TableDataType<T>.GetTyped<R>(request ?? DefRequest, data, title);
            if (r != null)
                r.RefreshRate = refreshRate;
            return r.Translate<T, R>(translationContext);
        }


        #endregion// Typed versions


        /// <summary>
        /// Convert an object into a values array (one boxed value per column, in column order).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The row object.</param>
        /// <returns>A new array of values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Object[] GetValues<T>(T data)
            => TableDataType<T>.Extract(data);

        /// <summary>
        /// Convert an object into a table row.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The row object.</param>
        /// <returns>A new row with one boxed value per column.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TableDataRow GetRow<T>(T data)
            => new TableDataRow { Values = TableDataType<T>.Extract(data) };


        static readonly TableDataRequest DefRequest = new ()
        {
            MaxRowCount = 20
        };

        #region Runtime manipulation

        #region Column header


        /// <summary>
        /// If table data columns exist, replace them with clones (so that they can be modified without affecting the shared, per type, columns) and return true.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table to modify.</param>
        /// <param name="cols">The columns data to modify</param>
        /// <param name="addColumns">Add columns</param>
        /// <returns>True if column information exists, else false</returns>
        public static bool ModifyColumns<T>(this T data, out TableDataColumn[] cols, int addColumns = 0) where T : CommonTableData
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
        /// Remove a column, and the corresponding value from every row, based on index.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table to modify (in place).</param>
        /// <param name="columnIndex">The index of the column to remove.</param>
        /// <returns>The source table data.</returns>
        public static T RemoveColumnIndex<T>(this T data, int columnIndex) where T : BaseTableData
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
        /// Remove a column definition from a typed table, based on index (the row objects are unaffected).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The table to modify (in place).</param>
        /// <param name="columnIndex">The index of the column (in the table's <see cref="CommonTableData.Cols"/>) to remove.</param>
        /// <returns>The source table data.</returns>
        public static TypedTableData<T> RemoveColumnIndex<T>(this TypedTableData<T> data, int columnIndex)
        {
            var src = data.Cols;
            if (src != null)
                data.Cols = data.Cols.RemoveAt(columnIndex);
            return data;
        }


        /// <summary>
        /// Remove a column definition from a typed table, based on name.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The table to modify (in place).</param>
        /// <param name="name">The name of the column.</param>
        /// <returns>The source table data.</returns>
        /// <exception cref="KeyNotFoundException">No column with that name exists in <typeparamref name="T"/>.</exception>
        /// <remarks>The column is looked up by name in the table's <see cref="CommonTableData.Cols"/>, if it isn't present there (but exists in <typeparamref name="T"/>) nothing is removed.</remarks>
        public static TypedTableData<T> RemoveColumn<T>(this TypedTableData<T> data, String name)
        {
            var cols = data.Cols;
            var index = cols == null ? -1 : Array.FindIndex(cols, c => c.Name == name);
            if (index >= 0)
                return RemoveColumnIndex(data, index);
            if (!TableDataType<T>.NameToColumnIndex.ContainsKey(name))
                throw new KeyNotFoundException("No column named \"" + name + "\" exists in " + typeof(T).FullName);
            return data;
        }

        /// <summary>
        /// Hide a column in some table data (sets <see cref="TableDataColumnProps.Hide"/>).
        /// The column objects are modified in place, use <see cref="ModifyColumns{T}(T, out TableDataColumn[], int)"/> first if they are shared.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <returns>The source table data</returns>
        public static T HideColumn<T>(this T data, String name) where T : CommonTableData
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
        /// Set the title of a column.
        /// The column objects are modified in place, use <see cref="ModifyColumns{T}(T, out TableDataColumn[], int)"/> first if they are shared.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="title">The new title</param>
        /// <returns>The source table data</returns>
        public static T SetColumnTitle<T>(this T data, String name, String title) where T : CommonTableData
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
        /// Set the title of a column.
        /// The column objects are modified in place, use <see cref="ModifyColumns{T}(T, out TableDataColumn[], int)"/> first if they are shared.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="getTitle">A function that gets a new title, given the column</param>
        /// <returns>The source table data</returns>
        public static T SetColumnTitle<T>(this T data, String name, Func<TableDataColumn, String> getTitle) where T : CommonTableData
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
        /// Set the description of a column.
        /// The column objects are modified in place, use <see cref="ModifyColumns{T}(T, out TableDataColumn[], int)"/> first if they are shared.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="desc">The new description</param>
        /// <returns>The source table data</returns>
        public static T SetColumnDesc<T>(this T data, String name, String desc) where T : CommonTableData
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
        /// Set the description of a column using a function.
        /// The column objects are modified in place, use <see cref="ModifyColumns{T}(T, out TableDataColumn[], int)"/> first if they are shared.
        /// </summary>
        /// <typeparam name="T">The table type.</typeparam>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="name">The name of the column (member name)</param>
        /// <param name="getDesc">A function that gets a new description, given the column</param>
        /// <returns>The source table data</returns>
        public static T SetColumnDesc<T>(this T data, String name, Func<TableDataColumn, String> getDesc) where T : CommonTableData
        {
            var c = data.Cols;
            if (c == null)
                return data;
            foreach (var x in c)
            {
                if (x.Name == name)
                    x.Desc = getDesc(x);
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
        /// For a given type and column name (member name), get the column index (in <see cref="GetCols{T}"/> and in extracted value arrays).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="name">The column name (member name)</param>
        /// <returns>The column index or -1 if not found</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary>
        /// Information about a supported column type: an optional expression conversion to a serializable value and a (throwing) string parser for filter values.
        /// </summary>
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


        /// <summary>
        /// The member types that can be used directly as columns (in addition to enums and nullables of these).
        /// <see cref="Type"/> columns are converted to the full name, <see cref="Exception"/> to the message and <see cref="Object"/> values to strings unless primitive or supported.
        /// </summary>
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

        /// <summary>
        /// Get the instance fields and properties (any non method member) of a type, ordered by <see cref="TableDataOrderAttribute"/> (stable).
        /// For interfaces, members of inherited interfaces are included (first member with a given name wins).
        /// </summary>
        static internal IEnumerable<MemberInfo> GetInstanceMembers(Type t, BindingFlags flags = BindingFlags.Public)
        {
            flags |= BindingFlags.Instance;
            Func<MemberInfo, int> orderFn = GetOrder;
            if (t.IsInterface)
            {
                HashSet<String> seen = new HashSet<string>();
                List<MemberInfo> mi = new List<MemberInfo>();
                foreach (var x in t.GetMembers(flags))
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
                    foreach (var x in i.GetMembers(flags))
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
            }
            else
            {
                foreach (var x in t.GetMembers(flags).Where(i => i.MemberType != MemberTypes.Method).OrderBy(orderFn))
                    yield return x;
            }
        }


        /// <summary>
        /// Get column information for a type (all columns).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <returns>The shared column array, clone before modifying.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TableDataColumn[] GetCols<T>() => TableDataType<T>.Cols;

        /// <summary>
        /// Get the member type of each column for a type.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <returns>The shared array of types, in column order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type[] GetColTypes<T>() => TableDataType<T>.ColTypes;


        /// <summary>
        /// Get the column information used for typed tables (excludes read only and computed members).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <returns>The shared column array, clone before modifying.</returns>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TableDataColumn[] GetTypedCols<T>() => TableDataType<T>.TypedCols;


        /// <summary>
        /// Get column information for a type (all columns), using reflection, see <see cref="GetCols{T}"/>.
        /// </summary>
        /// <param name="t">The row type.</param>
        /// <returns>The shared column array, clone before modifying.</returns>
        public static TableDataColumn[] GetCols(Type t)
        {
            var gt = (TableDataColumn[])typeof(TableDataType<>).MakeGenericType(t).GetField(nameof(TableDataType<int>.Cols), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return gt;
        }


        /// <summary>
        /// Get the column information used for typed tables, using reflection, see <see cref="GetTypedCols{T}"/>.
        /// </summary>
        /// <param name="t">The row type.</param>
        /// <returns>The shared column array, clone before modifying.</returns>
        public static TableDataColumn[] GetTypedCols(Type t)
        {
            var gt = (TableDataColumn[])typeof(TableDataType<>).MakeGenericType(t).GetField(nameof(TableDataType<int>.TypedCols), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return gt;
        }

        /// <summary>
        /// Get the member type of each column for a type, using reflection, see <see cref="GetColTypes{T}"/>.
        /// </summary>
        /// <param name="t">The row type.</param>
        /// <returns>The shared array of types, in column order.</returns>
        public static Type[] GetColTypes(Type t)
        {
            var gt = (Type[])typeof(TableDataType<>).MakeGenericType(t).GetField(nameof(TableDataType<int>.ColTypes), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
            return gt;
        }

        /// <summary>
        /// Extract data from enumerable (should already be filtered, ordered, offsetted etc)
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="count">Number of extracted rows plus the number of look ahead items found</param>
        /// <param name="data">Enumerable data</param>
        /// <param name="limit">Maximum number of rows to take, zero or negative means no limit</param>
        /// <param name="lookAhead">Number of additional items to count (but not extract)</param>
        /// <returns>Data rows, never null</returns>
        public static TableDataRow[] ExtractGet<T>(out long count, IEnumerable<T> data, long limit = long.MaxValue, long lookAhead = 0) => TableDataType<T>.ExtractGet(out count, data, limit, lookAhead);


        /// <summary>
        /// Take rows from enumerable (should already be filtered, ordered, offsetted etc)
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="count">Number of returned rows plus the number of look ahead items found</param>
        /// <param name="data">Enumerable data</param>
        /// <param name="limit">Maximum number of rows to take, zero or negative means no limit</param>
        /// <param name="lookAhead">Number of additional items to count (but not return)</param>
        /// <returns>The row objects, never null</returns>
        public static T[] ExtractGetTyped<T>(out long count, IEnumerable<T> data, long limit = long.MaxValue, long lookAhead = 0) => TableDataType<T>.ExtractTypedGet(out count, data, limit, lookAhead);


        internal static readonly ConstantExpression CommaArrayCharExp = Expression.Constant(new Char[] { ',' });
        internal static readonly ConstantExpression StringSplitOptionsExp = Expression.Constant(StringSplitOptions.TrimEntries);

        internal static readonly MethodInfo StringSplitMethod = typeof(String).GetMethod(nameof(String.Split), BindingFlags.Public | BindingFlags.Instance, new Type[] { typeof(Char[]), typeof(StringSplitOptions) });
        internal static readonly MethodInfo LinqSelectMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == nameof(Enumerable.Select)).First(mi => mi.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);

        internal static readonly MethodInfo LinqMinMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsGenericMethod && x.Name == nameof(Enumerable.Min)).First(mi => mi.GetParameters().Length == 1);
        internal static readonly MethodInfo LinqMaxMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.IsGenericMethod && x.Name == nameof(Enumerable.Max)).First(mi => mi.GetParameters().Length == 1);
        internal static readonly IReadOnlyDictionary<Type, MethodInfo> LinqMinMethods;
        internal static readonly IReadOnlyDictionary<Type, MethodInfo> LinqMaxMethods;

        /// <summary>
        /// Build an expression that splits a comma separated string (entries trimmed) and converts each entry to <paramref name="type"/>
        /// using the lenient <see cref="StringConverter"/> (invalid entries become the default value).
        /// </summary>
        /// <param name="stringValueExpression">A string expression.</param>
        /// <param name="type">The element type.</param>
        /// <returns>An expression of type <c>String[]</c> or <c>IEnumerable&lt;type&gt;</c>.</returns>
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

        /// <summary>
        /// Build an expression that creates a <see cref="HashSet{T}"/> (default comparer) from an enumerable or array expression.
        /// </summary>
        /// <param name="enumExpression">An array or <see cref="IEnumerable{T}"/> expression.</param>
        /// <returns>A new hash set expression.</returns>
        public static Expression GetHashSetFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            var hashCon = typeof(HashSet<>).MakeGenericType(elementType).GetConstructor(new Type[] { typeof(IEnumerable<>).MakeGenericType(elementType) });
            var hashSetExpression = Expression.New(hashCon, enumExpression);
            return hashSetExpression;
        }

        /// <summary>
        /// Build an expression that creates a <see cref="HashSet{T}"/> from an enumerable or array expression,
        /// using an ordinal (optionally case insensitive) comparer for strings.
        /// </summary>
        /// <param name="enumExpression">An array or <see cref="IEnumerable{T}"/> expression.</param>
        /// <param name="caseSensitive">For string elements, true to use <see cref="StringComparer.Ordinal"/>, false for <see cref="StringComparer.OrdinalIgnoreCase"/>.</param>
        /// <returns>A new hash set expression.</returns>
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

        /// <summary>
        /// Build an expression that computes the (min, max) tuple of an enumerable or array expression using <c>EnumerableExt.MinMax</c>.
        /// </summary>
        /// <param name="enumExpression">An array or <see cref="IEnumerable{T}"/> expression.</param>
        /// <returns>An expression returning a <see cref="Tuple{T1, T2}"/> with the min and max values.</returns>
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


        /// <summary>
        /// Build an expression that computes the minimum of an enumerable or array expression using <see cref="Enumerable"/>.Min.
        /// </summary>
        /// <param name="enumExpression">An array or <see cref="IEnumerable{T}"/> expression.</param>
        /// <returns>The min expression.</returns>
        public static Expression GetMinFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            return Expression.Call(LinqMinMethods.TryGetValue(elementType, out var m) ? m : LinqMinMethod.MakeGenericMethod(elementType), enumExpression);
        }

        /// <summary>
        /// Build an expression that computes the maximum of an enumerable or array expression using <see cref="Enumerable"/>.Max.
        /// </summary>
        /// <param name="enumExpression">An array or <see cref="IEnumerable{T}"/> expression.</param>
        /// <returns>The max expression.</returns>
        public static Expression GetMaxFromEnum(Expression enumExpression)
        {
            var et = enumExpression.Type;
            var elementType = et.HasElementType ? et.GetElementType() : et.GetGenericArguments()[0];
            return Expression.Call(LinqMaxMethods.TryGetValue(elementType, out var m) ? m : LinqMaxMethod.MakeGenericMethod(elementType), enumExpression);
        }

        /// <summary>
        /// Build an expression that calls <see cref="HashSet{T}.Contains(T)"/>.
        /// </summary>
        /// <param name="hashSetExpression">A <see cref="HashSet{T}"/> expression.</param>
        /// <param name="value">The value expression to test.</param>
        /// <returns>A boolean expression.</returns>
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
        /// Create a function that returns table data from some static data.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The static data, enumerated once into a list (later changes to the source are not seen, but the row objects are shared).</param>
        /// <param name="columns">Optionally override the description, format, title and the hide/key/chart flags of the columns derived from the type.
        /// Must have the same number of columns, in the same order, as the type. Name and type are always taken from the type.</param>
        /// <param name="title">Optional title.</param>
        /// <returns>A function that can be used to get the static data</returns>
        /// <exception cref="Exception">The number of <paramref name="columns"/> doesn't match the type.</exception>
        public static Func<TableDataRequest, TableData> GetStaticTableFn<T>(IEnumerable<T> data, TableDataColumn[] columns = null, String title = null)
        {
            var d = data.ToList();
            if (columns == null)
                return r => Get(r, d, title);
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
            return r =>
            {
                var t = Get(r, d);
                return new TableData
                {
                    RowCount = t.RowCount,
                    RefreshRate = t.RefreshRate,
                    Rows = t.Rows,
                }.HandleCc(r.Cc, destCols, title);
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
        /// Create a function that returns table data from some static untyped data.
        /// A row type with one public field per column is emitted (and cached per column type and name set), and the rows are converted into instances of it.
        /// </summary>
        /// <param name="columns">The columns in the table, <see cref="TableDataBaseColumn.Type"/> must be resolvable type names.</param>
        /// <param name="rows">Data for each row and column, values are converted to the column type when needed (strings are parsed).
        /// Enumerated once, immediately.</param>
        /// <param name="title">Optional title</param>
        /// <returns>A function that can be used to get the static data</returns>
        /// <remarks>Null values in value type columns, or values that can't be converted, throw while creating the rows.</remarks>
        public static Func<TableDataRequest, TableData> GetStaticTableFn(TableDataColumn[] columns, IEnumerable<object[]> rows, String title = null)
        {
            var type = GetDynType(out var createFn, columns);
            using var it = rows.GetEnumerator();
            return createFn(it, columns, title);
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



        /// <summary>
        /// Define a public auto property (private backing field, public get and set accessors) on a type being built.
        /// </summary>
        /// <param name="typeBuilder">The type builder.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="propertyType">The type of the property.</param>
        public static void DefineAutoProperty(this TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            // 1. Define the private backing field (e.g., _status)
            FieldBuilder fieldBuilder = typeBuilder.DefineField(
                "_" + propertyName.ToLower(),
                propertyType,
                FieldAttributes.Private);

            // 2. Define the property metadata
            PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.HasDefault,
                propertyType,
                null);

            // Required method attributes for property accessors
            MethodAttributes accessorAttributes =
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig;

            // 3. Define the 'get' accessor method
            MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
                "get_" + propertyName,
                accessorAttributes,
                propertyType,
                Type.EmptyTypes);

            ILGenerator getIL = getMethodBuilder.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0);        // Load 'this' onto the evaluation stack
            getIL.Emit(OpCodes.Ldfld, fieldBuilder); // Load the value of the backing field
            getIL.Emit(OpCodes.Ret);            // Return the value

            // 4. Define the 'set' accessor method
            MethodBuilder setMethodBuilder = typeBuilder.DefineMethod(
                "set_" + propertyName,
                accessorAttributes,
                null,
                new Type[] { propertyType });

            ILGenerator setIL = setMethodBuilder.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0);        // Load 'this' onto the evaluation stack
            setIL.Emit(OpCodes.Ldarg_1);        // Load the incoming 'value' argument
            setIL.Emit(OpCodes.Stfld, fieldBuilder); // Store 'value' into the backing field
            setIL.Emit(OpCodes.Ret);            // Return void

            // 5. Bind the get/set methods to the property metadata
            propertyBuilder.SetGetMethod(getMethodBuilder);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }

        static Type GetPropertyType(TableDataColumn[] defs)
        {
            var types = defs.Select(x => TypeFinder.Get(x.Type)).ToArray();
            var names = defs.Select(x => x.Name).ToArray();
            var key = String.Join('|', String.Join('/', types.Select(x => x.FullName)), String.Join("\\", names));
            var cache = PropertyTypeCache;
            if (cache.TryGetValue(key, out var type))
                return type;
            lock (cache)
            {
                if (cache.TryGetValue(key, out type))
                    return type;
                var l = types.Length;
                var inpRows = InputRows;
                var varCols = VarCols;

                var typeName = "TableDatatPropType" + Interlocked.Increment(ref TypeName);
                var typeBuilder = ModuleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
                for (int i = 0; i < l; ++i)
                    typeBuilder.DefineAutoProperty(names[i], types[i]);
                type = typeBuilder.CreateType();
                if (!cache.TryAdd(key, type))
                    type = cache[key];
                return type;
            }
        }

        static readonly ConcurrentDictionary<String, Type> PropertyTypeCache = new(StringComparer.Ordinal);


        /// <summary>
        /// Create an array with one empty filter per column of the type (only <see cref="TableDataFilter.ColName"/> is set).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <returns>A new array of filters, in column order.</returns>
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

        /// <summary>
        /// Remove the filters of hidden columns from a per column filter array (in place, the array is resized if needed).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="f">One filter per column of the type, in column order.</param>
        /// <returns>The filters for the visible columns.</returns>
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


        /// <summary>
        /// Create a table UI state from a request, arranging the request filters into rows of per (visible) column filters
        /// (a column with multiple filters spans multiple rows).
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="r">The request.</param>
        /// <returns>The state, referencing the request.</returns>
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
                    if (!cindex.TryGetValue(ff.ColName ?? "", out var ci))
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



        /// <summary>
        /// Get a (cached) constant expression holding a default instance of a class, created using its public parameterless constructor.
        /// Used as a fallback for null values of expanded members.
        /// </summary>
        /// <returns>The constant expression, or null if the type has no public parameterless constructor or it threw.</returns>
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
        /// Full names of column types that can be charted (value axis).
        /// Columns of these types get <see cref="TableDataColumnProps.CanChart"/> if the row type has a primary key and they aren't part of it.
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
        /// Translate the <see cref="AutoTranslateAttribute"/> columns of a table data (in place, rows are processed concurrently).
        /// </summary>
        /// <typeparam name="T">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate, returned as is if null or if the translator is null</param>
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
            if (l > 0)
                await rows.ProcessAsync(row => tr(translator, to, row.Values, effort, retention)).ConfigureAwait(false);
            return data;
        }


        /// <summary>
        /// Translate the <see cref="AutoTranslateAttribute"/> members of the row objects in a typed table data (in place, rows are processed concurrently).
        /// </summary>
        /// <typeparam name="T">The type must match the element type used when creating the table</typeparam>
        /// <typeparam name="R">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate, returned as is if null or if the translator is null</param>
        /// <param name="translator">The translator to use</param>
        /// <param name="to">The target language</param>
        /// <param name="effort">The effort (cost / time) to put into the translation</param>
        /// <param name="retention">How long to cache the translation</param>
        /// <returns>A task to await for translation completion</returns>
        public static async Task<R> Translate<T, R>(this R data, ITranslator translator, String to, TranslationEffort effort = TranslationEffort.High, TranslationCacheRetention retention = TranslationCacheRetention.Long) where R : TypedTableData<T>, new()
        {
            if ((translator == null) || (data == null))
                return data;
            var tr = TypeTranslatorT<T>.Translate;
            if (tr == null)
                return data;
            var rows = data.Rows;
            if (rows == null)
                return data;
            var l = rows.Length;
            if (l > 0)
                await rows.ProcessAsync(row => tr(translator, to, row, effort, retention)).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Translate the data in a table data.
        /// </summary>
        /// <typeparam name="T">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate</param>
        /// <param name="translationContext">The translator and target language to use</param>
        /// <returns>A task to await for translation completion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<TableData> Translate<T>(this TableData data, ITranslationContext translationContext)
            => Translate<T>(data, translationContext?.Translator, translationContext?.Language);


        /// <summary>
        /// Translate the data in a table data.
        /// </summary>
        /// <typeparam name="T">The type must match the element type used when creating the table</typeparam>
        /// <typeparam name="R">The type must match the type used when creating the table</typeparam>
        /// <param name="data">The data to translate</param>
        /// <param name="translationContext">The translator and target language to use</param>
        /// <returns>A task to await for translation completion</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task<R> Translate<T, R>(this R data, ITranslationContext translationContext) where R : TypedTableData<T>, new()
            => Translate<T, R>(data, translationContext?.Translator, translationContext?.Language);



        /// <summary>
        /// Apply the column change counter logic: column definitions and title are only sent when the client doesn't already have them.
        /// The change counter is <see cref="EnvInfo.Cc"/>, a value unique to the server process instance.
        /// </summary>
        /// <param name="data">The table to update (in place).</param>
        /// <param name="requestCc">The change counter from the request: -1 = never send columns, the current counter = client already has the columns, any other value = send columns.</param>
        /// <param name="cols">The columns to send.</param>
        /// <param name="title">The title to set when columns are sent, if null the existing title is kept.</param>
        /// <returns>The source table data.</returns>
        public static TableData HandleCc(this TableData data, long requestCc, TableDataColumn[] cols, String title = null)
        {
            if (requestCc == -1)
            {
                data.Cc = requestCc;
                data.Cols = null;
                data.Title = null;
                return data;
            }
            var cc = EnvInfo.Cc;
            if (cc == requestCc)
            {
                data.Cc = cc;
                data.Cols = null;
                data.Title = null;
                return data;
            }
            data.Cc = cc;
            data.Cols = cols;
            if (title != null)
                data.Title = title;
            return data;
        }

        /// <summary>
        /// Apply the column change counter logic to a typed table, see <see cref="HandleCc(TableData, long, TableDataColumn[], string)"/>.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="data">The table to update (in place).</param>
        /// <param name="requestCc">The change counter from the request.</param>
        /// <param name="cols">The columns to send.</param>
        /// <param name="title">The title to set when columns are sent, if null the existing title is kept.</param>
        /// <returns>The source table data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypedTableData<T> HandleCc<T>(this TypedTableData<T> data, long requestCc, TableDataColumn[] cols, String title = null)
            => HandleCc<T, TypedTableData<T>>(data, requestCc, cols, title);

        /// <summary>
        /// Apply the column change counter logic to a custom typed table, see <see cref="HandleCc(TableData, long, TableDataColumn[], string)"/>.
        /// </summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <typeparam name="R">The typed table type.</typeparam>
        /// <param name="data">The table to update (in place).</param>
        /// <param name="requestCc">The change counter from the request.</param>
        /// <param name="cols">The columns to send.</param>
        /// <param name="title">The title to set when columns are sent, if null the existing title is kept.</param>
        /// <returns>The source table data.</returns>
        public static R HandleCc<T, R>(this R data, long requestCc, TableDataColumn[] cols, String title = null) where R : TypedTableData<T>
        {
            if (requestCc == -1)
            {
                data.Cc = requestCc;
                data.Cols = null;
                data.Title = null;
                return data;
            }
            var cc = EnvInfo.Cc;
            if (cc == requestCc)
            {
                data.Cc = cc;
                data.Cols = null;
                data.Title = null;
                return data;
            }
            data.Cc = cc;
            data.Cols = cols;
            if (title != null)
                data.Title = title;
            return data;
        }

    }






}
