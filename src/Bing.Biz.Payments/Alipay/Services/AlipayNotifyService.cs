using Bing.Biz.Payments.Alipay.Abstractions;
using Bing.Biz.Payments.Alipay.Configs;
using Bing.Biz.Payments.Alipay.Enums;
using Bing.Biz.Payments.Alipay.Services.Base;
using Bing.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Parameters;
using Bing.Validations;

namespace Bing.Biz.Payments.Alipay.Services
{
    /// <summary>
    /// 支付宝回调通知服务
    /// </summary>
    public class AlipayNotifyService:AlipayNotifyServiceBase,IAlipayNotifyService
    {
        /// <summary>
        /// 交易状态
        /// </summary>
        public TradeStatus? Status
        {
            get
            {
                switch (GetParam(AlipayConst.TradeStatus))
                {
                    case "WAIT_BUYER_PAY":
                        return TradeStatus.WaitPay;
                    case "TRADE_CLOSED":
                        return TradeStatus.Close;
                    case "TRADE_SUCCESS":
                        return TradeStatus.Success;
                    case "TRADE_FINISHED":
                        return TradeStatus.Finished;
                }

                return null;
            }
        }

        /// <summary>
        /// 初始化一个<see cref="AlipayNotifyService"/>类型的实例
        /// </summary>
        /// <param name="configProvider">配置提供器</param>
        public AlipayNotifyService(IAlipayConfigProvider configProvider) : base(configProvider)
        {
        }

        /// <summary>
        /// 加载参数
        /// </summary>
        /// <param name="builder">参数生成器</param>
        protected override void Load(UrlParameterBuilder builder)
        {
            builder.LoadForm();
        }

        /// <summary>
        /// 验证
        /// </summary>
        /// <returns></returns>
        protected override ValidationResultCollection Validate()
        {
            if (Status != TradeStatus.Success)
            {
                return new ValidationResultCollection(Status.Description());
            }
            return ValidationResultCollection.Success;
        }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <returns></returns>
        public string Success()
        {
            return "success";
        }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <returns></returns>
        public string Fail()
        {
            return "fail";
        }

        /// <summary>
        /// 获取日志标题
        /// </summary>
        /// <returns></returns>
        protected override string GetCaption()
        {
            return "支付宝回调通知";
        }
    }
}
