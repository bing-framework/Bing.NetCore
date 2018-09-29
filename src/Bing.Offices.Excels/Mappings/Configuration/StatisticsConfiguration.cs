using Bing.Offices.Excels.Mappings.Abstractions;

namespace Bing.Offices.Excels.Mappings.Configuration
{
    /// <summary>
    /// 统计配置。表示指定实体的Excel统计信息
    /// </summary>
    public class StatisticsConfiguration:IStatisticsMap
    {
        /// <summary>
        /// 统计名称。默认名称位置为（最后一行，第一个单元格）
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// 计算公式。如：SUM、AVERAGE等，可用于垂直统计
        /// </summary>
        public string Formula { get;internal set; }

        /// <summary>
        /// 计算列索引。如果<see cref="Formula"/>是SUM，而<see cref="Columns"/>是[1,3]，例如：列1和列3将是SUM第一行到最后一行。
        /// </summary>
        public int[] Columns { get;internal set; }
    }
}
