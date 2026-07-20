namespace MiniOrm;

/// <summary>
/// Manages all tracked entities (EF Core equivalent: ChangeTracker)
/// Implements Identity Map pattern: one instance per entity
/// </summary>
public class ChangeTracker
{
    private readonly Dictionary<(Type Type, object Id), EntityEntry> _entries = new();

    public IReadOnlyCollection<EntityEntry> Entries() => _entries.Values.ToList();

    public void Track(object entity, EntityType entityType, EntityState state)
    {
        var keyProp = entityType.KeyProperty;
        var keyValue = keyProp.GetValue(entity);

        var key = (entityType.ClrType, keyValue!);

        if (_entries.TryGetValue(key, out var existing))
        {
            // Already tracked (Identity Map) - update entity reference for queried results
            if (state == EntityState.Unchanged)
            {
                existing.Entity = entity;
                foreach (var prop in entityType.Properties)
                {
                    existing.CurrentValues[prop.Name] = prop.GetValue(entity);
                }
            }
            else if (state == EntityState.Deleted)
            {
                existing.State = EntityState.Deleted;
            }
            return;
        }

        _entries[key] = new EntityEntry(entity, entityType, state);
    }

    public EntityEntry? Entry(object entity)
    {
        return _entries.Values.FirstOrDefault(e => e.Entity == entity);
    }

    public EntityEntry? Entry<T>() where T : class
    {
        return _entries.Values.FirstOrDefault(e => e.Entity.GetType() == typeof(T));
    }

    /// <summary>
    /// Run change detection on all tracked entities
    /// EF Core calls this automatically before SaveChanges (AutoDetectChanges)
    /// </summary>
    public void DetectChanges()
    {
        foreach (var entry in _entries.Values)
        {
            entry.DetectChanges();
        }
    }

    /// <summary>
    /// After successful SaveChanges, accept all changes
    /// </summary>
    public void AcceptAllChanges()
    {
        var toRemove = _entries.Values
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in toRemove)
        {
            var key = (entry.EntityType.ClrType, entry.EntityType.KeyProperty.GetValue(entry.Entity)!);
            _entries.Remove(key);
        }

        foreach (var entry in _entries.Values)
        {
            entry.AcceptChanges();
        }
    }

    internal void Remove(object entity)
    {
        var entry = _entries.Values.FirstOrDefault(e => e.Entity == entity);
        if (entry != null)
        {
            var key = (entry.EntityType.ClrType, entry.EntityType.KeyProperty.GetValue(entry.Entity)!);
            _entries.Remove(key);
        }
    }
}
