using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Attributes
{
    public class ExcelStatisticsAttribute:Attribute,IStatisticsMap
    {
        public string Name { get; set; }
        public string Formula { get; set; }
        public int[] Columns { get; set; }
    }
}
