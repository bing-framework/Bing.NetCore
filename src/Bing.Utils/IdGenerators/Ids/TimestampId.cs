using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bing.Utils.Extensions;

namespace Bing.Utils.IdGenerators.Ids
{
    /// <summary>
    /// 时间戳ID，借鉴雪花算法，生成唯一时间戳ID
    /// 参考文章：http://www.cnblogs.com/rjf1979/p/6282855.html
    /// </summary>
    public class TimestampId
    {
        private long _lastTimestamp;
        private long _sequence;//计数从零开始
        private readonly DateTime? _initialDateTime;
        private static TimestampId _timestampId;
        private const int MAX_END_NUMBER = 9999;

        private TimestampId(DateTime? initialDateTime)
        {
            _initialDateTime = initialDateTime;
        }

        /// <summary>
        /// 获取单个实例对象
        /// </summary>
        /// <param name="initialDateTime">初始化时间，与当前时间做一个相差取时间戳</param>
        /// <returns></returns>
        public static TimestampId GetInstance(DateTime? initialDateTime = null)
        {
            if (initialDateTime.IsNull())
            {
                Interlocked.CompareExchange(ref _timestampId, new TimestampId(initialDateTime), null);
            }
            return _timestampId;
        }

        /// <summary>
        /// 初始化时间，作用时间戳的相差
        /// </summary>
        protected DateTime InitialDateTime
        {
            get
            {
                if (_initialDateTime == null || _initialDateTime.Value == DateTime.MinValue)
                {
                    return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                }
                return _initialDateTime.Value;
            }
        }

        /// <summary>
        /// 获取唯一时间戳ID
        /// </summary>
        /// <returns></returns>
        public string GetId()
        {
            long temp;
            var timestamp = GetUniqueTimeStamp(_lastTimestamp, out temp);
            return $"{timestamp}{Fill(temp)}";
        }

        /// <summary>
        /// 补位填充
        /// </summary>
        /// <param name="temp">数字</param>
        /// <returns></returns>
        private string Fill(long temp)
        {
            var num = temp.ToString();
            IList<char> chars = new List<char>();
            for (int i = 0; i < MAX_END_NUMBER.ToString().Length - num.Length; i++)
            {
                chars.Add('0');
            }
            return new string(chars.ToArray()) + num;
        }

        /// <summary>
        /// 获取唯一时间戳
        /// </summary>
        /// <param name="lastTimeStamp">最后时间戳</param>
        /// <param name="temp">临时时间戳</param>
        /// <returns></returns>
        public long GetUniqueTimeStamp(long lastTimeStamp, out long temp)
        {
            lock (this)
            {
                temp = 1;
                var timeStamp = GetTimeStamp();
                if (timeStamp == _lastTimestamp)
                {
                    _sequence = _sequence + 1;
                    temp = _sequence;
                    if (temp >= MAX_END_NUMBER)
                    {
                        timeStamp = GetTimeStamp();
                        _lastTimestamp = timeStamp;
                        temp = _sequence = 1;
                    }
                }
                else
                {
                    _sequence = 1;
                    _lastTimestamp = timeStamp;
                }
                return timeStamp;
            }
        }

        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <returns></returns>
        private long GetTimeStamp()
        {
            if (InitialDateTime >= DateTime.Now)
            {
                throw new Exception("初始化时间比当前时间还大，不合理");
            }
            var ts = DateTime.UtcNow - InitialDateTime;
            return (long)ts.TotalMilliseconds;
        }
    }
}
