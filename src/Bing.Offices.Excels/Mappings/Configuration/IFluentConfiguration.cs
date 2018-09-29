using System.Collections.Generic;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// Fluent 配置
    /// </summary>
    public interface IFluentConfiguration
    {
        /// <summary>
        /// 属性配置
        /// </summary>
        IReadOnlyDictionary<string,PropertyConfiguration> PropertyConfigurations { get; }

        /// <summary>
        /// 统计配置
        /// </summary>
        IReadOnlyList<StatisticsConfiguration> StatisticsConfigurations { get; }

        /// <summary>
        /// 筛选配置
        /// </summary>
        IReadOnlyList<FilterConfiguration> FilterConfigurations { get; }

        /// <summary>
        /// 冻结窗口配置
        /// </summary>
        IReadOnlyList<FreezeConfiguration> FreezeConfigurations { get; }

        /// <summary>
        /// 行数据校验器
        /// </summary>
        RowDataValidator RowDataValidator { get; }

        /// <summary>
        /// 加载数据时，是否跳过含有验证失败值的行
        /// </summary>
        bool SkipInvalidRows { get; }

        /// <summary>
        /// 是否忽略单元格的值全部为空白或空白的行
        /// </summary>
        bool IgnoreWitespaceRows { get; }
    }
}
