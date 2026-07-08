using Bing.DependencyInjection;

namespace Bing.Timing;

/// <summary>
/// 基于系统时钟的 <see cref="IClock"/> 实现（生产环境默认实现）。
/// 直接委托给 <see cref="DateTime"/> 和 <see cref="DateTimeOffset"/>。
/// </summary>
public class SystemClock : IClock, ISingletonDependency
{
    /// <summary>
    /// 系统时钟单例，可用于无 DI 场景
    /// </summary>
    public static readonly IClock Instance = new SystemClock();

    /// <inheritdoc />
    public DateTime Now => DateTime.Now;

    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset NowOffset => DateTimeOffset.Now;
}
