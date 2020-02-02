using System.ComponentModel;
using Bing.Extensions;

namespace Bing.Utils.Files
{
    /// <summary>
    /// 文件大小单位
    /// </summary>
    public enum FileSizeUnit
    {
        /// <summary>
        /// 字节
        /// </summary>
        [Description("B")]
        Byte,
        /// <summary>
        /// K字节
        /// </summary>
        [Description("KB")]
        K,
        /// <summary>
        /// M字节
        /// </summary>
        [Description("MB")]
        M,
        /// <summary>
        /// G字节
        /// </summary>
        [Description("GB")]
        G
    }

    /// <summary>
    /// 文件大小单位枚举扩展
    /// </summary>
    public static class FileSizeUnitExtensions
    {
        /// <summary>
        /// 获取描述
        /// </summary>
        /// <param name="unit">文件大小单位</param>
        public static string Description(this FileSizeUnit? unit) => unit == null ? string.Empty : unit.Value.Description();

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="unit">文件大小单位</param>
        public static int? Value(this FileSizeUnit? unit) => unit?.Value();
    }
}
