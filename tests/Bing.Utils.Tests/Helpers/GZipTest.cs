using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// GZip压缩操作测试
    /// </summary>
    public class GZipTest : TestBase
    {
        public GZipTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试压缩以及解压缩
        /// </summary>
        [Fact]
        public void Test_Compress_Decompress()
        {
            var content = "测试一下压缩情况";
            var compressResult = GZip.Compress(content);
            var decompressResulut = GZip.Decompress(compressResult);
            Assert.Equal(content, decompressResulut);
        }
    }
}
