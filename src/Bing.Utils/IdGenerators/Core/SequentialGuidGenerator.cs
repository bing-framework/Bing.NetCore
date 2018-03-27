using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.IdGenerators.Abstractions;

namespace Bing.Utils.IdGenerators.Core
{
    /// <summary>
    /// 有序Guid 生成器
    /// 代码出自：https://github.com/jhtodd/SequentialGuid/blob/master/SequentialGuid/Classes/SequentialGuid.cs
    /// </summary>
    public class SequentialGuidGenerator:IGuidGenerator
    {
        public Guid Create()
        {
            throw new NotImplementedException();
        }
    }
}
