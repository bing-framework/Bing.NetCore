using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 类型转换操作测试
    /// </summary>
    public class ConvTest : TestBase
    {
        /// <summary>
        /// 初始化一个<see cref="ConvTest"/>类型的实例
        /// </summary>
        public ConvTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试转换为8位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("1A", 0)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("12.3", 12)]
        [InlineData("12.335556", 12)]
        public void Test_ToByte(object input, byte result)
        {
            Assert.Equal(result, Conv.ToByte(input));
        }

        /// <summary>
        /// 测试转换为8位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("1A", null)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("12.3", 12)]
        [InlineData("12.335556", 12)]
        public void Test_ToByteOrNull(object input, int? result)
        {
            Assert.Equal(result, Conv.ToByteOrNull(input));
        }

        /// <summary>
        /// 测试转换为字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, default(char))]
        [InlineData("", default(char))]
        [InlineData("1", '1')]
        [InlineData("A", 'A')]
        public void Test_ToChar(object input, char result)
        {
            Assert.Equal(result, Conv.ToChar(input));
        }

        /// <summary>
        /// 测试转换为可空字符
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("1", '1')]
        [InlineData("A", 'A')]
        public void Test_ToCharOrNull(object input, char? result)
        {
            Assert.Equal(result, Conv.ToCharOrNull(input));
        }

        /// <summary>
        /// 测试转换为16位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("1A", 0)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("12.3", 12)]
        [InlineData("12.335556", 12)]
        public void Test_ToShort(object input, short result)
        {
            Assert.Equal(result, Conv.ToShort(input));
        }

        /// <summary>
        /// 测试转换为16位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("1A", null)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("12.3", 12)]
        [InlineData("12.335556", 12)]
        public void Test_ToShortOrNull(object input, int? result)
        {
            Assert.Equal(result, Conv.ToShortOrNull(input));
        }

        /// <summary>
        /// 测试转换为32位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("1A", 0)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("1778019.78", 1778020)]
        [InlineData("1778019.7801684", 1778020)]
        public void Test_ToInt(object input, int result)
        {
            Assert.Equal(result, Conv.ToInt(input));
        }

        /// <summary>
        /// 测试转换为32位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("1A", null)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("1778019.78", 1778020)]
        [InlineData("1778019.7801684", 1778020)]
        public void Test_ToIntOrNull(object input, int? result)
        {
            Assert.Equal(result, Conv.ToIntOrNull(input));
        }

        /// <summary>
        /// 转换为64位整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("1A", 0)]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("1778019.7801684", 1778020)]
        [InlineData("177801978016841234", 177801978016841234)]
        public void Test_ToLong(object input, long result)
        {
            Assert.Equal(result, Conv.ToLong(input));
        }

        /// <summary>
        /// 转换为64位可空整型
        /// </summary>
        /// <param name="input">输入值</param>
        /// <param name="result">结果</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("1A", null)]
        [InlineData("0", 0L)]
        [InlineData("1", 1L)]
        [InlineData("1778019.7801684", 1778020L)]
        [InlineData("177801978016841234", 177801978016841234L)]
        public void Test_ToLongOrNull(object input, long? result)
        {
            Assert.Equal(result, Conv.ToLongOrNull(input));
        }
    }
}
