using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Enums;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 工作簿基类
    /// </summary>
    public abstract class WorkbookBase:IWorkbook
    {
        /// <summary>
        /// Excel格式类型
        /// </summary>
        protected ExcelFormat ExcelFormat;

        /// <summary>
        /// 是否已释放
        /// </summary>
        protected bool Disposed = false;

        /// <summary>
        /// 工作表列表
        /// </summary>
        public List<IWorkSheet> WorkSheets { get; protected set; }

        /// <summary>
        /// 初始化一个<see cref="WorkbookBase"/>类型的实例
        /// </summary>
        protected WorkbookBase()
        {
            WorkSheets = new List<IWorkSheet>();
        }

        /// <summary>
        /// 初始化一个<see cref="WorkbookBase"/>类型的实例
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        protected WorkbookBase(string fileName)
        {
            ExcelFormat = GetExcelFormat(fileName);
            WorkSheets = new List<IWorkSheet>();
            PrePare();
            LoadWorkbook(fileName);
        }

        /// <summary>
        /// 准备工作
        /// </summary>
        protected virtual void PrePare()
        {
        }

        /// <summary>
        /// 加载工作簿
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        protected abstract void LoadWorkbook(string fileName);

        /// <summary>
        /// 获取Excel格式类型
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        /// <returns></returns>
        protected ExcelFormat GetExcelFormat(string fileName)
        {
            string extension = Path.GetExtension(fileName)?.ToLower();
            switch (extension)
            {
                case ".xlsx":
                    return ExcelFormat.Xlsx;
                case ".xls":
                    return ExcelFormat.Xls;
            }
            throw new Exception("未知 Excel 格式文件");
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="fileName">文件名</param>
        public abstract void Save(string fileName);

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet GetSheet(string sheetName);

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet InsertSheet(string sheetName);

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="table">数据表</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="withHeader">是否包含表头</param>
        /// <returns></returns>
        public abstract IWorkSheet InsertSheet(DataTable table, string sheetName, bool withHeader);

        /// <summary>
        /// 获取工作表名称列表
        /// </summary>
        /// <returns></returns>
        public List<string> GetSheetNameList() => WorkSheets.Select(x => x.SheetName).ToList();

        /// <summary>
        /// 获取工作表总数
        /// </summary>
        /// <returns></returns>
        public int GetSheetCount() => WorkSheets.Count;

        /// <summary>
        /// 获取工作表，根据工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public IWorkSheet GetSheetByName(string sheetName) =>
            WorkSheets.Find(x => x.SheetName.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 获取工作表，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public IWorkSheet GetSheetByIndex(int sheetIndex) => WorkSheets[sheetIndex];

        /// <summary>
        /// 获取工作表名称，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public string GetSheetNameByIndex(int sheetIndex) => WorkSheets[sheetIndex].SheetName;

        /// <summary>
        /// 复制工作表，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public abstract IWorkSheet CloneSheet(int sheetIndex);

        /// <summary>
        /// 复制工作表，根据工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet CloneSheet(string sheetName);

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放中</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }
        }
    }
}
