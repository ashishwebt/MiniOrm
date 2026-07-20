using System.Reflection;

namespace MiniOrm;

/// <summary>
/// Fluent API for configuring entities (EF Core equivalent: ModelBuilder)
/// Usage: modelBuilder.Entity<Blog>().ToTable("Blogs");
/// </summary>
public class ModelBuilder
{
    private readonly Dictionary<Type, EntityType> _entityTypes = new();

    public EntityTypeBuilder<T> Entity<T>() where T : class
    {
        var type = typeof(T);
        if (!_entityTypes.TryGetValue(type, out var entityType))
        {
            entityType = new EntityType(type);
            _entityTypes[type] = entityType;
        }
        return new EntityTypeBuilder<T>(entityType);
    }

    internal IReadOnlyDictionary<Type, EntityType> GetEntityTypes() => _entityTypes;
}

/// <summary>
/// Fluent configuration for a specific entity type
/// EF Core equivalent: EntityTypeBuilder<T>
/// </summary>
public class EntityTypeBuilder<T> where T : class
{
    private readonly EntityType _entityType;

    internal EntityTypeBuilder(EntityType entityType)
    {
        _entityType = entityType;
    }

    public EntityTypeBuilder<T> ToTable(string tableName)
    {
        _entityType.TableName = tableName;
        return this;
    }
}
