namespace Bing.Offices.Handlers
{
    /// <summary>
    /// Excel字典处理器
    /// </summary>
    public interface IExcelDictHandler
    {
        /// <summary>
        /// 从值翻译到名称
        /// </summary>
        /// <param name="dict">字典Key</param>
        /// <param name="obj">对象</param>
        /// <param name="name">属性名称</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        string ToName(string dict, object obj, string name, object value);

        /// <summary>
        /// 从名称翻译到值
        /// </summary>
        /// <param name="dict">字典Key</param>
        /// <param name="obj">对象</param>
        /// <param name="name">属性名称</param>
        /// <param name="value">属性值</param>
        /// <returns></returns>
        string ToValue(string dict, object obj, string name, object value);
    }
}
