using System;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Requests.Systems.Extensions;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Application.Services;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.ObjectMapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 操作服务
    /// </summary>
    public class OperationService : AppServiceBase, IOperationService
    {
        /// <summary>
        /// 初始化一个<see cref="OperationService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="operationRepository">操作仓储</param>
        /// <param name="moduleRepository">模块仓储</param>
        public OperationService(IAdminUnitOfWork unitOfWork, IOperationRepository operationRepository, IModuleRepository moduleRepository)
        {
            UnitOfWork = unitOfWork;
            OperationRepository = operationRepository;
            ModuleRepository = moduleRepository;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 操作仓储
        /// </summary>
        protected IOperationRepository OperationRepository { get; set; }

        /// <summary>
        /// 模块仓储
        /// </summary>
        protected IModuleRepository ModuleRepository { get; set; }

        /// <summary>
        /// 创建操作
        /// </summary>
        /// <param name="request">请求</param>
        public async Task<Guid> CreateAsync(CreateOperationRequest request)
        {
            var operation = request.ToOperation();
            await ValidateCreateAsync(operation);
            operation.Init();
            await OperationRepository.AddAsync(operation);
            await UnitOfWork.CommitAsync();
            return operation.Id;
        }

        /// <summary>
        /// 校验创建操作
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task ValidateCreateAsync(Operation entity)
        {
            entity.CheckNull(nameof(entity));
            if (!await OperationRepository.CanCreateAsync(entity))
                ThrowUriRepeatException(entity);
            if (!await ModuleRepository.ExistsAsync(entity.ModuleId))
                throw new Warning("模块不存在");
        }

        /// <summary>
        /// 爬出资源标识重复异常
        /// </summary>
        /// <param name="entity">实体</param>
        private void ThrowUriRepeatException(Operation entity) => throw new Warning($"操作编码 {entity.Code} 已存在");

        /// <summary>
        /// 修改操作
        /// </summary>
        /// <param name="request">请求</param>
        public async Task UpdateAsync(UpdateOperationRequest request)
        {
            var operation = await OperationRepository.FindAsync(request.Id.ToGuid());
            request.MapTo(operation);
            await ValidateUpdateAsync(operation);
            operation.InitPinYin();
            await OperationRepository.UpdateAsync(operation);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 校验修改操作
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task ValidateUpdateAsync(Operation entity)
        {
            entity.CheckNull(nameof(entity));
            if (!await OperationRepository.CanUpdateAsync(entity))
                ThrowUriRepeatException(entity);
            if (!await ModuleRepository.ExistsAsync(entity.ModuleId))
                throw new Warning("模块不存在");
        }

        /// <summary>
        /// 删除操作
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        public async Task DeleteAsync(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return;
            var entities = await OperationRepository.FindByIdsAsync(ids);
            if (entities?.Count == 0)
                return;
            await OperationRepository.RemoveAsync(entities);
            await UnitOfWork.CommitAsync();
        }
    }
}
