using System.Drawing;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Enums;
using Bing.Offices.Npoi.Excels.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Offices.Tests.Npoi
{
    /// <summary>
    /// Npoi 工作簿
    /// https://github.com/EdwardLiulei/ExcelRW.git
    /// </summary>
    public class NpoiWorkbookTest:TestBase
    {
        public NpoiWorkbookTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_Save()
        {
            IWorkbook workbook = new NpoiWorkbook(ExcelFormat.Xlsx);
            var sheet = workbook.InsertSheet("测试工作表");
            sheet.InsertRow(1);
            var cell = sheet.GetCell(1, 1);
            cell.SetValue("测试数值");
            cell.SetFontColor(Color.Yellow);
            cell.SetBackgroundColor(Color.Red);
            cell.Italic = true;
            cell.Bold = true;
            workbook.Save("D:\\测试Npoi.xlsx");
        }
    }
}
