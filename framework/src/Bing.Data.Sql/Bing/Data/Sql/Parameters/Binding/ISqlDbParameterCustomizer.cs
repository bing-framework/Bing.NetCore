using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据库参数定制器
/// </summary>
public interface ISqlDbParameterCustomizer
{
    /// <summary>
    /// 判断是否支持数据库类型
    /// </summary>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>支持时返回 true</returns>
    bool CanHandle(DatabaseType databaseType);

    /// <summary>
    /// 配置数据库参数
    /// </summary>
    /// <param name="dbParameter">数据库参数</param>
    /// <param name="sqlParameter">SQL 参数元数据</param>
    void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter);
}