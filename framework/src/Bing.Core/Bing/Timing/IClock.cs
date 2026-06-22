namespace Bing.Timing;

/// <summary>
/// 时钟抽象，提供对当前时间的可测试访问。
/// 在生产环境中使用 <see cref="SystemClock"/>；
/// 在测试中可注入 <c>FakeClock</c> 以精确控制时间，消除测试的非确定性。
/// </summary>
public interface IClock
{
    /// <summary>
    /// 获取当前本地时间
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// 获取当前 UTC 时间
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// 获取当前时间（带时区信息）
    /// </summary>
    DateTimeOffset NowOffset { get; }
}
