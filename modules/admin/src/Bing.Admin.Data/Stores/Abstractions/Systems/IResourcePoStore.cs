using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Data;

namespace Bing.Admin.Data.Stores.Abstractions.Systems
{
    /// <summary>
    /// 资源存储器
    /// </summary>
    public interface IResourcePoStore : IStore<ResourcePo>
    {
        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task<List<ResourcePo>> GetModulesAsync(Guid applicationId, List<Guid> roleIds);

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task<List<ResourcePo>> GetOperationsAsync(Guid applicationId, List<Guid> roleIds);

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        Task<List<ResourcePo>> GetModulesAsync(Guid applicationId);

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        Task<List<ResourcePo>> GetChildrenModuleAsync(Guid applicationId, Guid? moduleId);

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        Task<List<ResourcePo>> GetOperationsAsync(Guid applicationId);

        /// <summary>
        /// 获取最大排序号
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        Task<int?> GetMaxSortIdAsync(Guid applicationId, Guid? moduleId);
    }
}
