using System;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Extensions;
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
        /// 测试 - 获取格式化日期时间字符串
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
        /// 测试 - 获取格式化日期字符串
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
        /// 测试 - 获取格式化时间字符串
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
        /// 测试 - 获取格式化毫秒字符串
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
        /// 测试 - 获取格式化中文日期字符串
        /// </summary>
        [Fact]
        public void Test_ToChineseDateString()
        {
            string date = "2018-03-12";
            Assert.Equal("2018年3月12日", Conv.ToDate(date).ToChineseDateString());
            Assert.Equal("2018年12月12日", Conv.ToDate("2018-12-12").ToChineseDateString());
            Assert.Equal("", Conv.ToDateOrNull("").ToChineseDateString());
            Assert.Equal("2018年3月12日", Conv.ToDateOrNull(date).ToChineseDateString());
        }

        /// <summary>
        /// 测试 - 获取格式化中文日期时间字符串
        /// </summary>
        [Fact]
        public void Test_ToChineseDateTimeString()
        {
            string date = "2018-03-12 11:11:11";
            Assert.Equal("2018年3月12日 11时11分11秒", Conv.ToDate(date).ToChineseDateTimeString());
            Assert.Equal("2018年12月12日 11时11分11秒", Conv.ToDate("2018-12-12 11:11:11").ToChineseDateTimeString());
            Assert.Equal("2018年3月12日 11时11分", Conv.ToDate(date).ToChineseDateTimeString(true));
            Assert.Equal("", Conv.ToDateOrNull("").ToChineseDateTimeString());
            Assert.Equal("2018年3月12日 11时11分11秒", Conv.ToDateOrNull(date).ToChineseDateTimeString());
            Assert.Equal("2018年3月12日 11时11分", Conv.ToDateOrNull(date).ToChineseDateTimeString(true));
        }

        /// <summary>
        /// 测试 - 获取时间间隔描述
        /// </summary>
        [Fact]
        public void Test_Description_Span()
        {
            TimeSpan span = new DateTime(2000, 1, 1, 1, 0, 1) - new DateTime(2000, 1, 1, 1, 0, 0);
            Assert.Equal("1秒", span.Description());
            span = new DateTime(2000, 1, 1, 1, 1, 0) - new DateTime(2000, 1, 1, 1, 0, 0);
            Assert.Equal("1分", span.Description());
            span = new DateTime(2000, 1, 1, 1, 0, 0) - new DateTime(2000, 1, 1, 0, 0, 0);
            Assert.Equal("1小时", span.Description());
            span = new DateTime(2000, 1, 2, 0, 0, 0) - new DateTime(2000, 1, 1, 0, 0, 0);
            Assert.Equal("1天", span.Description());
            span = new DateTime(2000, 1, 2, 0, 2, 0) - new DateTime(2000, 1, 1, 0, 0, 0);
            Assert.Equal("1天2分", span.Description());
            span = "2000-1-1 06:10:10.123".ToDate() - "2000-1-1 06:10:10.122".ToDate();
            Assert.Equal("1毫秒", span.Description());
            span = "2000-1-1 06:10:10.1000001".ToDate() - "2000-1-1 06:10:10.1000000".ToDate();
            Assert.Equal("0.0001毫秒", span.Description());
        }
    }
}
