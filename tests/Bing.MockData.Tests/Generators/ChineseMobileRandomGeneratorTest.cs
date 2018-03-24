using System;
using Bing.MockData.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class ChineseMobileRandomGeneratorTest:TestBase
    {
        public ChineseMobileRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_Generate()
        {
            for (int i = 0; i < 1000; i++)
            {
                Output.WriteLine(ChineseMobileRandomGenerator.Instance.Generate());
            }
        }

        [Fact]
        public void Test_GenerateFake()
        {
            for (int i = 0; i < 1000; i++)
            {
                Output.WriteLine(ChineseMobileRandomGenerator.Instance.GenerateFake());
            }
        }
    }
}
