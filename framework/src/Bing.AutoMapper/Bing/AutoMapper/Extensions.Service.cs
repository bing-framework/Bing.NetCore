using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Bing.ObjectMapping;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IObjectMapper = Bing.ObjectMapping.IObjectMapper;

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
            var mapperProfileTypeFinder = services.GetOrAddTypeFinder<IMapperProfileTypeFinder>(assemblyFinder => new MapperProfileTypeFinder(assemblyFinder));
            var instances = mapperProfileTypeFinder
                .FindAll()
                .Select(type => Reflections.CreateInstance<IObjectMapperProfile>(type))
                .ToList();
            var configuration = new MapperConfiguration(cfg =>
            {
                foreach (var instance in instances)
                {
                    
                    Debug.WriteLine($"初始化AutoMapper配置：{instance.GetType().FullName}");
                    instance.CreateMap();
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    cfg.AddProfile(instance as Profile);
                }
            });
            var mapper = new AutoMapperObjectMapper(configuration);
            ObjectMapperExtensions.SetMapper(mapper);
            services.TryAddSingleton<IObjectMapper>(mapper);
        }
    }
}
