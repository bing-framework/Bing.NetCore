using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Application.Services;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 模块查询服务
    /// </summary>
    public interface IQueryModuleService : ITreesAppService<ModuleDto, ResourceQuery>
    {
        /// <summary>
        /// 获取模块树型列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        Task<List<ModuleResponse>> GetModuleTreeAsync(Guid applicationId);

        /// <summary>
        /// 获取子模块列表
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="moduleId">模块标识</param>
        Task<List<ModuleResponse>> GetChildrenAsync(Guid applicationId, Guid? moduleId);
    }
}
