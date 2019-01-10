using System;
using Bing.Aspects;

namespace Bing.Datas.Matedatas
{
    /// <summary>
    /// 实体元数据
    /// </summary>
    [Ignore]
    public interface IEntityMatedata
    {
        /// <summary>
        /// 获取表名 
        /// </summary>
        /// <param name="type">实体类型</param>
        /// <returns></returns>
        string GetTable(Type type);

        /// <summary>
        /// 获取架构
        /// </summary>
        /// <param name="type">实体类型</param>
        /// <returns></returns>
        string GetSchema(Type type);

        /// <summary>
        /// 获取列名
        /// </summary>
        /// <param name="type">实体类型</param>
        /// <param name="property">属性名</param>
        /// <returns></returns>
        string GetColumn(Type type, string property);
    }
}
