using System;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core.Styles;
using Bing.Offices.Excels.Npoi.Core;
using Xunit;

namespace Bing.Offices.Excels.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            IWorkbook workbook=new Workbook(ExcelVersion.Xlsx);
            var sheet = workbook.CreateSheet("测试工作表");
            var row = sheet.CreateRow();
            var cell = row.CreateCell();
            cell.SetValue("测试信息11111111111111111111111111");
            cell.SetStyle(new CellStyle()
            {
                FontColor = Color.Blue,
                BackgroundColor = Color.Red,
                FillPattern = FillPattern.SolidForeground
            });
            workbook.SaveToFile("D:\\测试Npoi_007.xlsx");
        }
    }
}
