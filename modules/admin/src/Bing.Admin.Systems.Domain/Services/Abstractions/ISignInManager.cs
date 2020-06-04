using System;
using Bing.Admin.Systems.Domain.Models;
using Bing.Permissions.Identity.Services.Abstractions;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 登录管理
    /// </summary>
    public interface ISignInManager : ISignInManager<User, Guid>
    {
    }
}
