using System;
using System.Security.Claims;
using System.Threading;

namespace Bing.Security.Claims
{
    /// <summary>
    /// 当前线程安全主体访问器
    /// </summary>
    public class ThreadCurrentPrincipalAccessor : ICurrentPrincipalAccessor
    {
        /// <summary>
        /// 当前安全主体
        /// </summary>
        private readonly AsyncLocal<ClaimsPrincipal> _currentPrincipal = new AsyncLocal<ClaimsPrincipal>();

        /// <summary>
        /// 安全主体
        /// </summary>
        public virtual ClaimsPrincipal Principal => _currentPrincipal.Value ?? GetClaimsPrincipal();

        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="principal">安全主体</param>
        public virtual IDisposable Change(ClaimsPrincipal principal) => SetCurrent(principal);

        /// <summary>
        /// 设置当前安全主体
        /// </summary>
        /// <param name="principal">安全主体</param>
        private IDisposable SetCurrent(ClaimsPrincipal principal)
        {
            var parent = Principal;
            _currentPrincipal.Value = principal;
            return new DisposeAction(() =>
            {
                _currentPrincipal.Value = parent;
            });
        }

        /// <summary>
        /// 获取安全主体
        /// </summary>
        public virtual ClaimsPrincipal GetClaimsPrincipal() => Thread.CurrentPrincipal as ClaimsPrincipal;
    }
}
