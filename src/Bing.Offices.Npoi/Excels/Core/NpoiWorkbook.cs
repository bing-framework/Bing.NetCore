using System;
using System.Data;
using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Enums;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的工作簿
    /// </summary>
    public class NpoiWorkbook:WorkbookBase
    {
        /// <summary>
        /// 工作簿
        /// </summary>
        private NPOI.SS.UserModel.IWorkbook _workbook;

        /// <summary>
        /// 初始化一个<see cref="NpoiWorkbook"/>类型的实例
        /// </summary>
        /// <param name="format">Excel格式类型</param>
        public NpoiWorkbook(ExcelFormat format):base()
        {
            _workbook = CreateWorkbook(format);
        }

        /// <summary>
        /// 初始化一个<see cref="NpoiWorkbook"/>类型的实例
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        public NpoiWorkbook(string fileName) : base(fileName) { }

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="format">Excel格式类型</param>
        /// <returns></returns>
        private NPOI.SS.UserModel.IWorkbook CreateWorkbook(ExcelFormat format)
        {
            switch (format)
            {
                case ExcelFormat.Xls:
                    return new HSSFWorkbook();
                case ExcelFormat.Xlsx:
                    return new XSSFWorkbook();
            }
            throw new Exception("未知Excel格式类型");
        }

        /// <summary>
        /// 加载工作簿
        /// </summary>
        /// <param name="fileName">文件名称，绝对路径</param>
        protected override void LoadWorkbook(string fileName)
        {
            FileStream fs = File.OpenRead(fileName);
            switch (ExcelFormat)
            {
                case ExcelFormat.Xls:
                    _workbook = new HSSFWorkbook(fs);
                    break;
                case ExcelFormat.Xlsx:
                    _workbook = new XSSFWorkbook(fs);
                    break;
                default:
                    throw new Exception("未知 Excel 格式文件");
            }
            fs.Close();

            // 读取当前表数据
            var sheetNum = _workbook.NumberOfSheets;
            for (int i = 0; i < sheetNum; i++)
            {
                ISheet sheet = _workbook.GetSheetAt(i);
                var worksheet = new NpoiWorkSheet(sheet);
                WorkSheets.Add(worksheet);
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="fileName">文件名</param>
        public override void Save(string fileName)
        {
            using (var workbook=new FileStream(fileName,FileMode.Create,FileAccess.Write))
            {
                _workbook.Write(workbook);
            }
        }

        /// <summary>
        /// 获取工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public override IWorkSheet GetSheet(string sheetName)
        {
            return WorkSheets.Find(x => x.SheetName.Equals(sheetName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public override IWorkSheet InsertSheet(string sheetName)
        {
            var sheet = _workbook.CreateSheet(sheetName);
            var worksheet = new NpoiWorkSheet(sheet);
            WorkSheets.Add(worksheet);
            return worksheet;
        }

        /// <summary>
        /// 插入工作表
        /// </summary>
        /// <param name="table">数据表</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="withHeader">是否包含表头</param>
        /// <returns></returns>
        public override IWorkSheet InsertSheet(DataTable table, string sheetName, bool withHeader)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 复制工作表，根据工作表索引
        /// </summary>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public override IWorkSheet CloneSheet(int sheetIndex)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 复制工作表，根据工作表名称
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public override IWorkSheet CloneSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }
    }
}
