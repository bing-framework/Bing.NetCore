using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;

namespace Bing.Offices.Excels.Npoi.Core
{
    /// <summary>
    /// 基于Npoi的单元范围
    /// </summary>
    public class Range:RangeBase
    {
        protected override IRow CreateRow(int rowIndex)
        {
            throw new System.NotImplementedException();
        }

        protected override void AddPlaceholderCell(ICell cell, int rowIndex)
        {
            throw new System.NotImplementedException();
        }
    }
}
