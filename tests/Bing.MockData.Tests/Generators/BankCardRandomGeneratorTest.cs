using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators.Banks;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class BankCardRandomGeneratorTest:TestBase
    {
        public BankCardRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_Generate()
        {
            for (int i = 0; i < 1000; i++)
            {
                Output.WriteLine(BankCardRandomGenerator.Instance.Generate());
            }
        }
    }
}
