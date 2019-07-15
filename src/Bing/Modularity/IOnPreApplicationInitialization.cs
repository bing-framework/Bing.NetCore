namespace Bing.Modularity
{
    /// <summary>
    /// 初始化应用程序之前
    /// </summary>
    public interface IOnPreApplicationInitialization
    {
        /// <summary>
        /// 初始化应用程序之前
        /// </summary>
        /// <param name="context">应用程序初始化上下文</param>
        void OnPreApplicationInitialization(ApplicationInitializationContext context);
    }
}
