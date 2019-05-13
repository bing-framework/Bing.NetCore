using Bing.Ui.Angular;
using Bing.Ui.Angular.Base;
using Bing.Ui.Angular.Forms.Configs;
using Bing.Ui.Angular.Forms.Resolvers;
using Bing.Ui.Builders;
using Bing.Ui.Configs;
using Bing.Ui.Zorro.Forms.Builders;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Bing.Ui.Zorro.Forms.Renders
{
    /// <summary>
    /// 单选框渲染器
    /// </summary>
    public class RadioRender : AngularRenderBase
    {
        /// <summary>
        /// 下拉列表配置
        /// </summary>
        private readonly SelectConfig _config;

        /// <summary>
        /// 初始化一个<see cref="RadioRender"/>类型的实例
        /// </summary>
        /// <param name="config">下拉列表配置</param>
        public RadioRender(SelectConfig config) : base(config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取标签生成器
        /// </summary>
        protected override TagBuilder GetTagBuilder()
        {
            ResolveExpression();
            var builder = new RadioWrapperBuilder();
            Config(builder);
            return builder;
        }

        /// <summary>
        /// 解析属性表达式
        /// </summary>
        protected void ResolveExpression()
        {
            if (_config.Contains(UiConst.For) == false)
            {
                return;
            }

            var expression = _config.GetValue<ModelExpression>(UiConst.For);
            SelectExpressionResolver.Init(expression, _config);
        }

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void Config(TagBuilder builder)
        {
            ConfigId(builder);
            ConfigName(builder);
            ConfigLabel(builder);
            ConfigDisabled(builder);
            ConfigModel(builder);
            ConfigRequired(builder);
            ConfigEvents(builder);
            ConfigUrl(builder);
            ConfigDataSource(builder);
        }

        /// <summary>
        /// 配置名称
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigName(TagBuilder builder)
        {
            builder.AddAttribute(UiConst.Name, _config.GetValue(UiConst.Name));
            builder.AddAttribute("[name]", _config.GetValue(AngularConst.BindName));
        }

        /// <summary>
        /// 配置标签
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigLabel(TagBuilder builder)
        {
            builder.AddAttribute("[vertical]", _config.GetBoolValue(UiConst.Vertical));
            builder.AddAttribute("label", _config.GetValue(UiConst.Label));
            builder.AddAttribute("[label]", _config.GetValue(AngularConst.BindLabel));
        }

        /// <summary>
        /// 配置禁用
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigDisabled(TagBuilder builder)
        {
            builder.AddAttribute("[disabled]", _config.GetBoolValue(UiConst.Disabled));
        }

        /// <summary>
        /// 配置模型绑定
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigModel(TagBuilder builder)
        {
            builder.AddAttribute("[(model)]", _config.GetValue(UiConst.Model));
            builder.AddAttribute("[(model)]", _config.GetValue(AngularConst.NgModel));
        }

        /// <summary>
        /// 配置必填项
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigRequired(TagBuilder builder)
        {
            builder.AddAttribute("[required]", _config.GetBoolValue(UiConst.Required));
        }

        /// <summary>
        /// 配置事件
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigEvents(TagBuilder builder)
        {
            builder.AddAttribute("(onChange)", _config.GetValue(UiConst.OnChange));
        }

        /// <summary>
        /// 配置Url
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigUrl(TagBuilder builder)
        {
            builder.AddAttribute(UiConst.Url, _config.GetValue(UiConst.Url));
            builder.AddAttribute("[url]", _config.GetValue(AngularConst.BindUrl));
        }

        /// <summary>
        /// 配置数据源
        /// </summary>
        /// <param name="builder">标签生成器</param>
        private void ConfigDataSource(TagBuilder builder)
        {
            AddItems();
            builder.AddAttribute("[dataSource]", _config.GetValue(UiConst.DataSource));
        }

        /// <summary>
        /// 添加项集合
        /// </summary>
        private void AddItems()
        {
            if (_config.Items.Count == 0)
            {
                return;
            }

            _config.SetAttribute(UiConst.DataSource, Bing.Utils.Json.JsonHelper.ToJson(_config.Items, true));
        }
    }
}
