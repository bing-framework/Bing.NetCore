using System;
using System.Collections.Generic;
using System.Text;
using Bing.BankCardInfo.Models.Results;
using WebApiClient;
using WebApiClient.Attributes;

namespace Bing.BankCardInfo.Core
{
    /// <summary>
    /// 银行卡Api
    /// </summary>
    public interface IBankCardApi:IHttpApiClient
    {
        /// <summary>
        /// 验证 银行卡信息
        /// </summary>
        /// <param name="cardNum">银行卡号</param>
        /// <returns></returns>
        [HttpGet("https://ccdcapi.alipay.com/validateAndCacheCardInfo.json?_input_charset=utf-8&cardNo={cardNum}&cardBinCheck=true")]
        [Return]
        ITask<ResponseData> ValidateAsync(string cardNum);

        /// <summary>
        /// 获取 银行图片
        /// </summary>
        /// <param name="bankCode">银行编码</param>
        /// <returns></returns>
        [HttpGet("https://apimg.alipay.com/combo.png?d=cashier&t={bankCode}")]
        [Base64Return]
        ITask<string> BankImg(string bankCode);
    }
}
