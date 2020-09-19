using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Bing.Domain.Repositories;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 模块仓储
    /// </summary>
    public interface IModuleRepository : ITreeCompactRepository<Module>
    {
        /// <summary>
        /// 生成排序号
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="parentId">父标识</param>
        Task<int> GenerateSortIdAsync(Guid applicationId, Guid? parentId);

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task<List<Module>> GetModulesAsync(Guid applicationId, List<Guid> roleIds);

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        Task<List<Module>> GetModulesAsync(Guid applicationId);

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        Task<List<Module>> GetChildrenModuleAsync(Guid applicationId, Guid? moduleId);
    }
}
