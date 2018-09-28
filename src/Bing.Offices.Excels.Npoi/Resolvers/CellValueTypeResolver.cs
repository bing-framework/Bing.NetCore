using Bing.Offices.Excels.Core.Styles;
using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi.Resolvers
{
    /// <summary>
    /// 单元格类型解析器
    /// </summary>
    public class CellValueTypeResolver
    {
        /// <summary>
        /// 解析单元格值类型
        /// </summary>
        /// <param name="cellValueType">单元格值类型</param>
        /// <returns></returns>
        public static CellType Resolve(CellValueType cellValueType)
        {
            switch (cellValueType)
            {
                case CellValueType.Unknown:
                    return CellType.Unknown;
                case CellValueType.Number:
                    return CellType.Numeric;
                case CellValueType.Date:
                case CellValueType.String:
                    return CellType.String;
                case CellValueType.Formula:
                    return CellType.Formula;
                case CellValueType.Empty:
                    return CellType.Blank;
                case CellValueType.Boolean:
                    return CellType.Boolean;
                case CellValueType.Error:
                    return CellType.Error;
                default:
                    return CellType.Blank;
            }
        }
    }
}
