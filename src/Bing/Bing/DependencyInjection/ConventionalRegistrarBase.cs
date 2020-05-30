using System;
using System.Linq;
using System.Reflection;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 通用注册器基类
    /// </summary>
    public abstract class ConventionalRegistrarBase : IConventionalRegistrar
    {
        /// <summary>
        /// 添加程序集
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assembly">程序集</param>
        public virtual void AddAssembly(IServiceCollection services, Assembly assembly)
        {
            var types = AssemblyHelper.GetAllTypes(assembly)
                .Where(type => type != null && type.IsClass && !type.IsAbstract && !type.IsGenericType)
                .ToArray();
            AddTypes(services, types);
        }

        /// <summary>
        /// 添加类型数组
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="types">类型数组</param>
        public void AddTypes(IServiceCollection services, params Type[] types)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 添加类型
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="type">类型</param>
        public abstract void AddType(IServiceCollection services, Type type);
    }
}
