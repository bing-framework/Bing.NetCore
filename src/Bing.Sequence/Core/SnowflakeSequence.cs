using Bing.Sequence.Abstractions;

namespace Bing.Sequence.Core
{
    /// <summary>
    /// 雪花算法序列号生成器
    /// </summary>
    public class SnowflakeSequence:ISequence
    {
        public long NextValue()
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// 设置数据标识ID
        /// </summary>
        /// <param name="datacenterId"></param>
        public void SetDatacenterId(long datacenterId)
        {

        }
    }
}
