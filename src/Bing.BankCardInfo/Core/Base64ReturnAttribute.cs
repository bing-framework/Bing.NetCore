using System;
using System.Threading.Tasks;
using WebApiClient.Attributes;
using WebApiClient.Contexts;

namespace Bing.BankCardInfo.Core
{
    /// <summary>
    /// 返回Base64字符串
    /// </summary>
    public class Base64ReturnAttribute:ApiReturnAttribute
    {
        protected override async Task<object> GetTaskResult(ApiActionContext context)
        {
            var response = context.ResponseMessage;
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return Convert.ToBase64String(bytes);
        }
    }
}
