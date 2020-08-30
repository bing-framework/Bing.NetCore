using System.ComponentModel;
using Bing.Extensions;

namespace Bing.Admin.Domain.Shared.Enums
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
        Rows = 4,
        /// <summary>
        /// 身份资源
        /// </summary>
        [Description("身份资源")]
        Identity = 5,
        /// <summary>
        /// Api资源
        /// </summary>
        [Description("Api资源")]
        Api = 6,
    }

    /// <summary>
    /// 资源类型枚举扩展
    /// </summary>
    public static class ResourceTypeExtensions
    {
        /// <summary>
        /// 获取描述
        /// </summary>
        public static string Description(this ResourceType? type) => type == null ? string.Empty : type.Value.Description();

        /// <summary>
        /// 获取值
        /// </summary>
        public static int? Value(this ResourceType? type) => type?.Value();
    }
}
