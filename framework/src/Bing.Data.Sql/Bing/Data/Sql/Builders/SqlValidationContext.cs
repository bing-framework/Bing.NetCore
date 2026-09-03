namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 结构验证上下文。
/// </summary>
public sealed class SqlValidationContext
{
    /// <summary>
    /// 初始化一个 <see cref="SqlValidationContext"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="parameterCount">当前命令的参数数量。</param>
    /// <param name="allowAllRows">是否显式允许全表写操作。</param>
    /// <param name="executionKind">当前 SQL 的执行类型。</param>
    public SqlValidationContext(ISqlProvider provider, int parameterCount, bool allowAllRows,
        SqlExecutionKind executionKind)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        ParameterCount = parameterCount;
        AllowAllRows = allowAllRows;
        ExecutionKind = executionKind;
        Profile = SqlProviderCapabilityResolver.GetProfile(provider);
    }

    /// <summary>
    /// 当前 SQL Provider。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// 当前 Provider 的统一能力档案。
    /// </summary>
    public SqlProviderProfile Profile { get; }

    /// <summary>
    /// 当前 Provider 是否声明了统一能力档案。
    /// </summary>
    internal bool IsProfileDeclared => SqlProviderCapabilityResolver.HasProfile(Provider);

    /// <summary>
    /// 当前命令已经分配的参数数量。
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// 是否显式允许 Update 或 Delete 影响全部行。
    /// </summary>
    public bool AllowAllRows { get; }

    /// <summary>
    /// 当前 SQL 的执行类型。
    /// </summary>
    public SqlExecutionKind ExecutionKind { get; }
}