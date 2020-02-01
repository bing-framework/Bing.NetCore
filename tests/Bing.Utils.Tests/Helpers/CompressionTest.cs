using Bing.Tests;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 压缩操作测试
    /// </summary>
    public class CompressionTest:TestBase
    {
        /// <summary>
        /// 初始化一个<see cref="CompressionTest"/>类型的实例
        /// </summary>
        public CompressionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试对字符串进行压缩
        /// </summary>
        [Fact]
        public void Test_Compress()
        {
            var result = Compression.Compress("测试中国");
            Output.WriteLine(result);
            Assert.Equal("测试中国",Compression.Decompress(result));
        }
    }
}
