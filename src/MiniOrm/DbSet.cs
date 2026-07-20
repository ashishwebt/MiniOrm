using System.Linq.Expressions;

namespace MiniOrm;

/// <summary>
/// Represents a collection of entities that can be queried/modified
/// EF Core equivalent: DbSet<T>
/// Implements IQueryable to support LINQ queries
/// </summary>
public class DbSet<T> : IQueryable<T>, IAsyncEnumerable<T>
{
    private readonly DbContext _context;
    private readonly EntityType _entityType;
    public Expression Expression { get; set; }

    internal DbSet(DbContext context, EntityType entityType)
    {
        _context = context;
        _entityType = entityType;
        Expression = Expression.Constant(this);
    }

    public void Add(T entity)
    {
        _context.ChangeTracker.Track(entity!, _entityType, EntityState.Added);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            Add(entity);
        }
    }

    public void Remove(T entity)
    {
        var entry = _context.ChangeTracker.Entry(entity!);
        if (entry != null)
        {
            entry.State = EntityState.Deleted;
        }
        else
        {
            // Entity reference may differ from tracked instance (Identity Map)
            // Track as deleted - Track will find existing entry by key and mark it
            _context.ChangeTracker.Track(entity!, _entityType, EntityState.Deleted);
        }
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            Remove(entity);
        }
    }

    // IQueryable implementation - allows LINQ to work
    public Type ElementType => typeof(T);
    public IQueryProvider Provider => new DbSetQueryProvider<T>(_context, _entityType);

    public IEnumerator<T> GetEnumerator()
    {
        var results = Provider.Execute<IEnumerable<T>>(Expression);
        return results.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Async not supported in this demo - see QueryTranslator for the concept");
    }
}
