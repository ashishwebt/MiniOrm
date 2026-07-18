using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace MiniOrmDemo
{
    public static class Program
    {
        public static void Main()
        {
            using var conn = new SqliteConnection("Data Source=demo.db");
            conn.Open();

            // Fresh table each run, so the demo is repeatable.
            using (var setup = conn.CreateCommand())
            {
                setup.CommandText = @"
                    DROP TABLE IF EXISTS Person;
                    CREATE TABLE Person (
                        Id   INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Age  INTEGER NOT NULL
                    );";
                setup.ExecuteNonQuery();
            }

            var orm = new MiniOrm<Person>(conn);

            Console.WriteLine("=== INSERT ===");
            orm.Insert(new Person { Name = "Alice", Age = 30 });
            orm.Insert(new Person { Name = "Bob", Age = 25 });
            orm.Insert(new Person { Name = "Amanda", Age = 40 });

            Console.WriteLine("\n=== SELECT: p => p.Id == 1 ===");
            var byId = orm.Select(p => p.Id == 1);
            Print(byId);

            Console.WriteLine("\n=== SELECT: p => p.Name.StartsWith(\"A\") ===");
            var startsWithA = orm.Select(p => p.Name.StartsWith("A"));
            Print(startsWithA);

            Console.WriteLine("\n=== SELECT: p => p.Age > 26 && p.Name != \"Amanda\" (closure demo) ===");
            int minAge = 26; // captured local variable -> becomes a SQL parameter
            var filtered = orm.Select(p => p.Age > minAge && p.Name != "Amanda");
            Print(filtered);

            Console.WriteLine("\n=== UPDATE: Bob's age -> 26 ===");
            var bob = orm.Select(p => p.Name == "Bob").First();
            bob.Age = 26;
            orm.Update(bob);
            Print(orm.Select(p => p.Id == bob.Id));

            Console.WriteLine("\n=== DELETE: p => p.Name == \"Alice\" ===");
            orm.Delete(p => p.Name == "Alice");
            Print(orm.Select());

            Console.WriteLine("\nDone.");
        }

        private static void Print(List<Person> people)
        {
            foreach (var p in people)
                Console.WriteLine($"  Id={p.Id}, Name={p.Name}, Age={p.Age}");
        }
    }
}
