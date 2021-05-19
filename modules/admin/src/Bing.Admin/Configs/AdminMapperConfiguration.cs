using AutoMapper;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.ObjectMapping;

namespace Bing.Admin.Configs
{
    /// <summary>
    /// 后台映射器配置
    /// </summary>
    public class AdminMapperConfiguration : Profile, IObjectMapperProfile
    {
        /// <summary>
        /// 创建映射配置
        /// </summary>
        public void CreateMap()
        {
            CreateMap<AdministratorCreateRequest, UserParameter>();
        }
    }
}
