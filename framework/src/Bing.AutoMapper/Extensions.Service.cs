using System;
using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Bing.Mapping;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IMapper = Bing.Mapping.IMapper;

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
        /// <param name="services">服务集合</param>
        public static void AddAutoMapper(this IServiceCollection services)
        {
            var typeFinder = services.GetOrAddTypeFinder<ITypeFinder>(assemblyFinder => new TypeFinder(assemblyFinder));
            var mapperConfigurations = typeFinder.Find<IOrderedMapperProfile>();
            var instances = mapperConfigurations.Select(mapperConfiguration =>
                    (IOrderedMapperProfile)Activator.CreateInstance(mapperConfiguration))
                .OrderBy(mapperConfiguration => mapperConfiguration.Order);
            var config = new MapperConfiguration(cfg =>
            {
                foreach (var instance in instances)
                {
                    Debug.WriteLine($"初始化AutoMapper配置：{instance.GetType().FullName}");
                    cfg.AddProfile(instance.GetType());
                }
            });
            AutoMapperConfiguration.Init(config);
            var mapper = new AutoMapperMapper();
            services.TryAddSingleton<IMapper>(mapper);
            MapperExtensions.SetMapper(mapper);
        }
    }
}
