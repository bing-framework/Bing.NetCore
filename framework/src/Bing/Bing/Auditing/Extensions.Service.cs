using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Auditing
{
    /// <summary>
    /// 服务扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册审计
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddAudit(this IServiceCollection services) => services.AddAudit<NullAuditStore>();

        /// <summary>
        /// 注册审计
        /// </summary>
        /// <typeparam name="TAuditStore">审计存储器</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddAudit<TAuditStore>(this IServiceCollection services) where TAuditStore : class, IAuditStore => services.TryAddScoped<IAuditStore, TAuditStore>();
    }
}
