namespace Bing.Permissions.Identity.Results;

/// <summary>
/// 表示用户登录处理的状态、用户标识和提示消息。
/// </summary>
public class SignInResult
{
    /// <summary>
    /// 获取或设置登录处理状态。
    /// </summary>
    public SignInState State { get; set; }

    /// <summary>
    /// 获取或设置登录成功时对应的用户标识。
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置登录处理的提示或错误消息。
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// 初始化一个登录失败的 <see cref="SignInResult"/> 实例。
    /// </summary>
    public SignInResult() => State = SignInState.Failed;

    /// <summary>
    /// 使用指定状态、用户标识和消息初始化一个 <see cref="SignInResult"/> 实例。
    /// </summary>
    /// <param name="state">登录处理状态。</param>
    /// <param name="userId">登录成功时对应的用户标识。</param>
    /// <param name="message">登录处理的提示或错误消息，可为空。</param>
    public SignInResult(SignInState state, string userId, string message = null)
    {
        State = state;
        UserId = userId;
        Message = message;
    }
}