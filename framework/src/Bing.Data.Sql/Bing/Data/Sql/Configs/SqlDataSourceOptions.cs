namespace Bing.Data.Sql.Configs;

/// <summary>
/// 配置可供 SQL 操作解析的数据源及其默认选择规则。
/// </summary>
public sealed class SqlDataSourceOptions
{
    /// <summary>
    /// 获取或设置无显式数据源标识时使用的默认数据源键；该值必须对应 <see cref="DataSources"/> 中的键。
    /// </summary>
    public string DefaultDataSourceKey { get; set; } = "default";

    /// <summary>
    /// 获取按数据源标识索引的数据源集合；键不区分大小写。
    /// </summary>
    public IDictionary<string, SqlDataSourceDescriptor> DataSources { get; } =
        new Dictionary<string, SqlDataSourceDescriptor>(StringComparer.OrdinalIgnoreCase);
}