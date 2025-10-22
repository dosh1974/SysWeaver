using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SimpleStack.Orm.Expressions.Statements;

namespace SimpleStack.Orm.Expressions
{
    internal sealed class TableWFieldsExpresionVisitor<T> : ExpressionVisitor
    {
        private readonly bool _addAliasSpecification;
        private readonly ModelDefinition _modelDefinition;

        public TableWFieldsExpresionVisitor(IDialectProvider dialectProvider,
            StatementParameters parameters,
            ModelDefinition modelDefinition,
            bool addAliasSpecification)
            : base(dialectProvider, parameters)
        {
            _modelDefinition = modelDefinition;
            _addAliasSpecification = addAliasSpecification;
        }

        public bool IsGroupBy;

        public override string VisitExpression(Expression exp)
        {
            var statement = Visit(exp);
            return statement.ToString();
        }

        /// <inheritdoc />
        protected override StatementPart VisitParameter(ParameterExpression parameterExpression)
        {
            var fn = _modelDefinition.FieldDefinitions.First(
                x => x.Name.ToLower() == parameterExpression.Name.ToLower());

            return new ColumnAccessPart(DialectProvider.GetQuotedColumnName(parameterExpression.Name), fn.FieldType);
        }

        protected override StatementPart VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.DeclaringType == typeof(Sql))
            {
                return VisitSqlMethodCall(m);
            }

            if (IsIEnumerableContainsMethod(m))
            {
                return VisitArrayMethodCall(m);
            }

            if (IsColumnAccess(m))
            {
                return VisitColumnAccessMethod(m);
            }

            var value = Expression.Lambda(m).Compile().DynamicInvoke();
            if (value == null)
            {
                return null;
            }

            return AddParameter(value);
        }

        protected override StatementPart VisitMemberAccess(MemberExpression m)
        {
            if (m.Member.DeclaringType == typeof(DateTime))
            {
                return VisitDateTimeMemberAccess(m);
            }

            if (m.Expression != null && (m.Expression.NodeType == ExpressionType.Parameter ||
                                         m.Expression.NodeType == ExpressionType.Convert))
            {
                var propertyInfo = m.Member as PropertyInfo;

                return new ColumnAccessPart(GetQuotedColumnName(m.Member.Name), propertyInfo.PropertyType);
            }

            var r = Expression.Lambda(m).Compile().DynamicInvoke();
            if (r != null)
            {
                return AddParameter(r);
            }

            return null;
        }


        static readonly String[] SplitAs = new String[] { " AS " };

        private StatementPart VisitSqlMethodCall(MethodCallExpression m)
        {
            var args = VisitSqlParameters(m.Arguments);
            var quotedColName = args.Dequeue().ToString();

            quotedColName = quotedColName.Split(SplitAs, StringSplitOptions.None)[0];

            string statement;

            switch (m.Method.Name)
            {
                case "As":
                    {
                        var a = args.Dequeue();
                        //var name = a.ToString();
                        var p = this.Parameters[a.ToString()];
                        var name = p.Value.ToString();
                        statement = $"{quotedColName} As {DialectProvider.GetQuotedColumnName(name)}";
                        return new StatementPart(statement);
                    }
                case "Sum":
                case "Count":
                case "Min":
                case "Max":
                case "Avg":
                case nameof(Sql.IsNull):
                    statement =
                        $"{m.Method.Name}({quotedColName}{(args.Count == 1 ? $",{args.Dequeue()}" : string.Empty)})";
                    break;
                case nameof(Sql.If):
                    statement =
                        $"{m.Method.Name}({quotedColName},{$"{args.Dequeue()}"},{$"{args.Dequeue()}"})";
                    break;
                default:
                    throw new NotSupportedException();
            }
            if ((!IsGroupBy) && _addAliasSpecification)
                statement = String.Join(" AS ", statement, quotedColName);
            return new StatementPart(statement);
        }

        protected override bool IsColumnAccess(MethodCallExpression m)
        {
            if (m.Object is MethodCallExpression)
            {
                return IsColumnAccess((MethodCallExpression) m.Object);
            }

            var exp = m.Object as MemberExpression;
            return exp?.Expression != null && exp.Expression.Type == typeof(T) &&
                   exp.Expression.NodeType == ExpressionType.Parameter;
        }

        private Queue<StatementPart> VisitSqlParameters(ReadOnlyCollection<Expression> parameters)
        {
            var list = new Queue<StatementPart>();
            foreach (var e in parameters)
            {
                switch (e.NodeType)
                {
                    case ExpressionType.NewArrayInit:
                    case ExpressionType.NewArrayBounds:
                        foreach (var p in VisitNewArrayFromExpressionList(e as NewArrayExpression))
                        {
                            list.Enqueue(p);
                        }

                        break;
                    case ExpressionType.MemberAccess:
                        var m = e as MemberExpression;
                        list.Enqueue(new ColumnAccessPart(m.Member.Name, m.Member.DeclaringType));
                        break;
                    default:
                        list.Enqueue(Visit(e));
                        break;
                }
            }

            return list;
        }

        private string GetQuotedColumnName(string memberName)
        {
            var fd = _modelDefinition.FieldDefinitions.First(x => x.Name.ToLower() == memberName.ToLower());
            var fn = fd?.FieldName ?? memberName;

            var operand = fd.IsComputed ? fd.ComputeExpression : DialectProvider.GetQuotedColumnName(fn);

            if (_addAliasSpecification && fn != fd.Name)
            {
                return operand + " AS " + fd.Name;
            }

            return operand;
        }

        protected override StatementPart VisitLambda(LambdaExpression lambdaExpression)
        {
            return Visit(lambdaExpression.Body);
        }

        protected override StatementPart VisitNew(NewExpression newExpression)
        {
            var exprs = VisitExpressionList(newExpression.Arguments);
            return new StatementPart(exprs.Select(x => x.Text).Aggregate((x, y) => x + "," + y));
        }

        protected override StatementPart VisitMemberInit(MemberInitExpression memberInitExpression)
        {
            var exprs = VisitExpressionList(memberInitExpression.Bindings
                .Where(x => x.BindingType == MemberBindingType.Assignment)
                .Select(x => ((MemberAssignment) x).Expression));
            return new StatementPart(exprs.Select(x => x.Text).Aggregate((x, y) => x + "," + y));
        }
    }
}