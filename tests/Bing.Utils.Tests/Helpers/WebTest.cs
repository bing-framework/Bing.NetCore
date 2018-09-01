using System.Threading.Tasks;
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
    }
}
