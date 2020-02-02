
using System;
using Bing.Helpers;
using Bing.Tests;
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

        [Fact]
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
