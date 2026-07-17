namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文快照帮助器。
/// </summary>
internal static class DatabaseContextSnapshot
{
    /// <summary>
    /// 默认快照工厂。
    /// </summary>
    private static readonly IDatabaseContextSnapshotFactory Factory = new DefaultDatabaseContextSnapshotFactory();

    /// <summary>
    /// 创建默认深快照。
    /// </summary>
    /// <param name="source">源数据库上下文。</param>
    /// <returns>独立的数据库上下文快照。</returns>
    public static DatabaseContext Create(DatabaseContext source) => Factory.Create(source);
}