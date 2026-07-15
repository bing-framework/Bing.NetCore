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

    #region UseReadPreference(设置读取偏好)

    /// <summary>
    /// 设置读取偏好
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="readPreference">读取偏好</param>
    public static ISqlQuery UseReadPreference(this ISqlQuery source, SqlReadPreference readPreference)
    {
        source.CheckNull(nameof(source));
        source.Config(options =>
        {
            var context = options.GetDatabaseContext() ?? new DatabaseContext();
            context.ReadPreference = readPreference;
            options.SetDatabaseContext(context);
        });
        return source;
    }

    /// <summary>
    /// 使用主库读取
    /// </summary>
    /// <param name="source">源</param>
    public static ISqlQuery UsePrimary(this ISqlQuery source) =>
        source.UseReadPreference(SqlReadPreference.Primary);

    #endregion

    #region SetTransaction(设置数据库事务)

    /// <summary>
    /// 设置数据库事务
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="transaction">数据库事务</param>
    public static ISqlQuery SetTransaction(this ISqlQuery source, IDbTransaction transaction)
    {
        source.CheckNull(nameof(source));
        if (source is IDbTransactionManager manager)
            manager.SetTransaction(transaction);
        return source;
    }

    #endregion
}
