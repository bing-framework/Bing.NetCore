using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库描述信息
/// </summary>
public sealed class DatabaseDescriptor
{
    /// <summary>
    /// 数据库键
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库角色
    /// </summary>
    public DatabaseRole Role { get; set; } = DatabaseRole.Default;

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool ReadOnly { get; set; }
}