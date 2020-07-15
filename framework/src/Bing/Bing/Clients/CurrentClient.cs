using System.Security.Principal;
using Bing.DependencyInjection;
using Bing.Security.Claims;

namespace Bing.Clients
{
    /// <summary>
    /// 当前客户端
    /// </summary>
    public class CurrentClient : ICurrentClient, ITransientDependency
    {
        /// <summary>
        /// 安全主体访问器
        /// </summary>
        private readonly ICurrentPrincipalAccessor _principalAccessor;

        /// <summary>
        /// 标识
        /// </summary>
        public virtual string Id => _principalAccessor.Principal?.FindClientId();

        /// <summary>
        /// 是否已认证
        /// </summary>
        public virtual bool IsAuthenticated => Id != null;

        /// <summary>
        /// 初始化一个<see cref="CurrentClient"/>类型的实例
        /// </summary>
        /// <param name="principalAccessor">安全主体访问器</param>
        public CurrentClient(ICurrentPrincipalAccessor principalAccessor) => _principalAccessor = principalAccessor;
    }
}
