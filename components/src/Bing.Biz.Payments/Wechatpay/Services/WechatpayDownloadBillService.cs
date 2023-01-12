using Bing.Biz.Payments.Wechatpay.Abstractions;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Enums;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Biz.Payments.Wechatpay.Parameters.Requests;
using Bing.Biz.Payments.Wechatpay.Results;
using Bing.Biz.Payments.Wechatpay.Services.Base;
using Bing.Biz.Payments.Wechatpay.Services.CsvMappings;
using Bing.Extensions;
using Microsoft.Extensions.Logging;
using TinyCsvParser;
using TinyCsvParser.Mapping;
using TinyCsvParser.Tokenizer;

namespace Bing.Biz.Payments.Wechatpay.Services;

/// <summary>
/// 微信支付下载交易账单服务
/// </summary>
public class WechatpayDownloadBillService : WechatpayServiceBase<WechatpayDownloadBillRequest, WechatpayParameterBuilder>, IWechatpayDownloadBillService
{
    /// <summary>
    /// 初始化一个<see cref="WechatpayDownloadBillService"/>类型的实例
    /// </summary>
    /// <param name="configProvider">微信支付配置提供程序</param>
    /// <param name="loggerFactory">日志工厂</param>
    public WechatpayDownloadBillService(IWechatpayConfigProvider configProvider, ILoggerFactory loggerFactory) 
        : base(configProvider, loggerFactory)
    {
    }

    /// <summary>
    /// 下载对账单
    /// </summary>
    /// <param name="request">下载对账单参数</param>
    public async Task<WechatpayDownloadBillResult> DownloadAsync(WechatpayDownloadBillRequest request)
    {
        var config = await ConfigProvider.GetConfigAsync(request);
        Validate(config, request);
        var builder = new WechatpayParameterBuilder(config);
        ConfigBuilder(builder, request);
        return await RequestAsync(config, builder, request);
    }

    /// <summary>
    /// 创建参数生成器
    /// </summary>
    /// <param name="config">微信支付配置</param>
    protected override WechatpayParameterBuilder CreateParameterBuilder(WechatpayConfig config) => new WechatpayParameterBuilder(config);

    /// <summary>
    /// 配置参数生成器
    /// </summary>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="param">请求参数</param>
    protected override void ConfigBuilder(WechatpayParameterBuilder builder, WechatpayDownloadBillRequest param)
    {
        builder.Init();
        builder.Add("bill_date", param.GetBillDate());
        builder.Add("bill_type", param.BillType.Description());
    }

    /// <summary>
    /// 请求结果
    /// </summary>
    /// <param name="config">微信支付配置</param>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="request">请求参数</param>
    protected async Task<WechatpayDownloadBillResult> RequestAsync(WechatpayConfig config, WechatpayParameterBuilder builder, WechatpayDownloadBillRequest request)
    {
        var response = await SendRequest(config, builder);
        if (response.StartsWith("交易时间"))
            return Success(response, builder, request);
        return await Fail(response, config, builder);
    }

    /// <summary>
    /// 请求成功
    /// </summary>
    /// <param name="response">响应内容</param>
    /// <param name="builder">微信支付参数生成器</param>
    /// <param name="request">请求参数</param>
    private WechatpayDownloadBillResult Success(string response, WechatpayParameterBuilder builder, WechatpayDownloadBillRequest request)
    {
        var bills = GetBills(response, request);
        return new WechatpayDownloadBillResult(true, builder.ToString(), bills);
    }

    /// <summary>
    /// 获取对账单
    /// </summary>
    /// <param name="response">响应内容</param>
    /// <param name="request">请求参数</param>
    private List<WechatpayBillInfo> GetBills(string response, WechatpayDownloadBillRequest request)
    {
        var result = new List<WechatpayBillInfo>();
        var tokenizer = new QuotedStringTokenizer(',');
        var options = new CsvParserOptions(true, tokenizer);
        var parser = new CsvParser<WechatpayBillInfo>(options, GetCsvMapping(request));
        var readerOptions = new CsvReaderOptions(new[] { Environment.NewLine });
        var records = parser.ReadFromString(readerOptions, response);
        foreach (var record in records)
        {
            if (record.IsValid == false)
                continue;
            if (record.Result.TransactionTime == "交易时间")
                continue;
            if (record.Result.TransactionTime == "总交易单数")
                break;
            result.Add(record.Result);
        }
        return result;
    }

    /// <summary>
    /// 获取Csv映射
    /// </summary>
    /// <param name="request">请求参数</param>
    private ICsvMapping<WechatpayBillInfo> GetCsvMapping(WechatpayDownloadBillRequest request)
    {
        switch (request.BillType)
        {
            case WechatpayBillType.Success:
                return new WechatpaySuccessBillInfoMapping();
            case WechatpayBillType.RechargeRefund:
                return new WechatpayRefundBillInfoMapping();
            default:
                return new WechatpayAllBillInfoMapping();
        }
    }

    /// <summary>
    /// 请求失败
    /// </summary>
    /// <param name="response">响应内容</param>
    /// <param name="config">微信支付配置</param>
    /// <param name="builder">微信支付参数生成器</param>
    private async Task<WechatpayDownloadBillResult> Fail(string response, WechatpayConfig config, WechatpayParameterBuilder builder)
    {
        var result = new WechatpayResult(ConfigProvider, response, config, builder);
        WriteLog(config, builder, result);
        var success = (await result.ValidateAsync()).IsValid;
        return new WechatpayDownloadBillResult(success, result);
    }

    /// <summary>
    /// 获取接口地址
    /// </summary>
    /// <param name="config">微信支付配置</param>
    protected override string GetUrl(WechatpayConfig config) => config.GetDownloadBillUrl();
}
