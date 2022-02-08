using System.Collections.Generic;
using System.Collections.Immutable;
using Bing.Collections;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板定义上下文
    /// </summary>
    public class TemplateDefinitionContext : ITemplateDefinitionContext
    {
        /// <summary>
        /// 模板字典
        /// </summary>
        protected Dictionary<string, TemplateDefinition> Templates { get; }

        /// <summary>
        /// 初始化一个<see cref="TemplateDefinitionContext"/>类型的实例
        /// </summary>
        /// <param name="templates">模板字典</param>
        public TemplateDefinitionContext(Dictionary<string, TemplateDefinition> templates) => Templates = templates;

        /// <summary>
        /// 获取指定名称的模板定义列表
        /// </summary>
        /// <param name="name">模板名称</param>
        public virtual IReadOnlyList<TemplateDefinition> GetAll(string name) => Templates.Values.ToImmutableList();

        /// <summary>
        /// 获取指定名称的模板定义
        /// </summary>
        /// <param name="name">模板名称</param>
        public virtual TemplateDefinition GetOrNull(string name) => Templates.GetOrDefault(name);

        /// <summary>
        /// 获取全部的模板定义列表
        /// </summary>
        public virtual IReadOnlyList<TemplateDefinition> GetAll() => Templates.Values.ToImmutableList();

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="definitions">模板定义数组</param>
        public void Add(params TemplateDefinition[] definitions)
        {
            if (definitions.IsNullOrEmpty())
                return;
            foreach (var definition in definitions)
                Templates[definition.Name] = definition;
        }
    }
}
