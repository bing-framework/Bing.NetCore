using System;
using System.Collections.Generic;
using Bing.TestData.Generator.Number;
using Xunit;
using Xunit.Abstractions;

namespace Bing.TestData.Generator.Tests
{
    public class UnitTest1
    {
        protected readonly ITestOutputHelper Output;

        public UnitTest1(ITestOutputHelper output)
        {
            Output = output;
        }

        [Fact]
        public void Test1()
        {
            for (int i = 0; i < 1000; i++)
            {
                Output.WriteLine(ChineseMobileNumberGenerator.GetInstance().Generate());
            }
        }

        [Fact]
        public void Test2()
        {
            for (int i = 0; i < 1000; i++)
            {
                Output.WriteLine(ChineseMobileNumberGenerator.GetInstance().GenerateFake());
            }
        }
    }
}
