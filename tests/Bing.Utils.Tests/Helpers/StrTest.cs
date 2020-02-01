using Bing.Tests;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 字符串操作测试
    /// </summary>
    public class StrTest:TestBase
    {
        public StrTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试获取汉字的全拼
        /// </summary>
        [Fact]
        public void Test_FullPinYin()
        {
            Output.WriteLine(Str.FullPinYin("隔壁老王"));
        }
    }
}
