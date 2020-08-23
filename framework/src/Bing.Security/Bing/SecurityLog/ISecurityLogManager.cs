using System;
using System.Threading.Tasks;

namespace Bing.SecurityLog
{
    /// <summary>
    /// 安全日志管理
    /// </summary>
    public interface ISecurityLogManager
    {
        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="saveAction">保存操作</param>
        Task SaveAsync(Action<SecurityLogInfo> saveAction = null);
    }
}
