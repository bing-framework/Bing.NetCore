using System;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板内容构造者上下文
    /// </summary>
    public class TemplateContentContributorContext
    {
        /// <summary>
        /// 模板定义
        /// </summary>
        public TemplateDefinition TemplateDefinition { get; }

        /// <summary>
        /// 服务提供程序
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 区域性
        /// </summary>
        public string Culture { get; }

        /// <summary>
        /// 初始化一个<see cref="TemplateContentContributorContext"/>类型的实例
        /// </summary>
        /// <param name="templateDefinition">模板定义</param>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="culture">区域性</param>
        public TemplateContentContributorContext(
            TemplateDefinition templateDefinition,
            IServiceProvider serviceProvider, 
            string culture)
        {
            TemplateDefinition = templateDefinition ?? throw new ArgumentNullException(nameof(templateDefinition));
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Culture = culture;
        }
    }
}
