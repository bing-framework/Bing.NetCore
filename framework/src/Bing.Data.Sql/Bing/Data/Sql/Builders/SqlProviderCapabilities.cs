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
    public SqlProviderCapabilities(bool supportsMultipleResultSets = false)
    {
        SupportsMultipleResultSets = supportsMultipleResultSets;
    }

    /// <summary>
    /// 是否支持单次命令读取多个结果集。
    /// </summary>
    public bool SupportsMultipleResultSets { get; }
}