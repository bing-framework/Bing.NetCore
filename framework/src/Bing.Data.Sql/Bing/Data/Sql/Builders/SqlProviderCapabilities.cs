namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL Provider 的可选运行能力。
/// </summary>
public sealed class SqlProviderCapabilities
{
    /// <summary>
    /// 初始化一个<see cref="SqlProviderCapabilities"/>类型的实例。
    /// </summary>
    /// <param name="supportsMultipleResultSets">是否支持单次命令读取多个结果集。</param>
    /// <param name="supportsMultiRowValues">是否支持标准多行 Values 语法。</param>
    /// <param name="supportsUpdateFrom">是否支持 Update From 语法。</param>
    public SqlProviderCapabilities(bool supportsMultipleResultSets = false, bool supportsMultiRowValues = true,
        bool supportsUpdateFrom = false)
        : this(supportsMultipleResultSets, supportsMultiRowValues, supportsUpdateFrom, false)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="SqlProviderCapabilities"/>类型的实例。
    /// </summary>
    /// <param name="supportsMultipleResultSets">是否支持单次命令读取多个结果集。</param>
    /// <param name="supportsMultiRowValues">是否支持标准多行 Values 语法。</param>
    /// <param name="supportsUpdateFrom">是否支持 Update From 语法。</param>
    /// <param name="supportsDeleteUsing">是否支持 Delete Using 语法。</param>
    public SqlProviderCapabilities(bool supportsMultipleResultSets, bool supportsMultiRowValues,
        bool supportsUpdateFrom, bool supportsDeleteUsing)
        : this(supportsMultipleResultSets, supportsMultiRowValues, supportsUpdateFrom, supportsDeleteUsing, false)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="SqlProviderCapabilities"/>类型的实例。
    /// </summary>
    /// <param name="supportsMultipleResultSets">是否支持单次命令读取多个结果集。</param>
    /// <param name="supportsMultiRowValues">是否支持标准多行 Values 语法。</param>
    /// <param name="supportsUpdateFrom">是否支持 Update From 语法。</param>
    /// <param name="supportsDeleteUsing">是否支持 Delete Using 语法。</param>
    /// <param name="supportsReturning">是否支持 Mutation Returning 语法。</param>
    public SqlProviderCapabilities(bool supportsMultipleResultSets, bool supportsMultiRowValues,
        bool supportsUpdateFrom, bool supportsDeleteUsing, bool supportsReturning)
    {
        SupportsMultipleResultSets = supportsMultipleResultSets;
        SupportsMultiRowValues = supportsMultiRowValues;
        SupportsUpdateFrom = supportsUpdateFrom;
        SupportsDeleteUsing = supportsDeleteUsing;
        SupportsReturning = supportsReturning;
    }

    /// <summary>
    /// 是否支持单次命令读取多个结果集。
    /// </summary>
    public bool SupportsMultipleResultSets { get; }

    /// <summary>
    /// 是否支持 <c>Values (...), (...)</c> 多行插入语法。
    /// </summary>
    public bool SupportsMultiRowValues { get; }

    /// <summary>
    /// 是否支持结构化 <c>Update ... Set ... From ... Where ...</c> 语法。
    /// </summary>
    public bool SupportsUpdateFrom { get; }

    /// <summary>
    /// 是否支持结构化 <c>Delete From ... Using ... Where ...</c> 语法。
    /// </summary>
    public bool SupportsDeleteUsing { get; }

    /// <summary>
    /// 是否支持结构化 Mutation 返回结果投影。
    /// </summary>
    public bool SupportsReturning { get; }

}