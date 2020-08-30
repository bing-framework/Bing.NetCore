using System;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Mapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 应用程序 服务
    /// </summary>
    public class ApplicationService : Bing.Application.Services.AppServiceBase, IApplicationService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="ApplicationService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="applicationRepository">应用程序仓储</param>
        public ApplicationService( IAdminUnitOfWork unitOfWork, IApplicationRepository applicationRepository )
        {
            UnitOfWork = unitOfWork;
            ApplicationRepository = applicationRepository;
        }

        /// <summary>
        /// 创建应用程序
        /// </summary>
        /// <param name="request">请求</param>
        public async Task<Guid> CreateAsync(CreateApplicationRequest request)
        {
            var entity = request.MapTo<Admin.Systems.Domain.Models.Application>();
            await ValidateCreateAsync(entity);
            entity.Init();
            await ApplicationRepository.AddAsync(entity);
            await UnitOfWork.CommitAsync();
            return entity.Id;
        }

        /// <summary>
        /// 验证创建应用程序
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task ValidateCreateAsync(Admin.Systems.Domain.Models.Application entity)
        {
            entity.CheckNull(nameof(entity));
            if (await ApplicationRepository.CanCreateAsync(entity) == false)
                ThrowCodeRepeatException(entity);
        }

        /// <summary>
        /// 抛出编码重复异常
        /// </summary>
        /// <param name="entity">实体</param>
        private void ThrowCodeRepeatException(Admin.Systems.Domain.Models.Application entity) => throw new Warning($"应用程序编码 {entity.Code} 已存在");

        /// <summary>
        /// 修改应用程序
        /// </summary>
        /// <param name="request">request</param>
        public async Task UpdateAsync(UpdateApplicationRequest request)
        {
            var entity = await ApplicationRepository.FindAsync(request.Id.ToGuid());
            request.MapTo(entity);
            await ValidateUpdateAsync(entity);
            await ApplicationRepository.UpdateAsync(entity);
            await UnitOfWork.CommitAsync();

        }


        /// <summary>
        /// 验证修改应用程序
        /// </summary>
        /// <param name="entity">实体</param>
        private async Task ValidateUpdateAsync(Admin.Systems.Domain.Models.Application entity)
        {
            entity.CheckNull(nameof(entity));
            if (await ApplicationRepository.CanUpdateAsync(entity) == false)
                ThrowCodeRepeatException(entity);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        public async Task DeleteAsync(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return;
            var entities = await ApplicationRepository.FindByIdsAsync(ids);
            if (entities?.Count == 0)
                return;
            await ApplicationRepository.RemoveAsync(entities);
            await UnitOfWork.CommitAsync();
        }
    }
}
