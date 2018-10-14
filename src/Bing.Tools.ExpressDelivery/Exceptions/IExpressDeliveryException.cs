namespace Bing.Tools.ExpressDelivery.Exceptions
{
    /// <summary>
    /// 快递异常
    /// </summary>
    public interface IExpressDeliveryException
    {
        /// <summary>
        /// 消息
        /// </summary>
        string Message { get; }
    }
}
