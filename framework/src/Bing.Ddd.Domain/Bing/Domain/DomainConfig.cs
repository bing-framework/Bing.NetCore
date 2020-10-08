using System;

namespace Bing.Domain
{
    /// <summary>
    /// 领域选项配置
    /// </summary>
    public class DomainOptions
    {
        /// <summary>
        /// Guid 生成函数
        /// </summary>
        public static Func<Guid> GuidGenerateFunc { get; set; } = Guid.NewGuid;
    }
}
