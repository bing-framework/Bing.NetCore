namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置用户密码的长度、字符种类和唯一字符要求。
/// </summary>
public class PasswordOptions
{
    /// <summary>
    /// 获取或设置密码允许的最小字符数，默认值为 <c>1</c>。
    /// </summary>
    public int MinLength { get; set; } = 1;

    /// <summary>
    /// 获取或设置密码必须包含的最少不重复字符数，默认值为 <c>1</c>。
    /// </summary>
    public int UniqueChars { get; set; } = 1;

    /// <summary>
    /// 获取或设置密码是否必须包含非字母数字字符，默认不启用。
    /// </summary>
    public bool NonAlphanumeric { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含大写字母，默认不启用。
    /// </summary>
    public bool Uppercase { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含小写字母，默认不启用。
    /// </summary>
    public bool Lowercase { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含数字，默认不启用。
    /// </summary>
    public bool Digit { get; set; }
}