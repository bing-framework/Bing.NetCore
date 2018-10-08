using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// Excel 属性配置
    /// </summary>
    public class PropertyConfiguration:IColumnMap
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// 是否自动索引，如果<see cref="Index"/>未设置，并且<see cref="AutoIndex"/>设置为true，则将尝试通过其标题查找列索引
        /// </summary>
        public bool AutoIndex { get; internal set; }

        /// <summary>
        /// 列索引
        /// </summary>
        public int Index { get; set; } = -1;

        /// <summary>
        /// 是否允许合并相同值的单元格
        /// </summary>
        public bool AllowMerge { get; internal set; }

        /// <summary>
        /// 数据格式化
        /// </summary>
        public string Formatter { get; internal set; }

        /// <summary>
        /// 是否忽略映射
        /// </summary>
        public bool Ignore { get; internal set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object DefaultValue { get; internal set; }

        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; internal set; }

        /// <summary>
        /// 自定义枚举
        /// </summary>
        public Type CustomEnum { get; internal set; }

        /// <summary>
        /// 是否忽略导出映射
        /// </summary>
        public bool IgnoreExport { get; internal set; }

        /// <summary>
        /// 是否忽略导入映射
        /// </summary>
        public bool IgnoreImport { get; internal set; }

        /// <summary>
        /// 单元格值校验器
        /// </summary>
        public CellValueValidator CellValueValidator { get; internal set; }

        /// <summary>
        /// 单元格值转换器
        /// </summary>
        public CellValueConverter CellValueConverter { get; internal set; }

        /// <summary>
        /// 列索引
        /// </summary>
        /// <param name="index">列索引</param>
        /// <returns></returns>
        public PropertyConfiguration HasExcelIndex(int index)
        {
            if (index < 0)
            {
                throw new IndexOutOfRangeException("索引不能小于0");
            }

            Index = index;
            AutoIndex = false;
            return this;
        }

        /// <summary>
        /// 标题
        /// </summary>
        /// <param name="title">标题</param>
        /// <returns></returns>
        public PropertyConfiguration HasExceclTitle(string title)
        {
            Title = title;
            return this;
        }

        /// <summary>
        /// 数据格式化
        /// </summary>
        /// <param name="formatter">数据格式化</param>
        /// <returns></returns>
        public PropertyConfiguration HasDataFormatter(string formatter)
        {
            Formatter = formatter;
            return this;
        }

        /// <summary>
        /// 自动索引
        /// </summary>
        /// <returns></returns>
        public PropertyConfiguration HasAutoIndex()
        {
            AutoIndex = true;
            Index = -1;
            return this;
        }

        /// <summary>
        /// 值转换器
        /// </summary>
        /// <param name="cellValueConverter">值转换器</param>
        /// <returns></returns>
        public PropertyConfiguration HasValueConverter(CellValueConverter cellValueConverter)
        {
            CellValueConverter = cellValueConverter;
            return this;
        }

        /// <summary>
        /// 值校验器
        /// </summary>
        /// <param name="cellValueValidator">值校验器</param>
        /// <returns></returns>
        public PropertyConfiguration HasValueValidator(CellValueValidator cellValueValidator)
        {
            CellValueValidator = cellValueValidator;
            return this;
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <returns></returns>
        public PropertyConfiguration IsMergeEnabled()
        {
            AllowMerge = true;
            return this;
        }

        /// <summary>
        /// 忽略映射
        /// </summary>
        /// <param name="exportingIsIgnored">忽略导出</param>
        /// <param name="importingIsIgnored">忽略导入</param>
        /// <returns></returns>
        public PropertyConfiguration IsIgnored(bool exportingIsIgnored, bool importingIsIgnored)
        {
            IgnoreExport = exportingIsIgnored;
            IgnoreImport = importingIsIgnored;
            return this;
        }

        /// <summary>
        /// 忽略映射
        /// </summary>
        /// <param name="index">列索引</param>
        /// <param name="title">标题</param>
        /// <param name="formatter">数据格式化</param>
        /// <param name="exportingIsIgnored">忽略导出</param>
        /// <param name="importingIsIgnored">忽略导入</param>
        public void IsIgnored(int index, string title, string formatter = null, bool exportingIsIgnored = true,
            bool importingIsIgnored = true)
        {
            if (index < 0)
            {
                throw new IndexOutOfRangeException("索引不能小于0");
            }

            Index = index;
            Title = title;
            Formatter = formatter;
            IgnoreExport = exportingIsIgnored;
            IgnoreImport = importingIsIgnored;
        }

        /// <summary>
        /// 设置Excel单元格，指定索引
        /// </summary>
        /// <param name="index">列索引</param>
        /// <param name="title">标题</param>
        /// <param name="formatter">数据格式化</param>
        /// <param name="allowMerge">是否合并单元格</param>
        /// <param name="cellValueConverter">值转换器</param>
        public void HasExcelCell(int index, string title, string formatter = null, bool allowMerge = false,
            CellValueConverter cellValueConverter = null)
        {
            if (index < 0)
            {
                throw new IndexOutOfRangeException("索引不能小于0");
            }

            Index = index;
            Title = title;
            Formatter = formatter;
            AutoIndex = false;
            AllowMerge = allowMerge;
            CellValueConverter = cellValueConverter;
        }

        /// <summary>
        /// 设置Excel单元格，自动发现索引
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="formatter">数据格式化</param>
        /// <param name="allowMerge">是否合并单元格</param>
        /// <param name="cellValueConverter">值转换器</param>
        public void HasAutoIndexExcelCell(string title, string formatter = null, bool allowMerge = false,
            CellValueConverter cellValueConverter = null)
        {
            Index = -1;
            Title = title;
            Formatter = formatter;
            AutoIndex = true;
            AllowMerge = allowMerge;
            CellValueConverter = cellValueConverter;
        }
    }
}
