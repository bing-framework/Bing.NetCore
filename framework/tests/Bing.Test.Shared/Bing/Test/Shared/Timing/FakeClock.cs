using Bing.Timing;

namespace Bing.Test.Shared.Timing;

/// <summary>
/// 测试专用伪时钟。
/// </summary>
/// <remarks>
/// 通过固定时间或手动推进时间，消除测试中的非确定性。
/// <code>
///   var clock = new FakeClock(new DateTime(2025, 1, 1, 0, 0, 0));
///   // 验证固定时间点
///   Assert.Equal(new DateTime(2025, 1, 1), clock.Now);
///   // 推进时间
///   clock.Advance(TimeSpan.FromHours(1));
///   Assert.Equal(new DateTime(2025, 1, 1, 1, 0, 0), clock.Now);
/// </code>
/// </remarks>
public class FakeClock : IClock
{
    /// <summary>
    /// 当前固定时间。
    /// </summary>
    private DateTime _currentTime;

    /// <summary>
    /// 初始化一个 <see cref="FakeClock"/> 类型的实例。
    /// </summary>
    /// <param name="fixedTime">固定本地时间。</param>
    public FakeClock(DateTime fixedTime)
    {
        _currentTime = fixedTime;
    }

    /// <summary>
    /// 初始化一个 <see cref="FakeClock"/> 类型的实例。
    /// </summary>
    public FakeClock() : this(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local))
    {
    }

    /// <inheritdoc />
    public DateTime Now => _currentTime;

    /// <inheritdoc />
    public DateTime UtcNow => _currentTime.ToUniversalTime();

    /// <inheritdoc />
    public DateTimeOffset NowOffset => new DateTimeOffset(_currentTime);

    /// <summary>
    /// 推进当前时间。
    /// </summary>
    /// <param name="timeSpan">要推进的时长。</param>
    /// <exception cref="ArgumentOutOfRangeException">timeSpan 为负值时抛出。</exception>
    public void Advance(TimeSpan timeSpan)
    {
        if (timeSpan < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeSpan), "推进时长不能为负值。");
        _currentTime = _currentTime.Add(timeSpan);
    }

    /// <summary>
    /// 设置当前时间。
    /// </summary>
    /// <param name="newTime">新的时间点。</param>
    public void Set(DateTime newTime) => _currentTime = newTime;
}
