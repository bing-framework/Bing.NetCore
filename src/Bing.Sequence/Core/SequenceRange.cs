using Bing.Sequence.Internal;

namespace Bing.Sequence.Core
{
    /// <summary>
    /// 序列号区间
    /// </summary>
    public class SequenceRange
    {
        /// <summary>
        /// 区间的序列号开始值
        /// </summary>
        public long Begin { get; private set; }

        /// <summary>
        /// 区间的序列号结束值
        /// </summary>
        public long End { get; private set; }

        /// <summary>
        /// 区间的序列号是否分配完毕，每次分配完毕就会去重新获取一个新的区间
        /// </summary>
        public bool Over { get; set; }

        /// <summary>
        /// 区间的序列号当前值
        /// </summary>
        private readonly AtomicLong _value;

        public SequenceRange(long begin, long end)
        {
            this.Begin = begin;
            this.End = end;
            _value = new AtomicLong(this.Begin);
        }

        /// <summary>
        /// 获取并递增下一个序列号
        /// </summary>
        /// <returns></returns>
        public long GetAndIncrement()
        {
            long currentValue = _value.GetAndIncrement();
            if (currentValue > End)
            {
                Over = true;
                return -1;
            }

            return currentValue;
        }

        /// <summary>
        /// 重写转换字符串方法
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"Begin: {Begin}, End: {End}, Value: {_value}";
        }
    }
}
