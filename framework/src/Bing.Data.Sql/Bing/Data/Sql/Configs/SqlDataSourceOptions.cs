namespace Bing.Data.Sql.Configs;

/// <summary>
/// SQL 数据源配置
/// </summary>
public sealed class SqlDataSourceOptions
{
    /// <summary>
    /// 数据源集合
    /// </summary>
    public IDictionary<string, SqlDataSourceDescriptor> DataSources { get; } =
        new Dictionary<string, SqlDataSourceDescriptor>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 默认数据源键
    /// </summary>
    public string DefaultDataSourceKey { get; set; }
}