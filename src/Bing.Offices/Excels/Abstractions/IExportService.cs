using System.Collections.Generic;
using Bing.Offices.Excels.Models.Parameters;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 导出服务
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// 获取单元格值
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        object GetCellValue(ExcelExportEntity entity, object obj);

        /// <summary>
        /// 获取集合的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        IList<T> GetListCellValue<T>(ExcelExportEntity entity, object obj);

        /// <summary>
        /// 获取行高
        /// </summary>
        /// <param name="excelParams"></param>
        /// <returns></returns>
        short GetRowHeight(List<ExcelExportEntity> excelParams);
    }
}
