using Bing.Offices.Excels.Models.Parameters;
using NPOI.SS.UserModel;

namespace Bing.Offices.Npoi.Excels.Exports.Styler
{
    /// <summary>
    /// Excel 导出样式器
    /// </summary>
    public interface IExcelExportStyler
    {
        /// <summary>
        /// 获取列表头样式
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        ICellStyle GetHeaderStyle(short color);

        /// <summary>
        /// 获取标题样式
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        ICellStyle GetTitleStyle(short color);

        /// <summary>
        /// 获取样式
        /// </summary>
        /// <param name="parity"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        ICellStyle GetStyles(bool parity, ExcelExportEntity entity);

        /// <summary>
        /// 获取样式
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="dataRow">数据行</param>
        /// <param name="entity"></param>
        /// <param name="obj">对象</param>
        /// <param name="data">数据</param>
        /// <returns></returns>
        ICellStyle GetStyles(ICell cell, int dataRow, ExcelExportEntity entity, object obj, object data);

    }
}
