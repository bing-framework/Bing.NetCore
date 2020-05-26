using System;
using Bing.Admin.Systems.Domain.Models;
using Bing.Domains.Services;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 用户管理
    /// </summary>
    public interface IUserManager : Bing.Permissions.Identity.Services.Abstractions.IUserManager<User, Guid>
    {
    }
}
