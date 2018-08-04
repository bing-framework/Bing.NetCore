using System;
using NPOI.SS.UserModel;

namespace Bing.Offices.Npoi.Extensions
{
    /// <summary>
    /// 单元格样式(<see cref="ICellStyle"/>) 扩展
    /// </summary>
    public static class CellStyleExtensions
    {
        /// <summary>
        /// 设置数据格式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="dataFormat">数据格式</param>
        /// <returns></returns>
        public static ICellStyle SetDataFormat(this ICellStyle cellStyle, short dataFormat)
        {
            cellStyle.DataFormat = dataFormat;
            return cellStyle;
        }

        /// <summary>
        /// 设置水平对齐
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="horizontalAlignment">水平对齐</param>
        /// <returns></returns>
        public static ICellStyle SetHorizontalAlignment(this ICellStyle cellStyle,
            HorizontalAlignment horizontalAlignment)
        {
            cellStyle.Alignment = horizontalAlignment;
            return cellStyle;
        }

        /// <summary>
        /// 设置垂直对齐
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="verticalAlignment">垂直对齐</param>
        /// <returns></returns>
        public static ICellStyle SetVerticalAlignment(this ICellStyle cellStyle, VerticalAlignment verticalAlignment)
        {
            cellStyle.VerticalAlignment = verticalAlignment;
            return cellStyle;
        }

        /// <summary>
        /// 设置填充前景颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="fillForegroundColor">填充前景颜色</param>
        /// <returns></returns>
        public static ICellStyle SetFillForegroundColor(this ICellStyle cellStyle, short fillForegroundColor)
        {
            cellStyle.FillForegroundColor = fillForegroundColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置填充背景颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="backgroundColor">填充背景颜色</param>
        /// <returns></returns>
        public static ICellStyle SetFillBackgroundColor(this ICellStyle cellStyle, short backgroundColor)
        {
            cellStyle.FillBackgroundColor = backgroundColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置填充模式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="fillPattern">填充图案</param>
        /// <returns></returns>
        public static ICellStyle SetFillPattern(this ICellStyle cellStyle, FillPattern fillPattern)
        {
            cellStyle.FillPattern = fillPattern;
            return cellStyle;
        }

        /// <summary>
        /// 设置上边框样式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderStyle">边框样式</param>
        /// <returns></returns>
        public static ICellStyle SetBorderTop(this ICellStyle cellStyle, BorderStyle borderStyle)
        {
            cellStyle.BorderTop = borderStyle;
            return cellStyle;
        }

        /// <summary>
        /// 设置右边框样式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderStyle">边框样式</param>
        /// <returns></returns>
        public static ICellStyle SetBorderRight(this ICellStyle cellStyle, BorderStyle borderStyle)
        {
            cellStyle.BorderRight = borderStyle;
            return cellStyle;
        }

        /// <summary>
        /// 设置下边框样式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderStyle">边框样式</param>
        /// <returns></returns>
        public static ICellStyle SetBorderBottom(this ICellStyle cellStyle, BorderStyle borderStyle)
        {
            cellStyle.BorderBottom = borderStyle;
            return cellStyle;
        }

        /// <summary>
        /// 设置左边框样式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderStyle">边框样式</param>
        /// <returns></returns>
        public static ICellStyle SetBorderLeft(this ICellStyle cellStyle, BorderStyle borderStyle)
        {
            cellStyle.BorderLeft = borderStyle;
            return cellStyle;
        }

        /// <summary>
        /// 设置边框样式
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderStyle">边框样式</param>
        /// <returns></returns>
        public static ICellStyle SetBorder(this ICellStyle cellStyle, BorderStyle borderStyle)
        {
            cellStyle.BorderTop = borderStyle;
            cellStyle.BorderRight = borderStyle;
            cellStyle.BorderBottom = borderStyle;
            cellStyle.BorderLeft = borderStyle;
            return cellStyle;
        }

        /// <summary>
        /// 设置上边框颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderColor">边框颜色</param>
        /// <returns></returns>
        public static ICellStyle SetBorderTopColor(this ICellStyle cellStyle, short borderColor)
        {
            cellStyle.TopBorderColor = borderColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置右边框颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderColor">边框颜色</param>
        /// <returns></returns>
        public static ICellStyle SetBorderRightColor(this ICellStyle cellStyle, short borderColor)
        {
            cellStyle.RightBorderColor = borderColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置下边框颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderColor">边框颜色</param>
        /// <returns></returns>
        public static ICellStyle SetBorderBottomColor(this ICellStyle cellStyle, short borderColor)
        {
            cellStyle.BottomBorderColor = borderColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置左边框颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderColor">边框颜色</param>
        /// <returns></returns>
        public static ICellStyle SetBorderLeftColor(this ICellStyle cellStyle, short borderColor)
        {
            cellStyle.LeftBorderColor = borderColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置边框颜色
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="borderColor">边框颜色</param>
        /// <returns></returns>
        public static ICellStyle SetBorderColor(this ICellStyle cellStyle, short borderColor)
        {
            cellStyle.TopBorderColor = borderColor;
            cellStyle.RightBorderColor = borderColor;
            cellStyle.BottomBorderColor = borderColor;
            cellStyle.LeftBorderColor = borderColor;
            return cellStyle;
        }

        /// <summary>
        /// 设置字体
        /// </summary>
        /// <param name="cellStyle">单元格样式</param>
        /// <param name="workbook">工作簿</param>
        /// <param name="action">字体</param>
        /// <returns></returns>
        public static ICellStyle SetFont(this ICellStyle cellStyle, IWorkbook workbook, Action<IFont> action)
        {
            IFont font = workbook.CreateFont();
            action?.Invoke(font);
            cellStyle.SetFont(font);
            return cellStyle;
        }
    }
}
