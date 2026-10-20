using System.Collections.Generic;
using System.Security.Claims;
using Bing.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 存储测试使用的用户声明。
/// </summary>
public class FakeUserClaims : ISingletonDependency
{
    /// <summary>
    /// 获取用户声明列表。
    /// </summary>
    public List<Claim> Claims { get; } = new List<Claim>();
}
