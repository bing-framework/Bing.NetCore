namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文访问器
/// </summary>
public interface IDatabaseContextAccessor
{
    /// <summary>
    /// 当前数据库上下文
    /// </summary>
    DatabaseContext Current { get; set; }
}
