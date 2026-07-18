using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace MiniOrmDemo
{
    // =====================================================================
    // 2. MiniOrm<T>
    //    Reflection-based mapping of a POCO to a table, plus CRUD methods
    //    that build and execute parameterized SQL.
    // =====================================================================
    public class MiniOrm<T> where T : new()
    {
        private readonly SqliteConnection _conn;
        private readonly string _table;
        private readonly PropertyInfo[] _props;
        private readonly PropertyInfo _key;

        public MiniOrm(SqliteConnection connection, string tableName = null)
        {
            _conn = connection;
            _table = tableName ?? typeof(T).Name;
            _props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // Convention: a property named "Id" is the primary key, else first property.
            _key = _props.FirstOrDefault(p => p.Name == "Id") ?? _props[0];
        }

        // --- SELECT --------------------------------------------------------
        public List<T> Select(Expression<Func<T, bool>> predicate = null)
        {
            string columns = string.Join(", ", _props.Select(p => p.Name));
            string sql = $"SELECT {columns} FROM {_table}";
            var parameters = new List<SqliteParameter>();

            if (predicate != null)
            {
                var (where, ps) = SqlTranslator.Translate(predicate);
                sql += " WHERE " + where;
                parameters = ps;
            }

            LogSql(sql, parameters);

            using var cmd = new SqliteCommand(sql, _conn);
            foreach (var p in parameters) cmd.Parameters.Add(p);

            var results = new List<T>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var entity = new T();
                foreach (var prop in _props)
                {
                    object value = reader[prop.Name];
                    prop.SetValue(entity, value == DBNull.Value
                        ? null
                        : Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
                }
                results.Add(entity);
            }
            return results;
        }

        // --- INSERT ----------------------------------------------------------
        // Assumes the key column is INTEGER PRIMARY KEY AUTOINCREMENT and is
        // excluded from the INSERT; the generated id is read back and written
        // onto the entity.
        public long Insert(T entity)
        {
            var cols = _props.Where(p => p.Name != _key.Name).ToArray();
            string colList = string.Join(", ", cols.Select(c => c.Name));
            string paramList = string.Join(", ", cols.Select((c, i) => "@p" + i));
            string sql = $"INSERT INTO {_table} ({colList}) VALUES ({paramList})";

            using var cmd = new SqliteCommand(sql, _conn);
            for (int i = 0; i < cols.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i, cols[i].GetValue(entity) ?? DBNull.Value);

            LogSql(sql, cmd.Parameters.Cast<SqliteParameter>().ToList());
            cmd.ExecuteNonQuery();

            using var idCmd = new SqliteCommand("SELECT last_insert_rowid();", _conn);
            long id = (long)idCmd.ExecuteScalar();
            _key.SetValue(entity, Convert.ChangeType(id, _key.PropertyType));
            return id;
        }

        // --- UPDATE ----------------------------------------------------------
        // Updates the row whose key matches entity's key value.
        public int Update(T entity)
        {
            var cols = _props.Where(p => p.Name != _key.Name).ToArray();
            string setClause = string.Join(", ", cols.Select((c, i) => $"{c.Name} = @p{i}"));
            string sql = $"UPDATE {_table} SET {setClause} WHERE {_key.Name} = @key";

            using var cmd = new SqliteCommand(sql, _conn);
            for (int i = 0; i < cols.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i, cols[i].GetValue(entity) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@key", _key.GetValue(entity));

            LogSql(sql, cmd.Parameters.Cast<SqliteParameter>().ToList());
            return cmd.ExecuteNonQuery();
        }

        // --- DELETE ----------------------------------------------------------
        public int Delete(Expression<Func<T, bool>> predicate)
        {
            var (where, ps) = SqlTranslator.Translate(predicate);
            string sql = $"DELETE FROM {_table} WHERE {where}";

            using var cmd = new SqliteCommand(sql, _conn);
            foreach (var p in ps) cmd.Parameters.Add(p);

            LogSql(sql, ps);
            return cmd.ExecuteNonQuery();
        }

        private static void LogSql(string sql, List<SqliteParameter> ps)
        {
            string args = string.Join(", ", ps.Select(p => $"{p.ParameterName}={p.Value}"));
            Console.WriteLine($"[SQL] {sql}" + (args.Length > 0 ? $"   -- params: {args}" : ""));
        }
    }
}
