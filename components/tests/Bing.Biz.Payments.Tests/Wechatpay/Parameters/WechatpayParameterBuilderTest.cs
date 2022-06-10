using System;
using Bing.Biz.Payments.Wechatpay.Configs;
using Bing.Biz.Payments.Wechatpay.Parameters;
using Bing.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Biz.Payments.Tests.Wechatpay.Parameters
{
    /// <summary>
    /// 微信支付参数生成器测试
    /// </summary>
    public class WechatpayParameterBuilderTest : IDisposable
    {
        /// <summary>
        /// 输出
        /// </summary>
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// 微信支付参数生成器
        /// </summary>
        private readonly WechatpayParameterBuilder _builder;

        /// <summary>
        /// 测试初始化
        /// </summary>
        public WechatpayParameterBuilderTest(ITestOutputHelper output)
        {
            _output = output;
            Time.SetTime(TestConst.Time);
            _builder = new WechatpayParameterBuilder(new WechatpayConfig());
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Time.Reset();
        }

        /// <summary>
        /// 测试 - 设置应用标识
        /// </summary>
        [Fact]
        public void Test_AppId()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<appid><![CDATA[a]]></appid>");
            result.Append("</xml>");

            // 操作
            _builder.AppId("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置商户号
        /// </summary>
        [Fact]
        public void Test_MerchantId()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<mch_id><![CDATA[a]]></mch_id>");
            result.Append("</xml>");

            // 操作
            _builder.MerchantId("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置签名类型
        /// </summary>
        [Fact]
        public void Test_SignType()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<sign_type><![CDATA[a]]></sign_type>");
            result.Append("</xml>");

            // 操作
            _builder.SignType("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置标题
        /// </summary>
        [Fact]
        public void Test_Body()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<body><![CDATA[a]]></body>");
            result.Append("</xml>");

            // 操作
            _builder.Body("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置商户订单号
        /// </summary>
        [Fact]
        public void Test_OutTradeNo()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<out_trade_no><![CDATA[a]]></out_trade_no>");
            result.Append("</xml>");

            // 操作
            _builder.OutTradeNo("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置总金额
        /// </summary>
        [Fact]
        public void Test_TotalFee()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<total_fee>123</total_fee>");
            result.Append("</xml>");

            // 操作
            _builder.TotalFee(1.23M);

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置回调通知地址
        /// </summary>
        [Fact]
        public void Test_NotifyUrl()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<notify_url><![CDATA[a]]></notify_url>");
            result.Append("</xml>");

            // 操作
            _builder.NotifyUrl("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置终端IP
        /// </summary>
        [Fact]
        public void Test_SpbillCreateIp()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<spbill_create_ip><![CDATA[a]]></spbill_create_ip>");
            result.Append("</xml>");

            // 操作
            _builder.SpbillCreateIp("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }

        /// <summary>
        /// 测试 - 设置交易类型
        /// </summary>
        [Fact]
        public void Test_TradeType()
        {
            // 结果
            var result = new Str();
            result.Append("<xml>");
            result.Append("<trade_type><![CDATA[a]]></trade_type>");
            result.Append("</xml>");

            // 操作
            _builder.TradeType("a");

            // 验证
            Assert.Equal(result.ToString(), _builder.ToXmlNoContainsSign());

            // 输出结果
            _output.WriteLine(_builder.ToString());
        }
    }
}
