using Bing.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AutoMapper
{
    /// <summary>
    /// 对象映射扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册AutoMapper对象映射操作
        /// </summary>
        /// <param name="service">服务集合</param>
        public static void AddAutoMapper(this IServiceCollection service)
        {
            var mapper = new AutoMapperMapper();
            service.TryAddSingleton<IMapper>(mapper);
            MapperExtensions.SetMapper(mapper);
        }
    }
}
