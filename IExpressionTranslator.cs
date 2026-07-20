using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;

namespace MiniOrmDemo
{
    public interface IExpressionTranslator
    {
        (string Sql, List<DbParameter> Parameters) Translate<T>(Expression<Func<T, bool>> expression);
    }
}
