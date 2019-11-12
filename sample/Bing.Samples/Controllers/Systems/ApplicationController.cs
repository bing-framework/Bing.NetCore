using Bing.Samples.Service.Abstractions.Systems;
using Bing.Samples.Service.Dtos.Systems;
using Bing.Samples.Service.Queries.Systems;
using Bing.Utils;
using Bing.Webs.Controllers;

namespace Bing.Samples.Controllers.Systems
{
    /// <summary>
    /// 应用程序控制器
    /// </summary>
    public class ApplicationController : CrudControllerBase<ApplicationDto, ApplicationQuery>
    {
        /// <summary>
        /// 初始化应用程序控制器
        /// </summary>
        /// <param name="service">应用程序服务</param>
        public ApplicationController(IApplicationService service) : base(service)
        {
        }

        /// <summary>
        /// 将Dto转换为列表项
        /// </summary>
        protected override Item ToItem(ApplicationDto dto)
        {
            return new Item(dto.Name, dto.Id);
        }
    }
}
