using System.Drawing;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元列
    /// </summary>
    public interface IColumn
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
        /// 设置宽度
        /// </summary>
        /// <param name="width">宽度</param>
        void SetWidth(int width);

        /// <summary>
        /// 获取单元格
        /// </summary>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        ICell GetCell(int rowIndex);
    }
}
