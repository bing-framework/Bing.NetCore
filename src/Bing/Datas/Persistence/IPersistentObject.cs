using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Dependency;
using Bing.Domains.Entities;

namespace Bing.Datas.Persistence
{
    /// <summary>
    /// 持久化对象
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public interface IPersistentObject<out TKey> : IKey<TKey>, IScopeDependency, IVersion
    {
    }
}
