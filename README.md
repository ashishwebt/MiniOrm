# MiniOrm
[日本語](./README.ja.md)

> **This is not a production ORM.** MiniOrm is an educational, from-scratch reimplementation of Entity Framework Core's core internals, built to show *how EF Core actually works under the hood* — `DbContext`, `DbSet<T>`, the `ChangeTracker`, LINQ-to-SQL translation, and `SaveChanges` — in plain, readable C# with no hidden machinery.

If you've ever used EF Core and wondered "what actually happens when I call `SaveChanges()`?" or "how does `.Where(x => x.Name.Contains(\"foo\"))` turn into a SQL query?" — this repo answers that by implementing a tiny, working version of it yourself.

## Why this exists

EF Core's real source is large, heavily abstracted, and hard to trace through as a learning exercise. MiniOrm strips the same architecture down to ~500 lines across a handful of files, each one deliberately mirroring a real EF Core component so you can read the code and see the pattern directly, without wading through the full framework.

| MiniOrm type | EF Core equivalent | What it teaches |
|---|---|---|
| `DbContext` | `DbContext` | Unit of Work — owns the connection, the model, and coordinates `SaveChanges` |
| `DbSet<T>` | `DbSet<T>` | Repository + `IQueryable<T>` — the entry point for querying and mutating a table |
| `ChangeTracker` | `ChangeTracker` | The Identity Map — one tracked instance per entity, keyed by type + primary key |
| `EntityEntry` | `InternalEntityEntry` | Per-entity state machine (`Added` / `Unchanged` / `Modified` / `Deleted`) and change detection via snapshot comparison |
| `EntityType` / `ModelBuilder` | `IEntityType` / `ModelBuilder` | Convention-based mapping of a CLR class to a table (e.g. `Id` → primary key, `Blog` → `Blogs`) |
| `QueryTranslator` | `RelationalSqlTranslatingExpressionVisitor` | Walks a LINQ **expression tree** and emits parameterized SQL — the "magic" behind `context.Blogs.Where(...)` |

## What it actually demonstrates

Running the demo walks through the full lifecycle EF Core hides behind a few method calls:

1. **Model building** — `OnModelCreating` maps `Blog` to a `Blogs` table; `EnsureCreated()` issues the `CREATE TABLE` from reflected property metadata.
2. **Change tracking** — calling `context.Blogs.Add(...)` doesn't touch the database. It just registers an `EntityEntry` in the `ChangeTracker` with state `Added`.
3. **LINQ → SQL translation** — `context.Blogs.Where(b => b.Title.Contains("EF"))` builds an expression tree that `QueryTranslator` visits node-by-node to produce `SELECT * FROM "Blogs" WHERE "Title" LIKE '%EF%'`.
4. **Identity Map** — entities returned from a query are tracked as `Unchanged`; querying the same row twice returns the *same* object reference.
5. **Snapshot-based change detection** — mutating a tracked entity's property doesn't do anything by itself. `DetectChanges()` compares current values against an original snapshot to flip the state to `Modified` — exactly what EF Core does automatically before every `SaveChanges()`.
6. **SaveChanges** — a single transaction that walks every tracked entry and generates the matching `INSERT` / `UPDATE` / `DELETE`, then calls `AcceptAllChanges()` to reset tracking state.

The demo prints the `ChangeTracker` state at every step so you can watch entities move through `Added → Unchanged → Modified → (removed)` in real time.

## Project structure

```
MiniOrm/
├── src/
│   ├── MiniOrm/                  # The mini ORM library
│   │   ├── DbContext.cs          # Unit of Work: connection, model, SaveChanges
│   │   ├── DbSet.cs              # IQueryable<T> entry point (Add/Remove/query)
│   │   ├── DbSetQueryProvider.cs # Executes translated SQL, materializes entities
│   │   ├── QueryTranslator.cs    # Expression tree → SQL visitor
│   │   ├── ChangeTracker.cs      # Identity Map of all tracked entities
│   │   ├── EntityEntry.cs        # Per-entity state + snapshot change detection
│   │   ├── EntityType.cs         # Reflection-based CLR type → table mapping
│   │   └── ModelBuilder.cs       # Fluent API: modelBuilder.Entity<T>().ToTable(...)
│   └── MiniOrm.Demo/
│       └── Program.cs            # Step-by-step walkthrough (see below)
├── Directory.Build.props/targets # Shared MSBuild settings
├── global.json                   # Pinned .NET SDK version
├── MiniOrm.sln
└── LICENSE
```

## Getting started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) (version pinned in `global.json`)

### Clone and run the demo

```bash
git clone https://github.com/ashishwebt/MiniOrm.git
cd MiniOrm
dotnet run --project src/MiniOrm.Demo
```

This builds a local SQLite database (`minidemo.db`) and prints each step of the EF Core lifecycle described above straight to the console — no setup required.

### Build only

```bash
dotnet restore
dotnet build
```

## Minimal usage example

```csharp
using MiniOrm;

public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class BlogContext : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=minidemo.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Blog>().ToTable("Blogs");
}

using var context = new BlogContext();
context.EnsureCreated();

context.Blogs.Add(new Blog { Id = 1, Title = "Hello MiniOrm" });
context.SaveChanges();

var post = context.Blogs.First(b => b.Title.Contains("Hello"));
```

## Scope and limitations

This is intentionally minimal, not feature-complete. It does **not** implement (and isn't trying to): migrations, relationships/navigation properties, lazy loading, async query execution, connection pooling, or query caching. Each of those is a real, much larger topic in EF Core's actual source — MiniOrm only covers the core request/response loop of a change-tracking ORM.

## Contributing

Contributions that improve the teaching value of the code (clearer comments, additional annotated concepts, more demo scenarios) are welcome.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes
4. Push to the branch and open a Pull Request

## License

MIT — see [LICENSE](LICENSE).
