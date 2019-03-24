using Bing.Offices.Excels.Mappings;
using Bing.Offices.Excels.Npoi.Imports;
using Bing.Offices.Excels.Tests.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Offices.Excels.Tests.Npoi
{
    /// <summary>
    /// 导入器 测试
    /// </summary>
    public class ImportTest:TestBase
    {
        public ImportTest(ITestOutputHelper output) : base(output)
        {
            ExcelSetting.Default.For<SimpleImprotModel>().Property(x => x.Id).HasAutoIndex().HasExceclTitle("标识");
            ExcelSetting.Default.For<SimpleImprotModel>().Property(x => x.Name).HasAutoIndex().HasExceclTitle("名称");
            ExcelSetting.Default.For<SimpleImprotModel>().Property(x => x.City).HasAutoIndex().HasExceclTitle("城市");
            ExcelSetting.Default.For<SimpleImprotModel>().AdjustAutoIndex();
        }

        /// <summary>
        /// 测试导入数据
        /// </summary>
        [Fact]
        public void Test_Import()
        {
            //var result = ImportFactory.CreateExcel2007Import(@"D:\Test_Data\Npoi_Demo.xlsx")
            //    .GetResult<SimpleImprotModel>();

            //foreach (var item in result)
            //{
            //    Output.WriteLine($"标识:{item.Id},名称:{item.Name},城市:{item.City}");
            //}
        }
    }
}
