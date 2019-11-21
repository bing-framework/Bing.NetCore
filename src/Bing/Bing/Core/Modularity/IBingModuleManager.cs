using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity
{
    /// <summary>
    /// Bing 模块管理器
    /// </summary>
    public interface IBingModuleManager
    {
        /// <summary>
        /// 自动检索到的所有模块信息
        /// </summary>
        IEnumerable<BingModule> SourceModules { get; }

        /// <summary>
        /// 最终加载的模块信息集合
        /// </summary>
        IEnumerable<BingModule> LoadedModules { get; }

        /// <summary>
        /// 加载模块服务
        /// </summary>
        /// <param name="services">服务集合</param>
        IServiceCollection LoadModules(IServiceCollection services);

        /// <summary>
        /// 应用模块服务
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        void UseModule(IServiceProvider provider);
    }
}
