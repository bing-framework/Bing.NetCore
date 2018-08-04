using System;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Bing.Offices.Npoi.Extensions
{
    /// <summary>
    /// 工作表(<see cref="ISheet"/>) 扩展
    /// </summary>
    public static class SheetExtensions
    {
        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static string GetCellValue(this ISheet sheet, int rowIndex, int columnIndex)
        {
            string value;
            if (IsMergedRegion(sheet, rowIndex, columnIndex))
            {
                value = GetMergedRegionValue(sheet, rowIndex, columnIndex);
            }
            else
            {
                IRow row = sheet.GetRow(rowIndex);
                ICell cell = row.GetCell(columnIndex);
                value = cell.GetCellValue();
            }
            return value;
        }

        /// <summary>
        /// 获取合并单元格的值
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static string GetMergedRegionValue(this ISheet sheet, int rowIndex, int columnIndex)
        {
            int sheetMergeCount = sheet.NumMergedRegions;
            for (int i = 0; i < sheetMergeCount; i++)
            {
                CellRangeAddress cellRange = sheet.GetMergedRegion(i);
                int firstColumn = cellRange.FirstColumn;
                int lastColumn = cellRange.LastColumn;
                int firstRow = cellRange.FirstRow;
                int lastRow = cellRange.LastRow;

                if (rowIndex >= firstRow && rowIndex <= lastRow)
                {
                    if (columnIndex >= firstColumn && columnIndex <= lastColumn)
                    {
                        IRow row = sheet.GetRow(firstRow);
                        ICell cell = row.GetCell(firstColumn);
                        return cell.GetCellValue();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 判断指定的单元格是否是合并单元格
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static bool IsMergedRegion(this ISheet sheet, int rowIndex, int columnIndex)
        {
            int sheetMergeCount = sheet.NumMergedRegions;
            for (int i = 0; i < sheetMergeCount; i++)
            {
                CellRangeAddress cellRange = sheet.GetMergedRegion(i);
                int firstColumn = cellRange.FirstColumn;
                int lastColumn = cellRange.LastColumn;
                int firstRow = cellRange.FirstRow;
                int lastRow = cellRange.LastRow;

                if (rowIndex >= firstRow && rowIndex <= lastRow)
                {
                    if (columnIndex >= firstColumn && columnIndex <= lastColumn)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 删除工作表中指定列，并移动右侧列到左侧列
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="columnIndex">列索引</param>
        public static void DeleteColumn(this ISheet sheet, int columnIndex)
        {
            int maxColumn = 0;
            for (int i = 0; i < sheet.LastRowNum + 1; i++)
            {
                IRow row = sheet.GetRow(i);                
                if (row == null)
                {
                    continue;
                }

                int lastColumn = row.LastCellNum;
                if (lastColumn > maxColumn)
                {
                    maxColumn = lastColumn;
                }

                if (lastColumn < columnIndex)
                {
                    continue;
                }

                for (int x = columnIndex + 1; x < lastColumn + 1; x++)
                {
                    ICell oldCell = row.GetCell(x - 1);
                    if (oldCell != null)
                    {
                        row.RemoveCell(oldCell);
                    }

                    ICell nextCell = row.GetCell(x);
                    if (nextCell != null)
                    {
                        ICell newCell = row.CreateCell(x - 1, nextCell.CellType);
                        CloneCell(oldCell, newCell);
                    }
                }
            }
            // 调整列宽
            for (int c = columnIndex; c < maxColumn; c++)
            {
                sheet.SetColumnWidth(c, sheet.GetColumnWidth(c + 1));
            }
        }

        /// <summary>
        /// 克隆单元格
        /// </summary>
        /// <param name="oldCell">旧单元格</param>
        /// <param name="newCell">新单元格</param>
        private static void CloneCell(ICell oldCell, ICell newCell)
        {
            newCell.CellComment = oldCell.CellComment;
            newCell.CellStyle = oldCell.CellStyle;

            switch (newCell.CellType)
            {
                case CellType.Boolean:
                    newCell.SetCellValue(oldCell.BooleanCellValue);
                    break;
                case CellType.Numeric:
                    newCell.SetCellValue(oldCell.NumericCellValue);
                    break;
                case CellType.String:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
                case CellType.Error:
                    newCell.SetCellValue(oldCell.ErrorCellValue);
                    break;
                case CellType.Formula:
                    newCell.SetCellValue(oldCell.CellFormula);
                    break;
                default:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
            }
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        public static CellRangeAddress MergedCell(this ISheet sheet, int firstRow, int lastRow, int firstColumn, int lastColumn)
        {
            var region = new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn);
            sheet.AddMergedRegion(region);
            return region;
        }

        /// <summary>
        /// 获取或创建行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public static IRow GetOrCreateRow(this ISheet sheet, int rowIndex)
        {
            return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        }

        /// <summary>
        /// 创建行并进行操作
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="action">行操作</param>
        /// <returns></returns>
        public static ISheet CreateRow(this ISheet sheet, int rowIndex, Action<IRow> action)
        {
            var row = sheet.GetOrCreateRow(rowIndex);
            action?.Invoke(row);
            return sheet;
        }
    }
}
