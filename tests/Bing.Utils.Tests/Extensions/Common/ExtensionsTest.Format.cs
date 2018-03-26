using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 系统扩展测试 - 格式化扩展
    /// </summary>
    public partial class ExtensionsTest:TestBase
    {
        public ExtensionsTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_FormatMessage()
        {
            try
            {
                int num = 0;
                num = 1 / num;
            }
            catch (Exception e)
            {
                Output.WriteLine(e.FormatMessage());
            }
        }

        
    }
}
