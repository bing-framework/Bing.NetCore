using System.ComponentModel;

namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 缩略图模式
    /// </summary>
    public enum ThumbImgMode
    {
        /// <summary>
        /// 默认
        /// </summary>
        [Description("默认")]
        Default = 0,

        /// <summary>
        /// 指定高宽缩放（可能变形）
        /// </summary>
        [Description("指定高宽缩放（可能变形）")]
        HW,

        /// <summary>
        /// 画布固定大小，图片居中，根据长宽比例，自动以宽或高按比例缩放
        /// </summary>
        [Description("画布固定大小，图片居中，根据长宽比例，自动以宽或高按比例缩放")]
        FillHW,

        /// <summary>
        /// 根据长宽比例，自动以宽或高按比例缩放
        /// </summary>
        [Description("根据长宽比例，自动以宽或高按比例缩放")]
        AutoHW,

        /// <summary>
        /// 指定宽，高按比例
        /// </summary>
        [Description("指定宽，高按比例")]
        W,

        /// <summary>
        /// 指定高，宽按比例
        /// </summary>
        [Description("指定高，宽按比例")]
        H,

        /// <summary>
        /// 指定高宽裁减（不变形）
        /// </summary>
        [Description("指定高宽裁减（不变形）")]
        Cut
    }
}
