using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定上下文
/// </summary>
public sealed class SqlParameterBindingContext
{
    /// <summary>
    /// 获取或设置SQL 语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 获取或设置数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 获取或设置数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 获取或设置实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 获取或设置原始参数源
    /// </summary>
    public object Source { get; set; }
}