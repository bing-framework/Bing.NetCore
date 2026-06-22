using Bing.Timing;

namespace Bing.Test.Shared.Timing;

/// <summary>
/// 测试专用伪时钟，提供确定性的时间控制。
/// 通过固定时间或手动推进时间，消除测试中的非确定性。
/// 用法示例：
/// <code>
///   var clock = new FakeClock(new DateTime(2025, 1, 1, 0, 0, 0));
///   // 验证固定时间点
///   Assert.Equal(new DateTime(2025, 1, 1), clock.Now);
///   // 推进时间
///   clock.Advance(TimeSpan.FromHours(1));
///   Assert.Equal(new DateTime(2025, 1, 1, 1, 0, 0), clock.Now);
/// </code>
/// </summary>
public class FakeClock : IClock
{
    private DateTime _currentTime;

    /// <summary>
    /// 使用指定的固定本地时间初始化
    /// </summary>
    /// <param name="fixedTime">固定时间（本地时间）</param>
    public FakeClock(DateTime fixedTime)
    {
        _currentTime = fixedTime;
    }

    /// <summary>
    /// 使用默认时间（2000-01-01 00:00:00）初始化，便于无参测试场景
    /// </summary>
    public FakeClock() : this(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local))
    {
    }

    /// <summary>
    /// 获取当前本地时间（固定值）
    /// </summary>
    public DateTime Now => _currentTime;

    /// <summary>
    /// 获取当前 UTC 时间（固定值）
    /// </summary>
    public DateTime UtcNow => _currentTime.ToUniversalTime();

    /// <summary>
    /// 获取当前时间（带时区信息）
    /// </summary>
    public DateTimeOffset NowOffset => new DateTimeOffset(_currentTime);

    /// <summary>
    /// 将当前时间向前推进指定时长
    /// </summary>
    /// <param name="timeSpan">要推进的时长（必须为正值）</param>
    /// <exception cref="ArgumentOutOfRangeException">timeSpan 为负值时抛出</exception>
    public void Advance(TimeSpan timeSpan)
    {
        if (timeSpan < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeSpan), "推进时长不能为负值。");
        _currentTime = _currentTime.Add(timeSpan);
    }

    /// <summary>
    /// 将当前时间直接设置为指定时间
    /// </summary>
    /// <param name="newTime">新的时间点</param>
    public void Set(DateTime newTime) => _currentTime = newTime;
}
