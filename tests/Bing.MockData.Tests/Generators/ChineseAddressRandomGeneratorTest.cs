using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators.Address;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class ChineseAddressRandomGeneratorTest: TestBase
    {
        public ChineseAddressRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_BatchGenerate()
        {
            var list = ChineseAddressRandomGenerator.Instance.BatchGenerate(1000);
            foreach (var item in list)
            {
                Output.WriteLine(item);
            }
        }
    }
}
