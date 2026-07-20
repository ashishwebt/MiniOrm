# MiniOrm

A lightweight ORM inspired by Entity Framework Core, built to teach how EF Core works internally.

## Projects

| Project | Type | Description |
|---------|------|-------------|
| `MiniOrm` | Library | Core ORM: DbContext, DbSet, ChangeTracker, LINQ to SQL translation |
| `MiniOrm.Demo` | Console | Demo app showing all ORM features in action |

## Building

```bash
dotnet build MiniOrm.sln
```

## Running the Demo

```bash
dotnet run --project src/MiniOrm.Demo
```

## Features

- **DbContext** - Unit of Work pattern with `SaveChanges()` and transaction support
- **DbSet\<T\>** - Repository pattern with `Add`, `Remove`, and `IQueryable` support
- **Change Tracking** - Snapshot-based change detection with Identity Map pattern
- **LINQ to SQL** - Expression tree translation for `Where`, `OrderBy`, `Take`, `First`, string methods
- **Fluent API** - `ModelBuilder` for entity configuration (`ToTable`, etc.)
- **SQLite** - Database provider via `Microsoft.Data.Sqlite`

## Quick Start

```csharp
using MiniOrm;

// Define your entity
public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

// Define your context
public class BlogContext : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=app.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>().ToTable("Blogs");
    }
}

// Use it
using var context = new BlogContext();
context.EnsureCreated();

context.Blogs.Add(new Blog { Id = 1, Title = "Hello World" });
context.SaveChanges();

var blogs = context.Blogs.Where(b => b.Title.Contains("Hello")).ToList();
```

## How It Maps to EF Core

| MiniOrm | EF Core |
|---------|---------|
| `DbContext` | `DbContext` |
| `DbSet<T>` | `DbSet<T>` |
| `ChangeTracker` | `ChangeTracker` |
| `EntityEntry` | `InternalEntityEntry` |
| `EntityType` | `EntityType` |
| `ModelBuilder` | `ModelBuilder` |
| `QueryTranslator` | `RelationalSqlTranslatingExpressionVisitor` |
| `DbSetQueryProvider<T>` | `EntityQueryProvider` |
