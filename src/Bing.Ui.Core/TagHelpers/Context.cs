using Bing.Ui.Extensions;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bing.Ui.TagHelpers
{
    /// <summary>
    /// TagHelper上下文
    /// </summary>
    public class Context
    {
        /// <summary>
        /// TagHelper上下文
        /// </summary>
        public TagHelperContext TagHelperContext { get; }

        /// <summary>
        /// TagHelper输出
        /// </summary>
        public TagHelperOutput Output { get; }

        /// <summary>
        /// 全部属性集合
        /// </summary>
        public TagHelperAttributeList AllAttributes { get; }

        /// <summary>
        /// 输出属性集合，TagHelper中未明确定义的属性从该集合获取
        /// </summary>
        public TagHelperAttributeList OutputAttributes { get; }

        /// <summary>
        /// 内容
        /// </summary>
        public TagHelperContent Content { get; }

        /// <summary>
        /// 初始化一个<see cref="Context"/>类型的实例
        /// </summary>
        /// <param name="context">TagHelper上下文</param>
        /// <param name="output">TagHelper输出</param>
        /// <param name="content">内容</param>
        public Context(TagHelperContext context, TagHelperOutput output, TagHelperContent content)
        {
            TagHelperContext = context;
            Output = output;
            AllAttributes = new TagHelperAttributeList(context.AllAttributes);
            OutputAttributes = new TagHelperAttributeList(output.Attributes);
            Content = content;
        }

        /// <summary>
        /// 从TagHelperContext Items里获取值
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="key">键</param>
        /// <returns></returns>
        public T GetValueFromItems<T>(object key)
        {
            return TagHelperContext.GetValueFromItems<T>(key);
        }
    }
}
