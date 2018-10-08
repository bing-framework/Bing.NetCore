using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Offices.Excels.Imports;

namespace Bing.Offices.Excels.Npoi.Imports
{
    /// <summary>
    /// Npoi Excel 2007 导入器
    /// </summary>
    public class Excel2007Import:ExcelImportBase
    {
        /// <summary>
        /// 初始化一个<see cref="Excel2007Import"/>类型的实例
        /// </summary>
        /// <param name="path">文件路径，绝对路径</param>
        /// <param name="sheetName">工作表名称</param>
        public Excel2007Import(string path, string sheetName = "") : base(path, new Excel2007(), sheetName)
        {
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="stream">文件流</param>
        /// <returns></returns>
        protected override List<T> GetResult<T>(Stream stream)
        {
            return Excel.LoadAll<T>(stream).ToList();
        }
    }
}
