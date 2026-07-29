namespace Bing.Data.Sql.Builders.Mutations.Batching;

/// <summary>
/// 批量 Mutation 的通用选项。
/// </summary>
public class SqlMutationBatchOptions
{
    /// <summary>
    /// 用户指定的每批实体数量；为空时由规划器按 Provider 限制计算。
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// 单条命令允许的最大参数数量；为空时采用 Provider 限制。
    /// </summary>
    /// <remarks>
    /// 当 Provider 声明硬性参数上限时，该值只能收紧限制，不能放宽 Provider 上限。
    /// </remarks>
    public int? MaxParameterCount { get; set; }

    /// <summary>
    /// 单条命令允许的最大 SQL 字符数；为空时不额外限制。
    /// </summary>
    public int? MaxSqlLength { get; set; }

    /// <summary>
    /// 是否在一个事务中执行全部批次。
    /// </summary>
    public bool UseTransaction { get; set; } = true;

    /// <summary>
    /// 批量 SQL 生成策略；默认自动选择 Provider 支持的最优 Insert 策略。
    /// </summary>
    public SqlBatchStrategy Strategy { get; set; } = SqlBatchStrategy.Auto;

    /// <summary>
    /// 获取当前批处理适用的最大参数数量。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <returns>调用方与 Provider 上限中较小的有效值；二者均未限制时返回 null。</returns>
    public int? GetEffectiveMaxParameterCount(ISqlProvider provider)
    {
        var providerLimit = (provider as ISqlParameterLimitProvider)?.MaxParameterCount;
        if (MaxParameterCount == null)
            return providerLimit;
        if (providerLimit == null)
            return MaxParameterCount;
        return Math.Min(MaxParameterCount.Value, providerLimit.Value);
    }
}