using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.Data.Sqlite;

namespace MiniOrmDemo
{
    public class SqlTranslator : IExpressionTranslator
    {
        public (string Sql, List<DbParameter> Parameters) Translate<T>(
            Expression<Func<T, bool>> expression)
        {
            var parameters = new List<DbParameter>();
            string sql = Visit(expression.Body, parameters);
            return (sql, parameters);
        }

        private string Visit(Expression expr, List<DbParameter> ps)
        {
            switch (expr)
            {
                case UnaryExpression u when u.NodeType == ExpressionType.Convert:
                    return Visit(u.Operand, ps);

                case BinaryExpression b:
                    return VisitBinary(b, ps);

                case MethodCallExpression m:
                    return VisitMethodCall(m, ps);

                case MemberExpression me when IsParameterMember(me):
                    return me.Member.Name;

                default:
                    object value = Evaluate(expr);
                    return AddParam(value, ps);
            }
        }

        private static bool IsParameterMember(MemberExpression me) =>
            me.Expression is ParameterExpression;

        private string VisitBinary(BinaryExpression b, List<DbParameter> ps)
        {
            string op = b.NodeType switch
            {
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                _ => throw new NotSupportedException($"Operator '{b.NodeType}' is not supported.")
            };

            string left = Visit(b.Left, ps);
            string right = Visit(b.Right, ps);

            return op is "AND" or "OR" ? $"({left} {op} {right})" : $"{left} {op} {right}";
        }

        private string VisitMethodCall(MethodCallExpression m, List<DbParameter> ps)
        {
            if (m.Method.Name == nameof(string.StartsWith) && m.Object != null)
            {
                string column = Visit(m.Object, ps);
                var arg = Evaluate(m.Arguments[0])?.ToString() ?? "";
                string paramName = AddParam(arg + "%", ps);
                return $"{column} LIKE {paramName}";
            }

            throw new NotSupportedException($"Method '{m.Method.Name}' is not supported.");
        }

        private static object Evaluate(Expression expr)
        {
            var lambda = Expression.Lambda(expr);
            return lambda.Compile().DynamicInvoke();
        }

        private string AddParam(object value, List<DbParameter> ps)
        {
            string name = "@p" + ps.Count;
            var param = new SqliteParameter(name, value ?? DBNull.Value);
            ps.Add(param);
            return name;
        }
    }
}
