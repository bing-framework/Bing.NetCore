using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Bing.Offices.Excels.Core.Styles;
using Bing.Offices.Excels.Mappings;
using Bing.Offices.Excels.Mappings.Configuration;
using Bing.Offices.Excels.Npoi.Resolvers;
using NPOI.HPSF;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using VerticalAlignment = NPOI.SS.UserModel.VerticalAlignment;

namespace Bing.Offices.Excels.Npoi.Extensions
{
    /// <summary>
    /// 集合(<see cref="IEnumerable{T}"/>) 扩展
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// 计算公式
        /// </summary>
        private static IFormulaEvaluator _formulaEvaluator;

        public static byte[] ToExcelContent<T>(this IEnumerable<T> source, string sheetName = "sheet0",
            int maxRowsPerSheet = int.MaxValue, bool overwrite = false) where T:class
        {
            return ToExcel(source, null, s => sheetName, maxRowsPerSheet, overwrite);
        }

        public static void ToExcel<T>(this IEnumerable<T> source, string excelFile, string sheetName = "sheet0",
            int maxRowsPerSheet = int.MaxValue, bool overwrite = false) where T : class
        {
            ToExcel(source, excelFile, s => sheetName, maxRowsPerSheet, overwrite);
        }

        public static byte[] ToExcel<T>(this IEnumerable<T> source, string excelFile,
            Expression<Func<T, string>> sheetSelector, int maxRowsPerSheet = int.MaxValue, bool overwrite = false)
            where T : class
        {
            return ToExcel(source, excelFile, ExcelSetting.Default, sheetSelector, maxRowsPerSheet, overwrite);
        }

        public static byte[] ToExcel<T>(this IEnumerable<T> source, string excelFile, ExcelSetting excelSetting,
            Expression<Func<T, string>> sheetSelecor, int maxRowsPersheet = int.MaxValue, bool overwrite = false)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            bool isVolatile = string.IsNullOrWhiteSpace(excelFile);
            if (!isVolatile)
            {
                var extension = Path.GetExtension(excelFile);
                if (extension.Equals(".xls", StringComparison.InvariantCultureIgnoreCase))
                {
                    excelSetting.UseXlsx = false;
                }
                else if (extension.Equals(".xlsx", StringComparison.InvariantCultureIgnoreCase))
                {
                    excelSetting.UseXlsx = true;
                }
                else
                {
                    throw new NotSupportedException($"不是Excel文件(*.xls|*.xlsx) 扩展名:{extension}");
                }
            }
            else
            {
                excelFile = null;
            }

            IWorkbook workbook = InitializeWorkbook(excelFile, excelSetting);
            using (Stream ms=isVolatile?(Stream)new MemoryStream():new FileStream(excelFile,FileMode.OpenOrCreate,FileAccess.Write))
            {
                IEnumerable<byte> output = Enumerable.Empty<byte>();
                foreach (var sheet in source.AsQueryable().GroupBy(sheetSelecor))
                {
                    int sheetIndex = 0;
                    var content = sheet.Select(row => row);
                    while (content.Any())
                    {
                        workbook = content.Take(maxRowsPersheet).ToWorkbook(workbook,
                            sheet.Key + (sheetIndex > 0 ? "_" + sheetIndex.ToString() : ""), overwrite);
                        sheetIndex++;
                        content = content.Skip(maxRowsPersheet);
                    }
                }
                workbook.Write(ms);
                return isVolatile ? ((MemoryStream) ms).ToArray() : null;
            }
        }

        public static IWorkbook ToWorkbook<T>(this IEnumerable<T> source, string sheetName = "sheet0")
            where T : class => ToWorkbook<T>(source, ExcelSetting.Default, sheetName);

        public static IWorkbook ToWorkbook<T>(this IEnumerable<T> source, ExcelSetting excelSetting,
            string sheetName = "sheet0") where T : class => ToWorkbook<T>(source,
            InitializeWorkbook(null, excelSetting), excelSetting, sheetName, false);

        public static IWorkbook ToWorkbook<T>(this IEnumerable<T> source, IWorkbook workbook,
            string sheetName = "sheet0", bool overwrite = false) where T : class =>
            ToWorkbook<T>(source, workbook, ExcelSetting.Default, sheetName, overwrite);

        public static IWorkbook ToWorkbook<T>(this IEnumerable<T> source, IWorkbook workbook, ExcelSetting excelSetting,
            string sheetName = "sheet0", bool overwrite = false) where T : class
        {
            if (null == source)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (null == workbook)
            {
                throw new ArgumentNullException(nameof(workbook));
            }
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                throw new ArgumentNullException($"工作表名称不能为空或空字符串", nameof(sheetName));
            }

            var properties =
                typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);

            bool fluentConfigEnabled = false;
            if (excelSetting.FluentConfigs.TryGetValue(typeof(T), out var fluentConfig))
            {
                fluentConfigEnabled = true;
                (fluentConfig as FluentConfiguration<T>)?.AdjustAutoIndex();
            }

            // 查找输出配置
            var propertyConfigurations = new PropertyConfiguration[properties.Length];
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (fluentConfigEnabled && fluentConfig.PropertyConfigurations.TryGetValue(property.Name, out var pc))
                {
                    propertyConfigurations[i] = pc;
                }
                else
                {
                    propertyConfigurations[i] = null;
                }
            }

            // 再次检查工作表名称
            var sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                sheet = workbook.CreateSheet(sheetName);
            }
            else
            {
                // 如果不需要，则不会覆盖现有工作表
                if (!overwrite)
                {
                    sheet = workbook.CreateSheet();
                }
            }

            // 缓存单元格样式
            var cellStyles = new Dictionary<int, ICellStyle>();

            // 标题行单元格样式
            ICellStyle titleStyle = null;
            if (excelSetting.TitleCellStyleApplier != null)
            {
                var style = new CellStyle();
                excelSetting.TitleCellStyleApplier(style);
                titleStyle = CellStyleResolver.Resolve(workbook, style);
            }

            var titleRow = sheet.CreateRow(0);
            var rowIndex = 1;
            foreach (var item in source)
            {
                var row = sheet.CreateRow(rowIndex);
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];

                    int index = i;
                    var config = propertyConfigurations[i];
                    if (config != null)
                    {
                        if (config.IgnoreExport)
                        {
                            continue;
                        }

                        index = config.Index;
                        if (index < 0)
                        {
                            throw new Exception($"{property.Name} 索引值不能小于0。请参考 HasExcelIndex(int index) 方法以获取更多信息。");
                        }
                    }

                    if (rowIndex == 1)
                    {
                        // 没有标题的时候，使用属性名作为标题
                        var title = property.Name;
                        if (!string.IsNullOrWhiteSpace(config?.Title))
                        {
                            title = config.Title;
                        }

                        if (!string.IsNullOrWhiteSpace(config?.Formatter))
                        {
                            try
                            {
                                var style = workbook.CreateCellStyle();
                                var dataFormat = workbook.CreateDataFormat();
                                style.DataFormat = dataFormat.GetFormat(config.Formatter);
                                cellStyles[i] = style;
                            }
                            catch (Exception e)
                            {
                                System.Diagnostics.Debug.WriteLine(e.ToString());
                            }
                        }

                        var titleCell = titleRow.CreateCell(index);
                        if (titleCell != null)
                        {
                            titleCell.CellStyle = titleStyle;
                        }
                        titleCell.SetValue(title);
                    }

                    var unwrapType = property.PropertyType.UnwrapNullableType();
                    var value = property.GetValue(item, null);
                    // 进行值转换
                    if (config?.CellValueConverter != null)
                    {
                        value = config.CellValueConverter(rowIndex, index, value);
                        if (value == null)
                        {
                            continue;
                        }
                        unwrapType = value.GetType().UnwrapNullableType();
                    }

                    if (value == null)
                    {
                        continue;
                    }

                    var cell = row.CreateCell(index);
                    if (cellStyles.TryGetValue(i, out var cellStyle))
                    {
                        cell.CellStyle = cellStyle;
                    }
                    else if (!string.IsNullOrWhiteSpace(config?.Formatter) && value is IFormattable fv)
                    {
                        // C#格式化程序
                        cell.SetCellValue(fv.ToString(config.Formatter, CultureInfo.CurrentCulture));
                        continue;
                    }

                    cell.SetValue(value);
                }

                rowIndex++;
            }

            // 合并单元格
            var mergableConfigs = propertyConfigurations.Where(c => c != null && c.AllowMerge).ToList();
            if (mergableConfigs.Any())
            {
                var vStyle = workbook.CreateCellStyle();
                vStyle.VerticalAlignment = VerticalAlignment.Center;

                foreach (var config in mergableConfigs)
                {
                    object previous = null;
                    int rowSpan = 0, row = 1;
                    for (row = 1; row < rowIndex; row++)
                    {
                        var value = sheet.GetRow(row).GetCellValue(config.Index, _formulaEvaluator);
                        if (object.Equals(previous, value) && value != null)
                        {
                            rowSpan++;
                        }
                        else
                        {
                            if (rowSpan > 1)
                            {
                                sheet.GetRow(row - rowSpan).Cells[config.Index].CellStyle = vStyle;
                                sheet.AddMergedRegion(new CellRangeAddress(row - rowSpan, row - 1, config.Index,
                                    config.Index));
                            }

                            rowSpan = 1;
                            previous = value;
                        }
                    }

                    if (rowSpan > 1)
                    {
                        sheet.GetRow(row - rowSpan).Cells[config.Index].CellStyle = vStyle;
                        sheet.AddMergedRegion(new CellRangeAddress(row - rowSpan, row - 1, config.Index, config.Index));
                    }
                }
            }

            if (rowIndex > 1 && fluentConfigEnabled)
            {
                var statistics = fluentConfig.StatisticsConfigurations;
                var filterConfigs = fluentConfig.FilterConfigurations;
                var freezeConfigs = fluentConfig.FreezeConfigurations;

                // 统计信息
                foreach (var item in statistics)
                {
                    var lastRow = sheet.CreateRow(rowIndex);
                    var cell = lastRow.CreateCell(0);
                    cell.SetValue(item.Name);
                    foreach (var column in item.Columns)
                    {
                        cell = lastRow.CreateCell(column);
                        cell.CellStyle = sheet.GetRow(rowIndex - 1)?.GetCell(column)?.CellStyle;
                        cell.CellFormula =
                            $"{item.Formula}({GetCellPosition(1, column)}:{GetCellPosition(rowIndex - 1, column)})";
                    }
                    rowIndex++;
                }

                // 冻结窗口
                foreach (var freeze in freezeConfigs)
                {
                    sheet.CreateFreezePane(freeze.ColumnSplit, freeze.RowSpit, freeze.LeftMostColumn, freeze.TopRow);
                }

                // 筛选器
                foreach (var filter in filterConfigs)
                {
                    sheet.SetAutoFilter(new CellRangeAddress(filter.FirstRow, filter.LastRow ?? rowIndex,
                        filter.FirstColumn, filter.LastColumn));
                }
            }

            // 自动列宽
            if (excelSetting.AutoSizeColumnsEnabled)
            {
                for (var i = 0; i < properties.Length; i++)
                {
                    sheet.AutoSizeColumn(i);
                }
            }

            return workbook;
        }

        /// <summary>
        /// 初始化工作簿
        /// </summary>
        /// <param name="excelFile">Excel文件路径</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <returns></returns>
        private static IWorkbook InitializeWorkbook(string excelFile, ExcelSetting excelSetting = null)
        {
            var setting = excelSetting ?? ExcelSetting.Default;
            if (setting.UseXlsx)
            {
                if (!string.IsNullOrWhiteSpace(excelFile) && File.Exists(excelFile))
                {
                    using (var file=new FileStream(excelFile,FileMode.Open,FileAccess.Read))
                    {
                        var workbook = new XSSFWorkbook(file);
                        _formulaEvaluator = new XSSFFormulaEvaluator(workbook);
                        return workbook;
                    }
                }
                else
                {
                    var workbook = new XSSFWorkbook();
                    _formulaEvaluator = new XSSFFormulaEvaluator(workbook);

                    var props = workbook.GetProperties();
                    props.CoreProperties.Creator = setting.Author;
                    props.CoreProperties.Subject = setting.Subject;
                    props.ExtendedProperties.GetUnderlyingProperties().Company = setting.Company;

                    return workbook;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(excelFile) && File.Exists(excelFile))
                {
                    using (var file = new FileStream(excelFile, FileMode.Open, FileAccess.Read))
                    {
                        var workbook = new HSSFWorkbook(file);
                        _formulaEvaluator = new HSSFFormulaEvaluator(workbook);
                        return workbook;
                    }
                }
                else
                {
                    var workbook = new HSSFWorkbook();
                    _formulaEvaluator = new HSSFFormulaEvaluator(workbook);

                    var dsi = PropertySetFactory.CreateDocumentSummaryInformation();
                    dsi.Company = setting.Company;
                    workbook.DocumentSummaryInformation = dsi;

                    var si = PropertySetFactory.CreateSummaryInformation();
                    si.Author = setting.Author;
                    si.Subject = setting.Subject;
                    workbook.SummaryInformation = si;

                    return workbook;
                }
            }
        }

        /// <summary>
        /// 获取单元格位置
        /// </summary>
        /// <param name="row">行索引</param>
        /// <param name="col">列索引</param>
        /// <returns></returns>
        private static string GetCellPosition(int row, int col)
        {
            col = Convert.ToInt32('A') + col;
            row = row + 1;
            return ((char) col) + row.ToString();
        }
    }
}
