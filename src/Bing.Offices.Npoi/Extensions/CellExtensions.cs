using System.Globalization;
using NPOI.SS.UserModel;

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

        
    }
}
