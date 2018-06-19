using Bing.Utils.IdGenerators.Abstractions;
using Bing.Utils.IdGenerators.Ids;

namespace Bing.Utils.IdGenerators.Core
{
    /// <summary>
    /// 时间戳ID 生成器
    /// </summary>
    public class TimestampIdGenerator:IStringGenerator
    {
        /// <summary>
        /// 时间戳ID
        /// </summary>
        private readonly TimestampId _id=TimestampId.GetInstance();

        /// <summary>
        /// 获取<see cref="TimestampIdGenerator"/>类型的实例
        /// </summary>
        public static TimestampIdGenerator Current { get; } = new TimestampIdGenerator();

        /// <summary>
        /// 创建ID
        /// </summary>
        /// <returns></returns>
        public string Create()
        {
            return _id.GetId();
        }
    }
}
