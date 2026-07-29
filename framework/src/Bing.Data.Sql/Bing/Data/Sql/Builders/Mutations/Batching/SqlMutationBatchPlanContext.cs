namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 计算 Mutation 批次时需要的固定输入。
/// </summary>
public sealed class SqlMutationBatchPlanContext
{
    /// <summary>
    /// 初始化一个 <see cref="SqlMutationBatchPlanContext"/> 类型的实例。
    /// </summary>
    /// <param name="entityCount">待处理实体数量。</param>
    /// <param name="parametersPerEntity">每个实体使用的参数数量。</param>
    /// <param name="existingParameterCount">命令开始前已经占用的参数数量。</param>
    /// <param name="maxParameterCount">Provider 最大参数数量；为空时不限制。</param>
    /// <param name="estimatedSqlLengthPerEntity">每个实体预计增加的 SQL 字符数。</param>
    /// <param name="maxSqlLength">允许的最大 SQL 字符数；为空时不限制。</param>
    /// <param name="options">批量选项。</param>
    public SqlMutationBatchPlanContext(int entityCount, int parametersPerEntity, int existingParameterCount = 0,
        int? maxParameterCount = null, int estimatedSqlLengthPerEntity = 0, int? maxSqlLength = null,
        SqlMutationBatchOptions options = null)
    {
        if (entityCount < 0)
            throw new ArgumentOutOfRangeException(nameof(entityCount));
        if (parametersPerEntity <= 0)
            throw new ArgumentOutOfRangeException(nameof(parametersPerEntity));
        if (existingParameterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(existingParameterCount));
        if (maxParameterCount is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxParameterCount));
        if (estimatedSqlLengthPerEntity < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedSqlLengthPerEntity));
        if (maxSqlLength is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSqlLength));
        EntityCount = entityCount;
        ParametersPerEntity = parametersPerEntity;
        ExistingParameterCount = existingParameterCount;
        MaxParameterCount = maxParameterCount;
        EstimatedSqlLengthPerEntity = estimatedSqlLengthPerEntity;
        MaxSqlLength = maxSqlLength;
        Options = options ?? new SqlMutationBatchOptions();
    }

    /// <summary>
    /// 待处理实体数量。
    /// </summary>
    public int EntityCount { get; }

    /// <summary>
    /// 每个实体使用的参数数量。
    /// </summary>
    public int ParametersPerEntity { get; }

    /// <summary>
    /// 命令开始前已使用的参数数量。
    /// </summary>
    public int ExistingParameterCount { get; }

    /// <summary>
    /// Provider 最大参数数量；为空时不限制。
    /// </summary>
    public int? MaxParameterCount { get; }

    /// <summary>
    /// 每个实体预计增加的 SQL 字符数。
    /// </summary>
    public int EstimatedSqlLengthPerEntity { get; }

    /// <summary>
    /// 最大 SQL 字符数；为空时不限制。
    /// </summary>
    public int? MaxSqlLength { get; }

    /// <summary>
    /// 批量选项。
    /// </summary>
    public SqlMutationBatchOptions Options { get; }
}