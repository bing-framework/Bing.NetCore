namespace Bing.Modularity
{
    /// <summary>
    /// 配置服务
    /// </summary>
    public interface IPostConfigureServices
    {
        /// <summary>
        /// 配置服务
        /// </summary>
        /// <param name="context">服务配置上下文</param>
        void PostConfigureServices(ServiceConfigurationContext context);
    }
}
