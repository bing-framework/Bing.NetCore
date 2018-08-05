namespace Bing.Offices.Core
{
    /// <summary>
    /// 图片样式
    /// </summary>
    public class PictureStyle
    {
        /// <summary>
        /// 描点：x1
        /// </summary>
        public int AnchorDx1 { get; set; }

        /// <summary>
        /// 描点：x2
        /// </summary>
        public int AnchorDx2 { get; set; }

        /// <summary>
        /// 描点：y1
        /// </summary>
        public int AnchorDy1 { get; set; }

        /// <summary>
        /// 描点：y2
        /// </summary>
        public int AnchorDy2 { get; set; }

        /// <summary>
        /// 填充颜色
        /// </summary>
        public int FillColor { get; set; }

        /// <summary>
        /// 是否无填充
        /// </summary>
        public bool IsNoFill { get; set; }

        /// <summary>
        /// 线样式颜色
        /// </summary>
        public int LineStyleColor { get; set; }

        /// <summary>
        /// 线宽度
        /// </summary>
        public double LineWidth { get; set; }
    }
}
