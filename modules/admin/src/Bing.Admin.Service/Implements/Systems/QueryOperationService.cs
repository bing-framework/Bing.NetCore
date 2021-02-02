using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Application.Services;
using Bing.ObjectMapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 操作查询服务
    /// </summary>
    public class QueryOperationService : AppServiceBase, IQueryOperationService
    {
        /// <summary>
        /// 初始化一个<see cref="QueryOperationService"/>类型的实例
        /// </summary>
        /// <param name="resourcePoStore">资源存储器</param>
        /// <param name="operationRepository">操作仓储</param>
        public QueryOperationService(IResourcePoStore resourcePoStore, IOperationRepository operationRepository)
        {
            ResourcePoStore = resourcePoStore;
            OperationRepository = operationRepository;
        }

        /// <summary>
        /// 资源存储器
        /// </summary>
        protected IResourcePoStore ResourcePoStore { get; set; }

        /// <summary>
        /// 操作仓储
        /// </summary>
        protected IOperationRepository OperationRepository { get; set; }

        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">操作标识</param>
        public async Task<OperationDto> GetByIdAsync(Guid id)
        {
            var entity = await OperationRepository.FindAsync(id);
            return entity.MapTo<OperationDto>();
        }

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        public async Task<List<OperationDto>> GetOperationsAsync(Guid moduleId)
        {
            var entities = await OperationRepository.GetOperationsAsync(moduleId);
            return entities.MapToList<OperationDto>();
        }
    }
}
