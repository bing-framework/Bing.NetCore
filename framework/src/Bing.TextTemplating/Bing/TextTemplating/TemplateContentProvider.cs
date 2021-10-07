using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bing.DependencyInjection;
using Bing.Exceptions;
using Bing.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板内容提供程序
    /// </summary>
    public class TemplateContentProvider : ITemplateContentProvider, ITransientDependency
    {
        /// <summary>
        /// 服务作用域工厂
        /// </summary>
        public IServiceScopeFactory ServiceScopeFactory { get; }

        /// <summary>
        /// 文本模板选项配置
        /// </summary>
        public BingTextTemplatingOptions Options { get; }

        /// <summary>
        /// 模板定义管理器
        /// </summary>
        private readonly ITemplateDefinitionManager _templateDefinitionManager;

        /// <summary>
        /// 初始化一个<see cref="TemplateContentProvider"/>类型的实例
        /// </summary>
        /// <param name="templateDefinitionManager">模板定义管理器</param>
        /// <param name="serviceScopeFactory">服务作用域工厂</param>
        /// <param name="options">文本模板选项配置</param>
        public TemplateContentProvider(
            ITemplateDefinitionManager templateDefinitionManager,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<BingTextTemplatingOptions> options)
        {
            _templateDefinitionManager = templateDefinitionManager;
            ServiceScopeFactory = serviceScopeFactory;
            Options = options.Value;
        }

        /// <summary>
        /// 获取模板内容
        /// </summary>
        /// <param name="templateName">模板名称</param>
        /// <param name="cultureName">区域性名称</param>
        /// <param name="tryDefaults">尝试默认值</param>
        /// <param name="useCurrentCultureIfCultureNameIsNull">如果当前区域性名称为空，则默认使用当前区域</param>
        public virtual Task<string> GetContentOrNullAsync(string templateName, string cultureName = null, bool tryDefaults = true,
            bool useCurrentCultureIfCultureNameIsNull = true)
        {
            var template = _templateDefinitionManager.Get(templateName);
            return GetContentOrNullAsync(template, cultureName);
        }

        /// <summary>
        /// 获取模板内容
        /// </summary>
        /// <param name="templateDefinition">模板定义</param>
        /// <param name="cultureName">区域性名称</param>
        /// <param name="tryDefaults">尝试默认值</param>
        /// <param name="useCurrentCultureIfCultureNameIsNull">如果当前区域性名称为空，则默认使用当前区域</param>
        public virtual async Task<string> GetContentOrNullAsync(TemplateDefinition templateDefinition, string cultureName = null, bool tryDefaults = true, bool useCurrentCultureIfCultureNameIsNull = true)
        {
            Check.NotNull(templateDefinition, nameof(templateDefinition));
            if (!Options.ContentContributors.Any())
                throw new Warning($"No template content contributor was registered. Use {nameof(BingTextTemplatingOptions)} to register contributors!");
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                string templateString = null;
                if (cultureName == null && useCurrentCultureIfCultureNameIsNull)
                    cultureName = CultureInfo.CurrentUICulture.Name;
                var contributors = CreateTemplateContentContributors(scope.ServiceProvider);
                templateString = await GetContentOrNullAsync(contributors, new TemplateContentContributorContext(templateDefinition, scope.ServiceProvider, cultureName));
                if (templateString != null)
                    return templateString;
                if (!tryDefaults)
                    return null;
            }
            return null;
        }

        /// <summary>
        /// 创建模板内容构造者
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        protected virtual ITemplateContentContributor[] CreateTemplateContentContributors(IServiceProvider serviceProvider)
        {
            return Options.ContentContributors
                .Select(type => (ITemplateContentContributor)serviceProvider.GetRequiredService(type))
                .Reverse()
                .ToArray();
        }

        /// <summary>
        /// 获取模板内容
        /// </summary>
        /// <param name="contributors">模板内容构造者数组</param>
        /// <param name="context">模板内容构造者上下文</param>
        protected virtual async Task<string> GetContentOrNullAsync(ITemplateContentContributor[] contributors, TemplateContentContributorContext context)
        {
            foreach (var contributor in contributors)
            {
                var templateString = await contributor.GetOrNullAsync(context);
                if (templateString != null)
                    return templateString;
            }
            return null;
        }
    }
}
