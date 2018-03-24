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
        /// 随机数生成器
        /// </summary>
        protected RandomBuilder Builder { get; set; } = new RandomBuilder();

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
    }
}
