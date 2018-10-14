using System.IO;
using Bing.Tools.ExpressDelivery.Kdniao;
using Bing.Tools.ExpressDelivery.Kdniao.Configuration;
using Bing.Tools.ExpressDelivery.Kdniao.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tools.ExpressDelivery.Tests.Kdniao
{
    /// <summary>
    /// 快递鸟客户端测试
    /// </summary>
    public class KdniaoClientTest:TestBase
    {
        private readonly KdniaoConfig _config;
        private readonly KdniaoClient _client;

        public KdniaoClientTest(ITestOutputHelper output) : base(output)
        {
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true).Build();
            _config = configuration.GetSection("ExpressDelivery:Kdniao").Get<KdniaoConfig>();
            _client = new KdniaoClient(_config);
        }

        [Fact]
        public void Test_ConfigChecking()
        {
            Assert.NotNull(_config);
            Assert.NotNull(_config.Account);
            Assert.NotEmpty(_config.Account.MerchantId);
            Assert.NotEmpty(_config.Account.ApiKey);
        }

        [Fact]
        public async void Test_Track()
        {
            var request = new KdniaoTrackQuery()
            {
                LogisticCode = "",
                ShipperCode = "YTO",
            };

            var result = await _client.TracKAsync(request);
            Output.WriteLine(JsonConvert.SerializeObject(result));
        }
    }
}
