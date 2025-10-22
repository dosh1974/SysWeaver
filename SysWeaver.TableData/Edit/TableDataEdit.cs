using System;
using System.Collections.Generic;
using System.Linq;
using SysWeaver.Parser;

namespace SysWeaver.Data
{


    /// <summary>
    /// Extensions method for manipulating table data
    /// </summary>
    public static class TableDataEdit
    {


        /// <summary>
        /// Build a new table with the selected columns
        /// </summary>
        /// <param name="data">The table to manipulate</param>
        /// <param name="columnNames">The columns to keep (in desired order)</param>
        /// <returns>A new table with only the selected columns</returns>
        public static BaseTableData SelectColumns(this BaseTableData data, params String[] columnNames)
        {
            var lookup = BuildLookUpValidateNames(data, columnNames);
            var newCount = columnNames.Length;
            if (newCount <= 0)
                return new BaseTableData();
            TableDataColumn[] newCols = new TableDataColumn[newCount];
            int[] sourceIndices = new int[newCount];
            for (int i = 0; i < newCount; ++i)
            {
                var name = columnNames[i];
                var colD = lookup[name];
                sourceIndices[i] = colD.Item2;
                newCols[i] = colD.Item1;
            }
            return new BaseTableData
            {
                Cols = newCols,
                Rows = CreateNewRows(data, sourceIndices)
            };
        }

        /// <summary>
        /// Build a new table with some columns removed
        /// </summary>
        /// <param name="data">The table to manipulate</param>
        /// <param name="columnNames">The columns to remove</param>
        /// <returns>A new table without the selected columns</returns>
        public static BaseTableData RemoveColumns(this BaseTableData data, params String[] columnNames)
        {
            BuildLookUpValidateNames(data, columnNames);
            var cols = data.Cols;
            var count = cols.Length;
            HashSet<String> remove = new HashSet<string>(columnNames, StringComparer.Ordinal);
            var newCount = count - remove.Count;
            if (newCount <= 0)
                return new BaseTableData();
            TableDataColumn[] newCols = new TableDataColumn[newCount];
            int[] sourceIndices = new int[newCount];
            for (int i = 0, o = 0; i < count; ++i)
            {
                var col = cols[i];
                if (remove.Contains(col.Name))
                    continue;
                sourceIndices[o] = i;
                newCols[o] = col;
                ++o;
            }
            return new BaseTableData
            {
                Cols = newCols,
                Rows = CreateNewRows(data, sourceIndices),
            };

        }

        /// <summary>
        /// Add new (computed) column(s).
        /// </summary>
        /// <param name="data">The table to manipulate</param>
        /// <param name="columns">The columns to add</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static BaseTableData AddColumns(this BaseTableData data, params NewTableDataColumn[] columns)
        {
            var lookUp = BuildLookUpValidateNames(data);
            var destLookUp = DictionaryExt.Create(lookUp, lookUp.Comparer);

            void IncIndex(int cutOff)
            {
                var keys = destLookUp.Keys.ToList();
                foreach (var x in keys)
                {
                    var v = destLookUp[x];
                    var t = v.Item2;
                    if (t > cutOff)
                        destLookUp[x] = Tuple.Create(v.Item1, t + 1);
                }
            }

            int newCount = columns.Length;
            for (int i = 0; i < newCount; ++i)
            {
                var col = columns[i];
                if (destLookUp.ContainsKey(col.Name))
                    throw new ArgumentException("New column name may not be equal to existing column names, got duplicate: " + col.Name.ToQuoted(), nameof(col.Name));
                var loc = col.InsertBefore;
                int index = destLookUp.Count;
                if (!String.IsNullOrEmpty(loc))
                {
                    if (!destLookUp.TryGetValue(loc, out var e))
                        throw new ArgumentException("Column " + loc + " is not found!", nameof(col.InsertBefore));
                    index = e.Item2;
                    IncIndex(index - 1);
                }
                else
                {
                    loc = col.InsertAfter;
                    if (!String.IsNullOrEmpty(loc))
                    {
                        if (!destLookUp.TryGetValue(loc, out var e))
                            throw new ArgumentException("Column " + loc + " is not found!", nameof(col.InsertAfter));
                        index = e.Item2 + 1;
                        IncIndex(index - 1);
                    }
                }
                var ncol = col.Clone();
                if (ncol.Desc == null)
                    ncol.Desc = col.Expression;
                if (ncol.Title == null)
                    ncol.Title = StringTools.RemoveCamelCase(col.Name);
                destLookUp[col.Name] = Tuple.Create(ncol, index);
            }
            var destLen = destLookUp.Count;

            Func<Object[], Object>[] setExp = new Func<object[], object>[newCount];
            var invalid = new HashSet<String>(columns.Select(x => x.Name), StringComparer.Ordinal);
            int[] newPos = new int[newCount];
            for (int i = 0; i < newCount; ++i)
            {
                var col = columns[i];
                var type = TypeFinder.Get(col.Type);
                if (type == null)
                    throw new ArgumentException("New column " + col.Name.ToQuoted() + " is using an unknown type " + col.Type.ToQuoted(), nameof(col.Type));
                newPos[i] = destLookUp[col.Name].Item2;
                var cValue = StringToObject.GetDefault(type);
                setExp[i] = values => cValue;
                var exp = col.Expression;
                if (!String.IsNullOrEmpty(exp))
                {
                    try
                    {
                        var evType = ExpressionEvaluator.Get(type);
                        if (evType != null)
                        {
                            List<String> varNames = new(destLen);
                            List<Tuple<int, Func<Object, Object>>> vars = new(destLen);
                            foreach (var tcol in destLookUp)
                            {
                                var name = tcol.Key;
                                if (invalid.Contains(name))
                                    continue;
                                var c = ObjectConverter.TryGetConverter(TypeFinder.Get(tcol.Value.Item1.Type), type);
                                if (c != null)
                                {
                                    varNames.Add(name);
                                    vars.Add(Tuple.Create(tcol.Value.Item2, c));
                                }
                            }
                            var varCount = varNames.Count;
                            if (varCount > 0)
                            {
                                var ev = evType.ObjectEvaluator(exp, varNames);
                                var varTemp = new Object[varCount];
                                setExp[i] = values =>
                                {
                                    for (int k = 0; k < varCount; ++k)
                                    {
                                        var p = vars[k];
                                        varTemp[k] = p.Item2(values[p.Item1]);
                                    }
                                    return ev(varTemp);
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("New column " + col.Name.ToQuoted() + " is using an invalid expression, can't solve expression " + exp.ToQuoted() + ", exception: " + ex, nameof(col.Expression));
                    }
                }
                invalid.Remove(col.Name);
            }

            int newTotalCount = destLookUp.Count;
            TableDataColumn[] newCols = new TableDataColumn[newTotalCount];
            int[] sourceIndices = new int[newTotalCount];
            foreach (var x in destLookUp)
            {
                var v = x.Value;
                var name = x.Key;
                var i = v.Item2;
                var col = v.Item1;
                newCols[i] = col;
                if (lookUp.TryGetValue(name, out var org))
                    sourceIndices[i] = org.Item2;
            }
            var newRows = CreateNewRows(data, sourceIndices);
            foreach (var row in newRows)
            {
                var values = row.Values;
                for (int i = 0; i < newCount; ++i)
                {
                    var newVal = setExp[i](values);
                    values[newPos[i]] = newVal;
                }
            }
            return new BaseTableData
            {
                Cols = newCols,
                Rows = newRows,
            };
        }

        /// <summary>
        /// Append a table to some table, column count and types must match
        /// </summary>
        /// <param name="data">The table to append data to (at end)</param>
        /// <param name="dataToAppend">The data to append</param>
        /// <returns>A new table with the content of both tables</returns>
        /// <exception cref="Exception"></exception>
        public static BaseTableData Append(this BaseTableData data, BaseTableData dataToAppend)
        {
            var dcols = data.Cols;
            var scols = dataToAppend.Cols;
            var colCount = dcols.Length;
            if (colCount != scols.Length)
                throw new Exception("Tables must have matching columns, column counts are " + colCount + " and " + scols.Length);
            for (int i = 0; i < colCount; ++i)
                if (dcols[i].Type != scols[i].Type)
                    throw new Exception("Tables must have mathing columns, column " + i + " have the types " + dcols[i].Type.ToQuoted() + " and " + scols[i].Type.ToQuoted());
            var drows = data.Rows;
            var srows = dataToAppend.Rows;
            var dc = drows.Length;
            var sc = srows.Length;
            var newRows = new TableDataRow[dc + sc];
            for (int i = 0; i < dc; ++i)
                newRows[i] = drows[i];
            for (int i = 0; i < sc; ++i)
                newRows[i + dc] = srows[i];
            return new BaseTableData
            {
                Cols = dcols,
                Rows = newRows,
            };
        }

        /// <summary>
        /// Filter, Order a table and optionally keep a limited range of rows.
        /// </summary>
        /// <param name="data">The table to filter</param>
        /// <param name="r">The filter options</param>
        /// <returns>A new table</returns>
        public static BaseTableData Filter(this BaseTableData data, TableDataOrderRequest r)
        {
            var t = new TableDataRequest();
            t.Filters = r.Filters;
            t.Order = r.Order;
            t.Row = r.Row;
            t.MaxRowCount = r.MaxRowCount;
            return TableDataTools.GetStaticTableFn(data.Cols, data.Rows.Select(x => x.Values), data.Title)(t);
        }

        /// <summary>
        /// Reverse the order of the rows
        /// </summary>
        /// <param name="data">The table to reverse rows</param>
        /// <returns>A new table</returns>
        public static BaseTableData Reverse(this BaseTableData data)
        {
            var sr = data.Rows;
            var l = sr.Length;
            var d = new TableDataRow[l];
            int i = 0;
            while (l > 0)
            {
                -- l;
                d[i] = sr[l];
                ++i;
            }
            return new BaseTableData
            {
                Cols = data.Cols,
                Rows = d
            };
        }


        /// <summary>
        /// Build a new table with the selected columns from two different tables.
        /// The number of rows must be identical
        /// </summary>
        /// <param name="data">The first table to select columns from</param>
        /// <param name="other">The other table to select columns from</param>
        /// <param name="columnNames">The columns to keep (in desired order).
        /// If a column exist in both tables, use a prefix of '-' to take it from the first or '+' to take it from the other table</param>
        /// <returns>A new table with the selected columns</returns>
        public static BaseTableData MergeColumns(this BaseTableData data, BaseTableData other, params String[] columnNames)
        {
            var rows1 = data.Rows;
            var rows2 = other.Rows;
            var rowCount = rows1.Length;
            if (rows2.Length != rowCount)
                throw new Exception("Tables must have the same number of rows, got " + rowCount + " and " + rows2.Length);

            var lookup1 = BuildLookUp(data);
            var lookup2 = BuildLookUp(other);
            var newCount = columnNames.Length;
            if (newCount <= 0)
                return new BaseTableData();
            TableDataColumn[] newCols = new TableDataColumn[newCount];
            int[] sourceIndices = new int[newCount];
            bool[] usedOther = new bool[newCount];
            for (int i = 0; i < newCount; ++i)
            {
                var name = columnNames[i];
                switch (name[0])
                {
                    case '-':
                        {
                            var colD = lookup1[name];
                            sourceIndices[i] = colD.Item2;
                            newCols[i] = colD.Item1;
                        }
                        break;
                    case '+':
                        {
                            var colD = lookup2[name];
                            sourceIndices[i] = colD.Item2;
                            newCols[i] = colD.Item1;
                            usedOther[i] = true;
                        }
                        break;
                    default:
                        {
                            if (lookup1.TryGetValue(name, out var colD))
                            {
                                sourceIndices[i] = colD.Item2;
                                newCols[i] = colD.Item1;
                            }else
                            {
                                colD = lookup2[name];
                                sourceIndices[i] = colD.Item2;
                                newCols[i] = colD.Item1;
                                usedOther[i] = true;
                            }
                        }
                        break;
                }
            }
            TableDataRow[] newRows = new TableDataRow[rowCount];
            for (int i = 0; i < rowCount; ++i)
            {
                var src1 = rows1[i].Values;
                var src2 = rows2[i].Values;
                var newRowData = new Object[newCount];
                var newRow = new TableDataRow
                {
                    Values = newRowData,
                };
                newRows[i] = newRow;
                for (int j = 0; j < newCount; ++j)
                    newRowData[j] = (usedOther[j] ? src2 : src1)[sourceIndices[j]];
            }
            return new BaseTableData
            {
                Cols = newCols,
                Rows = newRows
            };
        }


        public static BaseTableData Aggregate(this BaseTableData data, params TableColumnAggregation[] columns)
        {
            return null;
        }



        /// <summary>
        /// Build a new table by appling a number of operations on it
        /// </summary>
        /// <param name="data">The table data to manipulate</param>
        /// <param name="referenceSolver">A function that resolves a data table reference (required for append rows and merge columns only)</param>
        /// <param name="ops">The operations to perform on the data (in order)</param>
        /// <returns>The new table data</returns>
        public static BaseTableData ApplyOps(this BaseTableData data, Func<String, BaseTableData> referenceSolver, params TableDataOp[] ops)
        {
            if (ops == null)
                return data;
            foreach (var op in ops)
            {
                //  Append rows
                foreach (var a in op.AppendTableDataRef.Nullable())
                {
                    var other = referenceSolver(a);
                    data = data.Append(other);
                }
                //  Merge columns
                foreach (var a in op.MergeColumns.Nullable())
                {
                    var other = referenceSolver(a.TableDataRef);
                    data = data.MergeColumns(other, a.SelectColumns);
                }
                //  Compute columns
                var cc = op.ComputeColumns;
                if ((cc?.Length ?? 0) > 0)
                    data = data.AddColumns(cc);
                //  Select columns
                var kc = op.SelectColumns;
                if ((kc?.Length ?? 0) > 0)
                    data = data.SelectColumns(kc);
                //  Remove columns
                var rc = op.RemoveColumns;
                if ((rc?.Length ?? 0) > 0)
                    data = data.RemoveColumns(rc);
                //  Order and filter rows
                foreach (var f in op.SortAndFilterRows.Nullable())
                    data = data.Filter(f);
            }
            return data;
        }
            


        #region Helpers

        static Dictionary<String, Tuple<TableDataColumn, int>> BuildLookUp(BaseTableData data)
        {
            var cols = data.Cols;
            if (cols == null)
                throw new Exception("Can only manipulate complete tables!");
            var count = cols.Length;
            Dictionary<String, Tuple<TableDataColumn, int>> lookup = new(count, StringComparer.Ordinal);
            for (int i = 0; i < count; ++i)
            {
                var col = cols[i];
                lookup[col.Name] = Tuple.Create(col, i);
            }
            return lookup;
        }

        static Dictionary<String, Tuple<TableDataColumn, int>> BuildLookUpValidateNames(BaseTableData data, params String[] columnNames)
        {
            var lookup = BuildLookUp(data);
            foreach (var name in columnNames)
                if (!lookup.TryGetValue(name, out var _))
                    throw new Exception("Column name " + name.ToQuoted() + " is NOT found in the table!");
            return lookup;
        }

        static TableDataRow[] CreateNewRows(BaseTableData data, int[] sourceIndices)
        {
            var newCount = sourceIndices.Length;
            var srcRows = data.Rows;
            var rowCount = srcRows.Length;
            TableDataRow[] newRows = new TableDataRow[rowCount];
            for (int i = 0; i < rowCount; ++i)
            {
                var srcRowData = srcRows[i].Values;
                var newRowData = new Object[newCount];
                var newRow = new TableDataRow
                {
                    Values = newRowData,
                };
                newRows[i] = newRow;
                for (int j = 0; j < newCount; ++j)
                    newRowData[j] = srcRowData[sourceIndices[j]];
            }
            return newRows;
        }

        #endregion//Helpers

    }



}
