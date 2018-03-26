using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.BankCardInfo.Core;
using Bing.BankCardInfo.Models.Results;
using WebApiClient;

namespace Bing.BankCardInfo
{
    /// <summary>
    /// 银行卡客户端
    /// </summary>
    public class BankCardClient
    {
        /// <summary>
        /// API代理
        /// </summary>
        private readonly IBankCardApi _proxy;

        /// <summary>
        /// 异常处理
        /// </summary>
        private readonly Action<Exception> _exceptionHandler;

        /// <summary>
        /// 初始化一个<see cref="BankCardClient"/>类型的实例
        /// </summary>
        /// <param name="exceptionHandler">异常处理</param>
        public BankCardClient(Action<Exception> exceptionHandler = null)
        {
            _proxy = HttpApiClient.Create<IBankCardApi>();
        }

        /// <summary>
        /// 验证 银行卡信息
        /// </summary>
        /// <param name="cardNum">银行卡卡号</param>
        /// <returns></returns>
        public async Task<Models.BankCardInfo> ValidateAsync(string cardNum)
        {
            if (string.IsNullOrWhiteSpace(cardNum))
            {
                throw new ArgumentNullException(nameof(cardNum));
            }

            var result= await _proxy.ValidateAsync(cardNum);
            Models.BankCardInfo info=new Models.BankCardInfo();
            info.CardNo = result.Key;
            info.Validated = result.Validated;
            if (result.Validated)
            {
                info.Code = result.Bank;
                info.CardType = result.CardType;
                var img = await _proxy.BankImg(info.Code);
                info.Logo = img;
            }

            return info;
        }
    }
}
