using System.Collections.Generic;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 工作簿
    /// </summary>
    public interface IWorkbook
    {
        /// <summary>
        /// 工作表列表
        /// </summary>
        List<IWorkSheet> Sheets { get; set; }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet GetSheet(string sheetName);

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        IWorkSheet GetSheetAt(int sheetIndex);

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <returns></returns>
        IWorkSheet CreateSheet();

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet CreateSheet(string sheetName);
    }
}
