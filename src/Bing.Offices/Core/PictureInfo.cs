namespace Bing.Offices.Core
{
    /// <summary>
    /// 图片信息
    /// </summary>
    public class PictureInfo
    {
        /// <summary>
        /// 起始行
        /// </summary>
        public int FirstRow { get; set; }

        /// <summary>
        /// 结束行
        /// </summary>
        public int LastRow { get; set; }

        /// <summary>
        /// 起始列
        /// </summary>
        public int FirstColumn { get; set; }

        /// <summary>
        /// 结束列
        /// </summary>
        public int LastColumn { get; set; }

        /// <summary>
        /// 图片数据
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 图片样式
        /// </summary>
        public PictureStyle Style { get; set; }

        /// <summary>
        /// 初始化一个<see cref="PictureInfo"/>类型的实例
        /// </summary>
        /// <param name="firstRow">起始行</param>
        /// <param name="lastRow">结束行</param>
        /// <param name="firstColumn">起始列</param>
        /// <param name="lastColumn">结束列</param>
        /// <param name="data">图片数据</param>
        /// <param name="style">图片样式</param>
        public PictureInfo(int firstRow, int lastRow, int firstColumn, int lastColumn, byte[] data, PictureStyle style)
        {
            FirstRow = firstRow;
            LastRow = lastRow;
            FirstColumn = firstColumn;
            LastColumn = lastColumn;
            Data = data;
            Style = style;
        }
    }
}
