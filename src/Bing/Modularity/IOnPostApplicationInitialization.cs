namespace Bing.Modularity
{
    /// <summary>
    /// 初始化应用程序之后
    /// </summary>
    public interface IOnPostApplicationInitialization
    {
        /// <summary>
        /// 初始化应用程序之后
        /// </summary>
        /// <param name="context">应用程序初始化上下文</param>
        void OnPostApplicationInitialization(ApplicationInitializationContext context);
    }
}
