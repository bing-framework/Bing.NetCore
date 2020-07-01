using System;
using System.Reflection;

namespace Bing
{
    /// <summary>
    /// 随机数(<see cref="Random"/>) 扩展
    /// </summary>
    public static class RandomExtensions
    {
        #region NextBool(随机返回true或false)

        /// <summary>
        /// 随机返回true或false。范围：[true,false]
        /// </summary>
        /// <param name="random">随机数</param>
        public static bool NextBool(this Random random) => random.Next() % 2 == 1;

        /// <summary>
        /// 随机返回true或false。范围：[true,false]
        /// </summary>
        /// <param name="random">随机数</param>
        /// <param name="probability">true的概率。范围：[0.0,1.0]</param>
        public static bool NextBool(this Random random, double probability) => random.NextDouble() < probability;

        #endregion

        #region NextEnum(随机返回一个指定的枚举对象的成员)

        /// <summary>
        /// 随机返回一个指定的枚举对象的成员
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="random">随机数</param>
        public static T NextEnum<T>(this Random random) where T : struct
        {
            var type = typeof(T);
            if (!type.GetTypeInfo().IsEnum)
                throw new InvalidOperationException();
            var array = Enum.GetValues(type);
            var index = random.Next(array.GetLowerBound(0), array.GetUpperBound(0) + 1);
            return (T)array.GetValue(index);
        }

        #endregion

        #region NextBytes(用随机数填充指定字节数组的元素)

        /// <summary>
        /// 用随机数填充指定字节数组的元素
        /// </summary>
        /// <param name="random">随机数</param>
        /// <param name="length">字节长度</param>
        public static byte[] NextBytes(this Random random, int length)
        {
            var data = new byte[length];
            random.NextBytes(data);
            return data;
        }

        #endregion

        #region NextUInt16(随机返回一个无符号八位整数)

        /// <summary>
        /// 随机返回一个无符号八位整数
        /// </summary>
        /// <param name="random">随机数</param>
        public static ushort NextUInt16(this Random random) => BitConverter.ToUInt16(random.NextBytes(2), 0);

        #endregion

        #region NextInt16(随机返回一个有符号十六位整数)

        /// <summary>
        /// 随机返回一个有符号十六位整数
        /// </summary>
        /// <param name="random">随机数</param>
        public static short NextInt16(this Random random) => BitConverter.ToInt16(random.NextBytes(2), 0);

        #endregion

        #region NextFloat(随机返回一个单精度浮点数)

        /// <summary>
        /// 随机返回一个单精度浮点数
        /// </summary>
        /// <param name="random">随机数</param>
        public static float NextFloat(this Random random) => BitConverter.ToSingle(random.NextBytes(4), 0);

        /// <summary>
        /// 随机返回一个单精度浮点数。范围：[0.0,max]
        /// </summary>
        /// <param name="random">随机数</param>
        /// <param name="max">最大值</param>
        public static float NextFloat(this Random random, float max) => (float)(random.NextDouble() * max);

        /// <summary>
        /// 随机返回一个单精度浮点数。范围：[min,max]
        /// </summary>
        /// <param name="random">随机数</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static float NextFloat(this Random random, float min, float max) =>
            (float)(random.NextDouble() * (max - min) + min);

        #endregion

        #region NextDateTime(随机返回一个时间)

        /// <summary>
        /// 随机返回一个时间
        /// </summary>
        /// <param name="random">随机数</param>
        public static DateTime NextDateTime(this Random random) =>
            NextDateTime(random, DateTime.MinValue, DateTime.MaxValue);

        /// <summary>
        /// 在指定范围内随机返回一个时间
        /// </summary>
        /// <param name="random">随机数</param>
        /// <param name="minValue">时间起始</param>
        /// <param name="maxValue">时间截止</param>
        public static DateTime NextDateTime(this Random random, DateTime minValue, DateTime maxValue) =>
            new DateTime(minValue.Ticks + (long)((maxValue.Ticks - minValue.Ticks) * random.NextDouble()));

        #endregion

        #region OneOf(随机获得一个指定范围的结果)

        /// <summary>
        /// 随机获得一个指定范围的结果
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="random">随机数</param>
        /// <param name="values">结果集合</param>
        public static T OneOf<T>(this Random random, params T[] values) => values[random.Next(values.Length)];

        #endregion
    }
}
