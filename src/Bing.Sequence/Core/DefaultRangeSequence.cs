using System;
using Bing.Sequence.Abstractions;

namespace Bing.Sequence.Core
{
    /// <summary>
    /// 默认序列号区间生成器
    /// </summary>
    public class DefaultRangeSequence:IRangeSequence
    {
        /// <summary>
        /// 序列号区间管理器
        /// </summary>
        private ISequenceRangeManager _sequenceRangeManager;

        /// <summary>
        /// 当前序列号区间
        /// </summary>
        private SequenceRange _currentRange;

        /// <summary>
        /// 需要获取区间的业务名称
        /// </summary>
        private string _name;

        /// <summary>
        /// 对象锁
        /// </summary>
        private readonly object _lock = new object();

        /// <summary>
        /// 生成下一个序列号
        /// </summary>
        /// <returns></returns>
        public long NextValue()
        {
            // 当前区间不存在，重新获取一个区间
            if (null == _currentRange)
            {
                lock (_lock)
                {
                    if (null == _currentRange)
                    {
                        _currentRange = _sequenceRangeManager.NextRange(_name);
                    }
                }
            }

            // 当value值为-1时，表明区间的序列号已经分配完，需要冲洗那获取区间
            long value = _currentRange.GetAndIncrement();
            if (value == -1)
            {
                lock (_lock)
                {
                    for (;;)
                    {
                        if (_currentRange.Over)
                        {
                            _currentRange = _sequenceRangeManager.NextRange(_name);
                        }

                        value = _currentRange.GetAndIncrement();
                        if (value == -1)
                        {
                            continue;
                        }
                        break;
                    }
                }
            }

            if (value < 0)
            {
                throw new ArgumentException($"序列值溢出。value = {value}");
            }

            return value;
        }

        /// <summary>
        /// 设置区间管理器
        /// </summary>
        /// <param name="manager">区间管理器</param>
        public void SetRangeManager(ISequenceRangeManager manager)
        {
            _sequenceRangeManager = manager;
        }

        /// <summary>
        /// 设置序列号名称
        /// </summary>
        /// <param name="name">名称</param>
        public void SetName(string name)
        {
            _name = name;
        }
    }
}
