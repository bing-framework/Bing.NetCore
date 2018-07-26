using System;
using System.Threading;

namespace Bing.Sequence.Internal
{
    /// <summary>
    /// 原子长整型
    /// </summary>
    internal class AtomicLong
    {
        /// <summary>
        /// 值
        /// </summary>
        private long _value;

        /// <summary>
        /// 初始化一个<see cref="AtomicLong"/>类型的实例
        /// </summary>
        public AtomicLong() : this(0) { }

        /// <summary>
        /// 初始化一个<see cref="AtomicLong"/>类型的实例
        /// </summary>
        /// <param name="value">初始值</param>
        public AtomicLong(long value)
        {
            _value = value;
        }

        /// <summary>
        /// 获取并添加指定值。先增加，后获取
        /// </summary>
        /// <param name="delta">递增值</param>
        /// <returns></returns>
        public long AddAndGet(long delta)
        {
            for (;;)
            {
                long current = Get();
                long next = current + delta;
                if (CompareAndSet(current, next))
                {
                    return next;
                }
            }
        }

        /// <summary>
        /// 比较并设置
        /// </summary>
        /// <param name="expect">目标值</param>
        /// <param name="update">更新值</param>
        /// <returns></returns>
        public bool CompareAndSet(long expect, long update)
        {
            return Interlocked.CompareExchange(ref _value, update, expect) == expect;
        }

        /// <summary>
        /// 获取并减少。先减少，后获取
        /// </summary>
        /// <returns></returns>
        public long DecrementAndGet()
        {
            for (; ; )
            {
                long current = Get();
                long next = current -1;
                if (CompareAndSet(current, next))
                {
                    return next;
                }
            }
        }

        /// <summary>
        /// 获取当前值
        /// </summary>
        /// <returns></returns>
        public long Get()
        {
            return Interlocked.Read(ref _value);
        }

        /// <summary>
        /// 获取并添加指定值。先获取，后增加
        /// </summary>
        /// <param name="delta">递增值</param>
        /// <returns></returns>
        public long GetAndAdd(long delta)
        {
            for (;;)
            {
                long current = Get();
                long next = current + delta;
                if (CompareAndSet(current, next))
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// 获取并减少。先获取，后减少
        /// </summary>
        /// <returns></returns>
        public long GetAndDecrement()
        {
            for (; ; )
            {
                long current = Get();
                long next = current - 1;
                if (CompareAndSet(current, next))
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// 获取并增加。先获取，后增加
        /// </summary>
        /// <returns></returns>
        public long GetAndIncrement()
        {
            return GetAndIncrement(1);
        }
        /// <summary>
        /// 获取并添加指定值。先获取，后增加
        /// </summary>
        /// <param name="delta">递增值</param>
        /// <returns></returns>
        public long GetAndIncrement(long delta)
        {
            for (; ; )
            {
                long current = Get();
                long next = current + delta;
                if (CompareAndSet(current, next))
                {
                    return current;
                }
            }
        }

        /// <summary>
        /// 获取并设置值，获取原有值，设置新值
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public long GetAndSet(long value)
        {
            return Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// 设置当前值
        /// </summary>
        /// <param name="value">值</param>
        public void Set(long value)
        {
            Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// 重写转换字符串方法
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Convert.ToString(Get());
        }

        /// <summary>
        /// 重写隐式转换，允许AtomicLong到long的转换
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator long(AtomicLong value)
        {
            return value.Get();
        }
    }
}
