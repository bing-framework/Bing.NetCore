namespace Bing.Utils.Drawing
{
    /// <summary>
    /// 缩略图模式
    /// </summary>
    public enum ThumbnailMode
    {
        /// <summary>
        /// 指定宽高裁剪（不变形）
        /// </summary>
        Cut = 1,

        /// <summary>
        /// 指定宽度，高度自动
        /// </summary>
        FixedW = 2,

        /// <summary>
        /// 指定高度，宽度自动
        /// </summary>
        FixedH = 4,

        /// <summary>
        /// 指定宽高（变形）
        /// </summary>
        FixedBoth = 8
    }
}
