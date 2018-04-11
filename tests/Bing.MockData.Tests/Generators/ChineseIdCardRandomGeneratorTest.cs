using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators.IdCards;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class ChineseIdCardRandomGeneratorTest : TestBase
    {
        public ChineseIdCardRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_BatchGenerate()
        {
            var list = ChineseIdCardRandomGenerator.Instance.BatchGenerate(1000);
            foreach (var item in list)
            {
                Output.WriteLine(item);
            }
        }


        [Fact]
        public void Test_GenerateIssueOrg()
        {
            for (int i = 0; i < 100; i++)
            {
                Output.WriteLine(ChineseIdCardRandomGenerator.Instance.GenerateIssueOrg());
            }
        }
    }
}
