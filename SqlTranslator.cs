using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.Data.Sqlite;

namespace MiniOrmDemo
{
    // =====================================================================
    // 1. SqlTranslator
    //    Walks a LINQ expression tree and produces a parameterized SQL
    //    WHERE clause. This is a tiny version of what EF Core's query
    //    compiler does when it turns your lambda into a SQL predicate.
    // =====================================================================
    public static class SqlTranslator
    {
        /// <summary>
        /// Entry point: translate p => p.Id == 1 (etc.) into SQL text + params.
        /// </summary>
        public static (string Sql, List<SqliteParameter> Parameters) Translate<T>(
            Expression<Func<T, bool>> predicate)
        {
            var parameters = new List<SqliteParameter>();
            string sql = Visit(predicate.Body, parameters);
            return (sql, parameters);
        }

        // Recursively visits nodes of the expression tree, similar to how
        // EF Core's ExpressionVisitor walks the tree node-by-node.
        private static string Visit(Expression expr, List<SqliteParameter> ps)
        {
            switch (expr)
            {
                // Strip harmless boxing/casts, e.g. (object)p.Age
                case UnaryExpression u when u.NodeType == ExpressionType.Convert:
                    return Visit(u.Operand, ps);

                // Binary operators: ==, !=, >, <, >=, <=, &&, ||
                case BinaryExpression b:
                    return VisitBinary(b, ps);

                // Method calls we support, e.g. p.Name.StartsWith("A")
                case MethodCallExpression m:
                    return VisitMethodCall(m, ps);

                // Access to a property of the lambda parameter (p.Name) => column name
                case MemberExpression me when IsParameterMember(me):
                    return me.Member.Name;

                // Anything else (constants, captured variables/closures, nested
                // member access like a local `filter.Name`) gets evaluated to an
                // actual value and turned into a SQL parameter.
                default:
                    object value = Evaluate(expr);
                    return AddParam(value, ps);
            }
        }

        // True if the member access ultimately roots at the lambda parameter,
        // e.g. p.Name -> true, someLocalObj.Name -> false (that's a closure).
        private static bool IsParameterMember(MemberExpression me) =>
            me.Expression is ParameterExpression;

        private static string VisitBinary(BinaryExpression b, List<SqliteParameter> ps)
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

            // Wrap boolean combinators in parens to preserve precedence.
            return op is "AND" or "OR" ? $"({left} {op} {right})" : $"{left} {op} {right}";
        }

        private static string VisitMethodCall(MethodCallExpression m, List<SqliteParameter> ps)
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

        // Compiles and executes an arbitrary sub-expression to get its runtime
        // value. This is how we resolve constants AND closures (captured
        // local variables) without hand-writing a case for each of them.
        private static object Evaluate(Expression expr)
        {
            var lambda = Expression.Lambda(expr);
            return lambda.Compile().DynamicInvoke();
        }

        private static string AddParam(object value, List<SqliteParameter> ps)
        {
            string name = "@p" + ps.Count;
            ps.Add(new SqliteParameter(name, value ?? DBNull.Value));
            return name;
        }
    }
}
