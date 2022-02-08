using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bing.Collections;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板定义管理器
    /// </summary>
    public class TemplateDefinitionManager : ITemplateDefinitionManager, ISingletonDependency
    {
        /// <summary>
        /// 模板定义字典
        /// </summary>
        protected Lazy<IDictionary<string, TemplateDefinition>> TemplateDefinitions { get; }

        /// <summary>
        /// 文本模板选项配置
        /// </summary>
        protected BingTextTemplatingOptions Options { get; }

        /// <summary>
        /// 服务提供程序
        /// </summary>
        protected IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 初始化一个<see cref="TemplateDefinitionManager"/>类型的实例
        /// </summary>
        /// <param name="options">文本模板选项配置</param>
        /// <param name="serviceProvider">服务提供程序</param>
        public TemplateDefinitionManager(
            IOptions<BingTextTemplatingOptions> options,
            IServiceProvider serviceProvider)
        {
            Options = options.Value;
            ServiceProvider = serviceProvider;
            TemplateDefinitions = new Lazy<IDictionary<string, TemplateDefinition>>();
        }

        /// <summary>
        /// 获取模板定义
        /// </summary>
        /// <param name="name">模板名称</param>
        public virtual TemplateDefinition Get(string name)
        {
            Check.NotNull(name, nameof(name));
            var template = GetOrNull(name);
            if (template == null)
                throw new Warning("Undefined template: " + name);
            return template;
        }

        /// <summary>
        /// 获取全部模板定义
        /// </summary>
        public virtual IReadOnlyList<TemplateDefinition> GetAll() => TemplateDefinitions.Value.Values.ToImmutableList();

        /// <summary>
        /// 获取模板定义
        /// </summary>
        /// <param name="name">模板名称</param>
        public virtual TemplateDefinition GetOrNull(string name) => TemplateDefinitions.Value.GetOrDefault(name);

        /// <summary>
        /// 创建文本模板定义字典
        /// </summary>
        protected virtual IDictionary<string, TemplateDefinition> CreateTextTemplateDefinitions()
        {
            var templates = new Dictionary<string, TemplateDefinition>();
            using var scope = ServiceProvider.CreateScope();
            var providers = Options
                .DefinitionProviders
                .Select(x => scope.ServiceProvider.GetRequiredService(x) as ITemplateDefinitionProvider)
                .ToList();
            var context = new TemplateDefinitionContext(templates);
            foreach (var provider in providers)
                provider.PreDefine(context);
            foreach (var provider in providers)
                provider.Define(context);
            foreach (var provider in providers)
                provider.PostDefine(context);

            return templates;
        }
    }
}
