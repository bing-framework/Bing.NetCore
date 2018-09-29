namespace Bing.Offices.Excels.Imports
{
    /// <summary>
    /// 文件导入器工厂
    /// </summary>
    public interface IImportFactory
    {
        /// <summary>
        /// 创建文件导入器
        /// </summary>
        /// <param name="version">Excel版本</param>
        /// <returns></returns>
        IImport Create(ExcelVersion version);
    }
}
