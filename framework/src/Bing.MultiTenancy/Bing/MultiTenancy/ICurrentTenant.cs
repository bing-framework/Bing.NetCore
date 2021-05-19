using System;

namespace Bing.MultiTenancy
{
    /// <summary>
    /// 当前租户
    /// </summary>
    public interface ICurrentTenant
    {
        /// <summary>
        /// 是否可用的
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 租户标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 租户编码
        /// </summary>
        string Code { get; }

        /// <summary>
        /// 租户名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 变更
        /// </summary>
        /// <param name="tenantId">租户标识</param>
        /// <param name="code">租户编码</param>
        /// <param name="name">租户名称</param>
        IDisposable Change(string tenantId, string code = null, string name = null);
    }
}
