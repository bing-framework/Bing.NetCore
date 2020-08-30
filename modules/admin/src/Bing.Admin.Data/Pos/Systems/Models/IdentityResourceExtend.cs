using System.Collections.Generic;

namespace Bing.Admin.Data.Pos.Systems.Models
{
    /// <summary>
    /// 身份资源扩展信息
    /// </summary>
    public class IdentityResourceExtend
    {
        /// <summary>
        /// 必选
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// 强调
        /// </summary>
        public bool Emphasize { get; set; }

        /// <summary>
        /// 发现文档中显示
        /// </summary>
        public bool ShowInDiscoveryDocument { get; set; }

        /// <summary>
        /// 用户声明
        /// </summary>
        public List<string> Claims { get; set; }
    }
}
