using System.Threading.Tasks;
using Bing.Biz.Payments.Tests.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Services;
using Bing.Extensions;
using Xunit;

namespace Bing.Biz.Payments.Tests.Wechatpay.Services;

/// <summary>
/// 微信支付 - 下载交易账单服务
/// </summary>
public class WechatpayDownloadBillServiceTest
{
    /// <summary>
    /// 测试 - 下载交易账单
    /// </summary>
    //[Fact]
    [Fact(Skip = "更改支付配置后测试")]
    public async Task Test_DownloadAsync()
    {
        var service = new WechatpayDownloadBillService(new TestConfigProvider());
        var request = new WechatpayDownloadBillRequest { BillDate = "2022-06-10".ToDate() };
        var result = await service.DownloadAsync(request);
        Assert.NotEmpty(result.Bills);
    }
}