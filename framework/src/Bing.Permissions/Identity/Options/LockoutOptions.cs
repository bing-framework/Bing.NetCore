namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置登录失败后的用户锁定策略。
/// </summary>
public class LockoutOptions
{
    /// <summary>
    /// 获取或设置是否允许新创建的用户参与锁定策略，默认值为 <c>true</c>。
    /// </summary>
    public bool AllowedForNewUsers { get; set; } = true;

    /// <summary>
    /// 获取或设置触发锁定所需的连续登录失败次数，默认值为 <c>5</c> 次。
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>
    /// 获取或设置用户每次触发锁定后的锁定时长，默认值为 5 分钟。
    /// </summary>
    public TimeSpan LockoutTimeSpan { get; set; } = TimeSpan.FromMinutes(5);
}