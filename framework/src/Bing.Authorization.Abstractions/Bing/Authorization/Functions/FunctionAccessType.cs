using System.ComponentModel;

namespace Bing.Authorization.Functions;

/// <summary>
/// 指定功能允许访问的身份范围。
/// </summary>
public enum FunctionAccessType
{
    /// <summary>
    /// 匿名用户即可访问。
    /// </summary>
    [Description("匿名访问")] 
    Anonymous = 0,

    /// <summary>
    /// 仅已登录用户可访问。
    /// </summary>
    [Description("登录访问")] 
    LoggedIn = 1,

    /// <summary>
    /// 仅满足指定角色限制的用户可访问。
    /// </summary>
    [Description("角色访问")] 
    RoleLimit = 2
}
