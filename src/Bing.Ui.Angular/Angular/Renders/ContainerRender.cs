using Bing.Ui.Angular.Builders;
using Bing.Ui.Builders;
using Bing.Ui.Configs;

namespace Bing.Ui.Angular.Renders
{
    /// <summary>
    /// ng-container 容器渲染器
    /// </summary>
    public class ContainerRender:AngularRenderBase
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly IConfig _config;

        /// <summary>
        /// 初始化一个<see cref="ContainerRender"/>类型的实例
        /// </summary>
        /// <param name="config">配置</param>
        public ContainerRender(IConfig config) : base(config)
        {
            _config = config;
        }

        /// <summary>
        /// 获取标签生成器
        /// </summary>
        /// <returns></returns>
        protected override TagBuilder GetTagBuilder()
        {
            var builder = new ContainerBuilder();
            ConfigId(builder);
            ConfigContent(builder);
            return builder;
        }
    }
}
