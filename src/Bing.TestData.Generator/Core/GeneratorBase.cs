using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.TestData.Generator.Core
{
    /// <summary>
    /// 生成器基类
    /// </summary>
    public abstract class GeneratorBase
    {
        /// <summary>
        /// 随机数
        /// </summary>
        private static Random _random = null;

        /// <summary>
        /// 生成数据
        /// </summary>
        /// <returns></returns>
        public abstract string Generate();

        /// <summary>
        /// 获取随机数实例
        /// </summary>
        /// <returns></returns>
        protected Random GetRandomInstance()
        {
            if (_random == null)
            {
                _random=new Random();
            }

            return _random;
        }
    }
}
