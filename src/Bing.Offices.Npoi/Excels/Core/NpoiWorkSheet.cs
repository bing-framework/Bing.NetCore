using System.Collections.Generic;
using System.Data;
using System.Drawing;
using Bing.Offices.Npoi.Extensions;
using NPOI.SS.UserModel;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的工作表
    /// </summary>
    public class NpoiWorkSheet: Bing.Offices.Excels.Core.WorkSheetBase
    {
        /// <summary>
        /// 工作表
        /// </summary>
        private ISheet _sheet;

        /// <summary>
        /// 列数
        /// </summary>
        public override int ColumnNum
        {
            get
            {
                int columnNum = 0;
                int rowNum = _sheet.LastRowNum;
                for (var i = 0; i < rowNum; i++)
                {
                    IRow row = _sheet.GetRow(i);
                    if (row == null)
                    {
                        continue;
                    }

                    if (row.LastCellNum > columnNum)
                    {
                        columnNum = row.LastCellNum;
                    }
                }

                return columnNum;
            }
        }

        /// <summary>
        /// 行数
        /// </summary>
        public override int RowNum => _sheet.LastRowNum;

        /// <summary>
        /// 初始化一个<see cref="NpoiWorkSheet"/>类型的实例
        /// </summary>
        /// <param name="sheet">工作表</param>
        public NpoiWorkSheet(ISheet sheet)
        {
            _sheet = sheet;
            SheetName = sheet.SheetName;
        }

        /// <summary>
        /// 获取单元格值
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override string GetCellValue(int rowIndex, int columnIndex)
        {
            if (rowIndex > _sheet.LastRowNum)
            {
                return string.Empty;
            }

            if (columnIndex > _sheet.GetRow(rowIndex).LastCellNum)
            {
                return string.Empty;
            }

            ICell cell = _sheet.GetRow(rowIndex).GetCell(columnIndex);
            return cell.GetCellValue();
        }

        /// <summary>
        /// 获取数据表内容
        /// </summary>
        /// <param name="hasHeader">是否包含表头，默认:false</param>
        /// <returns></returns>
        public override DataTable GetTableContent(bool hasHeader = false)
        {
            bool isColumnName = true;
            int startRow = 1;
            ICell cell;
            DataColumn column;
            IRow row;
            DataTable table = new DataTable();
            if (_sheet == null)
            {
                return table;
            }

            var rowCount = _sheet.LastRowNum;// 总行数
            if (rowCount > 0)
            {
                IRow firstRow = _sheet.GetRow(0);// 第一行
                var cellCount = firstRow.LastCellNum;// 列数

                // 构建datatable的列
                if (isColumnName)
                {
                    startRow = 1;// 如果第一行是列名，则从第二行开始读取
                    for (var i = firstRow.FirstCellNum; i < cellCount; ++i)
                    {
                        cell = firstRow.GetCell(i);

                        var cellValue = cell.GetCellValue();
                        column=new DataColumn(cellValue);
                        table.Columns.Add(column);
                    }
                }
                else
                {
                    for (var i = firstRow.FirstCellNum; i < cellCount; ++i)
                    {
                        column = new DataColumn($"column{i + 1}");
                        table.Columns.Add(column);
                    }
                }

                // 填充行
                for (var i = startRow; i <= rowCount; ++i)
                {
                    row = _sheet.GetRow(i);
                    if (row == null)
                    {
                        continue;
                    }

                    DataRow dataRow = table.NewRow();
                    for (var j = row.FirstCellNum; j < cellCount; ++j)
                    {
                        cell = row.GetCell(j);
                        if (cell == null)
                        {
                            dataRow[j] = "";
                        }
                        else
                        {
                            dataRow[j] = cell.GetCellValue();
                        }
                    }

                    table.Rows.Add(dataRow);
                }
            }

            return table;
        }

        /// <summary>
        /// 获取行列范围
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.IRange GetRange(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex)
        {
            return new NpoiRange(_sheet, startRowIndex, startColumnIndex, endRowIndex, endColumnIndex);
        }

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.ICell GetCell(int rowIndex, int columnIndex)
        {
            IRow row = _sheet.GetOrCreateRow(rowIndex - 1);
            ICell cell = row.GetOrCreateCell(columnIndex - 1);

            return new NpoiCell(cell);
        }

        /// <summary>
        /// 获取单元格的计算公式
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override string GetCellFormula(int rowIndex, int columnIndex)
        {
            return GetCell(rowIndex, columnIndex).GetValue();
        }

        /// <summary>
        /// 获取单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.IRow GetRow(int rowIndex)
        {
            return new NpoiRow(_sheet, rowIndex);
        }

        /// <summary>
        /// 获取单元列
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override Bing.Offices.Excels.Abstractions.IColumn GetColumn(int columnIndex)
        {
            return new NpoiColumn(_sheet, columnIndex);
        }

        /// <summary>
        /// 插入单元行
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        public override void InsertRow(int rowIndex)
        {
            _sheet.InsertRow(rowIndex);
        }

        /// <summary>
        /// 插入单元列
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        public override void InsertColumn(int columnIndex)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        public override void SetCellValue(string value, int rowIndex, int columnIndex)
        {
            GetCell(rowIndex, columnIndex).SetValue(value);
        }

        /// <summary>
        /// 设置单元格的计算公式
        /// </summary>
        /// <param name="formula">计算公式</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        public override void SetCellFormula(string formula, int rowIndex, int columnIndex)
        {
            GetCell(rowIndex, columnIndex).SetValue(formula);
        }

        /// <summary>
        /// 设置指定行列范围颜色
        /// </summary>
        /// <param name="range">行列范围</param>
        /// <param name="color">颜色</param>
        public override void SetRangeColor(Bing.Offices.Excels.Abstractions.IRange range, Color color)
        {
            range.SetBackgroundColor(color);
        }

        /// <summary>
        /// 设置单元格颜色
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <param name="color">颜色</param>
        public override void SetCellColor(int rowIndex, int columnIndex, Color color)
        {
            GetCell(rowIndex,columnIndex).SetBackgroundColor(color);
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="range">行列范围</param>
        public override void MergeCell(Bing.Offices.Excels.Abstractions.IRange range)
        {
            range.Merge();
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        public override void MergeCell(int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex)
        {
            GetRange(startRowIndex - 1, startColumnIndex - 1, endRowIndex - 1, endColumnIndex - 1).Merge();
        }

        /// <summary>
        /// 从单元行中获取数据。横向所有数据
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public override List<string> GetDataFromRow(int rowIndex)
        {
            List<string> rowData = new List<string>();
            for (var i = 0; i < ColumnNum; i++)
            {
                rowData.Add(GetCellValue(rowIndex,i));
            }

            return rowData;
        }

        /// <summary>
        /// 从单元列中获取数据。纵向所有数据
        /// </summary>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public override List<string> GetDataFromColumn(int columnIndex)
        {
            List<string> columnData = new List<string>();
            for (var i = 0; i < RowNum; i++)
            {
                columnData.Add(GetCellValue(i, columnIndex));
            }

            return columnData;
        }
    }
}
