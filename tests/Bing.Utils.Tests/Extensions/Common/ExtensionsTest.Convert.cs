using Bing.Tests;
using Bing.Utils.Extensions;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 扩展测试 - 类型转换
    /// </summary>
    public partial class ExtensionsTest : TestBase
    {
        /// <summary>
        /// 测试安全转换为字符串
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, "")]
        [InlineData("  ", "")]
        [InlineData(1, "1")]
        [InlineData(" 1 ", "1")]
        public void Test_SafeString(object input, string result)
        {
            Assert.Equal(result, input.SafeString());
        }

        /// <summary>
        /// 测试转换为bool
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("true", true)]
        [InlineData("1", true)]
        [InlineData("是", true)]
        [InlineData("ok", true)]
        [InlineData("yes", true)]
        [InlineData("", false)]
        public void Test_ToBool(string input, bool result)
        {
            Assert.Equal(result, input.ToBool());
        }

        /// <summary>
        /// 测试转换为可空bool
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("true", true)]
        [InlineData("1", true)]
        [InlineData("是", true)]
        [InlineData("ok", true)]
        [InlineData("yes", true)]
        [InlineData("", null)]
        public void Test_ToBoolOrNull(string input, bool? result)
        {
            Assert.Equal(result, input.ToBoolOrNull());
        }

        /// <summary>
        /// 测试转换为int
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", 0)]
        [InlineData("1", 1)]
        public void Test_ToInt(string input, int result)
        {
            Assert.Equal(result, input.ToInt());
        }

        /// <summary>
        /// 测试转换为可空int
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", null)]
        [InlineData("1", 1)]
        public void Test_ToIntOrNull(string input, int? result)
        {
            Assert.Equal(result, input.ToIntOrNull());
        }

        /// <summary>
        /// 测试转换为long
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", 0)]
        [InlineData("1", 1)]
        public void Test_ToLong(string input, long result)
        {
            Assert.Equal(result, input.ToLong());
        }

        /// <summary>
        /// 测试转换为可空long
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", null)]
        [InlineData("1", 1)]
        public void Test_ToLongOrNull(string input, long? result)
        {
            Assert.Equal(result, input.ToLongOrNull());
        }

        /// <summary>
        /// 测试转换为double
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", 0)]
        [InlineData("1", 1)]
        [InlineData("1.2", 1.2)]
        public void Test_ToDouble(string input, double result)
        {
            Assert.Equal(result, input.ToDouble());
        }

        /// <summary>
        /// 测试转换为可空double
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData("", null)]
        [InlineData("1", 1)]
        [InlineData("1.2", 1.2)]
        public void Test_ToDoubleOrNull(string input, double? result)
        {
            Assert.Equal(result, input.ToDoubleOrNull());
        }

        /// <summary>
        /// 测试转换为decimal
        /// </summary>
        [Fact]
        public void Test_ToDecimal()
        {
            Assert.Equal(0, "".ToDecimal());
            Assert.Equal(1.2M, "1.2".ToDecimal());
        }

        /// <summary>
        /// 测试转换为可空decimal
        /// </summary>
        [Fact]
        public void Test_ToDecimalOrNull()
        {
            Assert.Null("".ToDecimalOrNull());
            Assert.Equal(1.2M, "1.2".ToDecimalOrNull());
        }
    }
}
