using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// Excel 属性配置
    /// </summary>
    public class PropertyConfiguration:IColumnMap
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public bool AutoIndex { get; set; }
        public int Index { get; set; } = -1;
        public bool AllowMerge { get; set; }
        public string Format { get; set; }
        public string DateFormat { get; set; }
        public bool Ignore { get; set; }
        public object DefaultValue { get; set; }
        public string Value { get; set; }
        public Type CustomEnum { get; set; }
        public bool IgnoreExport { get; set; }
        public bool IgnoreImport { get; set; }
    }
}
