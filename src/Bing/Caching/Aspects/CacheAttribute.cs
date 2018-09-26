using System;

namespace Bing.Caching.Aspects
{
    /// <summary>
    /// 缓存 属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CacheAttribute:Attribute
    {
        /// <summary>
        /// 缓存过期时间，单位：秒，如果为0则表示永久缓存
        /// </summary>
        public int Expire { get; set; }

        /// <summary>
        /// 动态获取缓存过期时间的表达式，当其执行结果为null或小于0的整数时，会使用expire
        /// </summary>
        public string ExpireExpression { get; set; } = string.Empty;

        /// <summary>
        /// 预警自动刷新时间（单位：秒），必须满足 0 &lt; alarmTime &lt; expire才有效 当缓存在alarmTime时间内即将过期的的话，会自动刷新缓存内容
        /// </summary>
        public int AlarmTime { get; set; } = 0;

        /// <summary>
        /// 自定义缓存键，支持表达式
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 设置哈希表中的字段，如果设置此项，则用哈希表进行存储，支持表达式
        /// </summary>
        public string HashField { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用自动加载缓存，缓存时间必须大于120秒时才有效
        /// </summary>
        public bool AutoLoad { get; set; } = false;

        /// <summary>
        /// 自动缓存的条件，可以为空，返回 true 或者 false，如果设置了此值，AutoLoad 就失效，例如：null != #args[0].keyword，当第一个参数的keyword属性为null时设置为自动加载
        /// </summary>
        public string AutoloadCondition { get; set; } = string.Empty;

        /// <summary>
        /// 当AutoLoad为true时，缓存数据在 requestTimeout 秒之内没有使用了，就不进行自动加载数据，如果requestTimeout为0时，就一直自动加载
        /// </summary>
        public long RequestTimeout { get; set; } = 36000;

        /// <summary>
        /// 缓存的条件表达式，可以为空，返回 true 或者 false，只有为 true 才进行缓存
        /// </summary>
        public string Codition { get; set; } = string.Empty;

        /// <summary>
        /// 缓存操作类型：默认是ReadWrite，先缓存取数据，如果没有数据从仓储中获取并写入缓存，如果是Write则从仓储去完数据后，写入缓存
        /// </summary>
        public CacheOperation Operation { get; set; } = CacheOperation.ReadWrite;

        /// <summary>
        /// 并发等待时间（单位：毫秒），等待正在仓储中加载数据的线程返回的等待时间
        /// </summary>
        public int WaitTimeout { get; set; } = 500;

        /// <summary>
        /// 分布式锁的缓存时间（单位：秒），在设置分布式锁的前提下，如果此项值大于0，则使用分布式锁，如果小于等于0，则不会使用分布式锁
        /// </summary>
        public int LocakExpire { get; set; } = 10;

        /// <summary>
        /// 是否打开对参数进行深度复制，默认是true，是为了避免外部改变参数值。如果确保不被修改，最好是设置为false，这样性能会更高
        /// </summary>
        public bool ArgumentDeepCloneEnable { get; set; } = true;
    }
}
