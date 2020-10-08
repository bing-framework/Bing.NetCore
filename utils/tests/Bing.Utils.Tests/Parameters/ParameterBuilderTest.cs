using System;
using Bing.Tests.Samples;
using Bing.Utils.Parameters;
using Xunit;

namespace Bing.Utils.Tests.Parameters
{
    /// <summary>
    /// 参数生成器测试
    /// </summary>
    public class ParameterBuilderTest
    {
        /// <summary>
        /// 参数生成器
        /// </summary>
        private readonly ParameterBuilder _builder;

        /// <summary>
        /// 初始化一个<see cref="ParameterBuilderTest"/>类型的实例
        /// </summary>
        public ParameterBuilderTest() => _builder = new ParameterBuilder();

        /// <summary>
        /// 测试 - 验证key为空
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Test_Add_KeyIsEmpty(string key)
        {
            Assert.Empty(_builder.Add(key, "b").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 验证value为空
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Test_Add_ValueIsEmpty(string value)
        {
            Assert.Empty(_builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 去除键两端空格
        /// </summary>
        [Fact]
        public void Test_Add_TrimKey()
        {
            Assert.Equal("a:1", _builder.Add(" a ", "1").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 去除值两端空格
        /// </summary>
        [Fact]
        public void Test_Add_TrimValue()
        {
            Assert.Equal("a:1", _builder.Add("a", " 1 ").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加字符串参数
        /// </summary>
        [Fact]
        public void Test_Add_String()
        {
            Assert.Equal("a:1", _builder.Add("a", "1").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加2个字符串参数
        /// </summary>
        [Fact]
        public void Test_Add_String_2()
        {
            Assert.Equal("a:1|b:2", _builder.Add("a", " 1").Add("b ", "2 ").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加日期参数
        /// </summary>
        [Fact]
        public void Test_Add_DateTime()
        {
            DateTime value = new DateTime(2000, 10, 10, 10, 10, 10);
            Assert.Equal("a:2000-10-10 10:10:10", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加日期参数 - 可空
        /// </summary>
        [Fact]
        public void Test_Add_DateTime_Nullable()
        {
            DateTime? value = new DateTime(2000, 10, 10, 10, 10, 10);
            Assert.Equal("a:2000-10-10 10:10:10", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加日期参数 - 可空 - 传入null
        /// </summary>
        [Fact]
        public void Test_Add_DateTime_Nullable_Null()
        {
            DateTime? value = null;
            Assert.Empty(_builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加整型参数
        /// </summary>
        [Fact]
        public void Test_Add_Int()
        {
            int value = 1;
            Assert.Equal("a:1", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加整型参数 - 可空
        /// </summary>
        [Fact]
        public void Test_Add_Int_Nullable()
        {
            int? value = 1;
            Assert.Equal("a:1", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加整型参数 - 可空 - 传入null
        /// </summary>
        [Fact]
        public void Test_Add_Int_Nullable_Null()
        {
            int? value = null;
            Assert.Empty(_builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加布尔参数
        /// </summary>
        [Fact]
        public void Test_Add_Bool()
        {
            bool value = true;
            Assert.Equal("a:true", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加布尔参数 - 可空
        /// </summary>
        [Fact]
        public void Test_Add_Bool_Nullable()
        {
            bool? value = false;
            Assert.Equal("a:false", _builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 添加布尔参数 - 可空 - 传入null
        /// </summary>
        [Fact]
        public void Test_Add_Bool_Nullable_Null()
        {
            bool? value = null;
            Assert.Empty(_builder.Add("a", value).Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 已添加参数则覆盖
        /// </summary>
        [Fact]
        public void Test_Add_Exist()
        {
            Assert.Equal("a:2", _builder.Add("a", "1").Add("a", "2").Result(new ParameterFormatterSample()));
        }

        /// <summary>
        /// 测试 - 转换为json
        /// </summary>
        [Fact]
        public void Test_ToJson()
        {
            Assert.Equal("{\"a\":\"true\",\"b\":\"2000-10-10 10:10:10\"}", _builder.Add("a", true).Add("b", new DateTime(2000, 10, 10, 10, 10, 10)).ToJson());
        }
    }
}
