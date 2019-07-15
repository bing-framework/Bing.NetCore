using System;
using System.Collections.Generic;
using Bing.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Modularity
{
    /// <summary>
    /// 服务配置上下文
    /// </summary>
    public class ServiceConfigurationContext
    {
        /// <summary>
        /// 服务集合
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// 项字典
        /// </summary>
        public IDictionary<string,object> Items { get; }

        /// <summary>
        /// 服务配置
        /// </summary>
        /// <param name="key">键</param>
        public object this[string key]
        {
            get => Items.GetOrDefault(key);
            set => Items[key] = value;
        }

        /// <summary>
        /// 初始化一个<see cref="ServiceConfigurationContext"/>类型的实例
        /// </summary>
        /// <param name="services">服务集合</param>
        public ServiceConfigurationContext(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Items = new Dictionary<string, object>();
        }
    }
}
