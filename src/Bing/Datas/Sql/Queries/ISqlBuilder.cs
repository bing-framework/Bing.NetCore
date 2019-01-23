using System.Collections.Generic;
using Bing.Datas.Sql.Queries.Builders.Abstractions;
using Bing.Datas.Sql.Queries.Builders.Operations;
using Bing.Domains.Repositories;

namespace Bing.Datas.Sql.Queries
{
    /// <summary>
    /// Sql生成器
    /// </summary>
    public interface ISqlBuilder : ICondition, 
        ISelect, IFrom, 
        IJoin, ILeftJoin, IRightJoin, 
        ISplice, IWhere, IWhereLambda,
        IGroupBy, IOrderBy,
        IClear, IGetSql,
        IAggregateFunc
    {
        /// <summary>
        /// 分页
        /// </summary>
        IPager Pager { get; }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        ISqlBuilder Clone();

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder New();

        /// <summary>
        /// 添加Sql参数
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        void AddParam(string name, object value);

        /// <summary>
        /// 获取参数列表
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<string, object> GetParams();        

        /// <summary>
        /// 设置分页
        /// </summary>
        /// <param name="pager">分页参数</param>
        /// <returns></returns>
        ISqlBuilder Page(IPager pager);

        /// <summary>
        /// 设置跳过行数
        /// </summary>
        /// <param name="count">跳过的行数</param>
        /// <returns></returns>
        ISqlBuilder Skip(int count);

        /// <summary>
        /// 设置获取行数
        /// </summary>
        /// <param name="count">获取的行数</param>
        /// <returns></returns>
        ISqlBuilder Take(int count);
    }
}
