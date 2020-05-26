using System;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 角色管理
    /// </summary>
    public interface IRoleManager : Bing.Permissions.Identity.Services.Abstractions.IRoleManager<Role, Guid, Guid?>
    {
    }
}
