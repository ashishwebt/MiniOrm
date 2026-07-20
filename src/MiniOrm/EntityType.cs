using System.Reflection;

namespace MiniOrm;

/// <summary>
/// Stores metadata about an entity type (EF Core equivalent: EntityType)
/// Maps a CLR type to a database table with column information
/// </summary>
public class EntityType
{
    public Type ClrType { get; }
    public string TableName { get; set; }
    public PropertyInfo KeyProperty { get; set; }
    public List<PropertyInfo> Properties { get; } = new();

    public EntityType(Type clrType)
    {
        ClrType = clrType;
        TableName = clrType.Name + "s"; // Simple pluralization convention

        // Convention: Find "Id" property as key, or first property
        var props = clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToList();

        KeyProperty = props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            ?? props.First();

        Properties = props;
    }
}
