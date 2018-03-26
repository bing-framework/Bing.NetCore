using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Bing.Utils.Tests
{
    public class TestBase
    {
        protected ITestOutputHelper Output;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
        }
    }
}
