namespace Bing.Configurations
{
    /// <summary>
    /// 配置构建器选项
    /// </summary>
    public class ConfigurationBuilderOptions
    {
        /// <summary>
        /// 文件名。默认值：appsettings
        /// </summary>
        public string FileName { get; set; } = "appsettings";

        /// <summary>
        /// 环境名称。支持：Development(开发环境)、Staging(演示环境)、Production(生产环境)
        /// </summary>
        public string EnvironmentName { get; set; }

        /// <summary>
        /// 基本路径。用于读取配置文件的根路径
        /// </summary>
        public string BasePath { get; set; }
    }
}
