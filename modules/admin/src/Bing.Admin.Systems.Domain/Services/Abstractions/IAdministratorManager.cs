using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Domain.Services;

namespace Bing.Admin.Systems.Domain.Services.Abstractions
{
    /// <summary>
    /// 管理员管理
    /// </summary>
    public interface IAdministratorManager : IDomainService
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="parameter">参数</param>
        Task<User> CreateAsync(UserParameter parameter);

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="parameter">参数</param>
        Task UpdateAsync(UserParameter parameter);

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        Task EnableAsync(List<Guid> ids);

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        Task DisableAsync(List<Guid> ids);
    }
}
