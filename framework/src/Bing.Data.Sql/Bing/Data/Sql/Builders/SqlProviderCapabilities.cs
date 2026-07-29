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
    /// <param name="supportsReturning">是否支持标准 Returning 或 Output 扩展子句。</param>
    public SqlProviderCapabilities(bool supportsMultipleResultSets = false, bool supportsMultiRowValues = true,
        bool supportsReturning = false)
    {
        SupportsMultipleResultSets = supportsMultipleResultSets;
        SupportsMultiRowValues = supportsMultiRowValues;
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
    /// 是否支持 Provider 的 Returning 或 Output 扩展子句。
    /// </summary>
    public bool SupportsReturning { get; }
}