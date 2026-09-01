using Bing.Data.Enums;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 按数据库方言格式化 SQL 对象名称。
/// </summary>
public interface ISqlObjectNameFormatter
{
    /// <summary>
    /// 格式化表引用。
    /// </summary>
    /// <param name="reference">要格式化的结构化表引用。</param>
    /// <param name="dialect">用于生成对象名称的 SQL 方言。</param>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <returns>按指定方言和数据库类型格式化后的 SQL 对象名称。</returns>
    string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType);
}