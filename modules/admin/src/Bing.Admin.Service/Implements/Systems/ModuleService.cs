using System;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Requests.Systems.Extensions;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Application.Services;
using Bing.Extensions;
using Bing.Mapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 模块服务
    /// </summary>
    public class ModuleService : AppServiceBase, IModuleService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 模块仓储
        /// </summary>
        protected IModuleRepository ModuleRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="ModuleService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="moduleRepository">模块仓储</param>
        public ModuleService(IAdminUnitOfWork unitOfWork, IModuleRepository moduleRepository)
        {
            UnitOfWork = unitOfWork;
            ModuleRepository = moduleRepository;
        }

        /// <summary>
        /// 创建模块
        /// </summary>
        /// <param name="request">请求</param>
        public async Task<Guid> CreateAsync(CreateModuleRequest request)
        {
            var module = request.ToModule();
            module.CheckNull(nameof(module));
            module.Init();
            var parent = await ModuleRepository.FindAsync(module.ParentId);
            module.InitPath(parent);
            module.SortId = await ModuleRepository.GenerateSortIdAsync(module.ApplicationId.SafeValue(), module.ParentId);
            await ModuleRepository.AddAsync(module);
            await UnitOfWork.CommitAsync();
            return module.Id;
        }

        /// <summary>
        /// 修改模块
        /// </summary>
        /// <param name="request">请求</param>
        public async Task UpdateAsync(UpdateModuleRequest request)
        {
            var module = await ModuleRepository.FindAsync(request.Id.ToGuid());
            request.MapTo(module);
            module.InitPinYin();
            await ModuleRepository.UpdatePathAsync(module);
            await ModuleRepository.UpdateAsync(module);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 删除模块
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        public async Task DeleteAsync(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return;
            var entities = await ModuleRepository.FindByIdsAsync(ids);
            if (entities?.Count == 0)
                return;
            await ModuleRepository.RemoveAsync(entities);
            await UnitOfWork.CommitAsync();
        }
    }
}
