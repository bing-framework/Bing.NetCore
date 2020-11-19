using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Pos.Systems.Extensions;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;
using Bing.Extensions;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 操作仓储
    /// </summary>
    public class OperationRepository : CompactRepositoryBase<Operation, ResourcePo>, IOperationRepository
    {
        /// <summary>
        /// 资源存储器
        /// </summary>
        private readonly IResourcePoStore _store;

        /// <summary>
        /// 初始化一个<see cref="OperationRepository" />类型的实例
        /// </summary>
        /// <param name="store">存储器</param>
        public OperationRepository(IResourcePoStore store) : base(store)
        {
            _store = store;
        }

        /// <summary>
        /// 将持久化对象转成实体
        /// </summary>
        /// <param name="po">持久化对象</param>
        protected override Operation ToEntity(ResourcePo po) => po.ToOperation();

        /// <summary>
        /// 将实体转换成持久化对象
        /// </summary>
        /// <param name="entity">实体</param>
        protected override ResourcePo ToPo(Operation entity) => entity.ToPo();

        /// <summary>
        /// 生成排序号
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        public async Task<int> GenerateSortIdAsync(Guid applicationId, Guid moduleId)
        {
            var maxSortId = await _store.GetMaxSortIdAsync(applicationId, moduleId);
            return maxSortId.SafeValue() + 1;
        }

        /// <summary>
        /// 是否允许创建操作
        /// </summary>
        /// <param name="operation">操作</param>
        public async Task<bool> CanCreateAsync(Operation operation)
        {
            var exists = await _store.ExistsAsync(x =>
                x.Uri == operation.Code
                && x.Type == ResourceType.Operation
                && x.ApplicationId == operation.ApplicationId
                && x.ParentId == operation.ModuleId);
            return exists == false;
        }

        /// <summary>
        /// 是否允许修改操作
        /// </summary>
        /// <param name="operation">操作</param>
        public async Task<bool> CanUpdateAsync(Operation operation)
        {
            var exists = await _store.ExistsAsync(x =>
                x.Id != operation.Id
                && x.Uri == operation.Code
                && x.Type == ResourceType.Operation
                && x.ApplicationId == operation.ApplicationId
                && x.ParentId == operation.ModuleId);
            return exists == false;
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        public async Task<List<Operation>> GetOperationsAsync(Guid moduleId)
        {
            var pos = await _store.FindAllAsync(x => x.ParentId == moduleId && x.Type == ResourceType.Operation);
            return pos.Select(ToEntity).ToList();
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleIds">角色标识列表</param>
        public async Task<List<Operation>> GetOperationsAsync(Guid applicationId, List<Guid> roleIds)
        {
            var pos = await _store.GetOperationsAsync(applicationId, roleIds);
            return pos.Select(ToEntity).ToList();
        }

        /// <summary>
        /// 获取所有操作列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        public async Task<List<Operation>> GetAllOperationsAsync(Guid applicationId)
        {
            var pos = await _store.GetOperationsAsync(applicationId);
            return pos.Select(ToEntity).ToList();
        }
    }
}
