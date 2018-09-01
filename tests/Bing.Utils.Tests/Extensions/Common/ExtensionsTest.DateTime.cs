using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 系统扩展测试 - 日期格式扩展
    /// </summary>
    public partial class ExtensionsTest
    {
        /// <summary>
        /// 测试获取格式化日期时间字符串
        /// </summary>
        [Fact]
        public void Test_ToDateTimeString()
        {
            string date = "2018-03-12 15:00:00";
            Assert.Equal(date,Conv.ToDate(date).ToDateTimeString());
            Assert.Equal("2018-03-12 15:00",Conv.ToDate(date).ToDateTimeString(true));
            Assert.Equal("",Conv.ToDateOrNull("").ToDateTimeString());
            Assert.Equal(date, Conv.ToDateOrNull(date).ToDateTimeString());
        }

        /// <summary>
        /// 测试获取格式化日期字符串
        /// </summary>
        [Fact]
        public void Test_ToDateString()
        {
            string date = "2018-03-12";
            Assert.Equal(date,Conv.ToDate(date).ToDateString());
            Assert.Equal("",Conv.ToDateOrNull("").ToDateString());
            Assert.Equal(date,Conv.ToDateOrNull(date).ToDateString());
        }

        /// <summary>
        /// 测试获取格式化时间字符串
        /// </summary>
        [Fact]
        public void Test_ToTimeString()
        {
            string date = "2018-03-12 11:11:11";
            Assert.Equal("11:11:11",Conv.ToDate(date).ToTimeString());
            Assert.Equal("",Conv.ToDateOrNull("").ToTimeString());
            Assert.Equal("11:11:11",Conv.ToDateOrNull(date).ToTimeString());
        }

        /// <summary>
        /// 测试获取格式化毫秒字符串
        /// </summary>
        [Fact]
        public void Test_ToMillisecondString()
        {
            string date = "2018-03-12 11:11:11.111";
            Assert.Equal(date, Conv.ToDate(date).ToMillisecondString());
            Assert.Equal("", Conv.ToDateOrNull("").ToMillisecondString());
            Assert.Equal(date, Conv.ToDateOrNull(date).ToMillisecondString());
        }

        /// <summary>
        /// 测试获取格式化中文日期字符串
        /// </summary>
        [Fact]
        public void Test_ToChineseDateString()
        {

        }
    }
}
