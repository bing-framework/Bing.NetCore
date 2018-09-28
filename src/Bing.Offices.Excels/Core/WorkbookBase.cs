using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Offices.Excels.Abstractions;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 工作簿基类
    /// </summary>
    public abstract class WorkbookBase:IWorkbook
    {
        /// <summary>
        /// 工作表名称最大长度
        /// </summary>
        private const int MaxSensitveSheetNameLength = 31;

        /// <summary>
        /// 工作表列表
        /// </summary>
        public List<IWorkSheet> Sheets { get; protected set; }

        /// <summary>
        /// 工作表总数
        /// </summary>
        public int SheetCount => Sheets.Count;

        /// <summary>
        /// 工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public IWorkSheet this[int sheetIndex] => Sheets[sheetIndex];

        /// <summary>
        /// Excel 版本
        /// </summary>
        public ExcelVersion Version { get; protected set; }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public IWorkSheet GetSheet(string sheetName)
        {
            return Sheets.Find(x => x.SheetName.Equals(sheetName, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public IWorkSheet GetSheetAt(int sheetIndex)
        {
            ValidateSheetIndex(sheetIndex);
            return Sheets[sheetIndex];
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <returns></returns>
        public abstract IWorkSheet CreateSheet();

        /// <summary>
        /// 获取或创建工作表
        /// </summary>
        /// <returns></returns>
        public abstract IWorkSheet GetOrCreateSheet();

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet CreateSheet(string sheetName);

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet GetOrCreateSheet(string sheetName);

        /// <summary>
        /// 获取工作表名称列表
        /// </summary>
        /// <returns></returns>
        public IList<string> GetSheetNames() => Sheets.Select(x => x.SheetName).ToList();

        /// <summary>
        /// 复制工作表
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public abstract IWorkSheet CloneSheet(int sheetIndex);

        /// <summary>
        /// 复制工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public abstract IWorkSheet CloneSheet(string sheetName);

        /// <summary>
        /// 保存到文件
        /// </summary>
        /// <param name="fileName">文件名，绝对路径</param>
        public abstract void SaveToFile(string fileName);

        /// <summary>
        /// 保存到内存流
        /// </summary>
        /// <param name="stream">内存流</param>
        public abstract void SaveToStream(Stream stream);

        /// <summary>
        /// 保存到字节数组
        /// </summary>
        /// <returns></returns>
        public abstract byte[] SaveToBuffer();

        /// <summary>
        /// 验证工作表索引
        /// </summary>
        /// <param name="index">工作表索引</param>
        protected void ValidateSheetIndex(int index)
        {
            int lastSheetIndex = Sheets.Count - 1;
            if (index < 0 || index > lastSheetIndex)
            {
                throw new ArgumentOutOfRangeException($"工作表索引({index})超出索引范围。必须在(0-{lastSheetIndex})");
            }
        }

        /// <summary>
        /// 是否包含工作表
        /// </summary>
        /// <param name="name">工作表名称</param>
        /// <param name="excludeSheetIndex">忽略判断索引</param>
        /// <returns></returns>
        protected bool ContainsSheet(string name, int excludeSheetIndex)
        {
            if (name.Length > MaxSensitveSheetNameLength)
            {
                name = name.Substring(0, MaxSensitveSheetNameLength);
            }

            for (var i = 0; i < Sheets.Count; i++)
            {
                string sheetName = Sheets[i].SheetName;
                if (sheetName.Length > MaxSensitveSheetNameLength)
                {
                    sheetName = sheetName.Substring(0, MaxSensitveSheetNameLength);
                }

                if (excludeSheetIndex != i && name.Equals(sheetName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
