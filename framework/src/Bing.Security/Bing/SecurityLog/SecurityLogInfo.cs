using System;
using System.Collections.Generic;

namespace Bing.SecurityLog
{
    /// <summary>
    /// 安全日志信息
    /// </summary>
    [Serializable]
    public class SecurityLogInfo
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// 标识
        /// </summary>
        public string Identity { get; set; }

        /// <summary>
        /// 操作
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public Dictionary<string,object> ExtraProperties { get; set; }

        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 租户标识
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// 租户名称
        /// </summary>
        public string TenantName { get; set; }

        /// <summary>
        /// 客户端标识
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 跟踪标识
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        public string ClientIpAddress { get; set; }

        /// <summary>
        /// 浏览器信息
        /// </summary>
        public string BrowserInfo { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// 初始化一个<see cref="SecurityLogInfo"/>类型的实例
        /// </summary>
        public SecurityLogInfo()
        {
            ExtraProperties = new Dictionary<string, object>();
        }

        /// <summary>
        /// 输出字符串
        /// </summary>
        public override string ToString() => $"SECURITY LOG: [{ApplicationName} - {Identity} -{Action}]";
    }
}
