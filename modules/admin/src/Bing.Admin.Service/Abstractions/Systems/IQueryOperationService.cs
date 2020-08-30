using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Application.Services;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 操作查询服务
    /// </summary>
    public interface IQueryOperationService : IAppService
    {
        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">操作标识</param>
        Task<OperationDto> GetByIdAsync(Guid id);

        /// <summary>
        /// 获取操作列表
        /// </summary>
        /// <param name="moduleId">模块标识</param>
        Task<List<OperationDto>> GetOperationsAsync(Guid moduleId);
    }
}
