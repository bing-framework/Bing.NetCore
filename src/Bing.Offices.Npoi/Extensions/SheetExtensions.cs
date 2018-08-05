using System;
using System.Collections.Generic;
using Bing.Offices.Core;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace Bing.Offices.Npoi.Extensions
{
    /// <summary>
    /// 工作表(<see cref="ISheet"/>) 扩展
    /// </summary>
    public static class SheetExtensions
    {
        /// <summary>
        /// 获取单元格的值
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static string GetCellValue(this ISheet sheet, int rowIndex, int columnIndex)
        {
            string value;
            if (IsMergedRegion(sheet, rowIndex, columnIndex))
            {
                value = GetMergedRegionValue(sheet, rowIndex, columnIndex);
            }
            else
            {
                IRow row = sheet.GetRow(rowIndex);
                ICell cell = row.GetCell(columnIndex);
                value = cell.GetCellValue();
            }
            return value;
        }

        /// <summary>
        /// 获取合并单元格的值
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static string GetMergedRegionValue(this ISheet sheet, int rowIndex, int columnIndex)
        {
            int sheetMergeCount = sheet.NumMergedRegions;
            for (int i = 0; i < sheetMergeCount; i++)
            {
                CellRangeAddress cellRange = sheet.GetMergedRegion(i);
                int firstColumn = cellRange.FirstColumn;
                int lastColumn = cellRange.LastColumn;
                int firstRow = cellRange.FirstRow;
                int lastRow = cellRange.LastRow;

                if (rowIndex >= firstRow && rowIndex <= lastRow)
                {
                    if (columnIndex >= firstColumn && columnIndex <= lastColumn)
                    {
                        IRow row = sheet.GetRow(firstRow);
                        ICell cell = row.GetCell(firstColumn);
                        return cell.GetCellValue();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 判断指定的单元格是否是合并单元格
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="columnIndex">列索引</param>
        /// <returns></returns>
        public static bool IsMergedRegion(this ISheet sheet, int rowIndex, int columnIndex)
        {
            int sheetMergeCount = sheet.NumMergedRegions;
            for (int i = 0; i < sheetMergeCount; i++)
            {
                CellRangeAddress cellRange = sheet.GetMergedRegion(i);
                int firstColumn = cellRange.FirstColumn;
                int lastColumn = cellRange.LastColumn;
                int firstRow = cellRange.FirstRow;
                int lastRow = cellRange.LastRow;

                if (rowIndex >= firstRow && rowIndex <= lastRow)
                {
                    if (columnIndex >= firstColumn && columnIndex <= lastColumn)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 删除工作表中指定列，并移动右侧列到左侧列
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="columnIndex">列索引</param>
        public static void DeleteColumn(this ISheet sheet, int columnIndex)
        {
            int maxColumn = 0;
            for (int i = 0; i < sheet.LastRowNum + 1; i++)
            {
                IRow row = sheet.GetRow(i);                
                if (row == null)
                {
                    continue;
                }

                int lastColumn = row.LastCellNum;
                if (lastColumn > maxColumn)
                {
                    maxColumn = lastColumn;
                }

                if (lastColumn < columnIndex)
                {
                    continue;
                }

                for (int x = columnIndex + 1; x < lastColumn + 1; x++)
                {
                    ICell oldCell = row.GetCell(x - 1);
                    if (oldCell != null)
                    {
                        row.RemoveCell(oldCell);
                    }

                    ICell nextCell = row.GetCell(x);
                    if (nextCell != null)
                    {
                        ICell newCell = row.CreateCell(x - 1, nextCell.CellType);
                        CloneCell(oldCell, newCell);
                    }
                }
            }
            // 调整列宽
            for (int c = columnIndex; c < maxColumn; c++)
            {
                sheet.SetColumnWidth(c, sheet.GetColumnWidth(c + 1));
            }
        }

        /// <summary>
        /// 克隆单元格
        /// </summary>
        /// <param name="oldCell">旧单元格</param>
        /// <param name="newCell">新单元格</param>
        private static void CloneCell(ICell oldCell, ICell newCell)
        {
            newCell.CellComment = oldCell.CellComment;
            newCell.CellStyle = oldCell.CellStyle;

            switch (newCell.CellType)
            {
                case CellType.Boolean:
                    newCell.SetCellValue(oldCell.BooleanCellValue);
                    break;
                case CellType.Numeric:
                    newCell.SetCellValue(oldCell.NumericCellValue);
                    break;
                case CellType.String:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
                case CellType.Error:
                    newCell.SetCellValue(oldCell.ErrorCellValue);
                    break;
                case CellType.Formula:
                    newCell.SetCellValue(oldCell.CellFormula);
                    break;
                default:
                    newCell.SetCellValue(oldCell.StringCellValue);
                    break;
            }
        }

        /// <summary>
        /// 合并单元格
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        public static CellRangeAddress MergedCell(this ISheet sheet, int firstRow, int lastRow, int firstColumn, int lastColumn)
        {
            var region = new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn);
            sheet.AddMergedRegion(region);
            return region;
        }

        /// <summary>
        /// 获取或创建行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public static IRow GetOrCreateRow(this ISheet sheet, int rowIndex)
        {
            return sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);
        }

        /// <summary>
        /// 创建行并进行操作
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="action">行操作</param>
        /// <returns></returns>
        public static ISheet CreateRow(this ISheet sheet, int rowIndex, Action<IRow> action)
        {
            var row = sheet.GetOrCreateRow(rowIndex);
            action?.Invoke(row);
            return sheet;
        }

        #region InsertRow(插入行)

        /// <summary>
        /// 插入行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public static IRow InsertRow(this ISheet sheet, int rowIndex)
        {
            return sheet.InsertRows(rowIndex, 1)[0];
        }

        /// <summary>
        /// 插入行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <param name="rowsCount">行数</param>
        /// <returns></returns>
        public static IRow[] InsertRows(this ISheet sheet, int rowIndex, int rowsCount)
        {
            if (rowIndex <= sheet.LastRowNum)
            {
                sheet.ShiftRows(rowIndex, sheet.LastRowNum, rowsCount, true, false);
            }

            var rows = new List<IRow>();
            for (var i = 0; i < rowsCount; i++)
            {
                IRow row = sheet.CreateRow(rowIndex + i);
            }

            sheet.MovePictures(rowIndex, null, null, null, moveRowCount: rowsCount);
            return rows.ToArray();
        }

        #endregion

        #region RemoveRow(删除行)

        /// <summary>
        /// 删除行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public static int RemoveRow(this ISheet sheet, int rowIndex)
        {
            return sheet.RemoveRows(rowIndex, rowIndex);
        }

        /// <summary>
        /// 删除行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <returns></returns>
        public static int RemoveRows(this ISheet sheet, int startRowIndex, int endRowIndex)
        {
            int span = endRowIndex - startRowIndex + 1;
            // 删除合并区域
            sheet.RemoveMergedRegions(startRowIndex, endRowIndex, null, null);
            // 删除图片
            sheet.RemovePictures(startRowIndex, endRowIndex, null, null);
            // 删除行
            for (var i = endRowIndex; i >= startRowIndex; i--)
            {
                var row = sheet.GetRow(i);
                sheet.RemoveRow(row);
            }

            if ((endRowIndex + 1) <= sheet.LastRowNum)
            {
                sheet.ShiftRows(endRowIndex + 1, sheet.LastRowNum, -span, true, false);
                sheet.MovePictures(endRowIndex + 1, null, null, null, moveRowCount: -span);
            }

            return span;
        }

        #endregion

        #region CopyRow(复制行)

        /// <summary>
        /// 复制行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="rowIndex">行索引</param>
        /// <returns></returns>
        public static int CopyRow(this ISheet sheet, int rowIndex)
        {
            return sheet.CopyRows(rowIndex, rowIndex);
        }

        /// <summary>
        /// 复制行
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="startRowIndex">起始行索引</param>
        /// <param name="endRowIndex">结束行索引</param>
        /// <returns></returns>
        public static int CopyRows(this ISheet sheet, int startRowIndex, int endRowIndex)
        {
            int span = endRowIndex - startRowIndex + 1;
            int newStartRowIndex = startRowIndex + span;
            // 插入空行
            sheet.InsertRows(newStartRowIndex, span);
            // 复制行
            for (var i = startRowIndex; i <= endRowIndex; i++)
            {
                IRow sourceRow = sheet.GetRow(i);
                IRow targetRow = sheet.GetRow(i + span);

                targetRow.Height = sourceRow.Height;
                targetRow.ZeroHeight = sourceRow.ZeroHeight;

                // 复制单元格
                foreach (ICell sourceCell in sourceRow.Cells)
                {
                    ICell targetCell = targetRow.GetCell(sourceCell.ColumnIndex);
                    if (null == targetCell)
                    {
                        targetCell = targetRow.CreateCell(sourceCell.ColumnIndex);
                    }

                    if (null != sourceCell.CellStyle)
                    {
                        targetCell.CellStyle = sourceCell.CellStyle;
                    }
                    if (null != sourceCell.CellComment)
                    {
                        targetCell.CellComment = sourceCell.CellComment;
                    }
                    if (null != sourceCell.Hyperlink)
                    {
                        targetCell.Hyperlink = sourceCell.Hyperlink;
                    }

                    var cfrs = sourceCell.GetConditionalFormattingRules();// 复制条件样式
                    if (null != cfrs && cfrs.Length > 0)
                    {
                        targetCell.AddConditionalFormattingRules(cfrs);
                    }
                    targetCell.SetCellType(sourceCell.CellType);

                    // 复制值
                    switch (sourceCell.CellType)
                    {
                        case CellType.Numeric:
                            targetCell.SetCellValue(sourceCell.NumericCellValue);
                            break;
                        case CellType.String:
                            targetCell.SetCellValue(sourceCell.StringCellValue);
                            break;
                        case CellType.Formula:
                            targetCell.SetCellValue(sourceCell.CellFormula);
                            break;
                        case CellType.Blank:
                            targetCell.SetCellValue(sourceCell.StringCellValue);
                            break;
                        case CellType.Boolean:
                            targetCell.SetCellValue(sourceCell.BooleanCellValue);
                            break;
                        case CellType.Error:
                            targetCell.SetCellValue(sourceCell.ErrorCellValue);
                            break;
                    }
                }
            }

            // 获取模板行内的合并区域
            var regions = sheet.GetMergedRegionInfos(startRowIndex, endRowIndex, null, null);
            // 复制合并区域
            foreach (var regionInfo in regions)
            {
                regionInfo.FirstRow += span;
                regionInfo.LastRow += span;
                sheet.AddMergedRegion(regionInfo);
            }
            // 获取模板行内的图片
            var pictures = sheet.GetAllPictureInfos(startRowIndex, endRowIndex, null, null);
            // 复制图片
            foreach (var pictureInfo in pictures)
            {
                pictureInfo.FirstRow += span;
                pictureInfo.LastRow += span;
                sheet.AddPicture(pictureInfo);
            }

            return span;
        }

        #endregion

        #region MergedRegion(合并区域)

        #region AddMergedRegion(添加合并区域)

        /// <summary>
        /// 添加合并区域
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="regionInfo">合并区域信息</param>
        public static void AddMergedRegion(this ISheet sheet, MergedRegionInfo regionInfo)
        {
            var region = new CellRangeAddress(regionInfo.FirstRow, regionInfo.LastRow, regionInfo.FirstColumn,
                regionInfo.LastColumn);
            sheet.AddMergedRegion(region);
        }

        #endregion

        #region GetMergedRegionInfos(获取合并区域信息列表)

        /// <summary>
        /// 获取所有合并区域信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <returns></returns>
        public static List<MergedRegionInfo> GetMergedRegionInfos(this ISheet sheet)
        {
            return sheet.GetMergedRegionInfos(null, null, null, null);
        }

        /// <summary>
        /// 获取指定区域包含合并区域信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行</param>
        /// <param name="lastRow">结束行</param>
        /// <param name="firstColumn">起始列</param>
        /// <param name="lastColumn">结束列</param>
        /// <returns></returns>
        public static List<MergedRegionInfo> GetMergedRegionInfos(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn)
        {
            var regions = new List<MergedRegionInfo>();
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress range = sheet.GetMergedRegion(i);
                if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, range.FirstRow, range.LastRow,
                    range.FirstColumn, range.LastColumn, true))
                {
                    regions.Add(new MergedRegionInfo(i, range.FirstRow, range.LastRow, range.FirstColumn,
                        range.LastColumn));
                }
            }
            return regions;
        }

        #endregion

        #region RemoveMergedRegions(删除合并区域)

        /// <summary>
        /// 删除所有合并区域
        /// </summary>
        /// <param name="sheet">工作表</param>
        public static void RemoveMergedRegions(this ISheet sheet)
        {
            sheet.RemoveMergedRegions(null, null, null, null);
        }

        /// <summary>
        /// 删除指定区域内的合并区域
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行</param>
        /// <param name="lastRow">结束行</param>
        /// <param name="firstColumn">起始列</param>
        /// <param name="lastColumn">结束列</param>
        public static void RemoveMergedRegions(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn)
        {
            List<MergedRegionInfo> regions;
            do
            {
                regions = sheet.GetMergedRegionInfos(firstRow, lastRow, firstColumn, lastColumn);
                foreach (var region in regions)
                {
                    sheet.RemoveMergedRegion(region.Index);
                }
            } while (regions.Count > 0);
        }

        #endregion

        #region MoveMergedRegions(移动合并区域)

        /// <summary>
        /// 移动所有合并区域
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="moveRowCount">移动总行数</param>
        /// <param name="moveColumnCount">移动总列数</param>
        public static void MoveMergedRegions(this ISheet sheet, int moveRowCount, int moveColumnCount = 0)
        {
            sheet.MoveMergedRegions(null, null, null, null, moveRowCount, moveColumnCount);
        }

        /// <summary>
        /// 移动指定区域内的合并区域
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行</param>
        /// <param name="lastRow">结束行</param>
        /// <param name="firstColumn">起始列</param>
        /// <param name="lastColumn">结束列</param>
        /// <param name="moveRowCount">移动总行数</param>
        /// <param name="moveColumnCount">移动总列数</param>
        public static void MoveMergedRegions(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, int moveRowCount, int moveColumnCount = 0)
        {
            for (var i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress range = sheet.GetMergedRegion(i);
                if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, range.FirstRow, range.LastRow,
                    range.FirstColumn, range.LastColumn, true))
                {
                    range.FirstRow += moveRowCount;
                    range.LastRow += moveRowCount;
                    range.FirstColumn += moveColumnCount;
                    range.LastColumn += moveColumnCount;
                }
            }
        }

        #endregion

        #endregion

        #region Picture(图片)

        #region AddPicture(添加图片)

        /// <summary>
        /// 添加图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="pictureInfo">图片信息</param>
        public static void AddPicture(this ISheet sheet, PictureInfo pictureInfo)
        {
            int pictureIndex = sheet.Workbook.AddPicture(pictureInfo.Data, PictureType.PNG);
            IClientAnchor anchor = sheet.Workbook.GetCreationHelper().CreateClientAnchor();
            anchor.Col1 = pictureInfo.FirstColumn;
            anchor.Col2 = pictureInfo.LastColumn;
            anchor.Row1 = pictureInfo.FirstRow;
            anchor.Row2 = pictureInfo.LastRow;
            anchor.Dx1 = pictureInfo.Style.AnchorDx1;
            anchor.Dx2 = pictureInfo.Style.AnchorDx2;
            anchor.Dy1 = pictureInfo.Style.AnchorDy1;
            anchor.Dy2 = pictureInfo.Style.AnchorDy2;
            anchor.AnchorType = AnchorType.MoveDontResize;
            IDrawing drawing = sheet.CreateDrawingPatriarch();
            IPicture picture = drawing.CreatePicture(anchor, pictureIndex);
            if (sheet is HSSFSheet)
            {
                var shape = (HSSFShape)picture;
                shape.FillColor = pictureInfo.Style.FillColor;
                shape.IsNoFill = pictureInfo.Style.IsNoFill;
                //shape.LineStyle = pictureInfo.Style.LineStyle;
                shape.LineStyleColor = pictureInfo.Style.LineStyleColor;
                shape.LineWidth = (int)pictureInfo.Style.LineWidth;
            }
            else if (sheet is XSSFSheet)
            {
                var shape = (XSSFShape)picture;
                shape.FillColor = pictureInfo.Style.FillColor;
                shape.IsNoFill = pictureInfo.Style.IsNoFill;
                //shape.LineStyle = pictureInfo.Style.LineStyle;
                //shape.LineStyleColor = pictureInfo.Style.LineStyleColor;
                shape.LineWidth = (int)pictureInfo.Style.LineWidth;
            }
        }

        #endregion

        #region GetAllPictureInfos(获取图片信息列表)

        /// <summary>
        /// 获取所有包含图片信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <returns></returns>
        public static List<PictureInfo> GetAllPictureInfos(this ISheet sheet)
        {
            return sheet.GetAllPictureInfos(null, null, null, null);
        }

        /// <summary>
        /// 获取指定区域包含图片信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        /// <returns></returns>
        public static List<PictureInfo> GetAllPictureInfos(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal = true)
        {
            switch (sheet)
            {
                case HSSFSheet hssfSheet:
                    return GetAllPictureInfos(hssfSheet, firstRow, lastRow, firstColumn, lastColumn, onlyInternal);
                case XSSFSheet xssfSheet:
                    return GetAllPictureInfos(xssfSheet, firstRow, lastRow, firstColumn, lastColumn, onlyInternal);
            }

            throw new NotImplementedException($"未实现方法，没有为该 {sheet.GetType().Name} 类型添加：GetAllPicturesInfos()扩展方法！");
        }

        /// <summary>
        /// 获取指定区域包含图片信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        /// <returns></returns>
        private static List<PictureInfo> GetAllPictureInfos(HSSFSheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal)
        {
            var pictures = new List<PictureInfo>();
            if (sheet.DrawingPatriarch is HSSFShapeContainer shapeContainer)
            {
                IList<HSSFShape> shapes = shapeContainer.Children;
                foreach (var shape in shapes)
                {
                    if (shape is HSSFPicture picture && picture.Anchor is HSSFClientAnchor anchor)
                    {
                        if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, anchor.Row1, anchor.Row2,
                            anchor.Col1, anchor.Row2, onlyInternal))
                        {
                            var pictureStyle = new PictureStyle()
                            {
                                AnchorDx1 = anchor.Dx1,
                                AnchorDx2 = anchor.Dx2,
                                AnchorDy1 = anchor.Dy1,
                                AnchorDy2 = anchor.Dy2,
                                IsNoFill = picture.IsNoFill,
                                LineStyleColor = picture.LineStyleColor,
                                LineWidth = picture.LineWidth,
                                FillColor = picture.FillColor,
                            };
                            pictures.Add(new PictureInfo(anchor.Row1, anchor.Row2, anchor.Col1, anchor.Col2,
                                picture.PictureData.Data, pictureStyle));
                        }
                    }
                }
            }
            return pictures;
        }

        /// <summary>
        /// 获取指定区域包含图片信息的列表
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        /// <returns></returns>
        private static List<PictureInfo> GetAllPictureInfos(XSSFSheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal)
        {
            var pictures = new List<PictureInfo>();
            var documentPartList = sheet.GetRelations();
            foreach (var documentPart in documentPartList)
            {
                if (documentPart is XSSFDrawing drawing)
                {
                    List<XSSFShape> shapes = drawing.GetShapes();
                    foreach (var shape in shapes)
                    {
                        if (shape is XSSFPicture picture)
                        {
                            IClientAnchor anchor = picture.GetPreferredSize();
                            if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, anchor.Row1, anchor.Row2,
                                anchor.Col1, anchor.Row2, onlyInternal))
                            {
                                var pictureStyle = new PictureStyle()
                                {
                                    AnchorDx1 = anchor.Dx1,
                                    AnchorDx2 = anchor.Dx2,
                                    AnchorDy1 = anchor.Dy1,
                                    AnchorDy2 = anchor.Dy2,
                                    IsNoFill = picture.IsNoFill,
                                    LineStyleColor = picture.LineStyleColor,
                                    LineWidth = picture.LineWidth,
                                    FillColor = picture.FillColor,
                                };
                                pictures.Add(new PictureInfo(anchor.Row1, anchor.Row2, anchor.Col1, anchor.Col2,
                                    picture.PictureData.Data, pictureStyle));
                            }
                        }
                    }
                }
            }
            return pictures;
        }

        #endregion

        #region RemovePictures(删除图片)

        /// <summary>
        /// 删除所有图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        public static void RemovePictures(this ISheet sheet)
        {
            sheet.RemovePictures(null, null, null, null);
        }

        /// <summary>
        /// 删除指定区域的图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        public static void RemovePictures(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal = true)
        {
            switch (sheet)
            {
                case HSSFSheet hssfSheet:
                    RemovePictures(hssfSheet, firstRow, lastRow, firstColumn, lastColumn, onlyInternal);
                    return;
                case XSSFSheet xssfSheet:
                    RemovePictures(xssfSheet, firstRow, lastRow, firstColumn, lastColumn, onlyInternal);
                    return;
            }

            throw new NotImplementedException($"未实现方法，没有为该 {sheet.GetType().Name} 类型添加：RemovePictures()扩展方法！");
        }

        /// <summary>
        /// 删除指定区域的图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        private static void RemovePictures(HSSFSheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal)
        {
            if (sheet.DrawingPatriarch is HSSFShapeContainer shapeContainer)
            {
                IList<HSSFShape> shapes = shapeContainer.Children;
                foreach (var shape in shapes)
                {
                    if (shape is HSSFPicture picture && picture.Anchor is HSSFClientAnchor anchor)
                    {
                        if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, anchor.Row1, anchor.Row2,
                            anchor.Col1, anchor.Row2, onlyInternal))
                        {
                            shapeContainer.RemoveShape(picture);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 删除指定区域的图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        private static void RemovePictures(XSSFSheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal)
        {
            throw new NotImplementedException($"XSSFSheet 未实现RemovePictures()方法！");
            var documentPartList = sheet.GetRelations();
            foreach (var documentPart in documentPartList)
            {
                if (documentPart is XSSFDrawing drawing)
                {
                    List<XSSFShape> shapes = drawing.GetShapes();
                    foreach (var shape in shapes)
                    {
                        if (shape is XSSFPicture picture)
                        {
                            IClientAnchor anchor = picture.GetPreferredSize();
                            if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, anchor.Row1, anchor.Row2,
                                anchor.Col1, anchor.Row2, onlyInternal))
                            {
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region MovePictures(移动图片)

        /// <summary>
        /// 移动所有图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="moveRowCount">移动行总数</param>
        /// <param name="moveColumnCount">移动列总数</param>
        public static void MovePictures(this ISheet sheet, int moveRowCount = 0, int moveColumnCount = 0)
        {
            sheet.MovePictures(null, null, null, null, true, moveRowCount, moveColumnCount);
        }

        /// <summary>
        /// 移动指定区域的所有图片
        /// </summary>
        /// <param name="sheet">工作表</param>
        /// <param name="firstRow">起始行索引</param>
        /// <param name="lastRow">结束行索引</param>
        /// <param name="firstColumn">起始列索引</param>
        /// <param name="lastColumn">结束列索引</param>
        /// <param name="onlyInternal">是否内部区域</param>
        /// <param name="moveRowCount">移动行总数</param>
        /// <param name="moveColumnCount">移动列总数</param>
        public static void MovePictures(this ISheet sheet, int? firstRow, int? lastRow,
            int? firstColumn, int? lastColumn, bool onlyInternal = true, int moveRowCount = 0, int moveColumnCount = 0)
        {
            if (sheet.DrawingPatriarch is HSSFShapeContainer shapeContainer)
            {
                IList<HSSFShape> shapes = shapeContainer.Children;
                foreach (var shape in shapes)
                {
                    if (shape is HSSFPicture picture && picture.Anchor is HSSFClientAnchor anchor)
                    {
                        if (IsInternalOrIntersect(firstRow, lastRow, firstColumn, lastColumn, anchor.Row1, anchor.Row2,
                            anchor.Col1, anchor.Row2, onlyInternal))
                        {
                            anchor.Row1 += moveRowCount;
                            anchor.Row2 += moveRowCount;
                            anchor.Col1 += moveColumnCount;
                            anchor.Col1 += moveColumnCount;
                        }
                    }
                }
            }
        }

        #endregion

        #endregion

        /// <summary>
        /// 判断区域内部或交叉
        /// </summary>
        /// <param name="rangeFirstRow">区域起始行</param>
        /// <param name="rangeLastRow">区域结束行</param>
        /// <param name="rangeFirstColumn">区域起始列</param>
        /// <param name="rangeLastColumn">区域结束列</param>
        /// <param name="pictureFirstRow">图片起始行</param>
        /// <param name="pictureLastRow">图片结束行</param>
        /// <param name="pictureFirstColumn">图片起始列</param>
        /// <param name="pictureLastColumn">图片结束列</param>
        /// <param name="onlyInternal">是否内部区域</param>
        /// <returns></returns>
        private static bool IsInternalOrIntersect(int? rangeFirstRow, int? rangeLastRow, int? rangeFirstColumn,
            int? rangeLastColumn, int pictureFirstRow, int pictureLastRow, int pictureFirstColumn,
            int pictureLastColumn, bool onlyInternal)
        {
            int firstRow = rangeFirstRow ?? pictureFirstRow;
            int lastRow = rangeLastRow ?? pictureLastRow;
            int firstColumn = rangeFirstColumn ?? pictureFirstColumn;
            int lastColumn = rangeLastColumn ?? pictureLastColumn;

            if (onlyInternal)
            {
                return (firstRow <= pictureFirstRow && lastRow >= pictureLastRow && firstColumn <= pictureFirstColumn &&
                        lastColumn >= pictureLastColumn);
            }

            return Math.Abs(lastRow - firstRow) + Math.Abs(pictureLastRow - pictureFirstRow) >=
                   Math.Abs(lastRow + firstRow - pictureLastRow - pictureFirstRow) &&
                   Math.Abs(lastColumn - firstColumn) + Math.Abs(pictureLastColumn - pictureFirstColumn) >=
                   Math.Abs(lastColumn + firstColumn - pictureLastColumn - pictureFirstColumn);
        }
    }
}
