using Bing.Ui.Builders;
using Bing.Ui.Configs;
using Bing.Ui.Zorro.Forms.Builders;

namespace Bing.Ui.Zorro.Forms.Renders
{
    /// <summary>
    /// 开关渲染器
    /// </summary>
    public class SwitchRender : CheckBoxRender
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly Config _config;

        /// <summary>
        /// 初始化一个<see cref="SwitchRender"/>类型的实例
        /// </summary>
        /// <param name="config">配置</param>
        public SwitchRender(Config config) : base(config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取标签生成器
        /// </summary>
        protected override TagBuilder GetTagBuilder()
        {
            ResolveExpression();
            var builder = new SwitchBuilder();
            Config(builder);
            return builder;
        }

        /// <summary>
        /// 配置变更事件
        /// </summary>
        /// <param name="builder">标签生成器</param>
        protected override void ConfigOnChange(TagBuilder builder)
        {
            builder.AddAttribute("(ngModelChange)", _config.GetValue(UiConst.OnChange));
        }
    }
}
