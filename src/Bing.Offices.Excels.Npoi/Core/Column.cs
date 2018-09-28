using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Core.Styles;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的单元列
    /// </summary>
    public class Column:ColumnBase
    {
        public override IColumn SetWidth(int width)
        {
            throw new System.NotImplementedException();
        }

        public override int GetWidth()
        {
            throw new System.NotImplementedException();
        }

        public override IColumn SetStyle(CellStyle style)
        {
            throw new System.NotImplementedException();
        }
    }
}
