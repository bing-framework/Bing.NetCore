namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// EF Core SQL 查询连接模式
/// </summary>
public enum EfCoreSqlConnectionMode
{
    /// <summary>
    /// 共享 EF Core 连接和当前事务
    /// </summary>
    Shared,

    /// <summary>
    /// 使用独立 SQL 查询连接
    /// </summary>
    Independent
}