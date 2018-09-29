using System;
using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Attributes
{
    public class ExcelFreezeAttribute:Attribute,IFreezeMap
    {
        public int ColumnSplit { get; set; }
        public int RowSpit { get; set; }
        public int LeftMostColumn { get; set; }
        public int TopRow { get; set; }
    }
}
