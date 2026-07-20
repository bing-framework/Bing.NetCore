using System.Data;

namespace Bing.Data;

/// <summary>
/// 数据库连接访问器。
/// </summary>
public interface IDatabaseConnectionAccessor
{
    /// <summary>
    /// 获取数据库连接。
    /// </summary>
    /// <returns>数据库连接。</returns>
    IDbConnection GetConnection();
}