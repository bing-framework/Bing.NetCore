using Bing.Utils.Files;
using Xunit;

namespace Bing.Utils.Tests.Files
{
    /// <summary>
    /// 测试文件大小
    /// </summary>
    public class FileSizeTest
    {
        /// <summary>
        /// 测试 - 文件字节数
        /// </summary>
        [Theory]
        [InlineData(FileSizeUnit.Byte, 1)]
        [InlineData(FileSizeUnit.K, 1024)]
        [InlineData(FileSizeUnit.M, 1024 * 1024)]
        [InlineData(FileSizeUnit.G, 1024 * 1024 * 1024)]
        public void Test_Size(FileSizeUnit unit, long result)
        {
            Assert.Equal(result, new FileSize(1, unit).Size);
        }

        /// <summary>
        /// 测试 - 测试文件大小描述
        /// </summary>
        [Fact]
        public void Test_ToString()
        {
            Assert.Equal("1 B", new FileSize(1).ToString());
            Assert.Equal("1 KB", new FileSize(1 * 1024).ToString());
            Assert.Equal("1 MB", new FileSize(1 * 1024 * 1024).ToString());
            Assert.Equal("1 GB", new FileSize(1 * 1024 * 1024 * 1024).ToString());
        }

        /// <summary>
        /// 测试 - 获取文件大小，单位：K
        /// </summary>
        [Fact]
        public void Test_GetSizeByK()
        {
            Assert.Equal(0, new FileSize(0).GetSizeByK());
            Assert.Equal(1, new FileSize(1024).GetSizeByK());
            Assert.Equal(0.5, new FileSize(512).GetSizeByK());
        }

        /// <summary>
        /// 测试 - 获取文件大小，单位：M
        /// </summary>
        [Fact]
        public void Test_GetSizeByM()
        {
            Assert.Equal(0, new FileSize(0).GetSizeByM());
            Assert.Equal(1, new FileSize(1024 * 1024).GetSizeByM());
            Assert.Equal(0.5, new FileSize(512 * 1024).GetSizeByM());
        }

        /// <summary>
        /// 测试 - 获取文件大小，单位：G
        /// </summary>
        [Fact]
        public void Test_GetSizeByG()
        {
            Assert.Equal(0, new FileSize(0).GetSizeByG());
            Assert.Equal(1, new FileSize(1024 * 1024 * 1024).GetSizeByG());
            Assert.Equal(0.5, new FileSize(512 * 1024 * 1024).GetSizeByG());
        }
    }
}
