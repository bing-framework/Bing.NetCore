namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文快照工厂。
/// </summary>
public interface IDatabaseContextSnapshotFactory
{
    /// <summary>
    /// 创建独立的数据库上下文深快照。
    /// </summary>
    /// <param name="source">源数据库上下文。</param>
    /// <returns>独立的数据库上下文快照。</returns>
    DatabaseContext Create(DatabaseContext source);
}