using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Systems.Domain.Models;
using Bing.Data.Sql;
using Bing.Data.Stores;

namespace Bing.Admin.Data.Stores.Implements.Systems
{
    /// <summary>
    /// 资源存储器
    /// </summary>
    public class ResourcePoStore : StoreBase<ResourcePo>, IResourcePoStore
    {
        /// <summary>
        /// 初始化一个<see cref="ResourcePoStore"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ResourcePoStore(IAdminUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task<List<ResourcePo>> GetModulesAsync(Guid applicationId, List<Guid> roleIds)
        {
            if (applicationId == Guid.Empty || roleIds == null || roleIds.Count == 0)
                return new List<ResourcePo>();
            var result = await Sql
                .Select<ResourcePo>(true)
                .From<ResourcePo>("a")
                .Join<Permission>("b")
                .On<ResourcePo, Permission>((l, r) => l.Id == r.ResourceId)
                .Where<ResourcePo>(x => x.Type == ResourceType.Module && x.ApplicationId == applicationId && x.Enabled)
                .Where<Permission>(x => roleIds.Contains(x.RoleId) && x.IsDeny == false).ToListAsync<ResourcePo>();
            return result.Distinct<ResourcePo>().OrderBy(x => x.SortId).ToList();
        }

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        public async Task<List<ResourcePo>> GetModulesAsync(Guid applicationId)
        {
            if (applicationId == Guid.Empty)
                return new List<ResourcePo>();
            var result = await Sql
                .Select<ResourcePo>(true)
                .From<ResourcePo>("a")
                .Where<ResourcePo>(x => x.Type == ResourceType.Module && x.ApplicationId == applicationId)
                .ToListAsync<ResourcePo>();
            return result;
        }

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        public async Task<List<ResourcePo>> GetChildrenModuleAsync(Guid applicationId, Guid? moduleId)
        {
            if (moduleId == Guid.Empty)
                return new List<ResourcePo>();
            var result = await Sql
                .Select<ResourcePo>(true)
                .From<ResourcePo>("a")
                .Where<ResourcePo>(x => x.Type == ResourceType.Module && x.ApplicationId == applicationId && x.ParentId == moduleId)
                .ToListAsync<ResourcePo>();
            return result;
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task<List<ResourcePo>> GetOperationsAsync(Guid applicationId, List<Guid> roleIds)
        {
            if (applicationId == Guid.Empty || roleIds == null || roleIds.Count == 0)
                return new List<ResourcePo>();
            var result = await Sql
                .Select<ResourcePo>(true)
                .From<ResourcePo>("a")
                .Join<Permission>("b")
                .On<ResourcePo, Permission>((l, r) => l.Id == r.ResourceId)
                .Where<ResourcePo>(x =>
                    x.Type == ResourceType.Operation && x.ApplicationId == applicationId && x.Enabled)
                .Where<Permission>(x => roleIds.Contains(x.RoleId) && x.IsDeny == false).ToListAsync<ResourcePo>();
            return result.Distinct<ResourcePo>().OrderBy(x => x.SortId).ToList();
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        public async Task<List<ResourcePo>> GetOperationsAsync(Guid applicationId)
        {
            if (applicationId == Guid.Empty)
                return new List<ResourcePo>();
            var result = await Sql
                .Select<ResourcePo>(true)
                .From<ResourcePo>("a")
                .Where<ResourcePo>(x => x.Type == ResourceType.Operation && x.ApplicationId == applicationId)
                .ToListAsync<ResourcePo>();
            return result;
        }
    }
}
