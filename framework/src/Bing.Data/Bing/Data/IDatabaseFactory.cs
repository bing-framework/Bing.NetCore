using System.ComponentModel;

namespace Bing.Data;

/// <summary>
/// 数据库工厂
/// </summary>
[System.Obsolete("Dapper 连接创建已迁移至 ISqlDbConnectionFactoryResolver。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IDatabaseFactory
{
    /// <summary>
    /// 创建数据库信息
    /// </summary>
    /// <param name="connection">数据库连接字符串</param>
    IDatabase Create(string connection);
}
