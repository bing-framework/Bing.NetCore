using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bing.Extensions;
using Bing.Tests;
using Bing.Tests.Samples;
using Bing.Extensions;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 扩展测试 - 公共扩展
    /// </summary>
    public partial class ExtensionsTest : TestBase
    {
        /// <summary>
        /// 测试安全获取值-short
        /// </summary>
        [Fact]
        public void Test_SafeValue_Short()
        {
            short? value = null;
            Assert.Equal(0, value.SafeValue());
            value = 1;
            Assert.Equal(1, value.SafeValue());
        }

        /// <summary>
        /// 测试安全获取值-int
        /// </summary>
        [Fact]
        public void Test_SafeValue_Int()
        {
            int? value = null;
            Assert.Equal(0, value.SafeValue());
            value = 1;
            Assert.Equal(1, value.SafeValue());
        }

        /// <summary>
        /// 测试安全获取值-long
        /// </summary>
        [Fact]
        public void Test_SafeValue_Long()
        {
            long? value = null;
            Assert.Equal(0, value.SafeValue());
            value = 1;
            Assert.Equal(1, value.SafeValue());
        }

        /// <summary>
        /// 测试安全获取值-float
        /// </summary>
        [Fact]
        public void Test_SafeValue_Float()
        {
            float? value = null;
            Assert.Equal(0, value.SafeValue());
            value = 1;
            Assert.Equal(1, value.SafeValue());
        }

        /// <summary>
        /// 测试安全获取值-double
        /// </summary>
        [Fact]
        public void Test_SafeValue_Double()
        {
            double? value = null;
            Assert.Equal(0, value.SafeValue());
            value = 1;
            Assert.Equal(1, value.SafeValue());
        }

        /// <summary>
        /// 测试安全获取值-DateTime
        /// </summary>
        [Fact]
        public void Test_SafeValue_DateTime()
        {
            DateTime? value = null;
            Assert.Equal(DateTime.MinValue, value.SafeValue());
            value = new DateTime(2018, 9, 1);
            Assert.Equal(new DateTime(2018, 9, 1), value.SafeValue());
        }

        /// <summary>
        /// 测试获取枚举值
        /// </summary>
        [Fact]
        public void Test_Value()
        {
            Assert.Equal(2, EnumSample.B.Value());
            Assert.Equal("2", EnumSample.B.Value<string>());
        }

        /// <summary>
        /// 测试获取枚举描述
        /// </summary>
        [Fact]
        public void Test_Description()
        {
            Assert.Equal("B2", EnumSample.B.Description());
        }

        /// <summary>
        /// 测试转换为用分隔符连接的字符串
        /// </summary>
        [Fact]
        public void Test_Join()
        {
            var source = new List<int>() { 1, 2, 3 };
            Assert.Equal("1,2,3", source.Join());
            Assert.Equal("'1','2','3'", source.Join(quotes: "'"));
        }

        /// <summary>
        /// 测试是否匹配正则表达式-1
        /// </summary>
        [Fact]
        public void Test_IsMatch_1()
        {
            var source = "123456";
            Assert.True(source.IsMatch(@"^[0-9]*$"));
        }

        /// <summary>
        /// 测试是否匹配正则表达式-2
        /// </summary>
        [Fact]
        public void Test_IsMatch_2()
        {
            var source = "Abcd";
            Assert.True(source.IsMatch(@"^[a-z]+$", RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// 测试获取匹配项-第一个匹配项
        /// </summary>
        [Fact]
        public void Test_GetMatch()
        {
            var source = "123456";
            Assert.Equal("1", source.GetMatch(@"[0-9]"));
        }

        /// <summary>
        /// 测试获取匹配项-所有匹配项字符串集合
        /// </summary>
        [Fact]
        public void Test_GetMatchingValues()
        {
            var source = "123456";
            Assert.Equal(new string[] { "1", "2", "3", "4", "5", "6" }, source.GetMatchingValues(@"[0-9]"));
        }
    }
}
