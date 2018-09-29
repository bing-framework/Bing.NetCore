using System;
using System.IO;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的工作簿
    /// </summary>
    public class Workbook:WorkbookBase
    {
        /// <summary>
        /// 工作簿
        /// </summary>
        private NPOI.SS.UserModel.IWorkbook _workbook;

        /// <summary>
        /// 工作表索引
        /// </summary>
        private int _sheetIndex;

        public Workbook(string fileName):base(fileName)
        {
        }

        public Workbook(ExcelVersion version)
        {
            _workbook = CreateWorkbook(version);
            _sheetIndex = 0;
        }

        /// <summary>
        /// 创建工作簿
        /// </summary>
        /// <param name="version">Excel版本</param>
        /// <returns></returns>
        private NPOI.SS.UserModel.IWorkbook CreateWorkbook(ExcelVersion version)
        {
            switch (version)
            {
                case ExcelVersion.Xls:
                    return new HSSFWorkbook();
                case ExcelVersion.Xlsx:
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
            switch (Version)
            {
                case ExcelVersion.Xls:
                    _workbook = new HSSFWorkbook(fs);
                    break;
                case ExcelVersion.Xlsx:
                    _workbook = new XSSFWorkbook(fs);
                    break;
                default:
                    throw new Exception("未知 Excel 格式文件");
            }
            fs.Close();

            // 读取当前表数据
            var sheetNum = _workbook.NumberOfSheets;
            _sheetIndex = sheetNum;
            for (int i = 0; i < sheetNum; i++)
            {
                ISheet sheet = _workbook.GetSheetAt(i);
                var worksheet = new WorkSheet(this, sheet, i);
                Sheets.Add(worksheet);
            }
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <returns></returns>
        public override IWorkSheet CreateSheet()
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public override IWorkSheet CreateSheet(string sheetName)
        {
            var sheet = _workbook.CreateSheet(sheetName);
            var worksheet = new WorkSheet(this, sheet, _sheetIndex);
            _sheetIndex++;
            return worksheet;
        }

        /// <summary>
        /// 获取或创建工作表
        /// </summary>
        /// <returns></returns>
        public override IWorkSheet GetOrCreateSheet()
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 创建工作表
        /// </summary>
        /// <param name="sheetName">工作表名称</param>
        /// <returns></returns>
        public override IWorkSheet GetOrCreateSheet(string sheetName)
        {
            throw new System.NotImplementedException();
        }

        //public override IWorkSheet CloneSheet(int sheetIndex)
        //{
        //    throw new System.NotImplementedException();
        //}

        //public override IWorkSheet CloneSheet(string sheetName)
        //{
        //    throw new System.NotImplementedException();
        //}

        public override void SaveToFile(string fileName)
        {
            using (var workbook = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                _workbook.Write(workbook);
            }
        }

        public override void SaveToStream(Stream stream)
        {
            throw new System.NotImplementedException();
        }

        public override byte[] SaveToBuffer()
        {
            throw new System.NotImplementedException();
        }
    }
}
