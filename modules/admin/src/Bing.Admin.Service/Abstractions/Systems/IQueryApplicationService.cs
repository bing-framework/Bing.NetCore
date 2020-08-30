using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Dtos.NgAlain;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Application.Services;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 应用程序 查询服务
    /// </summary>
    public interface IQueryApplicationService : IQueryAppService<ApplicationDto, ApplicationQuery>
    {
        /// <summary>
        /// 通过应用程序编码查找
        /// </summary>
        /// <param name="code">应用程序编码</param>
        Task<ApplicationDto> GetByCodeAsync(string code);

        /// <summary>
        /// 根据id查询应用程序
        /// </summary>
        /// <param name="id"></param>
        Task<AppInfo> GetByIdAsync(Guid id);
    }
}
