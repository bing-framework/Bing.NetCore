using System.Collections.Generic;

namespace Bing.Offices.Handlers
{
    /// <summary>
    /// Excel 导入导出数据处理
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IExcelDataHandler<T>
    {
        /// <summary>
        /// 需要处理的字段，导入和导出统一处理了，减少书写的字段
        /// </summary>
        string[] NeedHandlerFields { get; set; }

        /// <summary>
        /// 导出处理
        /// </summary>
        /// <param name="obj">当前对象</param>
        /// <param name="name">当前字段名称</param>
        /// <param name="value">当前值</param>
        /// <returns></returns>
        object ExportHandler(T obj, string name, object value);        

        /// <summary>
        /// 导入处理
        /// </summary>
        /// <param name="obj">当前对象</param>
        /// <param name="name">当前字段名称</param>
        /// <param name="value">当前值</param>
        /// <returns></returns>
        object ImportHandler(T obj, string name, object value);

        /// <summary>
        /// 设置字典导入，自定义
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="originKey"></param>
        /// <param name="value"></param>
        void SetMapValue(IDictionary<string, object> dictionary, string originKey, object value);
    }
}
