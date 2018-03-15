using System;
using System.Collections.Generic;
using System.Text;
using Bing.Domains.Entities;

namespace Bing.Datas.EntityFramework.Internal
{
    /// <summary>
    /// 数据上下文包装器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    internal class DbContextWrapper<TEntity,TKey> where TEntity:class,IKey<TKey>,IVersion
    {
        #region 属性        

        #endregion
    }
}
