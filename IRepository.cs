using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MiniOrmDemo
{
    public interface IRepository<T> where T : new()
    {
        List<T> Select(Expression<Func<T, bool>> predicate = null);
        long Insert(T entity);
        int Update(T entity);
        int Delete(Expression<Func<T, bool>> predicate);
    }
}
