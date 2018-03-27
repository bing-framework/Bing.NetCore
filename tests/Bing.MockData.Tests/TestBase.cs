using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Develops;
using Xunit.Abstractions;

namespace Bing.MockData.Tests
{
    public class TestBase
    {
        protected ITestOutputHelper Output;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
            UnitTester.WriteLine = Output.WriteLine;
            CodeRamer.WriteLine = Output.WriteLine;
            CodeTimer.WriteLine = Output.WriteLine;
        }
    }
}
