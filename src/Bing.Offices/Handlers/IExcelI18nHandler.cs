namespace Bing.Offices.Handlers
{
    /// <summary>
    /// Excel国际化处理器
    /// </summary>
    public interface IExcelI18nHandler
    {
        /// <summary>
        /// 获取当前名称
        /// </summary>
        /// <param name="name">注解配置的名称</param>
        /// <returns></returns>
        string GetLocaleName(string name);
    }
}
