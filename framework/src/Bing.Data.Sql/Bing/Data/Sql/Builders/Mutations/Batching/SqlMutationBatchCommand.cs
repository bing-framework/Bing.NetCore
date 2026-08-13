using Bing.Data.Sql.Mutations;

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
    /// <param name="validateAffectedRows">是否校验实际受影响行数与实体数量一致。</param>
    /// <param name="operationName">受影响行数校验失败时使用的操作名称。</param>
    public SqlMutationBatchCommand(IReadOnlyList<SqlWriteCommand> commands, int entityCount,
        bool requiresTransaction, bool validateAffectedRows = false, string operationName = "Mutation")
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        if (entityCount < 0)
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        EntityCount = entityCount;
        RequiresTransaction = requiresTransaction;
        ValidateAffectedRows = validateAffectedRows || Commands.Any(command => command.ValidateAffectedRows);
        OperationName = string.IsNullOrWhiteSpace(operationName) ? "Mutation" : operationName;
    }

    /// <summary>
    /// 按执行顺序保存的 SQL 命令。
    /// </summary>
    public IReadOnlyList<SqlWriteCommand> Commands { get; }

    /// <summary>
    /// 本批覆盖的实体数量。
    /// </summary>
    public int EntityCount { get; }

    /// <summary>
    /// 是否要求在单一事务中执行。即使外层批处理选项禁用事务，执行器也必须尊重该要求。
    /// </summary>
    public bool RequiresTransaction { get; }

    /// <summary>
    /// 是否要求本批实际受影响行数与实体数量完全一致。
    /// </summary>
    public bool ValidateAffectedRows { get; }

    /// <summary>
    /// 受影响行数校验失败时使用的操作名称。
    /// </summary>
    public string OperationName { get; }
}