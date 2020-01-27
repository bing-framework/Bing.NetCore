using System.ComponentModel;

namespace Bing.Core.Enums
{
    /// <summary>
    /// 环境类型
    /// </summary>
    public enum EnvironmentType : byte
    {
        /// <summary>
        /// 开发环境
        /// </summary>
        [Description("开发环境")]
        Development = 1,
        /// <summary>
        /// 测试环境
        /// </summary>
        [Description("测试环境")]
        Test = 2,
        /// <summary>
        /// 预览环境
        /// </summary>
        [Description("预览环境")]
        Preview = 3,
        /// <summary>
        /// 生产环境
        /// </summary>
        [Description("生产环境")]
        Prod = 4
    }
}
