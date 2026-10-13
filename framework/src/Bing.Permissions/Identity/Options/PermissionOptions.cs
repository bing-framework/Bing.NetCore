namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 聚合用户、密码、存储、登录和锁定相关的身份权限配置。
/// </summary>
public class PermissionOptions
{
    /// <summary>
    /// 获取或设置密码策略配置。
    /// </summary>
    /// <remarks>
    /// 默认初始化为 <see cref="PasswordOptions"/>。
    /// </remarks>
    public PasswordOptions Password { get; set; } = new();

    /// <summary>
    /// 获取或设置用户策略配置。
    /// </summary>
    /// <remarks>
    /// 默认初始化为 <see cref="UserOptions"/>。
    /// </remarks>
    public UserOptions User { get; set; } = new();

    /// <summary>
    /// 获取或设置身份数据存储配置。
    /// </summary>
    /// <remarks>
    /// 默认初始化为 <see cref="StoreOptions"/>。
    /// </remarks>
    public StoreOptions Store { get; set; } = new();

    /// <summary>
    /// 获取或设置登录行为配置。
    /// </summary>
    /// <remarks>
    /// 默认初始化为 <see cref="SignInOptions"/>。
    /// </remarks>
    public SignInOptions SignIn { get; set; } = new();

    /// <summary>
    /// 获取或设置登录失败锁定配置。
    /// </summary>
    /// <remarks>
    /// 默认初始化为 <see cref="LockoutOptions"/>。
    /// </remarks>
    public LockoutOptions Lockout { get; set; } = new();
}
