using System;
using Bing.Offices.Excels.Exports;

namespace Bing.Offices.Excels.Npoi.Exports
{
    /// <summary>
    /// 导出器工厂
    /// </summary>
    public class ExportFactory:IExportFactory
    {
        /// <summary>
        /// 创建导出器
        /// </summary>
        /// <param name="version">Excel版本</param>
        /// <returns></returns>
        public IExport Create(ExcelVersion version)
        {
            switch (version)
            {
                case ExcelVersion.Xlsx:
                    return new Excel2007Export();
                case ExcelVersion.Xls:
                    return new Excel2003Export();
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// 创建 Excel 2003 导出器
        /// </summary>
        /// <returns></returns>
        public static IExport CreateExcel2003Export()
        {
            return new ExportFactory().Create(ExcelVersion.Xls);
        }

        /// <summary>
        /// 创建 Excel 2007 导出器
        /// </summary>
        /// <returns></returns>
        public static IExport CreateExcel2007Export()
        {
            return new ExportFactory().Create(ExcelVersion.Xlsx);
        }
    }
}
