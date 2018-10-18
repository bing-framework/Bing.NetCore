using Bing.Payments.Response;

namespace Bing.Payments.Request
{
    /// <summary>
    /// 请求基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TResponse">响应类型</typeparam>
    public abstract class RequestBase<TEntity,TResponse> where TResponse:IResponse
    {
        /// <summary>
        /// 请求地址
        /// </summary>
        public string ReuqestUrl { get; set; }

        /// <summary>
        /// 异步通知地址
        /// </summary>
        public string NotifyUrl { get; set; }

        /// <summary>
        /// 同步通知地址
        /// </summary>
        public string ReturnUrl { get; set; }



    }
}
