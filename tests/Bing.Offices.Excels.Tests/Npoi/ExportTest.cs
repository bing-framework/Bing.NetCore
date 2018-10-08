using System;
using System.Collections.Generic;
using Bing.Offices.Excels.Abstractions;
using Bing.Offices.Excels.Core;
using Bing.Offices.Excels.Npoi.Exports;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Offices.Excels.Tests.Npoi
{
    /// <summary>
    /// 导出器 测试
    /// </summary>
    public class ExportTest: TestBase
    {
        /// <summary>
        /// 标题列表
        /// </summary>
        private List<string> _titles = new List<string>() { "系统编号","用户名","密码","昵称","省","市","区"};

        /// <summary>
        /// 输出路径
        /// </summary>
        private string _outPath = "D:\\Npoi_Demo.xlsx";

        /// <summary>
        /// 版本号
        /// </summary>
        private ExcelVersion _version = ExcelVersion.Xlsx;

        /// <summary>
        /// 初始化一个<see cref="ExportTest"/>类型的实例
        /// </summary>
        public ExportTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 生成基础标题
        /// </summary>
        [Fact]
        public void Test_Generate_Base_Title()
        {
            ExportFactory.CreateExcel2007Export()
                .Head(_titles.ToArray())
                .WriteToFile("D:\\Test_Data", $"Test_{DateTime.Now:yyyyMMddHHmmss}");
        }

        /// <summary>
        /// 生成自定义标题
        /// </summary>
        [Fact]
        public void Test_Generate_Custome_Title()
        {
            List<ICell> oneCells = new List<ICell>
            {
                new Cell("系统编号", 1, 2),
                new Cell("用户名", 1, 2),
                new Cell("密码", 1, 2),
                new Cell("昵称", 1, 2),
                new Cell("地址信息", 3, 1)
            };
            List<ICell> twosCells = new List<ICell> {new Cell("省"), new Cell("市"), new Cell("区")};

            ExportFactory.CreateExcel2007Export()
                .Head(oneCells.ToArray())
                .Head(twosCells.ToArray())
                .WriteToFile("D:\\Test_Data", $"Test_{DateTime.Now:yyyyMMddHHmmss}");
        }
    }
}
