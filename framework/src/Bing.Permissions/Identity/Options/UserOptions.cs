namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置用户名格式和电子邮件唯一性策略。
/// </summary>
public class UserOptions
{
    /// <summary>
    /// 获取或设置用户名允许使用的字符集合，默认包含大小写英文字母、数字及 <c>-._@+</c>。
    /// </summary>
    public string UserNameCharacters { get; set; } = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    /// <summary>
    /// 获取或设置是否要求用户电子邮件地址唯一，默认不启用。
    /// </summary>
    public bool UniqueEmail { get; set; }
}