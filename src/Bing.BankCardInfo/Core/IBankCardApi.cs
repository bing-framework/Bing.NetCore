using System;
using System.Collections.Generic;
using System.Text;
using Bing.BankCardInfo.Models.Results;
using WebApiClient;

namespace Bing.BankCardInfo.Core
{
    /// <summary>
    /// 银行卡Api
    /// </summary>
    internal interface IBankCardApi:IHttpApiClient
    {
        //ITask<ResponseData> ValidateAsync(string )
    }
}
