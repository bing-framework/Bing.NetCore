using System.Drawing;

namespace Bing.Offices.Excels.Abstractions
{
    /// <summary>
    /// 单元格
    /// </summary>
    public interface ICell
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
        /// 获取单元格的值
        /// </summary>
        /// <returns></returns>
        string GetValue();

        /// <summary>
        /// 设置单元格的值
        /// </summary>
        /// <param name="value">值</param>
        void SetValue(string value);

        /// <summary>
        /// 设置单元格的计算公式
        /// </summary>
        /// <param name="formula">计算公式</param>
        void SetFormula(string formula);

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
        /// 是否合并单元格
        /// </summary>
        /// <returns></returns>
        bool IsMerged();
    }
}
