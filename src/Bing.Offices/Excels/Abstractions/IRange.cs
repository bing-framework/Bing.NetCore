using System.Drawing;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 行列范围
    /// </summary>
    public interface IRange
    {
        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        bool Bold { get; set; }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        bool Italic { get; set; }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        void SetFontStyle(Font font);

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetBackgroundColor(Color color);

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        void SetFontColor(Color color);

        /// <summary>
        /// 合并单元格
        /// </summary>
        void Merge();

        /// <summary>
        /// 取消单元格合并
        /// </summary>
        void UnMerge();

        /// <summary>
        /// 获取行列范围数据
        /// </summary>
        /// <returns></returns>
        string[,] GetRangeData();
    }
}
