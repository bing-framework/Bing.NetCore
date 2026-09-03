using Bing.Data;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Provider 的不可变查询语法能力。
/// </summary>
public sealed class SqlProviderQueryCapabilities
{
    /// <summary>
    /// 是否支持公用表表达式。
    /// </summary>
    public SqlQueryCapabilityState Cte { get; init; }

    /// <summary>
    /// 是否支持 Union。
    /// </summary>
    public SqlQueryCapabilityState Union { get; init; }

    /// <summary>
    /// 是否支持 Union All。
    /// </summary>
    public SqlQueryCapabilityState UnionAll { get; init; }

    /// <summary>
    /// 是否支持 Intersect。
    /// </summary>
    public SqlQueryCapabilityState Intersect { get; init; }

    /// <summary>
    /// 是否支持 Except。
    /// </summary>
    public SqlQueryCapabilityState Except { get; init; }

    /// <summary>
    /// 是否支持 Right Join。
    /// </summary>
    public SqlQueryCapabilityState RightJoin { get; init; }

    /// <summary>
    /// 是否支持 Full Join。
    /// </summary>
    public SqlQueryCapabilityState FullJoin { get; init; }

    /// <summary>
    /// 是否支持 Skip、Take 和 Page 分页语法。
    /// </summary>
    public SqlQueryCapabilityState Pagination { get; init; }
}

/// <summary>
/// SQL Provider 的不可变 Mutation 能力。
/// </summary>
public sealed class SqlProviderMutationCapabilities
{
    /// <summary>
    /// 是否支持标准多行 Values 插入语法。
    /// </summary>
    public bool SupportsMultiRowValues { get; init; }

    /// <summary>
    /// 多行 Values 关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? MultiRowValuesFailureReason { get; init; }

    /// <summary>
    /// 是否支持 Update From 语法。
    /// </summary>
    public bool SupportsUpdateFrom { get; init; }

    /// <summary>
    /// Update From 关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? UpdateFromFailureReason { get; init; }

    /// <summary>
    /// 是否支持 Delete Using 语法。
    /// </summary>
    public bool SupportsDeleteUsing { get; init; }

    /// <summary>
    /// Delete Using 关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? DeleteUsingFailureReason { get; init; }

    /// <summary>
    /// 是否支持 Mutation Returning 结果投影。
    /// </summary>
    public bool SupportsReturning { get; init; }

    /// <summary>
    /// Mutation Returning 关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? ReturningFailureReason { get; init; }
}

/// <summary>
/// SQL Provider 的不可变执行能力。
/// </summary>
public sealed class SqlProviderExecutionCapabilities
{
    /// <summary>
    /// 是否支持单次命令读取多个结果集。
    /// </summary>
    public bool SupportsMultipleResultSets { get; init; }

    /// <summary>
    /// 多结果集读取关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? MultipleResultSetsFailureReason { get; init; }

    /// <summary>
    /// 是否支持流式读取。
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// 流式读取关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? StreamingFailureReason { get; init; }

    /// <summary>
    /// 是否支持将取消令牌传递到异步命令。
    /// </summary>
    public bool SupportsCancellation { get; init; }

    /// <summary>
    /// 异步命令取消关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? CancellationFailureReason { get; init; }
}

/// <summary>
/// SQL Provider 的不可变事务能力。
/// </summary>
public sealed class SqlProviderTransactionCapabilities
{
    /// <summary>
    /// 是否支持本地事务。
    /// </summary>
    public bool SupportsTransactions { get; init; }

    /// <summary>
    /// 本地事务关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? TransactionsFailureReason { get; init; }

    /// <summary>
    /// 是否声明支持原生异步开始事务。
    /// </summary>
    public bool SupportsNativeAsyncBegin { get; init; }

    /// <summary>
    /// 是否声明支持原生异步提交事务。
    /// </summary>
    public bool SupportsNativeAsyncCommit { get; init; }

    /// <summary>
    /// 是否声明支持原生异步回滚事务。
    /// </summary>
    public bool SupportsNativeAsyncRollback { get; init; }
}

/// <summary>
/// SQL Provider 的不可变存储过程能力。
/// </summary>
public sealed class SqlProviderProcedureCapabilities
{
    /// <summary>
    /// 是否支持存储过程命令。
    /// </summary>
    public bool SupportsStoredProcedures { get; init; }

    /// <summary>
    /// 存储过程命令关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? StoredProceduresFailureReason { get; init; }

    /// <summary>
    /// 是否支持存储过程输出参数。
    /// </summary>
    public bool SupportsOutputParameters { get; init; }

    /// <summary>
    /// 存储过程输出参数关闭时的失败原因。
    /// </summary>
    public SqlCapabilityFailureReason? OutputParametersFailureReason { get; init; }
}

/// <summary>
/// SQL Provider 的不可变资源限制。
/// </summary>
public sealed class SqlProviderLimits
{
    /// <summary>
    /// 单条命令允许的最大参数数量；未限制时为 <see langword="null"/>。
    /// </summary>
    public int? MaxParameterCount { get; init; }
}

/// <summary>
/// SQL Provider 的统一不可变能力档案。
/// </summary>
/// <remarks>
/// 查询、Mutation 和执行能力被分别建模，避免位置布尔参数混合不同责任域。
/// </remarks>
public sealed class SqlProviderProfile
{
    /// <summary>
    /// 查询语法能力。
    /// </summary>
    public SqlProviderQueryCapabilities Query { get; init; } = new();

    /// <summary>
    /// Mutation 语法能力。
    /// </summary>
    public SqlProviderMutationCapabilities Mutation { get; init; } = new();

    /// <summary>
    /// 命令执行能力。
    /// </summary>
    public SqlProviderExecutionCapabilities Execution { get; init; } = new();

    /// <summary>
    /// 本地事务能力。
    /// </summary>
    public SqlProviderTransactionCapabilities Transaction { get; init; } = new();

    /// <summary>
    /// 存储过程能力。
    /// </summary>
    public SqlProviderProcedureCapabilities Procedure { get; init; } = new();

    /// <summary>
    /// Provider 资源限制。
    /// </summary>
    public SqlProviderLimits Limits { get; init; } = new();

}

/// <summary>
/// 暴露 SQL Provider 统一能力档案的可选契约。
/// </summary>
public interface ISqlProviderProfileProvider
{
    /// <summary>
    /// 当前 Provider 的统一能力档案。
    /// </summary>
    SqlProviderProfile Profile { get; }
}

/// <summary>
/// 解析 Provider 统一能力档案。
/// </summary>
internal static class SqlProviderCapabilityResolver
{
    /// <summary>
    /// 判断 Provider 是否声明了统一能力档案。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <returns>Provider 声明了非空能力档案时返回 <see langword="true"/>。</returns>
    internal static bool HasProfile(ISqlProvider provider) => provider is ISqlProviderProfileProvider
        {
            Profile: not null
        };

    /// <summary>
    /// 判断 Provider 是否声明了完整统一能力档案。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <returns>所有能力域均已声明时返回 <see langword="true"/>。</returns>
    internal static bool HasCompleteProfile(ISqlProvider provider) => provider is ISqlProviderProfileProvider
    {
        Profile: { Query: not null, Mutation: not null, Execution: not null, Transaction: not null,
            Procedure: not null, Limits: not null }
    };

    /// <summary>
    /// 获取 Provider 的统一能力档案。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <returns>不可变能力档案。</returns>
    internal static SqlProviderProfile GetProfile(ISqlProvider provider)
    {
        if (provider is ISqlProviderProfileProvider { Profile: not null } profileProvider)
            return profileProvider.Profile;
        return new SqlProviderProfile();
    }

    /// <summary>
    /// 创建 Provider 能力档案的深层不可变副本。
    /// </summary>
    /// <param name="provider">待快照的 SQL Provider。</param>
    /// <returns>不与 Provider 后续配置共享可变能力对象的档案副本。</returns>
    internal static SqlProviderProfile CreateSnapshot(ISqlProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        var profile = GetProfile(provider);
        var query = profile.Query ?? new SqlProviderQueryCapabilities();
        var mutation = profile.Mutation ?? new SqlProviderMutationCapabilities();
        var execution = profile.Execution ?? new SqlProviderExecutionCapabilities();
        var transaction = profile.Transaction ?? new SqlProviderTransactionCapabilities();
        var procedure = profile.Procedure ?? new SqlProviderProcedureCapabilities();
        var limits = profile.Limits ?? new SqlProviderLimits();
        return new SqlProviderProfile
        {
            Query = new SqlProviderQueryCapabilities
            {
                Cte = query.Cte,
                Union = query.Union,
                UnionAll = query.UnionAll,
                Intersect = query.Intersect,
                Except = query.Except,
                RightJoin = query.RightJoin,
                FullJoin = query.FullJoin,
                Pagination = query.Pagination
            },
            Mutation = new SqlProviderMutationCapabilities
            {
                SupportsMultiRowValues = mutation.SupportsMultiRowValues,
                MultiRowValuesFailureReason = mutation.MultiRowValuesFailureReason,
                SupportsUpdateFrom = mutation.SupportsUpdateFrom,
                UpdateFromFailureReason = mutation.UpdateFromFailureReason,
                SupportsDeleteUsing = mutation.SupportsDeleteUsing,
                DeleteUsingFailureReason = mutation.DeleteUsingFailureReason,
                SupportsReturning = mutation.SupportsReturning,
                ReturningFailureReason = mutation.ReturningFailureReason
            },
            Execution = new SqlProviderExecutionCapabilities
            {
                SupportsMultipleResultSets = execution.SupportsMultipleResultSets,
                MultipleResultSetsFailureReason = execution.MultipleResultSetsFailureReason,
                SupportsStreaming = execution.SupportsStreaming,
                StreamingFailureReason = execution.StreamingFailureReason,
                SupportsCancellation = execution.SupportsCancellation,
                CancellationFailureReason = execution.CancellationFailureReason
            },
            Transaction = new SqlProviderTransactionCapabilities
            {
                SupportsTransactions = transaction.SupportsTransactions,
                TransactionsFailureReason = transaction.TransactionsFailureReason,
                SupportsNativeAsyncBegin = transaction.SupportsNativeAsyncBegin,
                SupportsNativeAsyncCommit = transaction.SupportsNativeAsyncCommit,
                SupportsNativeAsyncRollback = transaction.SupportsNativeAsyncRollback
            },
            Procedure = new SqlProviderProcedureCapabilities
            {
                SupportsStoredProcedures = procedure.SupportsStoredProcedures,
                StoredProceduresFailureReason = procedure.StoredProceduresFailureReason,
                SupportsOutputParameters = procedure.SupportsOutputParameters,
                OutputParametersFailureReason = procedure.OutputParametersFailureReason
            },
            Limits = new SqlProviderLimits { MaxParameterCount = limits.MaxParameterCount }
        };
    }

    /// <summary>
    /// 获取 Provider 的查询能力基线副本。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <returns>可由调用方安全冻结的查询能力配置。</returns>
    internal static SqlQueryCapabilities GetQueryCapabilities(ISqlProvider provider)
    {
        var query = GetProfile(provider).Query ?? new SqlProviderQueryCapabilities();
        return new SqlQueryCapabilities
        {
            Cte = query.Cte,
            Union = query.Union,
            UnionAll = query.UnionAll,
            Intersect = query.Intersect,
            Except = query.Except,
            RightJoin = query.RightJoin,
            FullJoin = query.FullJoin,
            Pagination = query.Pagination
        };
    }
}