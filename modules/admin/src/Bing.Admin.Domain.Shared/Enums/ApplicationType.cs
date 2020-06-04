using System.ComponentModel;

namespace Bing.Admin.Domain.Shared.Enums
{
    /// <summary>
    /// 应用类型
    /// </summary>
    public enum ApplicationType
    {
        /// <summary>
        /// 常规应用
        /// </summary>
        [Description("常规应用")]
        General = 1,
        /// <summary>
        /// Api
        /// </summary>
        [Description("Api")]
        Api = 2,
        /// <summary>
        /// 客户端
        /// </summary>
        [Description("客户端")]
        Client = 3,
    }
}
