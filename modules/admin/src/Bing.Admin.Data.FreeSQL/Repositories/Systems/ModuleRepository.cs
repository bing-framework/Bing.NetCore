using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Pos.Systems.Extensions;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;
using Bing.Extensions;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 模块仓储
    /// </summary>
    public class ModuleRepository : TreeCompactRepositoryBase<Module, ResourcePo>, IModuleRepository
    {
        /// <summary>
        /// 资源存储器
        /// </summary>
        private readonly IResourcePoStore _store;

        /// <summary>
        /// 初始化一个<see cref="ModuleRepository" />类型的实例
        /// </summary>
        /// <param name="store">资源存储器</param>
        public ModuleRepository(IResourcePoStore store) : base(store)
        {
            _store = store;
        }

        /// <summary>
        /// 将持久化对象转成实体
        /// </summary>
        /// <param name="po">持久化对象</param>
        protected override Module ToEntity(ResourcePo po) => po.ToModule();

        /// <summary>
        /// 将实体转换成持久化对象
        /// </summary>
        /// <param name="entity">实体</param>
        protected override ResourcePo ToPo(Module entity) => entity.ToPo();

        /// <summary>
        /// 生成排序号
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="parentId">父标识</param>
        public async Task<int> GenerateSortIdAsync(Guid applicationId, Guid? parentId)
        {
            var maxSortId = await _store.GetMaxSortIdAsync(applicationId, parentId);
            return maxSortId.SafeValue() + 1;
        }

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task<List<Module>> GetModulesAsync(Guid applicationId, List<Guid> roleIds)
        {
            var pos = await _store.GetModulesAsync(applicationId, roleIds);
            return pos.Select(ToEntity).ToList();
        }

        /// <summary>
        /// 获取模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        public async Task<List<Module>> GetModulesAsync(Guid applicationId)
        {
            var pos = await _store.GetModulesAsync(applicationId);
            return pos.Select(ToEntity).ToList();
        }

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        public async Task<List<Module>> GetChildrenModuleAsync(Guid applicationId, Guid? moduleId)
        {
            var pos = await _store.GetChildrenModuleAsync(applicationId, moduleId);
            return pos.Select(ToEntity).ToList();
        }
    }
}
