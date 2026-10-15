using Microsoft.Extensions.Configuration;

namespace Bing.AspNetCore.Hosting;

/// <summary>
/// 表示当前宿主承担的运行角色。
/// </summary>
public enum BingHostRole
{
    /// <summary>
    /// 提供 HTTP 请求处理。
    /// </summary>
    Web,

    /// <summary>
    /// 处理 CAP 消息。
    /// </summary>
    Consumer,

    /// <summary>
    /// 执行后台任务。
    /// </summary>
    Task
}

/// <summary>
/// 提供宿主角色配置读取方法。
/// </summary>
public static class BingHostRoleExtensions
{
    /// <summary>
    /// 宿主角色配置键。
    /// </summary>
    public const string ConfigurationKey = "BING_HOST_ROLE";

    /// <summary>
    /// 读取当前宿主角色。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <returns>配置的宿主角色；未配置时返回 <see cref="BingHostRole.Web"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> 为空时抛出。</exception>
    /// <exception cref="InvalidOperationException">配置值不是支持的角色时抛出。</exception>
    public static BingHostRole GetBingHostRole(this IConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
            return BingHostRole.Web;
        if (string.Equals(value, "web", StringComparison.OrdinalIgnoreCase))
            return BingHostRole.Web;
        if (string.Equals(value, "consumer", StringComparison.OrdinalIgnoreCase))
            return BingHostRole.Consumer;
        if (string.Equals(value, "task", StringComparison.OrdinalIgnoreCase))
            return BingHostRole.Task;
        throw new InvalidOperationException($"{ConfigurationKey} 必须为 web、consumer 或 task。实际值无效。");
    }

    /// <summary>
    /// 判断当前宿主是否为 CAP 消费角色。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <returns>当前角色为 <see cref="BingHostRole.Consumer"/> 时返回 <see langword="true"/>。</returns>
    public static bool IsBingConsumerRole(this IConfiguration configuration) =>
        configuration.GetBingHostRole() == BingHostRole.Consumer;

    /// <summary>
    /// 判断当前宿主是否承担后台角色。
    /// </summary>
    /// <param name="configuration">宿主配置。</param>
    /// <returns>当前角色为 <see cref="BingHostRole.Consumer"/> 或 <see cref="BingHostRole.Task"/> 时返回 <see langword="true"/>。</returns>
    public static bool IsBingBackgroundRole(this IConfiguration configuration)
    {
        var role = configuration.GetBingHostRole();
        return role == BingHostRole.Consumer || role == BingHostRole.Task;
    }
}
