using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Attributes
{
    public class ExcelAttribute:Attribute,IExcelMap
    {
        public bool AutoIndex { get; set; }
        public string Title { get; set; }
    }
}
