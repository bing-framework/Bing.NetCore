using Bing.Offices.Handlers;

namespace Bing.Offices.Excels.Models
{
    /// <summary>
    /// 基础参数
    /// </summary>
    public class ExcelBaseParams<T>
    {
        /// <summary>
        /// 数据处理
        /// </summary>
        public IExcelDataHandler<T> DataHandler { get; set; }

        /// <summary>
        /// 字段处理器
        /// </summary>
        public IExcelDictHandler DictHandler { get; set; }

        /// <summary>
        /// 国际化处理器
        /// </summary>
        public IExcelI18nHandler I18NHandler { get; set; }
    }
}
