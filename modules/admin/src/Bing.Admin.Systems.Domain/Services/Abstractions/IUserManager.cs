using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 用户管理
    /// </summary>
    public interface IUserManager : Bing.Permissions.Identity.Services.Abstractions.IUserManager<User, Guid>
    {
        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">标识列表</param>
        Task EnableAsync(List<Guid> ids);

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">标识列表</param>
        Task DisableAsync(List<Guid> ids);
    }
}
