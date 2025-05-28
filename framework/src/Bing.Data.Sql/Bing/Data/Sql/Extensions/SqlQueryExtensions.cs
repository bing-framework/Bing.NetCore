using System.Data;
using Bing.Data.Sql.Database;
using Bing.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象扩展
/// </summary>
public static partial class SqlQueryExtensions
{
    #region GetConnection(获取数据库连接)

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    /// <param name="source">源</param>
    /// <returns>IDbConnection 或者 null</returns>
    public static IDbConnection GetConnection(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        if (source is IDbConnectionManager manager)
            return manager.GetConnection();
        return null;
    }

    #endregion

    #region SetConnection(设置数据库连接)

    /// <summary>
    /// 设置数据库连接
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="connection">数据库连接</param>
    public static ISqlQuery SetConnection(this ISqlQuery source, IDbConnection connection)
    {
        source.CheckNull(nameof(source));
        if (source is IDbConnectionManager manager)
            manager.SetConnection(connection);
        return source;
    }

    #endregion
}
