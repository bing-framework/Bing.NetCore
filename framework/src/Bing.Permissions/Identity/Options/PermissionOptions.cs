namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 聚合用户、密码、存储、登录和锁定相关的身份权限配置。
/// </summary>
public class PermissionOptions
{
    /// <summary>
    /// 获取或设置密码策略配置，默认初始化为 <see cref="PasswordOptions"/>。
    /// </summary>
    public PasswordOptions Password { get; set; } = new();

    /// <summary>
    /// 获取或设置用户策略配置，默认初始化为 <see cref="UserOptions"/>。
    /// </summary>
    public UserOptions User { get; set; } = new();

    /// <summary>
    /// 获取或设置身份数据存储配置，默认初始化为 <see cref="StoreOptions"/>。
    /// </summary>
    public StoreOptions Store { get; set; } = new();

    /// <summary>
    /// 获取或设置登录行为配置，默认初始化为 <see cref="SignInOptions"/>。
    /// </summary>
    public SignInOptions SignIn { get; set; } = new();

    /// <summary>
    /// 获取或设置登录失败锁定配置，默认初始化为 <see cref="LockoutOptions"/>。
    /// </summary>
    public LockoutOptions Lockout { get; set; } = new();
}
