using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.MockData
{
    /// <summary>
    /// 随机数生成器
    /// </summary>
    public interface IRandomGenerator
    {
        /// <summary>
        /// 生成随机数
        /// </summary>
        /// <returns></returns>
        string Generate();
    }
}
