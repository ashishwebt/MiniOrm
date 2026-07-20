using System.Reflection;
using Microsoft.Data.Sqlite;

namespace MiniOrm;

/// <summary>
/// Main entry point for the ORM (EF Core equivalent: DbContext)
/// Manages database connection, model configuration, and SaveChanges
/// </summary>
public abstract class DbContext : IDisposable
{
    private readonly ModelBuilder _modelBuilder = new();
    private SqliteConnection? _connection;
    private Dictionary<Type, object> _dbSets = new();
    private bool _disposed;

    public ChangeTracker ChangeTracker { get; } = new();
    public SqliteConnection Connection => _connection ?? throw new InvalidOperationException("Connection not initialized. Call EnsureCreated() first.");

    /// <summary>
    /// Override to configure connection and entity models
    /// EF Core equivalent: OnConfiguring + OnModelCreating
    /// </summary>
    protected abstract void OnConfiguring(DbContextOptionsBuilder optionsBuilder);
    protected abstract void OnModelCreating(ModelBuilder modelBuilder);

    /// <summary>
    /// Initialize the database and model
    /// </summary>
    public void EnsureCreated()
    {
        OnConfiguring(new DbContextOptionsBuilder(this));
        OnModelCreating(_modelBuilder);

        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        // Create tables for all configured entities
        foreach (var entityType in _modelBuilder.GetEntityTypes().Values)
        {
            CreateTable(entityType);
        }
    }

    private string _connectionString = "Data Source=:memory:";

    internal void SetConnectionString(string connectionString)
    {
        _connectionString = connectionString;
    }

    private void CreateTable(EntityType entityType)
    {
        var columns = entityType.Properties.Select(p =>
        {
            var sqlType = GetSqlType(p.PropertyType);
            var nullable = Nullable.GetUnderlyingType(p.PropertyType) != null || !p.PropertyType.IsValueType ? "NULL" : "NOT NULL";
            var primaryKey = p == entityType.KeyProperty ? "PRIMARY KEY" : "";
            return $"\"{p.Name}\" {sqlType} {primaryKey} {nullable}";
        });

        var sql = $"CREATE TABLE IF NOT EXISTS \"{entityType.TableName}\" ({string.Join(", ", columns)})";

        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string GetSqlType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying switch
        {
            Type t when t == typeof(int) => "INTEGER",
            Type t when t == typeof(long) => "INTEGER",
            Type t when t == typeof(string) => "TEXT",
            Type t when t == typeof(bool) => "INTEGER",
            Type t when t == typeof(DateTime) => "TEXT",
            Type t when t == typeof(double) => "REAL",
            Type t when t == typeof(float) => "REAL",
            Type t when t == typeof(decimal) => "REAL",
            Type t when t == typeof(Guid) => "TEXT",
            _ => "TEXT"
        };
    }

    /// <summary>
    /// Get or create a DbSet for an entity type
    /// Usage: context.Set<Blog>() or context.Blogs (via derived class property)
    /// </summary>
    public DbSet<T> Set<T>() where T : class
    {
        var type = typeof(T);
        if (!_dbSets.TryGetValue(type, out var dbSet))
        {
            var entityType = _modelBuilder.GetEntityTypes().GetValueOrDefault(type)
                ?? new EntityType(type); // Fallback: create entity type on-the-fly

            dbSet = new DbSet<T>(this, entityType);
            _dbSets[type] = dbSet;
        }
        return (DbSet<T>)dbSet;
    }

    /// <summary>
    /// Save all pending changes to database
    /// EF Core equivalent: SaveChanges / SaveChangesAsync
    /// </summary>
    public int SaveChanges()
    {
        // Step 1: Detect changes (AutoDetectChangesEnabled)
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker.Entries().ToList();
        int affected = 0;

        using var transaction = Connection.BeginTransaction();

        try
        {
            foreach (var entry in entries)
            {
                (string sql, List<(string name, object? value)> parameters)? result = entry.State switch
                {
                    EntityState.Added => GenerateInsert(entry),
                    EntityState.Modified => GenerateUpdate(entry),
                    EntityState.Deleted => GenerateDelete(entry),
                    _ => null
                };

                if (result.HasValue)
                {
                    var (sql, parameters) = result.Value;
                    using var command = Connection.CreateCommand();
                    command.CommandText = sql;
                    foreach (var param in parameters)
                    {
                        var p = command.CreateParameter();
                        p.ParameterName = param.name;
                        p.Value = param.value ?? DBNull.Value;
                        command.Parameters.Add(p);
                    }
                    affected += command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            ChangeTracker.AcceptAllChanges();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return affected;
    }

    private (string sql, List<(string name, object? value)> parameters) GenerateInsert(EntityEntry entry)
    {
        var props = entry.EntityType.Properties.Where(p => p != entry.EntityType.KeyProperty).ToList();
        var columns = string.Join(", ", props.Select(p => $"\"{p.Name}\""));
        var paramNames = string.Join(", ", props.Select(p => $"@{p.Name}"));

        var parameters = props.Select(p => (p.Name, p.GetValue(entry.Entity))).ToList();

        return (
            $"INSERT INTO \"{entry.EntityType.TableName}\" ({columns}) VALUES ({paramNames})",
            parameters
        );
    }

    private (string sql, List<(string name, object? value)> parameters) GenerateUpdate(EntityEntry entry)
    {
        var props = entry.EntityType.Properties.Where(p => p != entry.EntityType.KeyProperty).ToList();
        var setClauses = string.Join(", ", props.Select(p => $"\"{p.Name}\" = @{p.Name}"));
        var keyName = entry.EntityType.KeyProperty.Name;
        var keyValue = entry.EntityType.KeyProperty.GetValue(entry.Entity);

        var parameters = props.Select(p => (p.Name, p.GetValue(entry.Entity)))
            .Concat(new[] { (keyName, keyValue) })
            .ToList();

        return (
            $"UPDATE \"{entry.EntityType.TableName}\" SET {setClauses} WHERE \"{keyName}\" = @{keyName}",
            parameters
        );
    }

    private (string sql, List<(string name, object? value)> parameters) GenerateDelete(EntityEntry entry)
    {
        var keyName = entry.EntityType.KeyProperty.Name;
        var keyValue = entry.EntityType.KeyProperty.GetValue(entry.Entity);

        return (
            $"DELETE FROM \"{entry.EntityType.TableName}\" WHERE \"{keyName}\" = @{keyName}",
            new List<(string, object?)> { (keyName, keyValue) }
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Options builder for DbContext configuration
/// EF Core equivalent: DbContextOptionsBuilder
/// </summary>
public class DbContextOptionsBuilder
{
    private readonly DbContext _context;

    internal DbContextOptionsBuilder(DbContext context)
    {
        _context = context;
    }

    public void UseSqlite(string connectionString)
    {
        _context.SetConnectionString(connectionString);
    }
}
