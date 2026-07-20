namespace MiniOrm;

/// <summary>
/// Tracks the state of a single entity (EF Core equivalent: InternalEntityEntry)
/// Stores current/original values for change detection
/// </summary>
public class EntityEntry
{
    public object Entity { get; internal set; }
    public EntityType EntityType { get; }
    public EntityState State { get; set; }
    public Dictionary<string, object?> OriginalValues { get; } = new();
    public Dictionary<string, object?> CurrentValues { get; } = new();

    public EntityEntry(object entity, EntityType entityType, EntityState state)
    {
        Entity = entity;
        EntityType = entityType;
        State = state;

        // Snapshot: capture all property values at tracking time
        foreach (var prop in entityType.Properties)
        {
            var value = prop.GetValue(entity);
            OriginalValues[prop.Name] = value;
            CurrentValues[prop.Name] = value;
        }
    }

    /// <summary>
    /// Detect changes by comparing current values to original snapshot
    /// </summary>
    public void DetectChanges()
    {
        if (State == EntityState.Added) return; // New entities don't need change detection

        bool hasChanges = false;
        foreach (var prop in EntityType.Properties)
        {
            var currentValue = prop.GetValue(Entity);
            var originalValue = OriginalValues[prop.Name];

            if (!Equals(currentValue, originalValue))
            {
                CurrentValues[prop.Name] = currentValue;
                hasChanges = true;
            }
        }

        if (hasChanges && State == EntityState.Unchanged)
        {
            State = EntityState.Modified;
        }
    }

    /// <summary>
    /// After SaveChanges, update original values to match current
    /// </summary>
    public void AcceptChanges()
    {
        foreach (var prop in EntityType.Properties)
        {
            OriginalValues[prop.Name] = prop.GetValue(Entity);
        }

        if (State == EntityState.Added)
        {
            State = EntityState.Unchanged;
        }
        else if (State == EntityState.Modified)
        {
            State = EntityState.Unchanged;
        }
        else if (State == EntityState.Deleted)
        {
            // Entity is removed from tracking after delete
        }
    }
}

public enum EntityState
{
    Unchanged,
    Added,
    Modified,
    Deleted
}
