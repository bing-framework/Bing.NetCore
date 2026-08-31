using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// SQL 对象名格式化器
/// </summary>
public interface ISqlObjectNameFormatter
{
    /// <summary>
    /// 格式化表引用
    /// </summary>
    /// <param name="reference">结构化表引用</param>
    /// <param name="dialect">SQL 方言</param>
    /// <param name="databaseType">数据库类型</param>
    /// <returns>按指定方言和数据库类型格式化后的 SQL 对象名称。</returns>
    string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType);
}