using Bing.Payments.Request;
using Bing.Payments.Response;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 网关
    /// </summary>
    public interface IGateway
    {
        /// <summary>
        /// 商户数据
        /// </summary>
        IMerchant Merchant { get; set; }

        /// <summary>
        /// 执行
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TResponse">响应了洗ing</typeparam>
        /// <param name="request">请求数据</param>
        /// <returns></returns>
        TResponse Execute<TEntity, TResponse>(RequestBase<TEntity, TResponse> request) where TResponse : IResponse;
    }
}
