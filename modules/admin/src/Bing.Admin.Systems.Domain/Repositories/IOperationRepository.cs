using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Bing.Domains.Repositories;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 操作仓储
    /// </summary>
    public interface IOperationRepository : ICompactRepository<Operation>
    {
        /// <summary>
        /// 生成排序号
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        Task<int> GenerateSortIdAsync(Guid applicationId, Guid moduleId);

        /// <summary>
        /// 是否允许创建操作
        /// </summary>
        /// <param name="operation">操作</param>
        Task<bool> CanCreateAsync(Operation operation);

        /// <summary>
        /// 是否允许修改操作
        /// </summary>
        /// <param name="operation">操作</param>
        Task<bool> CanUpdateAsync(Operation operation);

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        Task<List<Operation>> GetOperationsAsync(Guid moduleId);

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        Task<List<Operation>> GetOperationsAsync(Guid applicationId, List<Guid> roleIds);

        /// <summary>
        /// 获取所有操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        Task<List<Operation>> GetAllOperationsAsync(Guid applicationId);
    }
}
