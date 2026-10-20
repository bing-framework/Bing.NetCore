using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 不可变、可执行的 SQL 写入命令。
/// </summary>
/// <remarks>
/// 命令在创建时冻结 SQL 文本和参数，不持有 Builder、连接、事务或根执行器。
/// 由 <see cref="SqlWriteCommandExtensions.ToSqlWriteCommand"/> 创建的命令还会冻结 Provider 和 Returning 语义，
/// 可交给 <see cref="ISqlExecutor"/> 的独立写入执行入口。
/// </remarks>
public sealed class SqlWriteCommand
{
    /// <summary>
    /// 内部参数快照，避免调用方通过公开集合修改命令输入。
    /// </summary>
    private readonly SqlParam[] _parameters;

    /// <summary>
    /// 初始化一个 <see cref="SqlWriteCommand"/> 类型的实例。
    /// </summary>
    /// <param name="sql">已生成的 SQL 语句。</param>
    /// <param name="parameters">已生成的参数集合。</param>
    /// <param name="validateAffectedRows">是否在实际受影响行数不符合预期时抛出并发异常。</param>
    public SqlWriteCommand(string sql, IReadOnlyCollection<SqlParam> parameters, bool validateAffectedRows = false)
        : this(sql, parameters, null, SqlOperationKind.None, false, validateAffectedRows)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlWriteCommand"/> 类型的实例。
    /// </summary>
    /// <param name="sql">已生成的 SQL 语句。</param>
    /// <param name="parameters">已生成的参数集合。</param>
    /// <param name="provider">生成 SQL 的 Provider。</param>
    /// <param name="operationKind">写入操作类型。</param>
    /// <param name="hasReturning">是否包含 Returning 或 Output 子句。</param>
    /// <param name="validateAffectedRows">是否在实际受影响行数不符合预期时抛出并发异常。</param>
    internal SqlWriteCommand(string sql, IEnumerable<SqlParam> parameters, ISqlProvider provider,
        SqlOperationKind operationKind, bool hasReturning, bool validateAffectedRows = false)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 语句不能为空。", nameof(sql));
        if (provider != null && string.IsNullOrWhiteSpace(provider.Key))
            throw new ArgumentException("Mutation Builder 的 SQL Provider Key 不能为空。", nameof(provider));
        Sql = sql;
        _parameters = parameters?.Where(parameter => parameter != null)
            .Select(SqlParameterSnapshot.CloneSqlParameter)
            .ToArray() ?? Array.Empty<SqlParam>();
        ProviderKey = provider?.Key?.Trim();
        ProviderProfile = provider == null ? null : SqlProviderCapabilityResolver.CreateSnapshot(provider);
        OperationKind = operationKind;
        HasReturning = hasReturning;
        ValidateAffectedRows = validateAffectedRows;
    }

    /// <summary>
    /// 复制命令并仅更新受影响行数校验语义。
    /// </summary>
    /// <param name="validateAffectedRows">是否要求受影响行数满足单实体并发约束。</param>
    /// <returns>保留 SQL、参数和 Provider 执行元数据的命令副本。</returns>
    internal SqlWriteCommand WithValidateAffectedRows(bool validateAffectedRows)
    {
        if (ValidateAffectedRows == validateAffectedRows)
            return this;
        return new SqlWriteCommand(this, validateAffectedRows);
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlWriteCommand"/> 类型的实例。
    /// </summary>
    /// <param name="command">待复制的冻结命令。</param>
    /// <param name="validateAffectedRows">是否要求受影响行数满足单实体并发约束。</param>
    private SqlWriteCommand(SqlWriteCommand command, bool validateAffectedRows)
    {
        Sql = command.Sql;
        _parameters = command._parameters.Select(SqlParameterSnapshot.CloneSqlParameter).ToArray();
        ProviderKey = command.ProviderKey;
        ProviderProfile = command.ProviderProfile;
        OperationKind = command.OperationKind;
        HasReturning = command.HasReturning;
        ValidateAffectedRows = validateAffectedRows;
    }

    /// <summary>
    /// 获取已生成的 SQL 语句。
    /// </summary>
    public string Sql { get; }

    /// <summary>
    /// 获取已生成的参数快照。
    /// </summary>
    public IReadOnlyCollection<SqlParam> Parameters => _parameters.Select(SqlParameterSnapshot.CloneSqlParameter).ToArray();

    /// <summary>
    /// 获取生成 SQL 的 Provider 标识。
    /// </summary>
    /// <remarks>通过公开构造函数创建的命令未绑定 Provider，该属性为 <see langword="null"/>。</remarks>
    public string ProviderKey { get; }

    /// <summary>
    /// 获取写入操作类型。
    /// </summary>
    /// <remarks>通过未指定操作类型的构造函数创建时，该属性为 <see cref="SqlOperationKind.None"/>。</remarks>
    public SqlOperationKind OperationKind { get; }

    /// <summary>
    /// 获取是否包含 Returning 或 Output 子句。
    /// </summary>
    public bool HasReturning { get; }

    /// <summary>
    /// 获取是否要求实际受影响行数为一行。
    /// </summary>
    public bool ValidateAffectedRows { get; }

    /// <summary>
    /// 获取生成 SQL 时冻结的 Provider 能力档案。
    /// </summary>
    public SqlProviderProfile ProviderProfile { get; }

    /// <summary>
    /// 为一次执行创建独立的参数集合。
    /// </summary>
    /// <returns>当前命令参数的独立快照集合。</returns>
    public IReadOnlyCollection<SqlParam> CreateParameters() => _parameters
        .Select(SqlParameterSnapshot.CloneSqlParameter)
        .ToArray();
}

/// <summary>
/// SQL 写入命令创建扩展。
/// </summary>
public static class SqlWriteCommandExtensions
{
    /// <summary>
    /// 将当前可变 Builder 冻结为包含 Provider 和 Mutation 语义的独立写入命令。
    /// </summary>
    /// <param name="builder">已完成 Mutation 构建的 SQL Builder。</param>
    /// <returns>不再依赖该 Builder 状态的写入命令。</returns>
    public static SqlWriteCommand ToSqlWriteCommand(this ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var provider = builder.Provider ?? throw new InvalidOperationException("Mutation Builder 必须提供 SQL Provider。");
        if (string.IsNullOrWhiteSpace(provider.Key))
            throw new InvalidOperationException("Mutation Builder 的 SQL Provider Key 不能为空。");
        if (builder.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            throw new ArgumentException("写入命令必须包含 Insert、Update 或 Delete 操作。", nameof(builder));
        if (builder is SqlBuilderBase sqlBuilder)
            return sqlBuilder.CreateWriteCommandSnapshot();
        var snapshot = builder.Clone();
        var hasReturning = snapshot is IReturningClauseAccessor { ReturningClause.IsEmpty: false };
        var sql = snapshot.ToSql();
        var parameters = snapshot.GetSqlParams();
        return new SqlWriteCommand(sql, parameters?.Values, provider, builder.OperationKind, hasReturning);
    }
}
