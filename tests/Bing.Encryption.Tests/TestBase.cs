using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Bing.Encryption.Tests
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
