namespace Bing.Permissions.Identity.Options;

/// <summary>
/// 配置用户密码的长度、字符种类和唯一字符要求。
/// </summary>
public class PasswordOptions
{
    /// <summary>
    /// 获取或设置密码允许的最小字符数。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>1</c>。
    /// </remarks>
    public int MinLength { get; set; } = 1;

    /// <summary>
    /// 获取或设置密码必须包含的最少不重复字符数。
    /// </summary>
    /// <remarks>
    /// 默认值为 <c>1</c>。
    /// </remarks>
    public int UniqueChars { get; set; } = 1;

    /// <summary>
    /// 获取或设置密码是否必须包含非字母数字字符。
    /// </summary>
    /// <remarks>
    /// 默认不启用。
    /// </remarks>
    public bool NonAlphanumeric { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含大写字母。
    /// </summary>
    /// <remarks>
    /// 默认不启用。
    /// </remarks>
    public bool Uppercase { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含小写字母。
    /// </summary>
    /// <remarks>
    /// 默认不启用。
    /// </remarks>
    public bool Lowercase { get; set; }

    /// <summary>
    /// 获取或设置密码是否必须包含数字。
    /// </summary>
    /// <remarks>
    /// 默认不启用。
    /// </remarks>
    public bool Digit { get; set; }
}