using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 服务集合(<see cref="IServiceCollection"/>) 扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册程序集中的所有公共的、非泛型的、非嵌套的类。
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assemblies">程序集列表</param>
        public static AutoRegisterData RegisterAssemblyPublicNonGenericClasses(this IServiceCollection services,
            params Assembly[] assemblies)
        {
            var allPublicTypes = assemblies.SelectMany(x => x.GetExportedTypes())
                .Where(x => x.IsClass && x.IsAbstract && !x.IsGenericType && !x.IsNested);
            return new AutoRegisterData(services, allPublicTypes);
        }
    }
}
