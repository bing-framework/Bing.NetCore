using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Core.Modularity;
using Bing.Core.Options;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Builders
{
    /// <summary>
    /// Bing 构建器
    /// </summary>
    public class BingBuilder : IBingBuilder
    {
        #region 字段

        /// <summary>
        /// 源
        /// </summary>
        private readonly List<BingModule> _source;

        /// <summary>
        /// 模块集合
        /// </summary>
        private List<BingModule> _modules;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="BingBuilder"/>类型的实例
        /// </summary>
        /// <param name="services">服务集合</param>
        public BingBuilder(IServiceCollection services)
        {
            Services = services;
            _source = GetAllModules(services);
            _modules = new List<BingModule>();
        }

        /// <summary>
        /// 获取所有模块
        /// </summary>
        /// <param name="services">服务集合</param>
        private static List<BingModule> GetAllModules(IServiceCollection services)
        {
            var moduleTypeFinder = services.GetOrAddTypeFinder<IBingModuleTypeFinder>(assemblyFinder => new BingModuleTypeFinder(assemblyFinder));
            var moduleTypes = moduleTypeFinder.FindAll();
            return moduleTypes.Select(m => (BingModule)Activator.CreateInstance(m))
                .OrderBy(m => m.Level)
                .ThenBy(m => m.Order)
                .ThenBy(m => m.GetType().FullName)
                .ToList();
        }

        #endregion

        #region 属性

        /// <summary>
        /// 服务集合
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// 加载的模块集合
        /// </summary>
        public IEnumerable<BingModule> Modules => _modules;

        /// <summary>
        /// Bing 选项配置委托
        /// </summary>
        public Action<BingOptions> OptionsAction { get; private set; }

        #endregion

        #region AddModule(添加模块)

        /// <summary>
        /// 添加指定模块。执行此功能后将仅加载指定的模块
        /// </summary>
        /// <typeparam name="TModule">要添加的模块类型</typeparam>
        public IBingBuilder AddModule<TModule>() where TModule : BingModule => AddModule(typeof(TModule));

        /// <summary>
        /// 添加模块
        /// </summary>
        /// <param name="type">类型</param>
        private IBingBuilder AddModule(Type type)
        {
            if (!type.IsBaseOn(typeof(BingModule)))
                throw new Warning($"要加载的模块类型“{type}”不派生于基类 {nameof(BingModule)}");
            if (_modules.Any(m => m.GetType() == type))
                return this;

            var tmpModules = new BingModule[_modules.Count];
            _modules.CopyTo(tmpModules);
            var module = _source.FirstOrDefault(x => x.GetType() == type);
            if (module == null)
                throw new Warning($"类型为“{type.FullName}”的模块实例无法找到");
            _modules.AddIfNotContains(module);

            // 添加依赖模块
            var dependTypes = module.GetDependModuleTypes();
            foreach (var dependType in dependTypes)
            {
                var dependModule = _source.Find(m => m.GetType() == dependType);
                if (dependModule == null)
                    throw new Warning($"加载模块{module.GetType().FullName}时无法找到依赖模块{dependType.FullName}");
                _modules.AddIfNotContains(dependModule);
            }

            // 按先层级后顺序的规则进行排序
            _modules = _modules.OrderBy(m => m.Level).ThenBy(m => m.Order).ToList();

            var logName = typeof(BingBuilder).FullName;
            tmpModules = _modules.Except(tmpModules).ToArray();
            foreach (var tmpModule in tmpModules)
            {
                var moduleType = tmpModule.GetType();
                var moduleName = Reflections.GetDescription(moduleType);
                Services.LogInformation($"添加模块 “{moduleName} ({moduleType.Name})” 的服务", logName);
                var tmp = Services.ToArray();
                AddModule(Services, tmpModule);
                Services.ServiceLogDebug(tmp, moduleType.FullName);
                Services.LogInformation($"模块 “{moduleName} ({moduleType.Name})” 的服务添加完毕，添加了 {Services.Count - tmp.Length} 个服务\n", logName);
            }
            return this;
        }

        /// <summary>
        /// 添加模块
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="module">模块</param>
        private static IServiceCollection AddModule(IServiceCollection services, BingModule module)
        {
            var type = module.GetType();
            var serviceType = typeof(BingModule);
            if (type.BaseType?.IsAbstract == false)
            {
                // 移除多重继承的模块
                var descriptors = services.Where(m =>
                        m.Lifetime == ServiceLifetime.Singleton
                        && m.ServiceType == serviceType
                        && m.ImplementationInstance?.GetType() == type.BaseType)
                    .ToArray();
                foreach (var descriptor in descriptors)
                    services.Remove(descriptor);
            }

            if (!services.Any(m =>
                m.Lifetime == ServiceLifetime.Singleton
                && m.ServiceType == serviceType
                && m.ImplementationInstance?.GetType() == type))
            {
                services.AddSingleton(typeof(BingModule), module);
                module.AddServices(services);
            }
            return services;
        }

        /// <summary>
        /// 添加加载的所有模块，并可排除指定的模块类型
        /// </summary>
        /// <param name="exceptModuleTypes">要排除的模块类型</param>
        public IBingBuilder AddModules(params Type[] exceptModuleTypes)
        {
            var source = _source.ToArray();
            var exceptModules = source.Where(x => exceptModuleTypes.Contains(x.GetType())).ToArray();
            source = source.Except(exceptModules).ToArray();
            foreach (var module in source) 
                AddModule(module.GetType());
            return this;
        }

        #endregion AddModule(添加模块)

        #region AddOptions(添加选项配置)

        /// <summary>
        /// 添加Bing选项配置
        /// </summary>
        /// <param name="optionsAction">选项操作</param>
        public IBingBuilder AddOptions(Action<BingOptions> optionsAction)
        {
            Check.NotNull(optionsAction, nameof(optionsAction));
            OptionsAction = optionsAction;
            return this;
        }

        #endregion
    }
}
