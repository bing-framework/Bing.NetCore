using Bing.Utils.Parameters;
using Xunit;

namespace Bing.Utils.Tests.Parameters
{
    /// <summary>
    /// Url参数生成器测试
    /// </summary>
    public class UrlParameterBuilderTest
    {
        /// <summary>
        /// Url参数生成器
        /// </summary>
        private UrlParameterBuilder _builder;

        /// <summary>
        /// 初始化一个<see cref="UrlParameterBuilderTest"/>类型的实例
        /// </summary>
        public UrlParameterBuilderTest() => _builder = new UrlParameterBuilder();

        /// <summary>
        /// 测试 - 默认值
        /// </summary>
        [Fact]
        public void Test_Default()
        {
            Assert.Empty(_builder.Result());
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_1()
        {
            _builder = new UrlParameterBuilder("a");
            Assert.Equal(0, _builder.GetDictionary().Count);
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_2()
        {
            _builder = new UrlParameterBuilder("a=b");
            Assert.Equal(1, _builder.GetDictionary().Count);
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_3()
        {
            _builder = new UrlParameterBuilder("a=1&b=2");
            Assert.Equal(2, _builder.GetDictionary().Count);
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_4()
        {
            _builder = new UrlParameterBuilder("a=1&b");
            Assert.Equal(1, _builder.GetDictionary().Count);
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_5()
        {
            _builder = new UrlParameterBuilder("a=1&b=");
            Assert.Equal(1, _builder.GetDictionary().Count);
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_6()
        {
            _builder = new UrlParameterBuilder("http://test.com?b=2&c=3&a=1");
            Assert.Equal(3, _builder.GetDictionary().Count);
            Assert.Equal("a=1&b=2&c=3", _builder.Result(true));
        }

        /// <summary>
        /// 测试 - 加载Url
        /// </summary>
        [Fact]
        public void Test_LoadUrl_7()
        {
            _builder = new UrlParameterBuilder("http://test.com ? b = 2 & c & a = 1 ");
            Assert.Equal("a=1&b=2", _builder.Result(true));
        }

        /// <summary>
        /// 测试 - 添加参数
        /// </summary>
        [Fact]
        public void Test_Add_1()
        {
            Assert.Equal("a=b", _builder.Add("a", "b").Result());
            Assert.Equal("a=b&c=d", _builder.Add("c", "d").Result());
        }

        /// <summary>
        /// 测试 - 添加参数 - 测试空格
        /// </summary>
        [Fact]
        public void Test_Add_2()
        {
            Assert.Equal("b=2&a=1", _builder.Add(" b ", " 2 ").Add(" a ", " 1 ").Result());
        }

        /// <summary>
        /// 测试 - 添加参数 - 测试排序
        /// </summary>
        [Fact]
        public void Test_Result_Sort()
        {
            Assert.Equal("a=1&b=2", _builder.Add("b", "2").Add("a", "1").Result(true));
        }

        /// <summary>
        /// 测试 - 连接Url
        /// </summary>
        [Theory]
        [InlineData("http://test.com", "http://test.com?a=1&b=2")]
        [InlineData("http://test.com?", "http://test.com?a=1&b=2")]
        [InlineData("http://test.com?c=3", "http://test.com?c=3&a=1&b=2")]
        [InlineData("http://test.com?c=3&", "http://test.com?c=3&a=1&b=2")]
        public void Test_JoinUrl(string url, string result)
        {
            Assert.Equal(result, _builder.Add("a", "1").Add("b", "2").JoinUrl(url));
        }
    }
}
