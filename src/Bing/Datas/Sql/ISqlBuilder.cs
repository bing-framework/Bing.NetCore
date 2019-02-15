using System.Collections.Generic;
using Bing.Datas.Sql.Builders;
using Bing.Domains.Repositories;

namespace Bing.Datas.Sql
{
    /// <summary>
    /// Sql生成器
    /// </summary>
    public interface ISqlBuilder : ICondition, ISelect, IFrom, IJoin, IWhere, IGroupBy, IOrderBy, IUnion, ICte
    {
        /// <summary>
        /// 分页参数
        /// </summary>
        IPager Pager { get; }

        /// <summary>
        /// 克隆
        /// </summary>
        /// <returns></returns>
        ISqlBuilder Clone();

        /// <summary>
        /// 生成调试Sql语句，Sql语句中的参数被替换为参数值
        /// </summary>
        /// <returns></returns>
        string ToDebugSql();

        /// <summary>
        /// 生成Sql语句
        /// </summary>
        /// <returns></returns>
        string ToSql();

        /// <summary>
        /// 创建Sql生成器
        /// </summary>
        /// <returns></returns>
        ISqlBuilder New();

        /// <summary>
        /// 清空并初始化
        /// </summary>
        void Clear();

        /// <summary>
        /// 清空Select子句
        /// </summary>
        void ClearSelect();

        /// <summary>
        /// 清空From子句
        /// </summary>
        void ClearFrom();

        /// <summary>
        /// 清空Join子句
        /// </summary>
        void ClearJoin();

        /// <summary>
        /// 清空Where子句
        /// </summary>
        void ClearWhere();

        /// <summary>
        /// 清空GroupBy子句
        /// </summary>
        void ClearGroupBy();

        /// <summary>
        /// 清空OrderBy子句
        /// </summary>
        void ClearOrderBy();

        /// <summary>
        /// 清空Sql参数
        /// </summary>
        void ClearSqlParams();

        /// <summary>
        /// 清空分页参数
        /// </summary>
        void ClearPageParams();

        /// <summary>
        /// 清空联合操作项
        /// </summary>
        void ClearUnionBuilders();

        /// <summary>
        /// 清空公用表表达式
        /// </summary>
        void ClearCte();

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
