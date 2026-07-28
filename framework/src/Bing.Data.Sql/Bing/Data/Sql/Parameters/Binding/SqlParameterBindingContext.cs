using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定上下文
/// </summary>
public sealed class SqlParameterBindingContext
{
    /// <summary>
    /// SQL 语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 原始参数源
    /// </summary>
    public object Source { get; set; }
}