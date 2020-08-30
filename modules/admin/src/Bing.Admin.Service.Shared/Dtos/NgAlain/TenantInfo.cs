using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Admin.Service.Shared.Dtos.NgAlain
{
    /// <summary>
    /// 租户信息
    /// </summary>
    public class TenantInfo
    {
        /// <summary>
        /// 租户标识
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 租户编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 租户名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 应用密钥
        /// </summary>
        public string AppSecret { get; set; }
    }
}
