using System;
using System.Collections.Generic;
using System.Globalization;
using Bing.Offices.Core;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Bing.Offices.Npoi.Extensions
{
    /// <summary>
    /// 单元格(<see cref="ICell"/>) 扩展
    /// </summary>
    public static class CellExtensions
    {
        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        public static string GetCellValue(this ICell cell)
        {
            if (cell == null)
            {
                return string.Empty;
            }

            if (cell.CellType == CellType.String)
            {
                return cell.StringCellValue;
            }

            if (cell.CellType == CellType.Boolean)
            {
                return cell.BooleanCellValue.ToString();
            }

            if (cell.CellType == CellType.Formula)
            {
                return cell.CellFormula;
            }

            if (cell.CellType == CellType.Numeric)
            {
                return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
            }

            cell.SetCellType(CellType.String);
            return cell.StringCellValue;
        }

        #region SetValue(设置单元格值)

        /// <summary>
        /// 设置单元格值
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="value">值</param>
        public static void SetValue(this ICell cell, object value)
        {
            if (cell == null)
            {
                return;
            }

            if (value == null)
            {
                cell.SetCellValue(string.Empty);
                return;
            }

            if (value.GetType().FullName.Equals("System.Byte[]"))
            {
                var pictureIndex = cell.Sheet.Workbook.AddPicture((byte[]) value, PictureType.PNG);
                IClientAnchor anchor = cell.Sheet.Workbook.GetCreationHelper().CreateClientAnchor();
                anchor.Col1 = cell.ColumnIndex;
                anchor.Col2 = cell.ColumnIndex + cell.GetSpan().ColumnSpan;
                anchor.Row1 = cell.RowIndex;
                anchor.Row2 = cell.RowIndex + cell.GetSpan().RowSpan;
                IDrawing drawing = cell.Sheet.CreateDrawingPatriarch();
                IPicture picture = drawing.CreatePicture(anchor, pictureIndex);
                return;
            }

            TypeCode valueTypeCode = Type.GetTypeCode(value.GetType());
            switch (valueTypeCode)
            {
                case TypeCode.String:
                    cell.SetCellValue(Convert.ToString(value));
                    break;
                case TypeCode.DateTime:
                    cell.SetCellValue(Convert.ToDateTime(value));
                    break;
                case TypeCode.Boolean:
                    cell.SetCellValue(Convert.ToBoolean(value));
                    break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Byte:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    cell.SetCellValue(Convert.ToDouble(value));
                    break;
                default:
                    cell.SetCellValue(string.Empty);
                    break;
            }
        }

        #endregion

        #region GetSpan(获取单元格跨度信息)

        /// <summary>
        /// 获取单元格跨度信息
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        public static CellSpan GetSpan(this ICell cell)
        {
            var cellSpan = new CellSpan(1, 1);
            if (cell.IsMergedCell)
            {
                var regionsNum = cell.Sheet.NumMergedRegions;
                for (var i = 0; i < regionsNum; i++)
                {
                    var range = cell.Sheet.GetMergedRegion(i);
                    if (range.FirstRow == cell.RowIndex && range.FirstColumn == cell.ColumnIndex)
                    {
                        cellSpan.RowSpan = range.LastRow - range.FirstRow + 1;
                        cellSpan.ColumnSpan = range.LastColumn - range.FirstColumn + 1;
                        break;
                    }
                }
            }

            return cellSpan;
        }

        #endregion

        #region AddConditionalFormattingRules(添加条件格式规则)

        /// <summary>
        /// 添加条件格式规则
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="cfrs">条件格式规则</param>
        public static void AddConditionalFormattingRules(this ICell cell, IConditionalFormattingRule[] cfrs)
        {
            CellRangeAddress[] regions =
            {
                new CellRangeAddress(cell.RowIndex, cell.RowIndex, cell.ColumnIndex, cell.ColumnIndex),
            };
            cell.Sheet.SheetConditionalFormatting.AddConditionalFormatting(regions, cfrs);
        }

        #endregion

        #region GetConditionalFormattingRules(获取条件格式规则)

        /// <summary>
        /// 获取条件格式规则
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <returns></returns>
        public static IConditionalFormattingRule[] GetConditionalFormattingRules(this ICell cell)
        {
            var cfrs = new List<IConditionalFormattingRule>();
            ISheetConditionalFormatting scf = cell.Sheet.SheetConditionalFormatting;
            for (var i = 0; i < scf.NumConditionalFormattings; i++)
            {
                IConditionalFormatting cf = scf.GetConditionalFormattingAt(i);
                if (cell.ExistConditionalFormatting(cf))
                {
                    for (var j = 0; j < cf.NumberOfRules; j++)
                    {
                        cfrs.Add(cf.GetRule(j));
                    }
                }
            }
            return cfrs.ToArray();
        }

        /// <summary>
        /// 判断单元格是否存在条件格式
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="cf">条件格式</param>
        /// <returns></returns>
        private static bool ExistConditionalFormatting(this ICell cell, IConditionalFormatting cf)
        {
            CellRangeAddress[] cras = cf.GetFormattingRanges();
            foreach (var cra in cras)
            {
                if (cell.RowIndex >= cra.FirstRow && cell.RowIndex <= cra.LastRow &&
                    cell.ColumnIndex >= cra.FirstColumn && cell.ColumnIndex <= cra.LastColumn)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion        
    }
}
