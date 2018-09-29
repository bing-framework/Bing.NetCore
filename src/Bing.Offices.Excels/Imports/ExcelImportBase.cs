using System.Collections.Generic;
using System.IO;

namespace Bing.Offices.Excels.Imports
{
    /// <summary>
    /// Excel 导入器基类
    /// </summary>
    public abstract class ExcelImportBase:ImportBase
    {
        /// <summary>
        /// Excel 操作
        /// </summary>
        private readonly IExcel _excel;

        /// <summary>
        /// 工作表名称
        /// </summary>
        private readonly string _sheetName;

        /// <summary>
        /// 初始化一个<see cref="ExcelImportBase"/>类型的实例
        /// </summary>
        /// <param name="path">文件路径，绝对路径</param>
        /// <param name="excel">Excel 操作</param>
        /// <param name="sheetName">工作表名称</param>
        protected ExcelImportBase(string path, IExcel excel, string sheetName = "") : base(path)
        {
            _excel = excel;
            _sheetName = sheetName;
        }

        protected override List<T> GetResult<T>(Stream stream)
        {
            return null;
        }
    }
}
