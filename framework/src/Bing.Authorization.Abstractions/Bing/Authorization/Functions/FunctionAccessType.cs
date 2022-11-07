using System.ComponentModel;

namespace Bing.Authorization.Functions;

/// <summary>
/// 功能访问类型
/// </summary>
public enum FunctionAccessType
{
    /// <summary>
    /// 匿名用户可访问
    /// </summary>
    [Description("匿名访问")] 
    Anonymous = 0,

    /// <summary>
    /// 登录用户可访问
    /// </summary>
    [Description("登录访问")] 
    LoggedIn = 1,

    /// <summary>
    /// 指定角色可访问
    /// </summary>
    [Description("角色访问")] 
    RoleLimit = 2
}
