using System;
using System.Threading.Tasks;
using Bing.Biz.Payments;
using Bing.Biz.Payments.Alipay.Parameters.Requests;
using Bing.Utils.Helpers;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 支付控制器
    /// </summary>
    public class PayController:ApiControllerBase
    {
        private readonly IPayFactory _payFactory;
        public PayController(IPayFactory factory)
        {
            _payFactory = factory;
        }

        [HttpPost]
        public async Task<string> CreateOrder()
        {
            var service = _payFactory.CreateAlipayWapPayService();
            return await service.PayAsync(new AlipayWapPayRequest()
            {
                Money = 100,
                Subject = "测试支付订单",
                OrderId = Id.Guid(),
            });

        }
    }
}
