namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置身份数据存储的键长度、个人数据保护和密码保存策略。
/// </summary>
public class StoreOptions
{
    /// <summary>
    /// 获取或设置身份存储键允许的最大长度。
    /// </summary>
    public int MaxLengthForKeys { get; set; }

    /// <summary>
    /// 获取或设置是否加密存储标记为 <c>ProtectPersonalData</c> 的用户数据，默认不启用。
    /// </summary>
    public bool ProtectPersonalData { get; set; }

    /// <summary>
    /// 获取或设置是否保存原始密码，默认不启用；启用会造成严重的敏感凭据泄露风险。
    /// </summary>
    public bool StoreOriginalPassword { get; set; }
}