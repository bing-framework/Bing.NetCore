using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.MockData.Core
{
    /// <summary>
    /// 随机数生成器基类
    /// </summary>
    public abstract class RandomGeneratorBase:IRandomGenerator
    {
        /// <summary>
        /// 随机数
        /// </summary>
        private static Random _random=null;

        /// <summary>
        /// 重复数
        /// </summary>
        private static int _repeat = 0;

        /// <summary>
        /// 生成随机数
        /// </summary>
        /// <returns></returns>
        public abstract string Generate();

        /// <summary>
        /// 批量生产随机数
        /// </summary>
        /// <param name="maxLength">生成数量</param>
        /// <returns></returns>
        public abstract List<string> BatchGenerate(int maxLength);        

        /// <summary>
        /// 获取随机数
        /// </summary>
        /// <returns></returns>
        protected Random GetRandom()
        {
            if (_random == null)
            {
                long ticks = DateTime.Now.Ticks+_repeat;
                _repeat++;
                _random=new Random((int)((ulong)ticks & 0xffffffffL) | (int)(ticks >> _repeat));
            }

            return _random;
        }
    }
}
