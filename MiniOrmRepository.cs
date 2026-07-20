using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MiniOrmDemo
{
    public class MiniOrm<T> : IRepository<T> where T : new()
    {
        private readonly DbConnection _conn;
        private readonly IExpressionTranslator _translator;
        private readonly string _table;
        private readonly PropertyInfo[] _props;
        private readonly PropertyInfo _key;

        public MiniOrm(DbConnection connection, IExpressionTranslator translator, string tableName = null)
        {
            _conn = connection;
            _translator = translator;
            _table = tableName ?? typeof(T).Name;
            _props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            _key = _props.FirstOrDefault(p => p.Name == "Id") ?? _props[0];
        }

        public List<T> Select(Expression<Func<T, bool>> predicate = null)
        {
            string columns = string.Join(", ", _props.Select(p => p.Name));
            string sql = $"SELECT {columns} FROM {_table}";
            var parameters = new List<DbParameter>();

            if (predicate != null)
            {
                var (where, ps) = _translator.Translate(predicate);
                sql += " WHERE " + where;
                parameters = ps;
            }

            LogSql(sql, parameters);

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
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

        public long Insert(T entity)
        {
            var cols = _props.Where(p => p.Name != _key.Name).ToArray();
            string colList = string.Join(", ", cols.Select(c => c.Name));
            string paramList = string.Join(", ", cols.Select((c, i) => "@p" + i));
            string sql = $"INSERT INTO {_table} ({colList}) VALUES ({paramList})";

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < cols.Length; i++)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@p" + i;
                param.Value = cols[i].GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            LogSql(sql, cmd.Parameters.Cast<DbParameter>().ToList());
            cmd.ExecuteNonQuery();

            using var idCmd = _conn.CreateCommand();
            idCmd.CommandText = "SELECT last_insert_rowid();";
            long id = (long)idCmd.ExecuteScalar();
            _key.SetValue(entity, Convert.ChangeType(id, _key.PropertyType));
            return id;
        }

        public int Update(T entity)
        {
            var cols = _props.Where(p => p.Name != _key.Name).ToArray();
            string setClause = string.Join(", ", cols.Select((c, i) => $"{c.Name} = @p{i}"));
            string sql = $"UPDATE {_table} SET {setClause} WHERE {_key.Name} = @key";

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < cols.Length; i++)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@p" + i;
                param.Value = cols[i].GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "@key";
            keyParam.Value = _key.GetValue(entity);
            cmd.Parameters.Add(keyParam);

            LogSql(sql, cmd.Parameters.Cast<DbParameter>().ToList());
            return cmd.ExecuteNonQuery();
        }

        public int Delete(Expression<Func<T, bool>> predicate)
        {
            var (where, ps) = _translator.Translate(predicate);
            string sql = $"DELETE FROM {_table} WHERE {where}";

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in ps) cmd.Parameters.Add(p);

            LogSql(sql, ps);
            return cmd.ExecuteNonQuery();
        }

        private static void LogSql(string sql, List<DbParameter> ps)
        {
            string args = string.Join(", ", ps.Select(p => $"{p.ParameterName}={p.Value}"));
            Console.WriteLine($"[SQL] {sql}" + (args.Length > 0 ? $"   -- params: {args}" : ""));
        }
    }
}
