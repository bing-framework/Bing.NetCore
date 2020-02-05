using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bing.Helpers;
using Bing.Tests;
using Bing.Utils.Develops;
using Bing.Extensions;
using Bing.Utils.Helpers;
using Bing.Utils.IO;
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
        [Fact(Skip = "未设置上传地址")]
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
            for (int i = 0; i < 100; i++)
            {
                await WriteFile("http://www.cnblogs.com");
            }
            await WriteFile("http://www.cnblogs.com");
            await WriteFile("http://www.cnblogs.com/artech/p/logging-for-net-core-05.html");
        }

        private async Task WriteFile(string url)
        {
            var path = @"D:\Test\File\";
            DirectoryHelper.CreateIfNotExists(path);
            var result = await Web.Client()
                .Get(url)
                .ResultAsync();
            var key = Bing.Utils.Randoms.GuidRandomGenerator.Instance.Generate();
            File.WriteAllBytes($"{path}test_{key}.txt", result.ToBytes());
        }

        /// <summary>
        /// 测试获取主机
        /// </summary>
        [Fact]
        public void Test_Host()
        {
            Output.WriteLine(Web.Host);
        }

        /// <summary>
        /// 测试获取客户端IP地址
        /// </summary>
        [Fact]
        public void Test_Ip()
        {
            Output.WriteLine(Web.IP);
        }

        /// <summary>
        /// 测试获取本地IP
        /// </summary>
        [Fact]
        public void Test_LocalIpAddress()
        {
            Output.WriteLine(Web.LocalIpAddress);
        }

        [Fact]
        public void Test_LocalIpAddress_1()
        {
            Output.WriteLine(GetLocalIPAddress());
        }

        private string GetLocalIPAddress()
        {
            string AddressIP = string.Empty;
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    AddressIP = _IPAddress.ToString();
                }
            }
            return AddressIP;
        }
    }
}
