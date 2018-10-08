using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// Fluent 配置
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class FluentConfiguration<TEntity>:IFluentConfiguration where TEntity:class
    {
        /// <summary>
        /// 属性配置字典
        /// </summary>
        private Dictionary<string, PropertyConfiguration> _propertyConfigurations;

        /// <summary>
        /// 统计配置
        /// </summary>
        private List<StatisticsConfiguration> _statisticsConfigurations;

        /// <summary>
        /// 筛选配置
        /// </summary>
        private List<FilterConfiguration> _filterConfigurations;

        /// <summary>
        /// 冻结窗口配置
        /// </summary>
        private List<FreezeConfiguration> _freezeConfigurations;

        /// <summary>
        /// 属性配置
        /// </summary>
        public IReadOnlyDictionary<string, PropertyConfiguration> PropertyConfigurations => _propertyConfigurations;

        /// <summary>
        /// 统计配置
        /// </summary>
        public IReadOnlyList<StatisticsConfiguration> StatisticsConfigurations =>
            _statisticsConfigurations.AsReadOnly();

        /// <summary>
        /// 筛选配置
        /// </summary>
        public IReadOnlyList<FilterConfiguration> FilterConfigurations => _filterConfigurations.AsReadOnly();

        /// <summary>
        /// 冻结窗口配置
        /// </summary>
        public IReadOnlyList<FreezeConfiguration> FreezeConfigurations => _freezeConfigurations.AsReadOnly();

        /// <summary>
        /// 行数据校验器
        /// </summary>
        public RowDataValidator RowDataValidator { get; internal set; }

        /// <summary>
        /// 加载数据时，是否跳过含有验证失败值的行
        /// </summary>
        public bool SkipInvalidRows { get; internal set; }

        /// <summary>
        /// 是否忽略单元格的值全部为空白或空白的行
        /// </summary>
        public bool IgnoreWitespaceRows { get; internal set; }

        /// <summary>
        /// 初始化一个<see cref="FluentConfiguration{TEntity}"/>类型的实例
        /// </summary>
        internal FluentConfiguration()
        {
            _propertyConfigurations = new Dictionary<string, PropertyConfiguration>();
            _statisticsConfigurations = new List<StatisticsConfiguration>();
            _filterConfigurations = new List<FilterConfiguration>();
            _freezeConfigurations = new List<FreezeConfiguration>();
        }

        /// <summary>
        /// 通过指定的属性表达式获取指定实体及其属性的属性配置
        /// </summary>
        /// <typeparam name="TProperty">属性</typeparam>
        /// <param name="propertyExpression">属性表达式</param>
        /// <returns></returns>
        public PropertyConfiguration Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
        {
            var propertyInfo = GetPropertyInfo(propertyExpression);

            if (!_propertyConfigurations.TryGetValue(propertyInfo.Name, out var pc))
            {
                pc = new PropertyConfiguration();
                _propertyConfigurations[propertyInfo.Name] = pc;
            }

            return pc;
        }

        /// <summary>
        /// 通过指定的属性信息获取指定实体的属性配置
        /// </summary>
        /// <param name="propertyInfo">属性信息</param>
        /// <returns></returns>
        public PropertyConfiguration Property(PropertyInfo propertyInfo)
        {
            if (propertyInfo.DeclaringType != typeof(TEntity))
            {
                throw new InvalidOperationException($"当前属性信息不属于 {nameof(TEntity)}");
            }

            if (!_propertyConfigurations.TryGetValue(propertyInfo.Name, out var pc))
            {
                pc = new PropertyConfiguration();
                _propertyConfigurations[propertyInfo.Name] = pc;
            }

            return pc;
        }

        /// <summary>
        /// 配置指定实体的忽略映射的属性
        /// </summary>
        /// <param name="propertyExpressions">属性表达式列表</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> HasIgnoredProperties(
            params Expression<Func<TEntity, object>>[] propertyExpressions)
        {
            foreach (var propertyExpression in propertyExpressions)
            {
                var propertyInfo = GetPropertyInfo(propertyExpression);

                if (!_propertyConfigurations.TryGetValue(propertyInfo.Name, out var pc))
                {
                    pc = new PropertyConfiguration();
                    _propertyConfigurations[propertyInfo.Name] = pc;
                }

                pc.IsIgnored(true, true);
            }

            return this;
        }

        /// <summary>
        /// 调整指定实体的所有具有自动索引配置属性的自动索引值
        /// </summary>
        /// <returns></returns>
        public FluentConfiguration<TEntity> AdjustAutoIndex()
        {
            var index = 0;
            var properties =
                typeof(TEntity).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
            foreach (var property in properties)
            {
                if (!_propertyConfigurations.TryGetValue(property.Name, out var pc))
                {
                    if (_propertyConfigurations.Values.Any(x => x.Index == index))
                    {
                        _propertyConfigurations[property.Name] = pc = new PropertyConfiguration()
                        {
                            Title = property.Name,
                            AutoIndex = true,
                            Index = -1
                        };
                    }
                    else
                    {
                        index++;
                        continue;
                    }
                }

                if (pc.AutoIndex && !pc.IgnoreExport && pc.Index == -1)
                {
                    while (_propertyConfigurations.Values.Any(x=>x.Index==index))
                    {
                        index++;
                    }

                    pc.HasExcelIndex(index++);
                }
            }

            return this;
        }

        /// <summary>
        /// 配置Excel统计信息，仅适用于纵向，不适用于横向统计
        /// </summary>
        /// <param name="name">统计信息名称，默认名称位置为最后一行第一个单元格</param>
        /// <param name="formula">单元格公式，如SUM、AVERAGE等，适用于垂直统计</param>
        /// <param name="columnIndexes">列索引统计。如果<paramref name="formula"/>是SUM，而<paramref name="columnIndexes"/>是[1,3]，例如：列1和列3将是SUM第一行到最后一行。</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> HasStatistics(string name, string formula, params int[] columnIndexes)
        {
            var statistics=new StatisticsConfiguration()
            {
                Name = name,
                Formula = formula,
                Columns = columnIndexes
            };

            _statisticsConfigurations.Add(statistics);

            return this;
        }

        /// <summary>
        /// 配置Excel筛选器
        /// </summary>
        /// <param name="firstColumn">第一列索引</param>
        /// <param name="lastColumn">最后一列索引</param>
        /// <param name="firstRow">第一行索引</param>
        /// <param name="lastRow">最后一行索引</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> HasFilter(int firstColumn, int lastColumn, int firstRow,
            int? lastRow = null)
        {
            var filter=new FilterConfiguration()
            {
                FirstColumn = firstColumn,
                LastColumn = lastColumn,
                FirstRow = firstRow,
                LastRow = lastRow
            };

            _filterConfigurations.Add(filter);

            return this;
        }

        /// <summary>
        /// 配置Excel冻结窗口
        /// </summary>
        /// <param name="columnSplit">要冻结单元格的列号</param>
        /// <param name="rowSplit">要冻结单元格的行号</param>
        /// <param name="leftMostColumn">最左边的列索引</param>
        /// <param name="topMostRow">最顶行的索引</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> HasFreeze(int columnSplit, int rowSplit, int leftMostColumn, int topMostRow)
        {
            var freeze=new FreezeConfiguration()
            {
                ColumnSplit = columnSplit,
                RowSpit = rowSplit,
                LeftMostColumn = leftMostColumn,
                TopRow = topMostRow
            };

            _freezeConfigurations.Add(freeze);

            return this;
        }

        /// <summary>
        /// 配置行数据校验器
        /// </summary>
        /// <param name="rowDataValidator">行数据校验器</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> HasRowDataValidator(RowDataValidator<TEntity> rowDataValidator)
        {
            if (null == rowDataValidator)
            {
                RowDataValidator = null;
                return this;
            }

            RowDataValidator = (rowIndex, rowData) =>
            {
                var model = rowData as TEntity;
                if (null == model && null != rowData)
                {
                    throw new ArgumentException($"行数据没有 {typeof(TEntity).Name} 类型", nameof(rowData));
                }

                return rowDataValidator(rowIndex, model);
            };

            return this;
        }

        /// <summary>
        /// 配置加载Excel数据时，是否跳过校验失败的行
        /// </summary>
        /// <param name="shouldSkip">是否跳过校验失败的行</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> ShouldSkipInvalidRows(bool shouldSkip = false)
        {
            SkipInvalidRows = shouldSkip;

            return this;
        }

        /// <summary>
        /// 配置加载Excel数据时，是否忽略其单元格全部为空白或空白的行
        /// </summary>
        /// <param name="shouldIgnore">是否忽略空白行</param>
        /// <returns></returns>
        public FluentConfiguration<TEntity> ShouldIgnoreWhitespaceRows(bool shouldIgnore = true)
        {
            IgnoreWitespaceRows = shouldIgnore;

            return this;
        }

        /// <summary>
        /// 获取属性信息
        /// </summary>
        /// <typeparam name="TProperty">属性</typeparam>
        /// <param name="propertyExpression">属性表达式</param>
        /// <returns></returns>
        private PropertyInfo GetPropertyInfo<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
        {
            if (propertyExpression.NodeType != ExpressionType.Lambda)
            {
                throw new ArgumentException($"{nameof(propertyExpression)} 必须是 Lambda 表达式", nameof(propertyExpression));
            }

            var lambda = (LambdaExpression)propertyExpression;

            var memberExpression = ExtractMemberExpression(lambda.Body);
            if (memberExpression == null)
            {
                throw new ArgumentException($"{nameof(propertyExpression)} 必须是 Lambda 表达式", nameof(propertyExpression));
            }
            if (memberExpression.Member.DeclaringType == null)
            {
                throw new InvalidOperationException("属性没有声明类型");
            }
            return memberExpression.Member.DeclaringType.GetProperty(memberExpression.Member.Name);
        }

        /// <summary>
        /// 提取成员表达式
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <returns></returns>
        private MemberExpression ExtractMemberExpression(Expression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                    return (MemberExpression)expression;
                case ExpressionType.Convert:
                    var operand = ((UnaryExpression)expression).Operand;
                    return ExtractMemberExpression(operand);
            }

            return null;
        }
    }
}
