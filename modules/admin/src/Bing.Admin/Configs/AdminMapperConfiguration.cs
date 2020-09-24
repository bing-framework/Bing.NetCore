using AutoMapper;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Mapping;

namespace Bing.Admin.Configs
{
    /// <summary>
    /// 后台映射器配置
    /// </summary>
    public class AdminMapperConfiguration : Profile, IOrderedMapperProfile
    {
        /// <summary>
        /// 排序
        /// </summary>
        public int Order => 0;

        /// <summary>
        /// 初始化一个<see cref="AdminMapperConfiguration"/>类型的实例
        /// </summary>
        public AdminMapperConfiguration()
        {
            CreateMap<AdministratorCreateRequest, UserParameter>();
        }
    }
}
