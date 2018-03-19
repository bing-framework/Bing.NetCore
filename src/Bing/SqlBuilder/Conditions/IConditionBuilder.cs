using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.SqlBuilder.Conditions
{
    /// <summary>
    /// 条件生成器
    /// </summary>
    public interface IConditionBuilder
    {
        /// <summary>
        /// 清空条件
        /// </summary>
        /// <returns></returns>
        IConditionBuilder Clear();

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="operator">操作符</param>
        /// <param name="conditionDict">条件字典，例如：A.Name 1</param>
        /// <returns></returns>
        IConditionBuilder Append(SqlOperator @operator, Dictionary<string, object> conditionDict);

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="operator">操作符</param>
        /// <param name="conditionDict">条件字典，例如：A.Name 1</param>
        /// <returns></returns>
        IConditionBuilder Append(RelationType relationType, SqlOperator @operator, Dictionary<string, object> conditionDict);

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="relationType">关联运算符</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="operator">操作符</param>
        /// <param name="fieldValue">字段值，注：1、不可谓数组;2、Between时，此字段必须填两个值</param>
        /// <returns></returns>
        IConditionBuilder Append<T>(RelationType relationType, string fieldName, SqlOperator @operator,
            params T[] fieldValue);

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="fieldName">字段名</param>
        /// <param name="operator">操作符</param>
        /// <param name="fieldValue">字段值，注：1、不可谓数组;2、Between时，此字段必须填两个值</param>
        /// <returns></returns>
        IConditionBuilder Append<T>(string fieldName, SqlOperator @operator, params T[] fieldValue);

        /// <summary>
        /// 添加Sql语句条件，允许你写任何不支持上面的方法，所有它会给你最大的灵活性
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        IConditionBuilder AppendRaw(RelationType relationType, string sql);

        /// <summary>
        /// 添加Sql语句条件，默认And连接，允许你写任何不支持上面的方法，所有它会给你最大的灵活性
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        IConditionBuilder AppendRaw(string sql, params object[] param);

        /// <summary>
        /// 添加含有括号的条件
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="condition">条件生成器</param>
        /// <returns></returns>
        IConditionBuilder Block(RelationType relationType, IConditionBuilder condition);

        /// <summary>
        /// 克隆对象
        /// </summary>
        /// <returns></returns>
        IConditionBuilder Clone();
    }
}
