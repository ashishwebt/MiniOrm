using System.Linq.Expressions;
using System.Reflection;

namespace MiniOrm;

/// <summary>
/// Translates LINQ expression trees to SQL queries
/// EF Core equivalent: RelationalSqlTranslatingExpressionVisitor
/// This is one of the most complex parts of EF Core - we're keeping it simple
/// </summary>
public class QueryTranslator : ExpressionVisitor
{
    private readonly DbContext _context;
    private readonly EntityType _entityType;
    private readonly List<(string name, object? value)> _parameters = new();
    private int _paramCounter;
    private readonly System.Text.StringBuilder _sql = new();

    public QueryTranslator(DbContext context, EntityType entityType)
    {
        _context = context;
        _entityType = entityType;
    }

    public (string sql, List<(string name, object? value)> parameters) Translate(Expression expression)
    {
        _sql.Clear();
        _parameters.Clear();
        _paramCounter = 0;

        // Build: SELECT * FROM TableName
        _sql.Append($"SELECT * FROM \"{_entityType.TableName}\"");

        // Visit the expression tree to add WHERE, ORDER BY, etc.
        Visit(expression);

        return (_sql.ToString(), _parameters);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle LINQ methods: Where, OrderBy, Select, FirstOrDefault, etc.
        switch (node.Method.Name)
        {
            case "Where":
                VisitWhere(node);
                break;
            case "OrderBy":
                VisitOrderBy(node, ascending: true);
                break;
            case "OrderByDescending":
                VisitOrderBy(node, ascending: false);
                break;
            case "Take":
                VisitTake(node);
                break;
            case "FirstOrDefault":
            case "First":
                // Handle First(predicate) - predicate goes in WHERE
                if (node.Arguments.Count > 1 && node.Arguments[1] is UnaryExpression unary && unary.Operand is LambdaExpression firstLambda)
                {
                    _sql.Append(" WHERE ");
                    Visit(firstLambda.Body);
                }
                _sql.Append(" LIMIT 1");
                break;
            case "Count":
                // Replace SELECT * with SELECT COUNT(*)
                _sql.Replace("SELECT *", "SELECT COUNT(*)");
                break;
            case "Contains":
            case "StartsWith":
            case "EndsWith":
                // Handle string methods
                VisitStringMethod(node);
                return node;
        }

        // Continue visiting the inner expression
        if (node.Arguments.Count > 0)
        {
            Visit(node.Arguments[0]);
        }

        return node;
    }

    private void VisitWhere(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        var predicate = node.Arguments[1];

        // Extract lambda expression
        if (predicate is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
        {
            _sql.Append(" WHERE ");
            Visit(lambda.Body);
        }
    }

    private void VisitOrderBy(MethodCallExpression node, bool ascending)
    {
        if (node.Arguments.Count < 2) return;

        var keySelector = node.Arguments[1];

        if (keySelector is UnaryExpression unary && unary.Operand is LambdaExpression lambda)
        {
            // Check if we already have ORDER BY
            if (_sql.ToString().Contains("ORDER BY"))
            {
                _sql.Append(", ");
            }
            else
            {
                _sql.Append(" ORDER BY ");
            }

            Visit(lambda.Body);
            _sql.Append(ascending ? " ASC" : " DESC");
        }
    }

    private void VisitTake(MethodCallExpression node)
    {
        if (node.Arguments.Count < 2) return;

        if (node.Arguments[1] is ConstantExpression constant)
        {
            _sql.Append($" LIMIT {constant.Value}");
        }
    }

    private void VisitStringMethod(MethodCallExpression node)
    {
        // Visit the object (e.g., x.Name)
        if (node.Object != null)
        {
            Visit(node.Object);
        }

        // Get the argument value
        var argValue = EvaluateExpression(node.Arguments[0]);

        switch (node.Method.Name)
        {
            case "Contains":
                _sql.Append($" LIKE '%{argValue}%'");
                break;
            case "StartsWith":
                _sql.Append($" LIKE '{argValue}%'");
                break;
            case "EndsWith":
                _sql.Append($" LIKE '%{argValue}'");
                break;
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Handle comparisons: ==, !=, <, >, <=, >=
        Visit(node.Left);

        var op = node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " != ",
            ExpressionType.LessThan => " < ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            _ => throw new NotSupportedException($"Binary operator {node.NodeType} not supported")
        };

        _sql.Append(op);

        Visit(node.Right);

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // Skip the root IQueryable constant (the DbSet itself)
        if (node.Value is IQueryable)
        {
            return node;
        }

        // Parameterize constants to prevent SQL injection
        if (node.Value != null && node.Type != typeof(string))
        {
            var paramName = $"@p{_paramCounter++}";
            _sql.Append(paramName);
            _parameters.Add((paramName, node.Value));
        }
        else if (node.Value is string strValue)
        {
            // String constants can be inlined for LIKE patterns
            _sql.Append($"'{strValue}'");
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle property access: x.Name, x.Id, etc.
        if (node.Member is PropertyInfo prop)
        {
            _sql.Append($"\"{prop.Name}\"");
        }
        return node;
    }

    private object? EvaluateExpression(Expression expression)
    {
        // Evaluate constant expressions
        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        // For complex expressions, we'd need to compile and execute
        // This is simplified - real EF Core uses a complex evaluation pipeline
        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }
}
