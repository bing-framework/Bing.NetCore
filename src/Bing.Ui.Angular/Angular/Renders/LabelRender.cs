using Bing.Ui.Angular.Enums;
using Bing.Ui.Angular.Resolvers;
using Bing.Ui.Builders;
using Bing.Ui.Configs;
using Bing.Utils.Extensions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Angular.Renders
{
    /// <summary>
    /// 标签渲染器
    /// </summary>
    public class LabelRender:AngularRenderBase
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly IConfig _config;

        /// <summary>
        /// 初始化一个<see cref="LabelRender"/>类型的实例
        /// </summary>
        /// <param name="config"></param>
        public LabelRender(IConfig config) : base(config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取标签生成器
        /// </summary>
        /// <returns></returns>
        protected override TagBuilder GetTagBuilder()
        {
            ResolveExpression();
            var builder = new SpanBuilder();
            Config(builder);
            return builder;
        }

        /// <summary>
        /// 解析属性表达式
        /// </summary>
        private void ResolveExpression()
        {
            if (_config.Contains(UiConst.For) == false)
            {
                return;
            }

            var expression = _config.GetValue<ModelExpression>(UiConst.For);
            LabelExpressionResolver.Init(expression, _config);
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">标签生成器</param>
        protected void Config(TagBuilder builder)
        {
            ConfigId(builder);
            ConfigText(builder);
        }

        /// <summary>
        /// 配置文本
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigText(TagBuilder builder)
        {
            var text = _config.GetValue(UiConst.Text);
            if (text.IsEmpty() == false)
            {
                builder.SetContent(text);
                return;
            }

            text = _config.GetValue(AngularConst.BindText);
            if (text.IsEmpty())
            {
                return;
            }

            ConfigByType(builder, text);
        }

        /// <summary>
        /// 根据类型配置
        /// </summary>
        /// <param name="builder">标签生成器</param>
        /// <param name="text">文本</param>
        private void ConfigByType(TagBuilder builder, string text)
        {
            var type = _config.GetValue<LabelType>(UiConst.Type);
            switch (type)
            {
                case LabelType.Bool:
                    AddBoolType(builder, text);
                    return;
                case LabelType.Date:
                    AddDateType(builder,text);
                    return;
                default:
                    AddText(builder, text);
                    return;
            }
        }

        /// <summary>
        /// 添加布尔类型
        /// </summary>
        /// <param name="builder">标签生成器</param>
        /// <param name="text">文本</param>
        private void AddBoolType(TagBuilder builder, string text)
        {
            // TODO:暂无生成器
        }

        /// <summary>
        /// 添加日期类型
        /// </summary>
        /// <param name="builder">标签生成器</param>
        /// <param name="text">文本</param>
        private void AddDateType(TagBuilder builder, string text)
        {
            var format = _config.GetValue(UiConst.DateFormat);
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "yyyy-MM-dd";
            }

            builder.AppendContent($"{{{{ {text} | date:\"{format}\" }}}}");
        }

        /// <summary>
        /// 添加文本
        /// </summary>
        /// <param name="builder">标签生成器</param>
        /// <param name="text">文本</param>
        private void AddText(TagBuilder builder, string text)
        {
            builder.SetContent($"{{{{{text}}}}}");
        }
    }
}
