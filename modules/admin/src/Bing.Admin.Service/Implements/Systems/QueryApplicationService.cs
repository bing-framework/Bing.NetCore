using System;
using System.Threading.Tasks;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.NgAlain;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems.Extensions;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Application.Services;
using Bing.Datas.Queries;
using Bing.Domains.Repositories;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 应用程序 查询服务
    /// </summary>
    public class QueryApplicationService : QueryAppServiceBase<ApplicationPo, ApplicationDto, ApplicationQuery>, IQueryApplicationService
    {
        /// <summary>
        /// 应用程序存储器
        /// </summary>
        protected IApplicationPoStore ApplicationPoStore { get; set; }

        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="QueryApplicationService"/>类型的实例
        /// </summary>
        /// <param name="applicationPoStore">应用程序存储器</param>
        /// <param name="applicationRepository">应用程序仓储</param>
        public QueryApplicationService(IApplicationPoStore applicationPoStore
            , IApplicationRepository applicationRepository) : base(applicationPoStore)
        {
            ApplicationPoStore = applicationPoStore;
            ApplicationRepository = applicationRepository;
        }

        /// <summary>
        /// 转换为数据传输对象
        /// </summary>
        protected override ApplicationDto ToDto(ApplicationPo po) => po.ToDto();

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        protected override IQueryBase<ApplicationPo> CreateQuery(ApplicationQuery parameter)
        {
            return new Query<ApplicationPo>(parameter)
                .WhereIfNotEmpty(x => x.Code.Contains(parameter.Code))
                .WhereIfNotEmpty(x => x.Name.Contains(parameter.Name))
                .WhereIfNotEmpty(x => x.Remark.Contains(parameter.Remark));
        }

        /// <summary>
        /// 通过应用程序编码查找
        /// </summary>
        /// <param name="code">应用程序编码</param>
        public async Task<ApplicationDto> GetByCodeAsync(string code)
        {
            var application = await ApplicationPoStore.SingleAsync(x => x.Code == code);
            return application.ToDto();
        }

        /// <summary>
        /// 根据id查询应用程序
        /// </summary>
        /// <param name="id"></param>
        public async Task<AppInfo> GetByIdAsync(Guid id)
        {
            var appInfo = new AppInfo();
            var application = await ApplicationPoStore.SingleAsync(x => x.Id == id);
            if (application == null) return appInfo;

            appInfo.Id = application.Id.ToString();
            appInfo.Code = application.Code;
            appInfo.Name = application.Name;
            appInfo.Description = application.Remark;

            return appInfo;
        }
    }
}
