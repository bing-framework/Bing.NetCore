using System;
using System.Collections.Generic;
using System.Data;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 工作簿
    /// </summary>
    public interface IWorkbook:IDisposable
    {
        /// <summary>
        /// 工作表列表
        /// </summary>
        List<IWorkSheet> WorkSheets { get; }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="fileName">文件名</param>
        void Save(string fileName);

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet GetSheet(string sheetName);

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet InsertSheet(string sheetName);

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="table">数据表</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="withHeader">是否包含表头</param>
        /// <returns></returns>
        IWorkSheet InsertSheet(DataTable table, string sheetName, bool withHeader);

        /// <summary>
        /// 获取工作表名称列表
        /// </summary>
        /// <returns></returns>
        List<string> GetSheetNameList();

        /// <summary>
        /// 获取工作表总数
        /// </summary>
        /// <returns></returns>
        int GetSheetCount();

        /// <summary>
        /// 获取工作表，根据工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet GetSheetByName(string sheetName);

        /// <summary>
        /// 获取工作表，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        IWorkSheet GetSheetByIndex(int sheetIndex);

        /// <summary>
        /// 获取工作表名称，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        string GetSheetNameByIndex(int sheetIndex);

        /// <summary>
        /// 复制工作表，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        IWorkSheet CloneSheet(int sheetIndex);

        /// <summary>
        /// 复制工作表，根据工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        IWorkSheet CloneSheet(string sheetName);
    }
}
