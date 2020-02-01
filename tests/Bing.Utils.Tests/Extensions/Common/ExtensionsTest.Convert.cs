using System;
using System.Collections.Generic;
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
        /// 测试 - 安全转换为字符串
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
        /// 测试 - 转换为bool
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
        /// 测试 - 转换为可空bool
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
        /// 测试 - 转换为int
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
        /// 测试 - 转换为可空int
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
        /// 测试 - 转换为long
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
        /// 测试 - 转换为可空long
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
        /// 测试 - 转换为double
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
        /// 测试 - 转换为可空double
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
        /// 测试 - 转换为decimal
        /// </summary>
        [Fact]
        public void Test_ToDecimal()
        {
            Assert.Equal(0, "".ToDecimal());
            Assert.Equal(1.2M, "1.2".ToDecimal());
        }

        /// <summary>
        /// 测试 - 转换为可空decimal
        /// </summary>
        [Fact]
        public void Test_ToDecimalOrNull()
        {
            Assert.Null("".ToDecimalOrNull());
            Assert.Equal(1.2M, "1.2".ToDecimalOrNull());
        }

        /// <summary>
        /// 测试 - 转换为日期
        /// </summary>
        [Fact]
        public void Test_ToDate()
        {
            Assert.Equal(DateTime.MinValue, "".ToDate());
            Assert.Equal(new DateTime(2000, 1, 1), "2000-1-1".ToDate());
        }

        /// <summary>
        /// 测试 - 转换为可空日期
        /// </summary>
        [Fact]
        public void Test_ToDateOrNull()
        {
            Assert.Null("".ToDateOrNull());
            Assert.Equal(new DateTime(2000, 1, 1), "2000-1-1".ToDateOrNull());
        }

        /// <summary>
        /// 测试 - 转换为Guid
        /// </summary>
        [Fact]
        public void Test_ToGuid()
        {
            Assert.Equal(Guid.Empty, "".ToGuid());
            Assert.Equal(new Guid("B9EB56E9-B720-40B4-9425-00483D311DDC"), "B9EB56E9-B720-40B4-9425-00483D311DDC".ToGuid());
        }

        /// <summary>
        /// 测试 - 转换为可空Guid
        /// </summary>
        [Fact]
        public void Test_ToGuidOrNull()
        {
            Assert.Null("".ToGuidOrNull());
            Assert.Equal(new Guid("B9EB56E9-B720-40B4-9425-00483D311DDC"), "B9EB56E9-B720-40B4-9425-00483D311DDC".ToGuidOrNull());
        }

        /// <summary>
        /// 测试 - 转换为Guid集合,值为字符串
        /// </summary>
        [Fact]
        public void Test_ToGuidList_String()
        {
            const string guid = "83B0233C-A24F-49FD-8083-1337209EBC9A,,EAB523C6-2FE7-47BE-89D5-C6D440C3033A,";
            Assert.Equal(2, guid.ToGuidList().Count);
            Assert.Equal(new Guid("83B0233C-A24F-49FD-8083-1337209EBC9A"), guid.ToGuidList()[0]);
            Assert.Equal(new Guid("EAB523C6-2FE7-47BE-89D5-C6D440C3033A"), guid.ToGuidList()[1]);
        }

        /// <summary>
        /// 测试 - 转换为Guid集合,值为字符串集合
        /// </summary>
        [Fact]
        public void Test_ToGuidList_StringList()
        {
            var list = new List<string> { "83B0233C-A24F-49FD-8083-1337209EBC9A", "EAB523C6-2FE7-47BE-89D5-C6D440C3033A" };
            Assert.Equal(2, list.ToGuidList().Count);
            Assert.Equal(new Guid("83B0233C-A24F-49FD-8083-1337209EBC9A"), list.ToGuidList()[0]);
            Assert.Equal(new Guid("EAB523C6-2FE7-47BE-89D5-C6D440C3033A"), list.ToGuidList()[1]);
        }
    }
}
