using NPOI.HSSF.UserModel;
using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Linq;
using Bing.Utils.Extensions;
using NPOI.SS.Util;
using Bing.Offices.Excels.Models;

namespace Bing.Utils.Helpers
{
    // TODO: 1. 确认功能是否需根进行拆分到其它项目，如Bing.Office, Bing.Office.Npoi。
    //       2. 程序中存在命名不规范（有歧义，如 方法名Download ）
    //       3. 功能类引自于原CMS8.0框架，目前仅处理.xls，未扩展.xlsx处理，部分功能可进行优化重构。
    public static class ExcelHelper
    {
        /// <summary>
        /// 导出DataTable为Excel格式的内存数据
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建列头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns></returns>
        public static byte[] ExportToBuffer(DataTable dt,
            List<string> columnTitles = null,
            string titleName = "",
            string templatePath = "",
            bool createHeader = true,
            string sheetName = "",
            int startRowNum = 2,
            byte[] dataValidity = null)
        {
            Debug.Assert(null != dt);

            using (var dset = new DataSet())
            {
                dset.Tables.Add(dt);

                var workBook = Datatable2ExcelWorkbook(dset, columnTitles, titleName,
                    templatePath, createHeader, sheetName, startRowNum, dataValidity);

                if (null == workBook)
                {
                    return new byte[0];
                }

                using (var ms = new MemoryStream())
                {
                    workBook.Write(ms);
                    ms.Flush();
                    workBook.Close();
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 导出DataTable到文件。
        /// </summary>
        /// <param name="dt">数据文件</param>
        /// <param name="fileName">文件名(fullname.xls)</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建列头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns></returns>
        public static bool ExportToFile(DataTable dt,
            string fileName,
            List<string> columnTitles = null,
            string titleName = "",
            string templatePath = "",
            bool createHeader = true,
            string sheetName = "",
            int startRowNum = 2,
            byte[] dataValidity = null)
        {
            Debug.Assert(null != dt && !string.IsNullOrEmpty(fileName));

            using (var dset = new DataSet())
            {
                dset.Tables.Add(dt);

                var workBook = Datatable2ExcelWorkbook(dset, columnTitles, titleName,
                    templatePath, createHeader, sheetName, startRowNum, dataValidity);

                if (null == workBook)
                {
                    return false;
                }

                using (var sw = File.Create(fileName))
                {
                    workBook.Write(sw);
                    workBook.Close();
                }

                return true;
            }
        }

        /// <summary>
        /// 根据模板生成Excel
        /// </summary>
        /// <param name="dataSource">数据源</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建列头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns>System.Byte[][].</returns>
        private static IWorkbook Datatable2ExcelWorkbook(DataSet dataSource,
            List<string> columnTitles = null,
            string titleName = "",
            string templatePath = "",
            bool createHeader = true,
            string sheetName = "",
            int startRowNum = 2,
            byte[] dataValidity = null)
        {
            //如果存在数据有效性的数据，直接处理数据有效性的内存数据
            var workbook = dataValidity != null ? GetTemplateWorkbook(dataValidity) : GetTemplateWorkbook(templatePath);
            bool defaultStyle = workbook == null;
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                int tableCount = 1;
                if (workbook == null)
                    workbook = new HSSFWorkbook();

                foreach (DataTable dataTable in dataSource.Tables)
                {
                    // 有传入sheetName，使用sheetName
                    // TableName为默认，使用Sheet+tableCount
                    // TableName不为默认，使用TableName
                    var tempSheetName = !string.IsNullOrEmpty(sheetName) ? sheetName :
                        ((dataTable.TableName.IndexOf("Table") >= 0) ? "Sheet" + tableCount : dataTable.TableName);

                    var sheet = workbook.GetSheet(tempSheetName);
                    if (sheet == null)
                    {
                        if (workbook.NumberOfSheets > 0)
                        {
                            sheet = workbook.GetSheetAt(0) ?? workbook.CreateSheet(tempSheetName);
                        }
                        else
                        {
                            sheet = workbook.CreateSheet(tempSheetName);
                        }
                    }

                    //拼接列头
                    if (columnTitles == null || columnTitles.Count == 0)
                    {
                        columnTitles = new List<string>();
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            columnTitles.Add(dataTable.Columns[i].ColumnName);
                        }
                    }

                    //设置标题
                    Title(workbook, sheet, titleName, columnTitles.Count, defaultStyle);

                    //列头的处理
                    if (createHeader)
                        HeaderCell(workbook, sheet, columnTitles, defaultStyle);

                    //填充内容
                    FillExcel(workbook, dataTable, sheet, defaultStyle, startRowNum);

                    tableCount++;
                }

                return workbook;
            }

            return null;
        }

        /// <summary>
        /// 转换字节数据为工作薄
        /// </summary>
        /// <param name="templateByte">The template byte.</param>
        /// <returns>HSSFWorkbook.</returns>
        private static HSSFWorkbook GetTemplateWorkbook(byte[] templateByte)
        {
            HSSFWorkbook workbook = null;
            if (templateByte.Length < 1)
                return workbook;

            using (Stream stream = new MemoryStream(templateByte))
            {
                workbook = new HSSFWorkbook(stream);
            }
            return workbook;
        }

        /// <summary>
        /// 获取模板工作簿对象
        /// </summary>
        /// <param name="templatePath">The template path.</param>
        /// <returns>HSSFWorkbook.</returns>
        private static HSSFWorkbook GetTemplateWorkbook(string templatePath)
        {
            HSSFWorkbook workbook = null;
            if (string.IsNullOrEmpty(templatePath))
                return workbook;
            using (var templateFs = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
            {
                workbook = new HSSFWorkbook(templateFs);
            }
            return workbook;
        }

        /// <summary>
        /// 表格的标题
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="sheet">The sheet.</param>
        /// <param name="title">The title.</param>
        /// <param name="columnNum">The column number.</param>
        /// <param name="defaultStyle">if set to <c>true</c> [default style].</param>
        private static void Title(HSSFWorkbook workbook,
            ISheet sheet,
            string title,
            int columnNum,
            bool defaultStyle = true)
        {
            if (!string.IsNullOrEmpty(title))
            {
                var rootRow = defaultStyle ? sheet.CreateRow(0) : sheet.GetRow(0);
                rootRow = rootRow ?? sheet.CreateRow(0);

                ICell rootCell = defaultStyle ? rootRow.CreateCell(0) : rootRow.Cells.Count > 0 ? rootRow.Cells[0] : rootRow.CreateCell(0);
                rootCell = rootCell ?? rootRow.CreateCell(0);
                if (defaultStyle)
                {
                    rootRow.Height = 729;
                    ICellStyle style = workbook.CreateCellStyle();
                    IFont font = workbook.CreateFont();
                    font.FontName = "宋体";
                    font.Boldweight = 396;
                    font.FontHeight = 396;
                    style.SetFont(font);
                    style.Alignment = HorizontalAlignment.Center;
                    style.VerticalAlignment = VerticalAlignment.Center;
                    style.WrapText = true;
                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(0, 0, 0, columnNum - 1));
                    rootCell.CellStyle = style;
                }
                rootCell.SetCellValue(title);
            }
        }

        /// <summary>
        /// 列头的处理
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="sheet">The sheet.</param>
        /// <param name="columnTitles">The column titles.</param>
        /// <param name="defaultStyle">if set to <c>true</c> [default style].</param>
        private static void HeaderCell(HSSFWorkbook workbook,
            ISheet sheet,
            List<string> columnTitles,
            bool defaultStyle = true)
        {
            //如果是默认则创建
            var dataRowFirst = defaultStyle ? sheet.CreateRow(1) : sheet.GetRow(1);
            dataRowFirst = dataRowFirst ?? sheet.CreateRow(1);
            ICellStyle[] styles = SetWorkbookStyle(workbook, dataRowFirst, defaultStyle);
            if (defaultStyle && styles != null && styles.Length > 0)
            {
                styles[0].FillPattern = FillPattern.SolidForeground;
                styles[0].FillForegroundColor = HSSFColor.Grey25Percent.Index;
            }
            for (int i = 0; i < columnTitles.Count; i++)
            {
                var newCell = defaultStyle ? dataRowFirst.CreateCell(i) : (dataRowFirst.Cells.Count > i ? dataRowFirst.Cells[i] : dataRowFirst.CreateCell(i));

                //设置默认样式
                if (styles != null && styles.Length > 0)
                {
                    newCell.CellStyle = styles[0];
                    sheet.SetColumnWidth(i, columnTitles[i].Length * 1827);//列宽
                }
                newCell.SetCellValue(columnTitles[i]);
            }
        }

        /// <summary>
        /// 填充内容
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="dataTable">The data table.</param>
        /// <param name="sheet">The sheet.</param>
        /// <param name="defaultStyle">if set to <c>true</c> [default style].</param>
        /// <param name="startRowNum">The start row number.</param>
        private static void FillExcel(HSSFWorkbook workbook,
            DataTable dataTable,
            ISheet sheet,
            bool defaultStyle = true,
            int startRowNum = 2)
        {
            int rowIndex = startRowNum;       // 起始行
            foreach (DataRow row in dataTable.Rows)
            {
                //如果默认样式，则创建行，否则获取行。便于获取样式。
                var dataRow = defaultStyle ? sheet.CreateRow(rowIndex) : sheet.GetRow(rowIndex);
                dataRow = dataRow ?? sheet.CreateRow(rowIndex);

                ICellStyle[] styles = SetWorkbookStyle(workbook, dataRow, defaultStyle, startRowNum);
                int columnIndex = 0;
                foreach (DataColumn column in dataTable.Columns)
                {
                    // 列序号赋值
                    if (columnIndex >= dataTable.Columns.Count)
                        break;

                    int styleIndex = defaultStyle ? 0 : columnIndex;
                    //如果当前行大于起始行，则创建。否则判断列数，存在则获取当前列的数。
                    var newCell = rowIndex > startRowNum ? dataRow.CreateCell(columnIndex) : (dataRow.Cells.Count > columnIndex ? dataRow.Cells[columnIndex] : dataRow.CreateCell(columnIndex));

                    if (styles.Length > 0)
                        newCell.CellStyle = styles[styleIndex];

                    string drValue = row[column].ToString();
                    switch (column.DataType.ToString())
                    {
                        case "System.String"://字符串类型
                            newCell.SetCellValue(drValue);
                            break;

                        case "System.DateTime"://日期类型
                                               //DateTime dateV;
                                               // DateTime.TryParse(drValue, out dateV);
                            newCell.SetCellValue(drValue);
                            break;

                        case "System.Boolean"://布尔型
                            bool boolV = false;
                            bool.TryParse(drValue, out boolV);
                            newCell.SetCellValue(boolV);
                            break;

                        case "System.Int16"://整型
                        case "System.Int32":
                        case "System.Int64":
                        case "System.Byte":
                            int intV = 0;
                            int.TryParse(drValue, out intV);
                            newCell.SetCellValue(intV);
                            break;

                        case "System.Decimal"://浮点型
                        case "System.Double":
                            double doubV = 0;
                            double.TryParse(drValue, out doubV);
                            newCell.SetCellValue(doubV);
                            break;

                        case "System.DBNull"://空值处理
                            newCell.SetCellValue("");
                            break;

                        case "System.Guid":
                            newCell.SetCellValue(drValue);
                            break;

                        default:
                            newCell.SetCellValue("");
                            break;
                    }
                    columnIndex++;
                }
                rowIndex++;
            }
            // 格式化当前sheet，用于数据total计算
            sheet.ForceFormulaRecalculation = true;
        }

        /// <summary>
        /// 设置样式
        /// </summary>
        /// <param name="workbook">工作簿对象</param>
        /// <param name="row">行对象</param>
        /// <param name="defaultStyle">是否默认</param>
        /// <param name="startRowNum">The start row number.</param>
        /// <returns>ICellStyle[][].</returns>
        private static ICellStyle[] SetWorkbookStyle(HSSFWorkbook workbook,
            IRow row,
            bool defaultStyle = true,
            int startRowNum = 2)
        {
            List<ICellStyle> styles = new List<ICellStyle>();
            if (defaultStyle)
            {
                if (workbook == null) return styles.ToArray();
                row.Height = 459;
                ICellStyle style = workbook.CreateCellStyle();
                style.Alignment = HorizontalAlignment.Center;
                style.VerticalAlignment = VerticalAlignment.Center;
                style.WrapText = true;
                style.BorderLeft = BorderStyle.Thin;
                style.BorderBottom = BorderStyle.Thin;
                style.BorderRight = BorderStyle.Thin;
                style.BorderTop = BorderStyle.Thin;
                styles.Add(style);
            }
            else
            {
                //如果不是前三行
                if (row.RowNum >= startRowNum)
                {
                    ISheet currentSheet = row.Sheet;
                    IRow styleRow = currentSheet.GetRow(startRowNum);//获取模板样式行
                    row.Height = styleRow.Height;
                    if (styleRow != null && styleRow.RowStyle != null)
                        row.RowStyle = styleRow.RowStyle;
                    foreach (ICell styleCell in styleRow.Cells)
                    {
                        if (styleCell != null && styleCell.CellStyle != null)
                            styles.Add(styleCell.CellStyle);
                    }
                }
            }
            return styles.ToArray();
        }

        /// <summary>
        /// 导入Excel并返回一个DataTable
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="maxColumn">The maximum column.</param>
        /// <param name="skipRow">默认跳过前2行（第一行标题，第二行表头）</param>
        /// <returns>DataTable.</returns>
        /// <exception cref="System.Exception"></exception>
        public static DataTable ImportExcel(string filePath, int maxColumn, int skipRow)
        {
            return ImportExcel(filePath, maxColumn, skipRow, null);
        }

        /// <summary>
        /// 导入Excel并返回一个DataTable
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="maxColumn">The maximum column.</param>
        /// <param name="skipRow">默认跳过前2行（第一行标题，第二行表头）</param>
        /// <param name="columnNames">The column names.</param>
        /// <returns>DataTable.</returns>
        /// <exception cref="System.Exception"></exception>
        public static DataTable ImportExcel(string filePath, int maxColumn, int skipRow, string[] columnNames)
        {
            DataTable result = null;
            //if (File.Exists(filePath))
            {
                var workbook = GetTemplateWorkbook(filePath);
                if (workbook != null)
                {
                    var sheet = workbook.GetSheetAt(0);
                    if (sheet != null)
                    {
                        ClearEmptyCell(sheet);
                        var dt = new DataTable("ImportExcelData");

                        for (int i = skipRow; i <= sheet.LastRowNum; i++)
                        {
                            var row = sheet.GetRow(i);
                            if (row != null)
                            {
                                DataRow dataRow = dt.NewRow();
                                List<string> rowList = new List<string>();
                                if (row.LastCellNum < maxColumn)
                                {
                                    //throw new Exception(string.Format("设置最大列[{0}],超过[{1}]行的最大列[{2}]。", maxColumn, i, row.LastCellNum));
                                }
                                for (int j = 0; j < maxColumn; j++)
                                {
                                    if (dt.Columns.Count < maxColumn)
                                    {
                                        if (columnNames != null && columnNames.Length >= dt.Columns.Count)
                                            dt.Columns.Add(columnNames[dt.Columns.Count]);
                                        else
                                            dt.Columns.Add();
                                    }
                                    if (row.GetCell(0) == null)
                                        break;
                                    string cellValue = row.GetCell(j) == null ? "" : row.GetCell(j).ToString();
                                    rowList.Add(cellValue.Trim());
                                }

                                dataRow.ItemArray = rowList.ToArray();
                                dt.Rows.Add(dataRow);
                            }
                        }
                        result = dt;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 导入Excel并返回一个List<T>对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <param name="maxColumn"></param>
        /// <param name="skipRow"></param>
        /// <param name="columnNames"></param>
        /// <returns></returns>
        public static List<T> ImportExcel<T>(string filePath, int maxColumn, int skipRow, string[] columnNames) where T : class, new()
        {
            List<T> result = new List<T>();
            //if (File.Exists(filePath))
            {
                var workbook = GetTemplateWorkbook(filePath);
                if (workbook != null)
                {
                    var sheet = workbook.GetSheetAt(0);
                    if (sheet != null)
                    {
                        ClearEmptyCell(sheet);
                        for (int i = skipRow; i <= sheet.LastRowNum; i++)
                        {
                            var row = sheet.GetRow(i);
                            if (row != null)
                            {
                                List<string> rowList = new List<string>();
                                if (row.LastCellNum < maxColumn)
                                {
                                    //throw new Exception(string.Format("设置最大列[{0}],超过[{1}]行的最大列[{2}]。", maxColumn, i, row.LastCellNum));
                                }
                                T model = new T();

                                PropertyInfo[] proInfo = typeof(T).GetProperties();
                                for (int index = 0; index < columnNames.Count(); index++)
                                {
                                    string cellValue = row.GetCell(index) == null ? "" : row.GetCell(index).ToString();
                                    PropertyInfo info = proInfo.Where(x => x.Name == columnNames[index]).FirstOrDefault();
                                    if (info == null) continue;
                                    switch (info.PropertyType.ToString())
                                    {
                                        case "System.String"://字符串类型
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, cellValue.Trim(), null);
                                            break;

                                        case "System.DateTime"://日期类型
                                            DateTime dateV;
                                            DateTime.TryParse(cellValue.Trim(), out dateV);
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, dateV, null);
                                            break;

                                        case "System.Boolean"://布尔型
                                            bool boolV = false;
                                            bool.TryParse(cellValue.Trim(), out boolV);
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, boolV, null);
                                            break;

                                        case "System.Int16"://整型
                                        case "System.Int32":
                                        case "System.Int64":
                                        case "System.Byte":
                                            int intV = 0;
                                            int.TryParse(cellValue.Trim(), out intV);
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, intV, null);
                                            break;

                                        case "System.Decimal"://浮点型
                                        case "System.Double":
                                            double doubV = 0;
                                            double.TryParse(cellValue.Trim(), out doubV);
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, doubV, null);
                                            break;

                                        case "System.DBNull"://空值处理
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, string.Empty, null);
                                            break;

                                        case "System.Guid":
                                            Guid guidV = Guid.Empty;
                                            if (cellValue.Trim() != guidV.ToString())
                                                guidV = Guid.Parse(cellValue.Trim());
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, guidV, null);
                                            break;

                                        default:
                                            model.GetType().GetProperty(columnNames[index]).SetValue(model, string.Empty, null);
                                            break;
                                    }
                                }

                                result.Add(model);
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 获取Excel
        /// </summary>
        /// <typeparam name="T">指定要转换的泛型</typeparam>
        /// <param name="varlist">源泛型</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] List2Excel<T>(List<T> varlist)
        {
            return DownloadExcel<T>(varlist, null);
        }

        /// <summary>
        /// 获取Excel
        /// </summary>
        /// <typeparam name="T">指定要转换的泛型</typeparam>
        /// <param name="sources">源泛型</param>
        /// <param name="columnTitles">列头的名称（传空就是用默认的）</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel<T>(List<T> sources, List<string> columnTitles)
        {
            DataSet dataset = new DataSet();
            if (sources != null && sources.Count > 0)
                dataset.Tables.Add(sources.ToDataTable<T>() /* ModelHelper.ListToDataTable<T>(sources, "")*/);
            return DownloadExcel(dataset, columnTitles);
        }

        /// <summary>
        /// 获取Excel
        /// </summary>
        /// <param name="dataSource">The data source.</param>
        /// <param name="columnTitles">列头的名称（传空就是用默认的）</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel(DataSet dataSource, List<string> columnTitles)
        {
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                var workbook = new HSSFWorkbook();
                foreach (DataTable dataTable in dataSource.Tables)
                {
                    var sheet = workbook.CreateSheet(dataTable.TableName);
                    sheet.CreateFreezePane(0, 2, 0, 2);
                    //拼接列头
                    if (columnTitles == null || columnTitles.Count == 0)
                    {
                        columnTitles = new List<string>();
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            columnTitles.Add(dataTable.Columns[i].ColumnName);
                        }
                    }
                    //列头的处理
                    HeaderCell(workbook, sheet, columnTitles);
                    //填充内容
                    FillExcel(workbook, dataTable, sheet);
                }
                //返回文件的二进制
                return Download(workbook);
            }
            return null;
        }

        /// <summary>
        /// 根据模板生成Excel
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">源数据.</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建猎头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel<T>(List<T> sources, List<string> columnTitles, string titleName, string templatePath = "", bool createHeader = true, string sheetName = "", int startRowNum = 2, byte[] dataValidity = null)
        {
            var dataset = new DataSet();
            if (sources != null && sources.Count > 0)
                dataset.Tables.Add(sources.ToDataTable<T>() /*ModelHelper.ListToDataTable<T>(sources, "")*/);
            return DownloadExcel(dataset, columnTitles, titleName, templatePath, createHeader, sheetName, startRowNum, dataValidity);
        }

        /// <summary>
        /// 根据模板生成Excel
        /// </summary>
        /// <param name="dataSource">数据源</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建猎头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel(DataSet dataSource, List<string> columnTitles, string titleName, string templatePath, bool createHeader, string sheetName, int startRowNum, byte[] dataValidity)
        {
            //如果存在数据有效性的数据，直接处理数据有效性的内存数据
            var workbook = dataValidity != null ? GetTemplateWorkbook(dataValidity) : GetTemplateWorkbook(templatePath);
            bool defaultStyle = (workbook == null);
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                int tableCount = 1;
                if (workbook == null)
                    workbook = new HSSFWorkbook();
                foreach (DataTable dataTable in dataSource.Tables)
                {
                    //有传入sheetName，使用sheetName
                    ////TableName为默认，使用Sheet+tableCount
                    ////TableName不为默认，使用TableName
                    var tempSheetName = !string.IsNullOrEmpty(sheetName) ? sheetName : ((dataTable.TableName.IndexOf("Table") >= 0) ? "Sheet" + tableCount : dataTable.TableName);

                    var sheet = workbook.GetSheet(tempSheetName);
                    if (sheet == null)
                    {
                        if (workbook.NumberOfSheets > 0)
                        {
                            sheet = workbook.GetSheetAt(0) ?? workbook.CreateSheet(tempSheetName);
                        }
                        else
                        {
                            sheet = workbook.CreateSheet(tempSheetName);
                        }
                    }
                    //拼接列头
                    if (columnTitles == null || columnTitles.Count == 0)
                    {
                        columnTitles = new List<string>();
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            columnTitles.Add(dataTable.Columns[i].ColumnName);
                        }
                    }
                    //设置标题
                    Title(workbook, sheet, titleName, columnTitles.Count, defaultStyle);

                    //列头的处理
                    if (createHeader)
                        HeaderCell(workbook, sheet, columnTitles, defaultStyle);
                    //填充内容
                    FillExcel(workbook, dataTable, sheet, defaultStyle, startRowNum);

                    tableCount++;
                }
                //返回文件的二进制
                return Download(workbook);
            }
            return null;
        }

        /// <summary>
        /// 根据模板生成Excel(针对一个excal生成多个table)
        /// </summary>
        /// <param name="dataSource">数据源</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题（titleName[0]是第一个table或sheet1的表标题）</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建猎头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]（startRowNum[0]是第一个table或sheet1的数据起始行，startRowNum[1]是第二个table或sheet1的数据起始行。下标和table或sheet的位置对应）</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel(DataSet dataSource, List<string> columnTitles, List<string> titleName, string templatePath, bool createHeader, string sheetName, List<int> startRowNum, byte[] dataValidity)
        {
            //如果存在数据有效性的数据，直接处理数据有效性的内存数据
            var workbook = dataValidity != null ? GetTemplateWorkbook(dataValidity) : GetTemplateWorkbook(templatePath);
            bool defaultStyle = (workbook == null);
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                int tableCount = 1;
                if (workbook == null)
                    workbook = new HSSFWorkbook();
                foreach (DataTable dataTable in dataSource.Tables)
                {
                    var tempSheetName = !string.IsNullOrEmpty(sheetName) ? sheetName : (defaultStyle ? "Sheet" + tableCount : string.IsNullOrEmpty(dataTable.TableName) ? "Sheet" + tableCount : dataTable.TableName);
                    var sheet = workbook.GetSheet(tempSheetName);
                    if (sheet == null)
                    {
                        if (workbook.NumberOfSheets > 0)
                        {
                            sheet = workbook.GetSheetAt(0) ?? workbook.CreateSheet(tempSheetName);
                        }
                        else
                        {
                            sheet = workbook.CreateSheet(tempSheetName);
                        }
                    }
                    //拼接列头
                    var columnTitleList = new List<string>();
                    if (columnTitles == null || columnTitles.Count == 0)
                    {
                        columnTitles = new List<string>();
                        for (int i = 0; i < dataTable.Columns.Count; i++)
                        {
                            columnTitleList.Add(dataTable.Columns[i].ColumnName);
                        }
                    }
                    //设置标题 titleName默认为"" 下标和table或sheet的位置对应
                    Title(workbook, sheet, titleName.Count > tableCount - 1 ? titleName[tableCount - 1] : "", columnTitles.Count == 0 ? columnTitleList.Count : columnTitles.Count, defaultStyle);

                    //列头的处理 columnTitles传入的列头为空的时候  用columnTitles拼接的列头
                    if (createHeader)
                        HeaderCell(workbook, sheet, columnTitles.Count == 0 ? columnTitleList : columnTitles, defaultStyle);
                    //填充内容 startRowNum默认为2 下标和table或sheet的位置对应
                    FillExcel(workbook, dataTable, sheet, defaultStyle, startRowNum.Count > tableCount - 1 ? startRowNum[tableCount - 1] : 2);
                    tableCount++;
                }
                //返回文件的二进制
                return Download(workbook);
            }
            return null;
        }

        /// <summary>
        /// 根据模板生成Excel
        /// </summary>
        /// <param name="dataSource">数据源</param>
        /// <param name="columnTitles">列头</param>
        /// <param name="titleName">表标题</param>
        /// <param name="templatePath">模板文件地址[可选]</param>
        /// <param name="createHeader">创建猎头[可选]</param>
        /// <param name="sheetName">工作薄名称[可选]</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="dataValidity">The data validity.</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel(DataSet dataSource, List<ExcelColumnTitleModel> columnTitles, string titleName, string templatePath, bool createHeader, string sheetName, int startRowNum, byte[] dataValidity)
        {
            //如果存在数据有效性的数据，直接处理数据有效性的内存数据
            var workbook = dataValidity != null ? GetTemplateWorkbook(dataValidity) : GetTemplateWorkbook(templatePath);
            bool defaultStyle = (workbook == null);
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                int tableCount = 1;
                if (workbook == null)
                    workbook = new HSSFWorkbook();
                foreach (DataTable dataTable in dataSource.Tables)
                {
                    var tempSheetName = !string.IsNullOrEmpty(sheetName) ? sheetName : (!defaultStyle ? "Sheet" + tableCount : string.IsNullOrEmpty(dataTable.TableName) ? "Sheet" + tableCount : dataTable.TableName);
                    var sheet = workbook.GetSheet(tempSheetName);
                    if (sheet == null)
                    {
                        if (workbook.NumberOfSheets > 0)
                        {
                            sheet = workbook.GetSheetAt(0) ?? workbook.CreateSheet(tempSheetName);
                        }
                        else
                        {
                            sheet = workbook.CreateSheet(tempSheetName);
                        }
                    }
                    //设置标题
                    Title(workbook, sheet, titleName, dataTable.Columns.Count, defaultStyle);

                    //列头的处理
                    if (createHeader)
                        HeaderCell(workbook, sheet, columnTitles, defaultStyle);
                    //填充内容
                    FillExcel(workbook, dataTable, sheet, defaultStyle, startRowNum);

                    tableCount++;
                }
                //返回文件的二进制
                return Download(workbook);
            }
            return null;
        }

        #region 数据有效性（2）

        /// <summary>
        /// 根据模板生成Excel数据有效性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">源数据</param>
        /// <param name="templatePath">模板文件地址</param>
        /// <param name="sheetName">工作薄名称</param>
        /// <param name="arguments">定义有效序列范围</param>
        /// <param name="parameterSheetName">Name of the parameter sheet.</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="title">The title.</param>
        /// <returns>System.Byte[][].</returns>
        public static byte[] DownloadExcel<T>(List<T> sources, string templatePath, string sheetName, List<string[]> arguments, string parameterSheetName, int startRowNum = 2, string title = "")
        {
            DataSet dataset = new DataSet();
            if (sources != null && sources.Count > 0)
                dataset.Tables.Add(sources.ToDataTable<T>() /*ModelHelper.ListToDataTable<T>(sources, "")*/);
            return DownloadExcel(dataset, templatePath, sheetName, arguments, parameterSheetName, startRowNum, title);
        }

        /// <summary>
        /// 根据模板生成Excel数据有效性
        /// </summary>
        /// <param name="dataSource">数据源</param>
        /// <param name="templatePath">模板文件地址</param>
        /// <param name="sheetName">工作薄名称</param>
        /// <param name="arguments">定义有效序列范围</param>
        /// <param name="parameterSheetName">Name of the parameter sheet.</param>
        /// <param name="startRowNum">数据起始行[可选]</param>
        /// <param name="title">The title.</param>
        /// <returns>System.Byte[][].</returns>
        /// <exception cref="System.Exception">工作薄名称不得为空
        /// 定义参数不得为空
        /// </exception>
        public static byte[] DownloadExcel(DataSet dataSource, string templatePath, string sheetName, List<string[]> arguments, string parameterSheetName, int startRowNum, string title = "")
        {
            if (string.IsNullOrEmpty(sheetName))
                throw new Exception("工作薄名称不得为空");
            if (arguments == null || arguments.Count < 1)
                throw new Exception("定义参数不得为空");
            var workbook = GetTemplateWorkbook(templatePath);
            bool defaultStyle = (workbook == null);
            if (dataSource != null && dataSource.Tables.Count > 0)
            {
                int tableCount = 1;
                if (workbook == null)
                    workbook = new HSSFWorkbook();

                var parameterSheet = workbook.GetSheet(parameterSheetName);
                if (parameterSheet == null)
                    throw new Exception(string.Format("参数配置工作薄[{0}]不存在,请检查模板.", parameterSheetName));

                foreach (DataTable dataTable in dataSource.Tables)
                {
                    var sheet = workbook.GetSheet(sheetName);
                    if (sheet == null)
                        throw new Exception(string.Format("工作薄[{0}]不存在.", sheetName));

                    //填充内容
                    FillExcel(workbook, dataTable, sheet, defaultStyle, startRowNum);

                    //遍历定义数据列，并创建范围
                    foreach (var argument in arguments)
                    {
                        if (argument != null)
                        {
                            IName iname = workbook.GetName(argument[1]);//如果参数已经存在,则不创建
                            if (iname != null)
                            {
                                if (iname.NameName == argument[1])
                                    continue;
                            }

                            HSSFName range = (HSSFName)workbook.CreateName();
                            range.RefersToFormula = argument[0];
                            range.NameName = argument[1];
                        }
                    }
                    //获取当前模板的第一张工作薄，在此模板第一张工作薄默认为报表工作薄。
                    var reportSheet = workbook.GetSheetAt(0);
                    if (reportSheet == null || reportSheet.SheetName.Equals(sheetName))
                        throw new Exception(string.Format("报表工作薄不存在或与系统[{0}]工作薄重名.", sheetName));

                    //获取参数配置表第一行，第一列
                    var rangeRowOne = GetSheetRowData(parameterSheet, 0, 0, 1);
                    if (rangeRowOne == null || rangeRowOne.Count == 0)
                        throw new Exception(string.Format("参数配置工作薄[{0}]缺少关键参数,请检查模板.", parameterSheetName));

                    var rangeRowOneArr = rangeRowOne[0].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (rangeRowOneArr.Length < 1)
                        throw new Exception(string.Format("参数配置工作薄[{0}]缺少关键参数,请检查模板.", parameterSheetName));

                    int rangeStartRow = 0;
                    if (!int.TryParse(rangeRowOneArr[1], out rangeStartRow))
                        throw new Exception(string.Format("参数配置工作薄[{0}]关键参数转换出错,请检查模板.", parameterSheetName));

                    //获取参数配置表第二行，第一列
                    var rangeRowTwo = GetSheetRowData(parameterSheet, 1, 0, 1);
                    if (rangeRowTwo == null || rangeRowTwo.Count == 0)
                        throw new Exception(string.Format("参数配置工作薄[{0}]缺少关键参数,请检查模板.", parameterSheetName));

                    var rangeCols = rangeRowTwo[0].Split(new char[] { '，' }, StringSplitOptions.RemoveEmptyEntries);

                    //指定数据有效性
                    foreach (var rangeCol in rangeCols)
                    {
                        var parame = rangeCol.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parame.Length < 1)
                            throw new Exception(string.Format("参数配置工作薄[{0}]缺少关键参数,请检查模板.", parameterSheetName));

                        var rangeColIndexArr = parame[0].Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        var validName = parame[1];

                        foreach (var rangeColIndex in rangeColIndexArr)
                        {
                            //指定区域范围
                            CellRangeAddressList regions = new CellRangeAddressList(rangeStartRow, 65535, int.Parse(rangeColIndex), int.Parse(rangeColIndex));
                            //指定序列引用
                            DVConstraint constraint = DVConstraint.CreateFormulaListConstraint(validName);
                            //创建
                            HSSFDataValidation dataValidate = new HSSFDataValidation(regions, constraint);
                            reportSheet.AddValidationData(dataValidate);
                        }
                    }

                    #region 设置第一张工作簿第一行第一列标题内容（如果有）

                    if (!string.IsNullOrEmpty(title))
                    {
                        reportSheet.GetRow(0).Cells[0].SetCellValue(title);
                    }

                    #endregion

                    tableCount++;
                }
                //返回文件的二进制
                return Download(workbook);
            }
            return null;
        }

        #endregion

        /// <summary>
        /// 得到工作薄内行数据
        /// </summary>
        /// <param name="templatePath">模板地址</param>
        /// <param name="sheetName">sheetName</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="startColumn">开始列</param>
        /// <param name="maxColumn">最大列</param>
        /// <returns>List{System.String}.</returns>
        /// <exception cref="System.Exception"></exception>
        public static List<string> GetSheetRowData(string templatePath, string sheetName, int rowIndex, int startColumn, int maxColumn)
        {
            List<string> rowList = null;
            var workbook = GetTemplateWorkbook(templatePath);
            if (workbook == null) return rowList;
            var sheet = workbook.GetSheet(sheetName);
            if (sheet == null) return rowList;
            ClearEmptyCell(sheet);
            var row = sheet.GetRow(rowIndex);
            if (row != null)
            {
                rowList = new List<string>();
                if (row.LastCellNum < maxColumn)
                {
                    throw new Exception(string.Format("设置最大列[{0}],超过[{1}]行的最大列[{2}]。", maxColumn, rowIndex, row.LastCellNum));
                }
                for (int j = startColumn; j < maxColumn; j++)
                {
                    if (row.GetCell(0) == null)
                        break;
                    string cellValue = row.GetCell(j) == null ? "" : row.GetCell(j).ToString();
                    if (!string.IsNullOrEmpty(cellValue.Trim()))
                    {
                        rowList.Add(cellValue);
                    }
                }
            }
            return rowList;
        }

        /// <summary>
        /// 获取工作薄指定行列数据
        /// </summary>
        /// <param name="sheet">工作薄</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="startColumn">其实列</param>
        /// <param name="maxColumn">最大列</param>
        /// <returns>List{System.String}.</returns>
        /// <exception cref="System.Exception"></exception>
        private static List<string> GetSheetRowData(ISheet sheet, int rowIndex, int startColumn, int maxColumn)
        {
            List<string> rowList = null;
            if (sheet == null) return rowList;
            ClearEmptyCell(sheet);
            var row = sheet.GetRow(rowIndex);
            if (row != null)
            {
                rowList = new List<string>();
                if (row.LastCellNum < maxColumn)
                {
                    throw new Exception(string.Format("设置最大列[{0}],超过[{1}]行的最大列[{2}]。", maxColumn, rowIndex, row.LastCellNum));
                }
                for (int j = startColumn; j < maxColumn; j++)
                {
                    if (row.GetCell(0) == null)
                        break;
                    string cellValue = row.GetCell(j) == null ? "" : row.GetCell(j).ToString();

                    if (!string.IsNullOrEmpty(cellValue.Trim()))
                    {
                        rowList.Add(cellValue);
                    }
                }
            }
            return rowList;
        }

        /// <summary>
        /// 列头的处理(含合并单元格)
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="sheet">The sheet.</param>
        /// <param name="columnTitles">The column titles.</param>
        /// <param name="defaultStyle">if set to <c>true</c> [default style].</param>
        private static void HeaderCell(HSSFWorkbook workbook, ISheet sheet, List<ExcelColumnTitleModel> columnTitles, bool defaultStyle = true)
        {
            int nowColIndex = 0;
            for (int i = 0; i < columnTitles.Count; i++)
            {
                //如果是默认则创建
                var dataRowFirst = defaultStyle && sheet.GetRow(i + 1) == null ? sheet.CreateRow(i + 1) : sheet.GetRow(i + 1);
                dataRowFirst = dataRowFirst ?? sheet.CreateRow(i + 1);
                ICellStyle[] styles = SetWorkbookStyle(workbook, dataRowFirst, defaultStyle);

                for (int j = 0; j < columnTitles[i].ListColumn.Count; j++)
                {
                    ExcelColumn column = columnTitles[i].ListColumn[j];
                    CellRangeAddress region = new CellRangeAddress(i + 1, (column.Rowspan < i + 1 ? i + 1 : column.Rowspan), column.NowColIndex, (column.NowColIndex + (column.Colspan - 1)));
                    sheet.AddMergedRegion(region);
                    var newCell = defaultStyle ? dataRowFirst.CreateCell(column.NowColIndex) : (dataRowFirst.Cells.Count > i ? dataRowFirst.Cells[column.NowColIndex] : dataRowFirst.CreateCell(column.NowColIndex));
                    SetRegionBorder(styles[0], region, sheet);
                    //设置默认样式
                    if (styles != null && styles.Length > 0)
                    {
                        newCell.CellStyle = styles[0];
                        sheet.SetColumnWidth(nowColIndex, column.Title.Length * 1827);//列宽
                    }
                    newCell.SetCellValue(column.Title);
                }
            }
        }

        /// <summary>
        /// 设置合并单元格的边框
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="region"></param>
        /// <param name="sheet"></param>
        private static void SetRegionBorder(ICellStyle cs, CellRangeAddress region, ISheet sheet)
        {
            for (int i = region.FirstRow; i <= region.LastRow; i++)
            {
                IRow row = sheet.GetRow(i);
                if (row == null) row = sheet.CreateRow(i);
                for (int j = region.FirstColumn; j <= region.LastColumn; j++)
                {
                    ICell cell = row.GetCell(j);
                    if (cell == null)
                    {
                        cell = row.CreateCell(j);
                        cell.SetCellValue("");
                    }
                    cell.CellStyle = cs;
                }
            }
        }

        /// <summary>
        /// 生成文件
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <param name="fileName">Name of the file.</param>
        private static void Save(HSSFWorkbook workbook, string fileName)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                workbook.Write(ms);
                ms.Flush();
                workbook = null;
                using (FileStream tempFs = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    byte[] data = ms.ToArray();
                    tempFs.Write(data, 0, data.Length);
                    tempFs.Flush();
                }
            }
        }

        /// <summary>
        /// 返回文件的二进制
        /// </summary>
        /// <param name="workbook">The workbook.</param>
        /// <returns>System.Byte[][].</returns>
        private static byte[] Download(HSSFWorkbook workbook)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                workbook.Write(ms);
                ms.Flush();
                workbook = null;
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 处理空的列
        /// </summary>
        /// <param name="sheet">The sheet.</param>
        private static void ClearEmptyCell(ISheet sheet)
        {
            System.Collections.IEnumerator rows = sheet.GetRowEnumerator();
            int cellCount = sheet.GetRow(0).LastCellNum;
            for (int i = (sheet.FirstRowNum + 1); i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row != null)
                {
                    for (int j = row.FirstCellNum; j < cellCount; j++)
                    {
                        var c = row.GetCell(j);
                        if (c != null)
                        {
                            switch (c.CellType)
                            {
                                case CellType.Numeric:
                                    if (c.NumericCellValue == 0)
                                    {
                                        c.SetCellType(CellType.String);
                                        c.SetCellValue(string.Empty);
                                    }
                                    break;

                                case CellType.Blank:
                                case CellType.String:
                                    if (c.StringCellValue == "0")
                                        c.SetCellValue(string.Empty);
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }
}
