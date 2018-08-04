namespace Bing.Offices.Models
{
    /// <summary>
    /// 图片信息
    /// </summary>
    public class ImageInfo
    {        
        /// <summary>
        /// 图片信息
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// 高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 输入方式
        /// </summary>
        public string Type { get; set; } = "url";

        /// <summary>
        /// 地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 行跨度
        /// </summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>
        /// 列跨度
        /// </summary>
        public int ColumnSpan { get; set; } = 1;

        /// <summary>
        /// 初始化一个<see cref="ImageInfo"/>类型的实例
        /// </summary>
        public ImageInfo() { }

        /// <summary>
        /// 初始化一个<see cref="ImageInfo"/>类型的实例
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public ImageInfo(byte[] data, int width, int height)
        {
            Data = data;
            Width = width;
            Height = height;
            Type = "data";
        }

        /// <summary>
        /// 初始化一个<see cref="ImageInfo"/>类型的实例
        /// </summary>
        /// <param name="url">图片地址</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        public ImageInfo(string url, int width, int height)
        {
            Url = url;
            Width = width;
            Height = height;
            Type = "url";
        }
    }
}
