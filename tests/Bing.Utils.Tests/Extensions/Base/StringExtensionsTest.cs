using System;
using Bing.Extensions;
using Bing.Text;
using Xunit;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展测试 - 操作
    /// </summary>
    public class StringExtensionsTest
    {
        /// <summary>
        /// 测试 - 重复指定字符串 - 空值
        /// </summary>
        [Fact]
        public void Test_Repeat_Empty()
        {
            var input = string.Empty;
            var result = string.Empty;
            Assert.Equal(result, input.Repeat(10));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 空格
        /// </summary>
        [Fact]
        public void Test_Repeat_Space()
        {
            var input = " ";
            var result = "     ";
            Assert.Equal(result, input.Repeat(5));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符 - 数字
        /// </summary>
        [Fact]
        public void Test_Repeat_Char_1()
        {
            var input = "1";
            var result = "11";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符 - 字母
        /// </summary>
        [Fact]
        public void Test_Repeat_Char_2()
        {
            var input = "a";
            var result = "aa";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符 - 符号
        /// </summary>
        [Fact]
        public void Test_Repeat_Char_3()
        {
            var input = "#";
            var result = "##";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符 - 汉字
        /// </summary>
        [Fact]
        public void Test_Repeat_Char_4()
        {
            var input = "隔";
            var result = "隔隔";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符串 - 数字
        /// </summary>
        [Fact]
        public void Test_Repeat_String_1()
        {
            var input = "11";
            var result = "1111";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符串 - 字母
        /// </summary>
        [Fact]
        public void Test_Repeat_String_2()
        {
            var input = "AA";
            var result = "AAAA";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符串 - 符号
        /// </summary>
        [Fact]
        public void Test_Repeat_String_3()
        {
            var input = "##";
            var result = "####";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 重复指定字符串 - 字符串 - 汉字
        /// </summary>
        [Fact]
        public void Test_Repeat_String_4()
        {
            var input = "隔壁";
            var result = "隔壁隔壁";
            Assert.Equal(result, input.Repeat(2));
        }

        /// <summary>
        /// 测试 - 提取指定范围字符串
        /// </summary>
        [Theory]
        [InlineData("这是一个隔壁老王的故事", "这是一个", 0, 0, 4)]
        [InlineData("这是一个隔壁老王的故事", "是一个隔", 1, 0, 4)]
        [InlineData("这是一个隔壁老王的故事", "这是一个隔", 1, 1, 4)]
        [InlineData("这是一个隔壁老王的故事", "是一个隔壁", 2, 1, 4)]
        [InlineData("这是一个隔壁老王的故事", "这是一个隔壁", 2, 3, 4)]
        [InlineData("这是一个隔壁老王的故事", "隔壁", 2, -2, 4)]
        public void Test_ExtractAround(string input, string result, int index, int left, int right)
        {
            Assert.Equal(result, input.ExtractAround(index, left, right));
        }

        /// <summary>
        /// 测试 - 提取字符串中所有字母以及数字
        /// </summary>
        [Theory]
        [InlineData("测试一个ABC以及1234","ABC1234")]
        [InlineData("测试一个ABC以###及1234", "ABC1234")]
        [InlineData("AA###及1234", "AA1234")]
        public void Test_ExtractLettersNumbers(string input, string result)
        {
            Assert.Equal(result, input.ExtractLettersNumbers());
        }

        /// <summary>
        /// 测试 - 提取字符串中所有数字
        /// </summary>
        [Theory]
        [InlineData("测试一个ABC以及1234", "1234")]
        [InlineData("测试一个ABC以###及1234", "1234")]
        [InlineData("AA###及1234", "1234")]
        [InlineData("3.14157", "314157")]
        public void Test_ExtractNumbers(string input, string result)
        {
            Assert.Equal(result, input.ExtractNumbers());
        }

        /// <summary>
        /// 测试 - 提取字符串中所有字母
        /// </summary>
        [Theory]
        [InlineData("测试一个ABC以及1234", "ABC")]
        [InlineData("测试一个ABC以###及1234", "ABC")]
        [InlineData("AA###及1234", "AA")]
        [InlineData("AA.aa.BB.bb", "AAaaBBbb")]
        public void Test_ExtractLetters(string input, string result)
        {
            Assert.Equal(result, input.ExtractLetters());
        }

        /// <summary>
        /// 测试 - 提取字符串中所有汉字
        /// </summary>
        [Theory]
        [InlineData("测试一个ABC以及1234", "测试一个以及")]
        [InlineData("测试一个ABC以###及1234", "测试一个以及")]
        [InlineData("AA###及1234", "及")]
        public void Test_ExtractChinese(string input, string result)
        {
            Assert.Equal(result, input.ExtractChinese());
        }
        
        /// <summary>
        /// 测试 - 过滤字符
        /// </summary>
        [Theory]
        [InlineData("测试.隔壁.老王.数据.","测试隔壁老王数据",'.')]
        [InlineData("测试#隔壁#老王#数据#", "测试隔壁老王数据", '#')]
        public void Test_FilterChars(string input, string result, char c)
        {
            Assert.Equal(result, input.FilterChars(x => x != c));
        }
    }
}
