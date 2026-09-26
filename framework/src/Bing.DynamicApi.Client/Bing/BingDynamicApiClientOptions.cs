using System.Text.Json;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 动态 API 客户端的远程服务配置集合。
/// </summary>
public class BingDynamicApiClientOptions
{
    /// <summary>
    /// 获取远程服务配置。
    /// </summary>
    public IDictionary<string, BingDynamicApiRemoteOptions> RemoteServices { get; } =
        new Dictionary<string, BingDynamicApiRemoteOptions>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取或设置 JSON 序列化选项。
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = CreateDefaultJsonOptions();

    /// <summary>
    /// 添加或配置一个具名远程服务。
    /// </summary>
    /// <param name="name">远程服务名称。</param>
    /// <param name="configure">远程服务配置委托。</param>
    public void AddRemoteService(string name, Action<BingDynamicApiRemoteOptions> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("远程服务名称不能为空。", nameof(name));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        if (!RemoteServices.TryGetValue(name, out var remoteOptions))
        {
            remoteOptions = new BingDynamicApiRemoteOptions();
            RemoteServices.Add(name, remoteOptions);
        }

        configure(remoteOptions);
    }

    /// <summary>
    /// 获取并校验远程服务配置。
    /// </summary>
    /// <param name="name">远程服务名称。</param>
    /// <returns>已配置且通过校验的远程服务选项。</returns>
    /// <exception cref="InvalidOperationException">服务未配置或选项无效。</exception>
    internal BingDynamicApiRemoteOptions GetRemoteService(string name)
    {
        if (!RemoteServices.TryGetValue(name, out var options))
            throw new InvalidOperationException($"未配置动态 API 远程服务 '{name}'。请先调用 AddRemoteService 配置 BaseUrl 和 ApiVersion。");
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"动态 API 远程服务 '{name}' 的 BaseUrl 必须是绝对 URI。");
        if (string.IsNullOrWhiteSpace(options.ApiVersion))
            throw new InvalidOperationException($"动态 API 远程服务 '{name}' 未配置 ApiVersion。");
        if (options.MaxRetries < 0)
            throw new InvalidOperationException($"动态 API 远程服务 '{name}' 的 MaxRetries 不能小于 0。");

        return options;
    }

    /// <summary>
    /// 创建默认 JSON 序列化选项。
    /// </summary>
    /// <returns>使用驼峰命名且不区分属性名大小写的选项。</returns>
    private static JsonSerializerOptions CreateDefaultJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}

/// <summary>
/// 单个动态 API 远程服务的连接与传输配置。
/// </summary>
public class BingDynamicApiRemoteOptions
{
    /// <summary>
    /// 获取或设置远程服务基础地址。
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 API 版本。
    /// </summary>
    public string ApiVersion { get; set; } = "v1";

    /// <summary>
    /// 获取或设置租户标识请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// 获取或设置关联标识请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-Id";

    /// <summary>
    /// 获取或设置语言请求头名称。
    /// </summary>
    /// <remarks>设为 null 可禁用透传。</remarks>
    public string LanguageHeaderName { get; set; } = "Accept-Language";

    /// <summary>
    /// 获取静态请求头集合。
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取或设置最大重试次数。
    /// </summary>
    /// <remarks>仅对允许重放的 GET 请求生效，默认为零，即关闭重试。</remarks>
    public int MaxRetries { get; set; }

    /// <summary>
    /// 获取或设置 GET 请求重试间隔。
    /// </summary>
    /// <remarks>默认间隔为 100 毫秒。</remarks>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
