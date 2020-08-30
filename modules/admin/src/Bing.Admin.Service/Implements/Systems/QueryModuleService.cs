using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems.Extensions;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Application.Services;
using Bing.Datas.Queries;
using Bing.Domains.Repositories;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 模块查询服务
    /// </summary>
    public class QueryModuleService : TreesAppServiceBase<ResourcePo, ModuleDto, ResourceQuery>, IQueryModuleService
    {
        /// <summary>
        /// 初始化一个<see cref="QueryModuleService" />类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="resourcePoStore">资源存储器</param>
        /// <param name="moduleRepository">模块仓储</param>
        public QueryModuleService(IAdminUnitOfWork unitOfWork
            , IResourcePoStore resourcePoStore
            , IModuleRepository moduleRepository)
            : base(unitOfWork, resourcePoStore)
        {
            UnitOfWork = unitOfWork;
            ResourcePoStore = resourcePoStore;
            ModuleRepository = moduleRepository;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 资源存储器
        /// </summary>
        protected IResourcePoStore ResourcePoStore { get; set; }

        /// <summary>
        /// 模块仓储
        /// </summary>
        protected IModuleRepository ModuleRepository { get; set; }

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        protected override IQueryBase<ResourcePo> CreateQuery(ResourceQuery parameter)
        {
            return new Query<ResourcePo>(parameter)
                .Where(t => t.Type == ResourceType.Module)
                .Where(t => t.ApplicationId == parameter.ApplicationId)
                .WhereIfNotEmpty(t => t.Name.Contains(parameter.Name))
                .WhereIfNotEmpty(t => t.Uri.Contains(parameter.Uri));
        }

        /// <summary>
        /// 转换为数据传输对象
        /// </summary>
        /// <param name="po">资源持久化对象</param>
        protected override ModuleDto ToDto(ResourcePo po) => po.ToModuleDto();

        /// <summary>
        /// 获取模块树型列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        public async Task<List<ModuleResponse>> GetModuleTreeAsync(Guid applicationId)
        {
            var modules = await ModuleRepository.GetModulesAsync(applicationId);
            return new ModuleResult(modules).GetResult();
        }

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        public async Task<List<ModuleResponse>> GetChildrenAsync(Guid applicationId, Guid? moduleId)
        {
            var modules = await ModuleRepository.GetChildrenModuleAsync(applicationId, moduleId);
            return new ModuleResult(modules).GetResult();
        }
    }
}
