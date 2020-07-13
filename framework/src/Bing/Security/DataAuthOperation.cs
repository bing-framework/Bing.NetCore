using System.ComponentModel;

namespace Bing.Security
{
    /// <summary>
    /// 数据权限操作
    /// </summary>
    public enum DataAuthOperation
    {
        /// <summary>
        /// 读取
        /// </summary>
        [Description("读取")]
        Read,

        /// <summary>
        /// 更新
        /// </summary>
        [Description("更新")]
        Update,

        /// <summary>
        /// 删除
        /// </summary>
        [Description("删除")]
        Delete,
    }
}
