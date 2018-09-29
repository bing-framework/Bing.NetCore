using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;
using Bing.Offices.Excels.Npoi.Core;
using Xunit;

namespace Bing.Offices.Excels.Tests.Npoi
{
    public class WorkbookTest
    {
        /// <summary>
        /// 标题列表
        /// </summary>
        private List<string> _titles = new List<string>() { "系统编号","用户名","密码","昵称"};

        /// <summary>
        /// 输出路径
        /// </summary>
        private string _outPath = "D:\\Npoi_Demo.xlsx";

        private ExcelVersion _version = ExcelVersion.Xlsx;

        [Fact]
        public void Test_Generate_Title()
        {
            IWorkbook workbook = new Workbook(_version);
            var sheet = workbook.GetOrCreateSheet("测试工作表");
            var row = sheet.CreateRow();
            foreach (var title in _titles)
            {
                var cell = row.CreateCell();
                cell.SetValue(title);
                cell.SetStyle(CellStyle.Head());
            }
            workbook.SaveToFile(_outPath);
        }
    }
}
