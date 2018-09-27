using System;
using System.Data;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Enums;
using NPOI.HSSF.UserModel;
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
        /// <param name="fileName">文件名称</param>
        protected override void LoadWorkbook(string fileName)
        {
            throw new System.NotImplementedException();
        }

        public override void Save(string fileName)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet GetSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet InsertSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet InsertSheet(DataTable table, string sheetName, bool withHeader)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet CloneSheet(int sheetIndex)
        {
            throw new System.NotImplementedException();
        }

        public override IWorkSheet CloneSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }
    }
}
