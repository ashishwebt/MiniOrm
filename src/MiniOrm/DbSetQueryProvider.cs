using System.Linq.Expressions;

namespace MiniOrm;

/// <summary>
/// Custom query provider that translates expressions to SQL
/// EF Core equivalent: EntityQueryProvider + IAsyncQueryProvider
/// This is where the magic happens: LINQ → Expression Tree → SQL
/// </summary>
internal class DbSetQueryProvider<T> : IQueryProvider
{
    private readonly DbContext _context;
    private readonly EntityType _entityType;

    public DbSetQueryProvider(DbContext context, EntityType entityType)
    {
        _context = context;
        _entityType = entityType;
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? typeof(T);
        var queryType = typeof(DbSet<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryType, _context, _entityType)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        // Create a new DbSet<TElement> with the given expression
        var set = new DbSet<TElement>(_context, _entityType);
        set.Expression = expression;
        return set;
    }

    public object? Execute(Expression expression)
    {
        return Execute<T>(expression);
    }

    public TResult Execute<TResult>(Expression expression)
    {
        // STEP 1: Translate expression tree to SQL
        var translator = new QueryTranslator(_context, _entityType);
        var (sql, parameters) = translator.Translate(expression);

        // STEP 2: Execute SQL against database
        using var command = _context.Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            command.Parameters.Add(param);
        }

        // STEP 3: Materialize results into objects
        var resultList = new List<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var entity = (T)Activator.CreateInstance(_entityType.ClrType)!;
            foreach (var prop in _entityType.Properties)
            {
                var ordinal = reader.GetOrdinal(prop.Name);
                if (!reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (value.GetType() != targetType)
                    {
                        value = Convert.ChangeType(value, targetType);
                    }
                    prop.SetValue(entity, value);
                }
            }
            resultList.Add(entity);
        }

        // STEP 4: Track queried entities (Identity Map pattern)
        foreach (var entity in resultList)
        {
            _context.ChangeTracker.Track(entity!, _entityType, EntityState.Unchanged);
        }

        // Return appropriate result type
        if (typeof(TResult) == typeof(List<T>))
        {
            return (TResult)(object)resultList;
        }
        if (typeof(TResult) == typeof(IEnumerable<T>))
        {
            return (TResult)(object)resultList;
        }

        // Single result (FirstOrDefault, etc.)
        return resultList.FirstOrDefault() is T item
            ? (TResult)(object)item
            : default!;
    }
}
