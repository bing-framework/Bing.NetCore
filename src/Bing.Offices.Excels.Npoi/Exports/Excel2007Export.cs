using System.IO;
using Bing.Offices.Excels.Exports;

namespace Bing.Offices.Excels.Npoi.Exports
{
    /// <summary>
    /// Npoi Excel 2007 导出操作
    /// </summary>
    public class Excel2007Export:ExcelExportBase
    {
        /// <summary>
        /// 初始化一个<see cref="Excel2007Export"/>类型的实例
        /// </summary>
        public Excel2007Export() : base(ExcelVersion.Xlsx, new Excel2007())
        {
        }

        public override IExport Title(string title)
        {
            throw new System.NotImplementedException();
        }
    }
}
