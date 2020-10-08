using Bing.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// Url操作测试
    /// </summary>
    public class UrlTest : TestBase
    {
        public UrlTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 合并Url
        /// </summary>
        [Theory]
        [InlineData("http://a.com", "b", "http://a.com/b")]
        [InlineData("http://a.com/", "b", "http://a.com/b")]
        [InlineData(@"http://a.com\", "b", "http://a.com/b")]
        [InlineData(@"http://a.com", "b=1", "http://a.com/b=1")]
        public void Test_Combine(string url, string url2, string result)
        {
            Assert.Equal(result, Url.Combine(url, url2));
        }

        /// <summary>
        /// 测试连接Url
        /// </summary>
        [Theory]
        [InlineData("http://test.com", "a=1", "http://test.com?a=1")]
        [InlineData("http://test.com?", "a=1", "http://test.com?a=1")]
        [InlineData("http://test.com?c=3", "a=1", "http://test.com?c=3&a=1")]
        [InlineData("http://test.com?c=3&", "a=1", "http://test.com?c=3&a=1")]
        public void Test_JoinUrl(string url, string param, string result)
        {
            Assert.Equal(result, Url.Join(url, param));
        }

        /// <summary>
        /// 测试连接多个Url
        /// </summary>
        [Theory]
        [InlineData("http://demo.com", "a=1", "b=2", "http://demo.com?a=1&b=2")]
        [InlineData("http://demo.com?", "a=1", "b=2", "http://demo.com?a=1&b=2")]
        [InlineData("http://demo.com?c=3", "a=1", "b=2", "http://demo.com?c=3&a=1&b=2")]
        [InlineData("http://demo.com?c=3&", "a=1", "b=2", "http://demo.com?c=3&a=1&b=2")]
        public void Test_JoinMoreUrl(string url, string param1, string param2, string result)
        {
            Assert.Equal(result, Url.Join(url, param1, param2));
        }

        [Theory(Skip = "尚未验证通过")]
        [InlineData("http://www.baidu.com","baidu.com")]
        [InlineData("http://baidu.com", "baidu.com")]
        [InlineData("http://www.baidu.com?a=xxx.aaa.cc", "baidu.com")]
        public void Test_GetMainDomain(string domain,string result)
        {
            Assert.Equal(result, Url.GetMainDomain(domain));
        }
    }
}
