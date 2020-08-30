using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 租户设置信息
    /// </summary>
    public class TenantSettingInfo
    {
        /// <summary>
        /// 租户标识
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 设置
        /// </summary>
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}
