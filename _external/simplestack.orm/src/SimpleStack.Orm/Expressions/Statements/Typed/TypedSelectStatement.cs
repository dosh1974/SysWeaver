using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace SimpleStack.Orm.Expressions.Statements.Typed
{
    public sealed class TypedSelectStatement<T> : TypedWhereStatement<T>
    {
        private readonly IDialectProvider _dialectProvider;
        private readonly ModelDefinition _modelDefinition;

        public TypedSelectStatement(IDialectProvider dialectProvider)
            : base(dialectProvider)
        {
            _dialectProvider = dialectProvider;
            _modelDefinition = ModelDefinition<T>.Definition;

            Statement.TableName = _dialectProvider.GetQuotedTableName(_modelDefinition);
            Statement.Columns.AddRange(_dialectProvider.GetColumnNames(_modelDefinition));
        }

        public SelectStatement Statement { get; } = new SelectStatement();

        protected override WhereStatement GetWhereStatement()
        {
            return Statement;
        }

        private TableWhereExpresionVisitor<T> GetExpressionVisitor()
        {
            return new TableWhereExpresionVisitor<T>(_dialectProvider, Statement.Parameters, _modelDefinition);
        }

        private TableWFieldsExpresionVisitor<T> GetFieldsExpressionVisitor(bool addAliasSpecification = false)
        {
            return new TableWFieldsExpresionVisitor<T>(_dialectProvider, Statement.Parameters, _modelDefinition,
                addAliasSpecification);
        }

        public TypedSelectStatement<T> Distinct(bool distinct = true)
        {
            Statement.IsDistinct = distinct;
            return this;
        }


        /// <summary>Select aggregated data (as defined with the Aggregation attribute on the type)</summary>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> SelectAggregated(String tableName, HashSet<String> groupBy = null, HashSet<String> anyValue = null)
        {
            var c = Statement.Columns;
            var dp = _dialectProvider;
            var tableNameQ = dp.GetQuotedTableName(tableName);
            var ns = dp.NamingStrategy;
            c.Clear();
            foreach (var column in ModelDefinition<T>.Definition.FieldDefinitions)
            {
                var cname = dp.GetQuotedColumnName(ns.GetColumnName(column.FieldName));
                switch (column.UpdateAggregation)
                {
                    case UpdateAggregations.Add:
                        c.Add(String.Concat("SUM(", cname, ") AS ", cname));
                        break;
                    case UpdateAggregations.Min:
                        c.Add(String.Concat("MIN(", cname, ") AS ", cname));
                        break;
                    case UpdateAggregations.Max:
                        c.Add(String.Concat("MAX(", cname, ") AS ", cname));
                        break;

                    case UpdateAggregations.AddSameResetMax:
                        {
                            c.Add(String.Concat("MAX(", cname, ") AS ", cname));
                        }
                        break;
                    case UpdateAggregations.AddSameResetMin:
                        {
                            c.Add(String.Concat("MIN(", cname, ") AS ", cname));
                        }
                        break;
                    case UpdateAggregations.SetIfNewMax:
                        {
                            c.Add(String.Concat("MAX(", cname, ") AS ", cname));
                        }
                        break;
                    case UpdateAggregations.SetIfNewMin:
                        {
                            c.Add(String.Concat("MIN(", cname, ") AS ", cname));
                        }
                        break;

                    case UpdateAggregations.SetIfNewOrEqualMax:
                        {
                            c.Add(String.Concat("MAX(", cname, ") AS ", cname));
                        }
                        break;
                    case UpdateAggregations.SetIfNewOrEqualMin:
                        {
                            c.Add(String.Concat("MIN(", cname, ") AS ", cname));
                        }
                        break;

                    default:
                        if ((anyValue != null) && (anyValue.Contains(column.FieldName)))
                        {
                            c.Add(String.Concat("ANY_VALUE(", cname, ") AS ", cname));
                            continue;
                        }

                        if ((groupBy == null) || (!groupBy.Contains(column.FieldName)))
                        {
                            var pname = "@d" + column.FieldName;
                            var t = column.FieldType;
                            Statement.Parameters.Add(new StatementParameter(pname, t, t.IsValueType ? Activator.CreateInstance(t) : null));
                            c.Add(String.Join(" AS ", pname, cname));
                            continue;
                        }
                        c.Add(cname);
                        break;
                }
            }
            return this;
        }

        /// <summary>Fields to be selected.</summary>
        /// <typeparam name="TKey">objectWithProperties.</typeparam>
        /// <param name="fields">x=> x.SomeProperty1 or x=> new{ x.SomeProperty1, x.SomeProperty2}</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> Select<TKey>(Expression<Func<T, TKey>> fields)
        {
            Statement.Columns.Clear();
            Statement.Columns.AddRange(GetFieldsExpressionVisitor(true).VisitExpression(fields).Split(','));
            return this;
        }

        /// <summary>Fields to be selected.</summary>
        /// <typeparam name="TKey">objectWithProperties.</typeparam>
        /// <param name="fields">x=> x.SomeProperty1 or x=> new{ x.SomeProperty1, x.SomeProperty2}</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> SelectAlso<TKey>(Expression<Func<T, TKey>> fields)
        {
            Statement.Columns.AddRange(GetFieldsExpressionVisitor(true).VisitExpression(fields).Split(','));
            return this;
        }

        /// <summary>Select distinct.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="fields">x=> x.SomeProperty1 or x=> new{ x.SomeProperty1, x.SomeProperty2}</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> SelectDistinct<TKey>(Expression<Func<T, TKey>> fields)
        {
            Statement.Columns.Clear();
            Select(fields);
            Distinct();
            return this;
        }

        public new TypedSelectStatement<T> Clear()
        {
            Statement.Clear();
            return this;
        }

        /// <summary>Wheres the given predicate.</summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> Where(Expression<Func<T, bool>> predicate)
        {
            base.And(predicate);
            return this;
        }


        /// <summary>Wheres the given predicate.</summary>
        /// <param name="func">The where function.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> Where(String func)
        {
            base.And(func);
            return this;
        }


        /// <summary>Ands the given predicate.</summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> And(Expression<Func<T, bool>> predicate)
        {
            base.Where(GetExpressionVisitor(), predicate);
            return this;
        }


        /// <summary>Ors the given predicate.</summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> Or(Expression<Func<T, bool>> predicate)
        {
            base.Where(GetExpressionVisitor(), predicate, "OR");
            return this;
        }


        /// <summary>Ands the given predicate.</summary>
        /// <param name="func">The where function.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> And(String func)
        {
            base.Where(GetExpressionVisitor(), func);
            return this;
        }


        /// <summary>Ors the given predicate.</summary>
        /// <param name="func">The where function.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public new TypedSelectStatement<T> Or(String func)
        {
            base.Where(GetExpressionVisitor(), func, "OR");
            return this;
        }


        /// <summary>Group by.</summary>
        /// <param name="groupBy">Describes who group this object.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> GroupBy(string groupBy)
        {
            if (Statement.GroupByExpression.Length > 0)
            {
                Statement.GroupByExpression.Append(",");
            }

            Statement.GroupByExpression.Append(groupBy);
            return this;
        }

        /// <summary>Group by.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> GroupBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            var ev = GetFieldsExpressionVisitor();
            ev.IsGroupBy = true;
            GroupBy(ev.VisitExpression(keySelector));
            return this;
        }

        /// <summary>Having the given predicate.</summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> Having(Expression<Func<T, bool>> predicate)
        {
            if (Statement.HavingExpression.Length > 0)
            {
                Statement.HavingExpression.Append(",");
            }

            Statement.HavingExpression.Append(GetExpressionVisitor().VisitExpression(predicate));
            return this;
        }

        /// <summary>Order by.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            Statement.OrderByExpression.Clear();
            Statement.OrderByExpression.Append(GetFieldsExpressionVisitor().VisitExpression(keySelector) + " ASC");
            return this;
        }

        /// <summary>Then by.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> ThenBy<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (Statement.OrderByExpression.Length > 0)
            {
                Statement.OrderByExpression.Append(",");
            }

            Statement.OrderByExpression.Append(GetFieldsExpressionVisitor().VisitExpression(keySelector) + " ASC");
            return this;
        }

        /// <summary>Order by descending.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (Statement.OrderByExpression.Length > 0)
            {
                Statement.OrderByExpression.Append(",");
            }

            Statement.OrderByExpression.Append(GetFieldsExpressionVisitor().VisitExpression(keySelector) + " DESC");
            return this;
        }

        /// <summary>Order by descending.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="quotedColumnName">The column names (quoted using the dialect provider)</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> OrderByDescendingColumn(String quotedColumnName)
        {
            if (Statement.OrderByExpression.Length > 0)
            {
                Statement.OrderByExpression.Append(",");
            }

            Statement.OrderByExpression.Append(quotedColumnName + " DESC");
            return this;
        }



        /// <summary>Then by descending.</summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (Statement.OrderByExpression.Length > 0)
            {
                Statement.OrderByExpression.Append(",");
            }

            Statement.OrderByExpression.Append(GetFieldsExpressionVisitor().VisitExpression(keySelector) + " DESC");
            return this;
        }


        /// <summary>Set the specified offset and rows for SQL Limit clause.</summary>
        /// <param name="skip">Offset of the first row to return. The offset of the initial row is 0.</param>
        /// <param name="rows">Number of rows returned by a SELECT statement.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> Limit(int skip, int rows)
        {
            Statement.MaxRows = rows;
            Statement.Offset = skip;
            return this;
        }

        /// <summary>Set the specified rows for Sql Limit clause.</summary>
        /// <param name="rows">Number of rows returned by a SELECT statement.</param>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> Limit(int rows)
        {
            Statement.MaxRows = rows;
            Statement.Offset = null;
            return this;
        }

        /// <summary>Clear Sql Limit clause.</summary>
        /// <returns>A SqlExpressionVisitor&lt;T&gt;</returns>
        public TypedSelectStatement<T> Limit()
        {
            Statement.Offset = null;
            Statement.MaxRows = null;
            return this;
        }
    }
}