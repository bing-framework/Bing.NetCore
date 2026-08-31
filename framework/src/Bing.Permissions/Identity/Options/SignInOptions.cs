namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置登录前所需的用户联系方式确认条件。
/// </summary>
public class SignInOptions
{
    /// <summary>
    /// 获取或设置是否要求用户已确认电子邮件地址后才能登录，默认不要求。
    /// </summary>
    public bool ConfirmedEmail { get; set; }

    /// <summary>
    /// 获取或设置是否要求用户已确认手机号码后才能登录，默认不要求。
    /// </summary>
    public bool ConfirmedPhoneNumber { get; set; }
}