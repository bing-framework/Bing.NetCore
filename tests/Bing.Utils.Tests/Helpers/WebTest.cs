using System;
using System.IO;
using System.Threading.Tasks;
using Bing.Utils.Develops;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// Web操作测试
    /// </summary>
    public class WebTest:TestBase
    {
        public WebTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试客户端上传文件
        /// </summary>
        [Fact]
        public async Task Test_Client_UploadFile()
        {
            var result = await Web.Client()
                .Post("")
                .FileData("files", @"")
                .IgnoreSsl()
                .ResultAsync();
            Output.WriteLine(result);
        }

        /// <summary>
        /// 测试客户端网页访问
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Test_Client_WebAccess()
        {
            await WriteFile("http://www.cnblogs.com");
            await WriteFile("http://www.cnblogs.com/artech/p/logging-for-net-core-05.html");
        }

        private async Task WriteFile(string url)
        {
            var path = @"D:\Test\File\";
            var result = await Web.Client()
                .Get(url)
                .ResultAsync();
            var key = Bing.Utils.Randoms.GuidRandomGenerator.Instance.Generate();
            File.WriteAllBytes($"{path}test_{key}.txt", result.ToBytes());
        }
    }
}
