using System.Threading.Tasks;

namespace Bing.SecurityLog
{
    /// <summary>
    /// 安全日志存储器
    /// </summary>
    public interface ISecurityLogStore
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="securityLogInfo">安全日志信息</param>
        Task SaveAsync(SecurityLogInfo securityLogInfo);
    }
}
