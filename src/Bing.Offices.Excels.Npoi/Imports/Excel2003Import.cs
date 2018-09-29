using Bing.Offices.Excels.Imports;

namespace Bing.Offices.Excels.Npoi.Imports
{
    /// <summary>
    /// Npoi Excel 2003 导入器
    /// </summary>
    public class Excel2003Import:ExcelImportBase
    {
        /// <summary>
        /// 初始化一个<see cref="Excel2003Import"/>类型的实例
        /// </summary>
        /// <param name="path">文件路径，绝对路径</param>
        /// <param name="sheetName">工作表名称</param>
        public Excel2003Import(string path, string sheetName = "") : base(path, new Excel2003(), sheetName)
        {
        }
    }
}
