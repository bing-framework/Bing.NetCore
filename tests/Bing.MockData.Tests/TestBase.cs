using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators.Address;
using Bing.MockData.Generators.Banks;
using Bing.Utils.Develops;
using Bing.Utils.Json;
using Xunit;
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

        [Fact]
        public void OutPut()
        {            
            Output.WriteLine(BankConfig.GetBankInfoList().ToJson());
        }
    }
}
