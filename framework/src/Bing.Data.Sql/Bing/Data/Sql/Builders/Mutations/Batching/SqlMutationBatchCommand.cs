namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 可在相同执行策略下运行的一组 Mutation 命令。
/// </summary>
public sealed class SqlMutationBatchCommand
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationBatchCommand"/> 类型的实例。
    /// </summary>
    /// <param name="commands">按执行顺序保存的命令。</param>
    /// <param name="entityCount">本批命令覆盖的实体数量。</param>
    /// <param name="requiresTransaction">是否要求在单一事务中执行。</param>
    public SqlMutationBatchCommand(IReadOnlyList<SqlMutationCommand> commands, int entityCount,
        bool requiresTransaction)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        if (entityCount < 0)
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        EntityCount = entityCount;
        RequiresTransaction = requiresTransaction;
    }

    /// <summary>
    /// 按执行顺序保存的 SQL 命令。
    /// </summary>
    public IReadOnlyList<SqlMutationCommand> Commands { get; }

    /// <summary>
    /// 本批覆盖的实体数量。
    /// </summary>
    public int EntityCount { get; }

    /// <summary>
    /// 是否要求在单一事务中执行。
    /// </summary>
    public bool RequiresTransaction { get; }
}