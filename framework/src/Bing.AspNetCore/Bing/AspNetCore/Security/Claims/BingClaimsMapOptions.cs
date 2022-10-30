using System;
using System.Collections.Generic;
using Bing.Security.Claims;

namespace Bing.AspNetCore.Security.Claims;

/// <summary>
/// 声明映射选项配置
/// </summary>
public class BingClaimsMapOptions
{
    /// <summary>
    /// 映射
    /// </summary>
    public Dictionary<string, Func<string>> Maps { get; private set; }

    /// <summary>
    /// 初始化一个<see cref="BingClaimsMapOptions"/>类型的实例
    /// </summary>
    public BingClaimsMapOptions()
    {
        Maps = new Dictionary<string, Func<string>>
        {
            {"sub", () => BingClaimTypes.UserId},
            {"role", () => BingClaimTypes.Role},
            {"email", () => BingClaimTypes.Email},
            {"name", () => BingClaimTypes.UserName},
        };
    }
}