using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators;
using Bing.MockData.Generators.Banks;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class ChineseNameRandomGeneratorTest:TestBase
    {
        public ChineseNameRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_BatchGenerate()
        {
            var list = ChineseNameRandomGenerator.Instance.BatchGenerate(1000);
            foreach (var item in list)
            {
                Output.WriteLine(item);
            }
        }
    }
}
