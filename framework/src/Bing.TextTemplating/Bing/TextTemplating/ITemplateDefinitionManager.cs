using System.Collections.Generic;

namespace Bing.TextTemplating
{
    /// <summary>
    /// 模板定义管理器
    /// </summary>
    public interface ITemplateDefinitionManager
    {
        /// <summary>
        /// 获取模板定义
        /// </summary>
        /// <param name="name">模板名称</param>
        TemplateDefinition Get(string name);

        /// <summary>
        /// 获取全部模板定义
        /// </summary>
        IReadOnlyList<TemplateDefinition> GetAll();

        /// <summary>
        /// 获取模板定义
        /// </summary>
        /// <param name="name">模板名称</param>
        TemplateDefinition GetOrNull(string name);
    }
}
