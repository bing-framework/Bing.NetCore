namespace Bing.Permissions.Identity.Results;

/// <summary>
/// 表示登录操作的处理结果。
/// </summary>
public enum SignInState
{
    /// <summary>
    /// 登录凭据验证成功。
    /// </summary>
    Succeeded,

    /// <summary>
    /// 登录失败。
    /// </summary>
    Failed,

    /// <summary>
    /// 登录已进入需要第二因素验证的阶段。
    /// </summary>
    TwoFactor
}