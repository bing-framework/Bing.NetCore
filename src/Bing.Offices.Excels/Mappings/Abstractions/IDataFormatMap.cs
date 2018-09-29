namespace Bing.Offices.Excels.Mappings.Abstractions
{
    /// <summary>
    /// 数据格式映射
    /// </summary>
    public interface IDataFormatMap
    {
        /// <summary>
        /// Excel 内置格式。
        /// 参考地址：https://poi.apache.org/apidocs/org/apache/poi/ss/usermodel/BuiltinFormats.htm
        /// </summary>
        short BuiltinFormat { get; set; }

        /// <summary>
        /// 自定义格式。
        /// 参考地址：https://support.office.com/en-nz/article/Create-or-delete-a-custom-number-format-78f2a361-936b-4c03-8772-09fab54be7f4
        /// </summary>
        string CustomFormat { get; set; }
    }
}
