using System;
using System.Collections.Generic;
using System.Text;
using Bing.MockData.Generators;
using Bing.Utils.Develops;
using Xunit;
using Xunit.Abstractions;

namespace Bing.MockData.Tests.Generators
{
    public class EmailAddressRandomGeneratorTest:TestBase
    {
        public EmailAddressRandomGeneratorTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_BatchGenerate()
        {
            CodeTimer.CodeExecuteTime(() =>
            {
                var list = EmailAddressRandomGenerator.Instance.BatchGenerate(1000);
                foreach (var item in list)
                {
                    Output.WriteLine(item);
                }
            });
            
        }
    }
}
