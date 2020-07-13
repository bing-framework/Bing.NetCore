namespace Bing.Numeric
{
    /// <summary>
    /// 数值操作
    /// </summary>
    public static class Numbers
    {
        /// <summary>
        /// 获取最小值和最大值之间的成员(包括最小值和最大值)
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static int[] GetMembersBetween(int min, int max)
        {
            if (min == max)
                return new[] { min };
            if (min > max)
            {
                var t = min;
                min = max;
                max = t;
            }
            var count = max - min + 1;
            var results = new int[count];
            var pointer = min;
            var index = 0;
            while (pointer <= max && index < count)
            {
                results[index] = pointer;
                ++index;
                ++pointer;
            }
            return results;
        }

        /// <summary>
        /// 获取最小值和最大值之间的成员(包括最小值和最大值)
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        public static long[] GetMembersBetween(long min, long max)
        {
            if (min == max)
                return new[] { min };
            if (min > max)
            {
                var t = min;
                min = max;
                max = t;
            }
            var count = max - min + 1;
            var results = new long[count];
            var pointer = min;
            var index = 0L;
            while (pointer <= max && index < count)
            {
                results[index] = pointer;
                ++index;
                ++pointer;
            }
            return results;
        }

        /// <summary>
        /// 是否NaN
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsNaN(double value) => value.IsNaN();

        /// <summary>
        /// 是否NaN
        /// </summary>
        /// <param name="value">值</param>
        public static bool IsNaN(float value) => value.IsNaN();
    }
}
