using Bing.Tests;
using Bing.Utils.Extensions;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 系统扩展测试 - 格式化扩展
    /// </summary>
    public partial class ExtensionsTest : TestBase
    {
        

        /// <summary>
        /// 测试 - 获取布尔值描述
        /// </summary>
        [Fact]
        public void Test_Description_Bool()
        {
            bool? value = null;
            Assert.Equal("", value.Description());
            Assert.Equal("是", true.Description());
            Assert.Equal("否", false.Description());
        }
    }
}
