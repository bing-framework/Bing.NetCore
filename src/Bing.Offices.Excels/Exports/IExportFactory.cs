namespace Bing.Offices.Excels.Exports
{
    /// <summary>
    /// 文件导出器工厂
    /// </summary>
    public interface IExportFactory
    {
        /// <summary>
        /// 创建文件导出器
        /// </summary>
        /// <param name="version">Excel版本</param>
        /// <returns></returns>
        IExport Create(ExcelVersion version);
    }
}
