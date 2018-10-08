using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Bing.Offices.Excels.Mappings;
using Bing.Offices.Excels.Mappings.Configuration;
using Bing.Offices.Excels.Npoi.Extensions;
using NPOI.SS.UserModel;

namespace Bing.Offices.Excels.Npoi
{
    /// <summary>
    /// Excel加载操作
    /// </summary>
    public static class Excel
    {
        /// <summary>
        /// 计算公式
        /// </summary>
        private static IFormulaEvaluator _formulaEvaluator;

        /// <summary>
        /// 从指定Excel文件加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelFile">Excel文件路径</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(string excelFile, int startRow = 1, int sheetIndex = 0)
            where T : class, new() => Load<T>(excelFile, ExcelSetting.Default, startRow, sheetIndex);

        /// <summary>
        /// 从指定Excel文件加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelFile">Excel文件路径</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(string excelFile, ExcelSetting excelSetting, int startRow = 1,
            int sheetIndex = 0) where T : class, new()
        {
            if (!File.Exists(excelFile))
            {
                throw new FileNotFoundException($"找不到文件 {excelFile}");
            }

            return Load<T>(File.OpenRead(excelFile), excelSetting, startRow, sheetIndex);
        }

        /// <summary>
        /// 从指定Excel文件加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelFile">Excel文件路径</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(string excelFile, string sheetName, int startRow = 1)
            where T : class, new() => Load<T>(excelFile, ExcelSetting.Default, sheetName, startRow);

        /// <summary>
        /// 从指定Excel文件加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelFile">Excel文件路径</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(string excelFile, ExcelSetting excelSetting, string sheetName,
            int startRow) where T : class, new()
        {
            if (!File.Exists(excelFile))
            {
                throw new FileNotFoundException($"找不到文件 {excelFile}");
            }

            return Load<T>(File.OpenRead(excelFile), excelSetting, sheetName, startRow);
        }

        /// <summary>
        /// 从指定流加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelStream">流</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(Stream excelStream, int startRow = 1, int sheetIndex = 0)
            where T : class, new() => Load<T>(excelStream, ExcelSetting.Default, startRow, sheetIndex);

        /// <summary>
        /// 从指定流加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelStream">流</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <param name="sheetIndex">工作表索引</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(Stream excelStream, ExcelSetting excelSetting, int startRow,
            int sheetIndex) where T : class, new()
        {
            var workbook = InitializeWorkbook(excelStream);

            var sheet = workbook.GetSheetAt(sheetIndex);
            if (null == sheet)
            {
                throw new ArgumentException($"找不到指定索引[{sheetIndex}]工作表", nameof(sheetIndex));
            }

            return Load<T>(sheet, _formulaEvaluator, excelSetting, startRow);
        }

        /// <summary>
        /// 从指定流加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelStream">流</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(Stream excelStream, string sheetName, int startRow = 1)
            where T : class, new() => Load<T>(excelStream, ExcelSetting.Default, sheetName, startRow);

        /// <summary>
        /// 从指定流加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="excelStream">流</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <param name="sheetName">工作表名称</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(Stream excelStream, ExcelSetting excelSetting, string sheetName,
            int startRow = 1) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                throw new ArgumentException("工作表名称不能为null或者是空字符", nameof(sheetName));
            }
            var workbook = InitializeWorkbook(excelStream);

            var sheet = workbook.GetSheet(sheetName);
            if (null == sheet)
            {
                throw new ArgumentException($"找不到指定名称[{sheetName}]工作表", nameof(sheetName));
            }

            return Load<T>(sheet, _formulaEvaluator, excelSetting, startRow);
        }

        /// <summary>
        /// 从指定工作表加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="sheet">工作表</param>
        /// <param name="formulaEvaluator">计算公式</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(ISheet sheet, IFormulaEvaluator formulaEvaluator, int startRow = 1)
            where T : class, new() => Load<T>(sheet, formulaEvaluator, ExcelSetting.Default, startRow);

        /// <summary>
        /// 从指定工作表加载数据到<see cref="IEnumerable{T}"/>集合
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="sheet">工作表</param>
        /// <param name="formulaEvaluator">计算公式</param>
        /// <param name="excelSetting">用于加载数据的Excel设置</param>
        /// <param name="startRow">数据读取起始行</param>
        /// <returns></returns>
        public static IEnumerable<T> Load<T>(ISheet sheet, IFormulaEvaluator formulaEvaluator,
            ExcelSetting excelSetting, int startRow = 1) where T : class, new()
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            var properties =
                typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty);

            bool fluentConfigEnabled = excelSetting.FluentConfigs.TryGetValue(typeof(T), out var fluentConfig);

            var propertyConfigurations = new PropertyConfiguration[properties.Length];
            for (var j = 0; j < properties.Length; j++)
            {
                var property = properties[j];
                if (fluentConfigEnabled && fluentConfig.PropertyConfigurations.TryGetValue(property.Name, out var pc))
                {
                    propertyConfigurations[j] = pc;
                }
                else
                {
                    propertyConfigurations[j] = null;
                }
            }

            var statistics = new List<StatisticsConfiguration>();
            if (fluentConfigEnabled)
            {
                statistics.AddRange(fluentConfig.StatisticsConfigurations);
            }

            var list = new List<T>();
            int idx = 0;

            IRow headerRow = null;

            var rows = sheet.GetRowEnumerator();
            while (rows.MoveNext())
            {
                var row = rows.Current as IRow;
                if (idx == 0)
                {
                    headerRow = row;
                }
                idx++;

                if (row.RowNum < startRow)
                {
                    continue;
                }

                if (true == fluentConfig?.IgnoreWitespaceRows)
                {
                    if (row.Cells.All(x =>
                        CellType.Blank == x.CellType || (CellType.String == x.CellType &&
                                                         string.IsNullOrWhiteSpace(x.StringCellValue))))
                    {
                        continue;
                    }
                }

                var item = new T();
                var itemIsValid = true;
                for (var i = 0; i < properties.Length; i++)
                {
                    var prop = properties[i];
                    int index = i;
                    var config = propertyConfigurations[i];
                    if (config != null)
                    {
                        if (config.IgnoreImport)
                        {
                            continue;
                        }
                        index = config.Index;

                        // 自动发现索引
                        if (index < 0 && config.AutoIndex && !string.IsNullOrWhiteSpace(config.Title))
                        {
                            foreach (var cell in headerRow.Cells)
                            {
                                if (!string.IsNullOrWhiteSpace(cell.StringCellValue))
                                {
                                    if (cell.StringCellValue.Equals(config.Title,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        index = cell.ColumnIndex;
                                        // 缓存单元格索引
                                        config.Index = index;
                                        break;
                                    }
                                }
                            }
                        }

                        // 再次检查索引是否设置
                        if (index < 0)
                        {
                            throw new ApplicationException("请设置'Index'或'AutoIndex'");
                        }
                    }

                    object value = row.GetCellValue(index, formulaEvaluator);
                    // 进行值校验
                    if (null != config?.CellValueValidator)
                    {
                        var validationResult = config.CellValueValidator(row.RowNum - 1, config.Index, value);
                        if (false == validationResult)
                        {
                            if (fluentConfig.SkipInvalidRows)
                            {
                                itemIsValid = false;
                                break;
                            }

                            throw new ArgumentException(
                                $"行 {row.RowNum} , 列 {config.Title}({config.Index + 1})中单元格值校验失败!值:[{value}]");
                        }
                    }

                    // 进行值转换
                    if (config?.CellValueConverter != null)
                    {
                        value = config.CellValueConverter(row.RowNum - 1, config.Index, value);
                    }

                    if (value == null)
                    {
                        continue;
                    }

                    // 检查是否统计行
                    if (idx > startRow + 1 && index == 0 && statistics.Any(x =>
                            x.Name.Equals(value.ToString(), StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var st = statistics.FirstOrDefault(x =>
                            x.Name.Equals(value.ToString(), StringComparison.InvariantCultureIgnoreCase));
                        var formula = row.GetCellValue(st.Columns.First()).ToString();
                        if (formula.StartsWith(st.Formula, StringComparison.InvariantCultureIgnoreCase))
                        {
                            itemIsValid = false;
                            break;
                        }
                    }

                    // 属性类型
                    var propType = prop.PropertyType.UnwrapNullableType();
                    var safeValue = Convert.ChangeType(value, propType, CultureInfo.CurrentCulture);
                    prop.SetValue(item,safeValue,null);
                }

                if (itemIsValid)
                {
                    // 进行行数据校验
                    if (null != fluentConfig?.RowDataValidator)
                    {
                        var validationResult = fluentConfig.RowDataValidator(row.RowNum - 1, item);
                        if (false == validationResult)
                        {
                            if (fluentConfig.SkipInvalidRows)
                            {
                                itemIsValid = false;
                                continue;
                            }
                            throw new ArgumentException($"行数据 {row.RowNum} 校验失败");
                        }
                    }

                    list.Add(item);
                }
            }

            return list;
        }

        /// <summary>
        /// 初始化工作簿
        /// </summary>
        /// <param name="excelFile">Excel文件路径</param>
        /// <returns></returns>
        private static IWorkbook InitializeWorkbook(string excelFile) => InitializeWorkbook(File.OpenRead(excelFile));

        /// <summary>
        /// 初始化工作簿
        /// </summary>
        /// <param name="excelStream">内存流</param>
        /// <returns></returns>
        private static IWorkbook InitializeWorkbook(Stream excelStream)
        {
            var workbook = WorkbookFactory.Create(excelStream);
            _formulaEvaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
            return workbook;
        }
    }
}
