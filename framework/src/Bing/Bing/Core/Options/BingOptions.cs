using Bing.Core.Enums;

namespace Bing.Core.Options
{
    /// <summary>
    /// Bing框架配置选项信息
    /// </summary>
    public class BingOptions
    {
        /// <summary>
        /// 环境类型
        /// </summary>
        public EnvironmentType Environment { get; set; } = EnvironmentType.Development;
    }
}
