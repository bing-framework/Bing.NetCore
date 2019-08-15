using System.ComponentModel;

namespace Bing.PermissionSystem.Domain.Enums
{
    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// 模块
        /// </summary>
        [Description("模块")]
        Module = 1,
        /// <summary>
        /// 操作
        /// </summary>
        [Description("操作")]
        Operation = 2,
        /// <summary>
        /// 列
        /// </summary>
        [Description("列")]
        Column = 3,
        /// <summary>
        /// 行集
        /// </summary>
        [Description("行集")]
        Rows = 4
    }
}
