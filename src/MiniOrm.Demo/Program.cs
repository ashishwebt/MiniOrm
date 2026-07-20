using MiniOrm;

// =============================================================================
// MiniOrm Demo - Teaching How EF Core Works
// =============================================================================
// This demo shows the core concepts of Entity Framework Core:
// 1. DbContext - Unit of Work pattern
// 2. DbSet<T> - Repository pattern
// 3. Change Tracking - Detecting what changed
// 4. LINQ to SQL - Translating queries
// 5. SaveChanges - Persisting to database
// =============================================================================

Console.WriteLine("MiniOrm Demo - How EF Core Works\n");

// STEP 1: Create your DbContext (Unit of Work)
using var context = new BlogContext();
context.EnsureCreated();

Console.WriteLine("=== Adding new blogs ===");
// STEP 2: Add entities to DbSet (Repository)
// This marks them as "Added" in the ChangeTracker
context.Blogs.Add(new Blog { Id = 1, Title = "EF Core Basics", Content = "Learning ORM" });
context.Blogs.Add(new Blog { Id = 2, Title = "Advanced EF Core", Content = "Change tracking deep dive" });

Console.WriteLine($"ChangeTracker entries: {context.ChangeTracker.Entries().Count}");
foreach (var entry in context.ChangeTracker.Entries())
{
    Console.WriteLine($"  - {entry.EntityType.ClrType.Name}: {entry.State}");
}

// STEP 3: SaveChanges generates and executes SQL
// It batches all pending changes into a transaction
Console.WriteLine("\n=== Saving changes ===");
int affected = context.SaveChanges();
Console.WriteLine($"Rows affected: {affected}");

Console.WriteLine("\n=== Querying with LINQ ===");
// STEP 4: LINQ queries get translated to SQL
var allBlogs = context.Blogs.ToList();
Console.WriteLine($"All blogs: {allBlogs.Count}");

// WHERE clause
var efBlogs = context.Blogs
    .Where(b => b.Title.Contains("EF"))
    .ToList();
Console.WriteLine($"Blogs with 'EF' in title: {efBlogs.Count}");

Console.WriteLine("\n=== Change Tracking in Action ===");
// STEP 5: Modify an entity from query results
// Identity Map: queried entities are tracked as Unchanged
// Modifying a tracked entity's property marks it as Modified (after DetectChanges)
var firstBlog = context.Blogs.First(); // Tracked as Unchanged
Console.WriteLine($"Before modify - State: {context.ChangeTracker.Entry(firstBlog)?.State}");

firstBlog.Title = "EF Core Basics (Updated)"; // Property changed!

// EF Core calls DetectChanges automatically before SaveChanges
// In our demo, we call it manually to show the concept
context.ChangeTracker.DetectChanges();
Console.WriteLine($"After DetectChanges - State: {context.ChangeTracker.Entry(firstBlog)?.State}");

context.SaveChanges();
Console.WriteLine($"After SaveChanges - State: {context.ChangeTracker.Entry(firstBlog)?.State}");

Console.WriteLine("\n=== Deleting an entity ===");
// STEP 6: Remove marks entity as "Deleted"
var blogToDelete = context.Blogs.First(b => b.Id == 2);
context.Blogs.Remove(blogToDelete);
Console.WriteLine($"After Remove - State: {context.ChangeTracker.Entry(blogToDelete)?.State}");
context.SaveChanges();

Console.WriteLine("\n=== Final state ===");
var remaining = context.Blogs.ToList();
Console.WriteLine($"Blogs remaining: {remaining.Count}");
foreach (var blog in remaining)
{
    Console.WriteLine($"  - {blog.Title}");
}

// =============================================================================
// Entity Definition
// =============================================================================
public class Blog
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

// =============================================================================
// DbContext Definition
// =============================================================================
public class BlogContext : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=minidemo.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>()
            .ToTable("Blogs");
    }
}
