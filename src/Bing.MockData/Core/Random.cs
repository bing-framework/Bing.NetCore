using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.MockData.Core
{
    /// <summary>
    /// 随机数操作
    /// </summary>
    internal class Random
    {
        #region 字段

        /// <summary>
        /// 随机数
        /// </summary>
        private readonly System.Random _random;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="Random"/>类型的实例
        /// </summary>
        public Random()
        {
            _random=new System.Random();
        }

        /// <summary>
        /// 初始化一个<see cref="Random"/>类型的实例
        /// </summary>
        /// <param name="seed">种子数</param>
        public Random(int seed)
        {
            _random=new System.Random(seed);
        }

        #endregion

        #region GetInt(获取指定范围的随机整数，该范围包括最小值，但不包括最大值)

        /// <summary>
        /// 获取指定范围的随机整数，该范围包括最小值，但不包括最大值
        /// </summary>
        /// <param name="minValue">最小值</param>
        /// <param name="maxValue">最大值</param>
        /// <returns></returns>
        public int GetInt(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }

        /// <summary>
        /// 获取随机整数
        /// </summary>
        /// <returns></returns>
        public int GetInt()
        {
            return _random.Next();
        }

        #endregion

        #region GetDouble(获取一个介于0.0和1.0之间的随机数)

        /// <summary>
        /// 获取一个介于0.0和1.0之间的随机数
        /// </summary>
        /// <returns></returns>
        public double GetDouble()
        {
            return _random.NextDouble();
        }

        #endregion
    }
}
