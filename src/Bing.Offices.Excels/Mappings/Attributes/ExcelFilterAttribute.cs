using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Attributes
{
    public class ExcelFilterAttribute:Attribute,IFilterMap
    {
        public int FirstRow { get; set; }
        public int? LastRow { get; set; }
        public int FirstColumn { get; set; }
        public int LastColumn { get; set; }
    }
}
