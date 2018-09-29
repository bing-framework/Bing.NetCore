using Bing.Offices.Excels.Core.Styles;
using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi.Resolvers
{
    /// <summary>
    /// 单元格样式解析器
    /// </summary>
    public class CellStyleResolver
    {
        #region 字段

        /// <summary>
        /// 工作簿
        /// </summary>
        private readonly IWorkbook _workbook;

        /// <summary>
        /// 单元格样式
        /// </summary>
        private readonly CellStyle _style;

        /// <summary>
        /// Npoi单元格样式
        /// </summary>
        private ICellStyle _result;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="CellStyleResolver"/>类型的实例
        /// </summary>
        /// <param name="workbook">工作簿</param>
        /// <param name="cellStyle">单元格样式</param>
        private CellStyleResolver(IWorkbook workbook, CellStyle cellStyle)
        {
            _workbook = workbook;
            _style = cellStyle;
        }

        #endregion

        #region Resolve(解析为Npoi单元格样式)

        /// <summary>
        /// 解析为Npoi单元格样式
        /// </summary>
        /// <returns></returns>
        public ICellStyle Resolve()
        {
            _result = _workbook.CreateCellStyle();
            _result.Alignment = GetHorizontalAlignment();
            _result.VerticalAlignment = GetVerticalAlignment();
            SetBackgroundColor();
            SetForegroundColor();
            SetFillPattern();
            SetBorderColor();
            SetFont();
            _result.WrapText = _style.IsWrap;
            return _result;
        }

        /// <summary>
        /// 解析单元格样式
        /// </summary>
        /// <param name="workbook">工作簿</param>
        /// <param name="style">单元格样式</param>
        /// <returns></returns>
        public static ICellStyle Resolve(IWorkbook workbook, CellStyle style)
        {
            return new CellStyleResolver(workbook, style).Resolve();
        }

        /// <summary>
        /// 获取水平对齐
        /// </summary>
        /// <returns></returns>
        private NPOI.SS.UserModel.HorizontalAlignment GetHorizontalAlignment()
        {
            if (_style.Alignment == Bing.Offices.Excels.Core.Styles.HorizontalAlignment.Left)
            {
                return NPOI.SS.UserModel.HorizontalAlignment.Left;
            }

            if (_style.Alignment == Bing.Offices.Excels.Core.Styles.HorizontalAlignment.Right)
            {
                return NPOI.SS.UserModel.HorizontalAlignment.Right;
            }
            return NPOI.SS.UserModel.HorizontalAlignment.Center;
        }

        /// <summary>
        /// 获取垂直对齐
        /// </summary>
        /// <returns></returns>
        private NPOI.SS.UserModel.VerticalAlignment GetVerticalAlignment()
        {
            if (_style.VerticalAlignment == Bing.Offices.Excels.Core.Styles.VerticalAlignment.Top)
            {
                return NPOI.SS.UserModel.VerticalAlignment.Top;
            }
            if (_style.VerticalAlignment == Bing.Offices.Excels.Core.Styles.VerticalAlignment.Bottom)
            {
                return NPOI.SS.UserModel.VerticalAlignment.Bottom;
            }
            return NPOI.SS.UserModel.VerticalAlignment.Center;
        }

        /// <summary>
        /// 设置背景色
        /// </summary>
        private void SetBackgroundColor()
        {
            _result.FillBackgroundColor = ColorResolver.Resolve(_style.BackgroundColor);
        }

        /// <summary>
        /// 设置前景色
        /// </summary>
        private void SetForegroundColor()
        {
            _result.FillForegroundColor = ColorResolver.Resolve(_style.ForegroundColor);
        }

        /// <summary>
        /// 设置填充模式
        /// </summary>
        private void SetFillPattern()
        {
            _result.FillPattern = ConvertFillPattern(_style.FillPattern);
        }

        /// <summary>
        /// 转换填充模式
        /// </summary>
        /// <param name="fillPattern">填充模式</param>
        /// <returns></returns>
        private NPOI.SS.UserModel.FillPattern ConvertFillPattern(
            Bing.Offices.Excels.Core.Styles.FillPattern fillPattern)
        {
            switch (fillPattern)
            {
                case Bing.Offices.Excels.Core.Styles.FillPattern.NoFill:
                    return NPOI.SS.UserModel.FillPattern.NoFill;
                case Bing.Offices.Excels.Core.Styles.FillPattern.SolidForeground:
                    return NPOI.SS.UserModel.FillPattern.SolidForeground;
                case Bing.Offices.Excels.Core.Styles.FillPattern.FineDots:
                    return NPOI.SS.UserModel.FillPattern.FineDots;
                case Bing.Offices.Excels.Core.Styles.FillPattern.AltBars:
                    return NPOI.SS.UserModel.FillPattern.AltBars;
                case Bing.Offices.Excels.Core.Styles.FillPattern.SparseDots:
                    return NPOI.SS.UserModel.FillPattern.SparseDots;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThickHorizontalBands:
                    return NPOI.SS.UserModel.FillPattern.ThickHorizontalBands;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThickVerticalBands:
                    return NPOI.SS.UserModel.FillPattern.ThickVerticalBands;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThickBackwardDiagonals:
                    return NPOI.SS.UserModel.FillPattern.ThickBackwardDiagonals;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThickForwardDiagonals:
                    return NPOI.SS.UserModel.FillPattern.ThickForwardDiagonals;
                case Bing.Offices.Excels.Core.Styles.FillPattern.BigSpots:
                    return NPOI.SS.UserModel.FillPattern.BigSpots;
                case Bing.Offices.Excels.Core.Styles.FillPattern.Bricks:
                    return NPOI.SS.UserModel.FillPattern.Bricks;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThinHorizontalBands:
                    return NPOI.SS.UserModel.FillPattern.ThinHorizontalBands;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThinVerticalBands:
                    return NPOI.SS.UserModel.FillPattern.ThinVerticalBands;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThinBackwardDiagonals:
                    return NPOI.SS.UserModel.FillPattern.ThinBackwardDiagonals;
                case Bing.Offices.Excels.Core.Styles.FillPattern.ThinForwardDiagonals:
                    return NPOI.SS.UserModel.FillPattern.ThinForwardDiagonals;
                case Bing.Offices.Excels.Core.Styles.FillPattern.Squares:
                    return NPOI.SS.UserModel.FillPattern.Squares;
                case Bing.Offices.Excels.Core.Styles.FillPattern.Diamonds:
                    return NPOI.SS.UserModel.FillPattern.Diamonds;
                case Bing.Offices.Excels.Core.Styles.FillPattern.LessDots:
                    return NPOI.SS.UserModel.FillPattern.LessDots;
                case Bing.Offices.Excels.Core.Styles.FillPattern.LeastDots:
                    return NPOI.SS.UserModel.FillPattern.LeastDots;
                default:
                    return NPOI.SS.UserModel.FillPattern.NoFill;
            }
        }

        /// <summary>
        /// 设置边框颜色
        /// </summary>
        private void SetBorderColor()
        {
            _result.BorderTop = BorderStyle.Thin;
            _result.BorderRight = BorderStyle.Thin;
            _result.BorderBottom = BorderStyle.Thin;
            _result.BorderLeft = BorderStyle.Thin;
            _result.TopBorderColor = ColorResolver.Resolve(_style.BorderColor);
            _result.RightBorderColor = ColorResolver.Resolve(_style.BorderColor);
            _result.BottomBorderColor = ColorResolver.Resolve(_style.BorderColor);
            _result.LeftBorderColor = ColorResolver.Resolve(_style.BorderColor);
        }

        /// <summary>
        /// 设置字体
        /// </summary>
        private void SetFont()
        {
            var font = _workbook.CreateFont();
            font.FontHeightInPoints = _style.FontSize;
            font.Color = ColorResolver.Resolve(_style.FontColor);
            font.Boldweight = _style.FontBoldWeight;
            font.IsItalic = _style.Italic;
            _result.SetFont(font);
        }

        #endregion
    }
}
