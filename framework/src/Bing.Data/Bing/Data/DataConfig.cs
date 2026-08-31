namespace Bing.Data;

/// <summary>
/// 配置数据访问层的日志、事务和过滤行为。
/// </summary>
public class DataConfig
{
    /// <summary>
    /// 获取或设置数据访问日志级别。
    /// </summary>
    public Data.DataLogLevel LogLevel { get; set; } = Data.DataLogLevel.Sql;

    /// <summary>
    /// 获取或设置默认 SQL 操作配置。
    /// </summary>
    public SqlOptions SqlOptions { get; set; }

    /// <summary>
    /// 获取或设置是否自动提交事务；默认值为 <c>false</c>。
    /// </summary>
    public bool AutoCommit { get; set; } = false;

    /// <summary>
    /// 获取或设置是否启用并发版本号验证；默认值为 <c>true</c>。
    /// </summary>
    public bool EnabledValidateVersion { get; set; } = true;

    /// <summary>
    /// 获取或设置可选的 ADO 诊断回调。
    /// </summary>
    /// <remarks>
    /// 回调参数的具体含义由调用该配置的 ADO 执行器定义；未设置时不会执行该回调。
    /// </remarks>
    public Action<string, string, object> AdoLogInterceptor { get; set; } = null;

    /// <summary>
    /// 获取或设置是否启用逻辑删除记录过滤；默认值为 <c>true</c>。
    /// </summary>
    public bool EnabledDeleteFilter { get; set; } = true;

    /// <summary>
    /// 初始化 <see cref="DataConfig"/> 的实例，并创建默认 SQL 配置。
    /// </summary>
    public DataConfig() => SqlOptions = new SqlOptions();
}