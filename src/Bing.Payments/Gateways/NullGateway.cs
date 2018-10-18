using System.Threading.Tasks;
using Bing.Payments.Request;

namespace Bing.Payments.Gateways
{
    /// <summary>
    /// 未知网关
    /// </summary>
    public class NullGateway:GatewayBase
    {
        public override string GatewayUrl { get; set; }
        protected internal override bool IsPaySuccess => false;
        protected internal override bool IsRefundSuccess => false;
        protected internal override bool IsCancelSuccess => false;
        protected internal override string[] NotifyVerifyParameter => null;
        protected internal override Task<bool> ValidateNotifyAsync()
        {
            return Task.FromResult(false);
        }

        public override TResponse Execute<TEntity, TResponse>(RequestBase<TEntity, TResponse> request)
        {
            return default(TResponse);
        }
    }
}
