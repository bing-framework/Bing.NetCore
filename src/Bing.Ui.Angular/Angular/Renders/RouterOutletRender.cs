using Bing.Ui.Angular.Builders;
using Bing.Ui.Builders;
using Bing.Ui.Configs;

namespace Bing.Ui.Angular.Renders
{
    /// <summary>
    /// router-outlet 路由出口渲染器
    /// </summary>
    public class RouterOutletRender:AngularRenderBase
    {
        /// <summary>
        /// 初始化一个<see cref="RouterOutletRender"/>类型的实例
        /// </summary>
        /// <param name="config">配置</param>
        public RouterOutletRender(IConfig config) : base(config)
        {
        }

        /// <summary>
        /// 获取标签生成器
        /// </summary>
        /// <returns></returns>
        protected override TagBuilder GetTagBuilder()
        {
            return new RouterOutletBuilder();
        }
    }
}
