using System.Collections.Generic;
using System.IO;

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
        List<IWorkSheet> Sheets { get; }

        /// <summary>
        /// 工作表总数
        /// </summary>
        int SheetCount { get; }

        /// <summary>
        /// 工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        IWorkSheet this[int sheetIndex] { get; }

        /// <summary>
        /// Excel 版本
        /// </summary>
        ExcelVersion Version { get; }

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
        /// 获取或创建工作表
        /// </summary>
        /// <returns></returns>
        IWorkSheet GetOrCreateSheet();

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet CreateSheet(string sheetName);

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet GetOrCreateSheet(string sheetName);

        /// <summary>
        /// 获取工作表名称列表
        /// </summary>
        /// <returns></returns>
        IList<string> GetSheetNames();

        /// <summary>
        /// 复制工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        IWorkSheet CloneSheet(int sheetIndex);

        /// <summary>
        /// 复制工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet CloneSheet(string sheetName);

        /// <summary>
        /// 保存到文件
        /// </summary>
        /// <param name="fileName">文件名，绝对路径</param>
        void SaveToFile(string fileName);

        /// <summary>
        /// 保存到内存流
        /// </summary>
        /// <param name="stream">内存流</param>
        void SaveToStream(Stream stream);

        /// <summary>
        /// 保存到字节数组
        /// </summary>
        /// <returns></returns>
        byte[] SaveToBuffer();
    }
}
