using System;
using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Internal;

namespace Bing.Offices.Excels.Core
{
    /// <summary>
    /// 工作簿
    /// </summary>
    public class Workbook:IWorkbook
    {
        /// <summary>
        /// 工作表名称最大长度
        /// </summary>
        private const int MaxSensitveSheetNameLength = 31;

        /// <summary>
        /// 工作表列表
        /// </summary>
        public List<IWorkSheet> Sheets { get; set; }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="name">工作表名称</param>
        /// <returns></returns>
        public IWorkSheet GetSheet(string name)
        {
            foreach (var sheet in Sheets)
            {
                if (name.Equals(sheet.SheetName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return sheet;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="index">工作表索引</param>
        /// <returns></returns>
        public IWorkSheet GetSheetAt(int index)
        {
            ValidateSheetIndex(index);
            return Sheets[index];
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <returns></returns>
        public IWorkSheet CreateSheet()
        {
            string sheetName = "Sheet" + (Sheets.Count);
            int idx = 0;
            while (GetSheet(sheetName) != null)
            {
                sheetName = "Sheet" + idx;
                idx++;
            }
            return CreateSheet(sheetName);
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="name">工作表名</param>
        /// <returns></returns>
        public IWorkSheet CreateSheet(string name)
        {
            if (name == null)
            {
                throw new ArgumentException("sheetName must not null");
            }

            if (ContainsSheet(name, Sheets.Count))
            {
                throw new ArgumentException("The workbook already contains a sheet of this name");
            }

            if (name.Length > 31)
            {
                name = name.Substring(0, MaxSensitveSheetNameLength);
            }
            WorkbookUtil.ValidateSheetName(name);

            IWorkSheet sheet = new WorkSheet();
            sheet.SheetName = name;
            Sheets.Add(sheet);
            return sheet;
        }

        /// <summary>
        /// 验证工作表索引
        /// </summary>
        /// <param name="index">索引</param>
        private void ValidateSheetIndex(int index)
        {
            int lastSheetIndex = Sheets.Count - 1;
            if (index < 0 || index > lastSheetIndex)
            {
                throw new ArgumentException($"Sheet index ({index}) is out of range (0..{lastSheetIndex})");
            }
        }

        /// <summary>
        /// 是否包含工作表
        /// </summary>
        /// <param name="name">工作表名</param>
        /// <param name="excludeSheetIdx">忽略索引</param>
        /// <returns></returns>
        private bool ContainsSheet(string name, int excludeSheetIdx)
        {
            if (name.Length > MaxSensitveSheetNameLength)
            {
                name = name.Substring(0, MaxSensitveSheetNameLength);
            }

            for (int i = 0; i < Sheets.Count; i++)
            {
                string sheetName = Sheets[i].SheetName;
                if (sheetName.Length > MaxSensitveSheetNameLength)
                {
                    sheetName = sheetName.Substring(0, MaxSensitveSheetNameLength);
                }

                if (excludeSheetIdx != i && name.Equals(sheetName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
