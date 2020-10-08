using System;
using Bing.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// Unix时间操作测试
    /// </summary>
    public class UnixTimeTest : TestBase
    {
        public UnixTimeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact(Skip = "由于运行时间，可能存在延迟")]
        public void Test_ToTimestamp()
        {
            var dto = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
            var result = UnixTime.ToTimestamp(false);
            Output.WriteLine(dto.ToString());
            Output.WriteLine(result.ToString());
            Assert.Equal(dto, result);
        }
    }
}
