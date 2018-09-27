using System.Drawing;
using Bing.Offices.Npoi.Extensions;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace Bing.Offices.Npoi.Excels.Core
{
    /// <summary>
    /// 基于NPOI的行列范围
    /// </summary>
    public class NpoiRange: Bing.Offices.Excels.Abstractions.IRange
    {
        /// <summary>
        /// 工作表
        /// </summary>
        private ISheet _sheet;

        /// <summary>
        /// 行列范围地址
        /// </summary>
        private CellRangeAddress _rangeAddress;

        /// <summary>
        /// 加粗。将文字加粗
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// 倾斜。将文字变为斜体
        /// </summary>
        public bool Italic { get; set; }

        /// <summary>
        /// 初始化一个<see cref="NpoiRange"/>类型的实例
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="startColumnIndex">起始列索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <param name="endColumnIndex">结束列索引</param>
        public NpoiRange(ISheet sheet, int startRowIndex, int startColumnIndex, int endRowIndex, int endColumnIndex)
        {
            _sheet = sheet;
            _rangeAddress = new CellRangeAddress(startRowIndex, endRowIndex, startColumnIndex, endColumnIndex);
        }

        /// <summary>
        /// 设置字体样式
        /// </summary>
        /// <param name="font">字体</param>
        public void SetFontStyle(Font font)
        {
            for (var i = _rangeAddress.FirstRow; i < _rangeAddress.LastRow; i++)
            {
                IRow row = _sheet.GetOrCreateRow(i);
                for (var j = _rangeAddress.FirstColumn; j < _rangeAddress.LastColumn; j++)
                {
                    ICell cell = row.GetOrCreateCell(j);
                    var npoiCell = new NpoiCell(cell);
                    npoiCell.SetFontStyle(font);
                }
            }
        }

        /// <summary>
        /// 设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetBackgroundColor(Color color)
        {
            for (var i = _rangeAddress.FirstRow; i < _rangeAddress.LastRow; i++)
            {
                IRow row = _sheet.GetOrCreateRow(i);
                for (var j = _rangeAddress.FirstColumn; j < _rangeAddress.LastColumn; j++)
                {
                    ICell cell = row.GetOrCreateCell(j);
                    var npoiCell = new NpoiCell(cell);
                    npoiCell.SetBackgroundColor(color);
                }
            }
        }

        /// <summary>
        /// 设置字体颜色
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetFontColor(Color color)
        {
            for (var i = _rangeAddress.FirstRow; i < _rangeAddress.LastRow; i++)
            {
                IRow row = _sheet.GetOrCreateRow(i);
                for (var j = _rangeAddress.FirstColumn; j < _rangeAddress.LastColumn; j++)
                {
                    ICell cell = row.GetOrCreateCell(j);
                    var npoiCell = new NpoiCell(cell);
                    npoiCell.SetFontColor(color);
                }
            }
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        public void Merge()
        {
            _sheet.AddMergedRegion(_rangeAddress);
        }

        /// <summary>
        /// 取消单元格合并
        /// </summary>
        public void UnMerge()
        {
            _sheet.RemoveMergedRegions(_rangeAddress.FirstRow, _rangeAddress.LastRow, _rangeAddress.FirstColumn,
                _rangeAddress.LastColumn);
        }

        /// <summary>
        /// 获取行列范围数据
        /// </summary>
        /// <returns></returns>
        public string[,] GetRangeData()
        {
            int columnNum = _rangeAddress.LastColumn - _rangeAddress.FirstColumn + 1;
            int rowNum = _rangeAddress.LastRow - _rangeAddress.FirstRow + 1;
            string[,] values=new string[rowNum,columnNum];
            for (var i = 0; i < rowNum; i++)
            {
                for (var j = 0; j < columnNum; j++)
                {
                    IRow row = _sheet.GetRow(i + _rangeAddress.FirstRow);
                    if (row == null)
                    {
                        values[i, j] = "";
                    }
                    else
                    {
                        values[i, j] = row.GetCell(j + _rangeAddress.FirstColumn).GetCellValue();
                    }
                }
            }

            return values;
        }
    }
}
