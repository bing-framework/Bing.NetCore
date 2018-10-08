using System;
using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi.Extensions
{
    /// <summary>
    /// 行(<see cref="IRow"/>) 扩展
    /// </summary>
    public static class RowExtensions
    {
        /// <summary>
        /// 获取或创建单元格
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="cellIndex">单元格索引</param>
        /// <returns></returns>
        public static ICell GetOrCreateCell(this IRow row, int cellIndex)
        {
            return row.GetCell(cellIndex) ?? row.CreateCell(cellIndex);
        }

        /// <summary>
        /// 创建单元格并进行操作
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="cellIndex">单元格索引</param>
        /// <param name="action">单元格操作</param>
        /// <returns></returns>
        public static IRow CreateCell(this IRow row, int cellIndex, Action<ICell> action)
        {
            var cell = row.GetOrCreateCell(cellIndex);
            action?.Invoke(cell);
            return row;
        }

        /// <summary>
        /// 清空内容
        /// </summary>
        /// <param name="row">行</param>
        /// <returns></returns>
        public static IRow ClearContent(this IRow row)
        {
            foreach (var cell in row.Cells)
            {
                cell.SetCellValue(string.Empty);
            }
            return row;
        }

        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="index">单元格索引</param>
        /// <param name="eval">计算公式</param>
        /// <returns></returns>
        public static string GetCellValue(this IRow row, int index, IFormulaEvaluator eval = null)
        {
            var cell = row.GetCell(index);

            return cell?.GetCellValue(eval);
        }
    }
}
