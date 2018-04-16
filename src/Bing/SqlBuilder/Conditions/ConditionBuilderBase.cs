using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Extensions;

namespace Bing.SqlBuilder.Conditions
{
    /// <summary>
    /// 条件生成器基类
    /// </summary>
    public abstract class ConditionBuilderBase : IConditionBuilder
    {
        #region 字段

        /// <summary>
        /// 参数前缀
        /// </summary>
        protected string ParameterPrefix = "@";

        /// <summary>
        /// 参数键
        /// </summary>
        protected string ParameterKey = "P";

        /// <summary>
        /// 条件拼接器
        /// </summary>
        protected StringBuilder ConditionAppendBuilder = new StringBuilder();

        /// <summary>
        /// 是否排除空或null值，true:排除字段为空或null时则不作为查询条件
        /// </summary>
        protected bool IsExcludeEmpty = true;

        /// <summary>
        /// 是否生成参数化Sql语句
        /// </summary>
        protected bool IsBuildParameterSql = true;

        /// <summary>
        /// 参数长度
        /// </summary>
        public int Length { get; protected set; }

        /// <summary>
        /// 参数字典，记录参数信息（参数名-参数值）
        /// </summary>
        protected Dictionary<string, object> ParamDictionary = new Dictionary<string, object>();

        /// <summary>
        /// 参数上下文
        /// </summary>
        internal ParamContext ParamContext { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ConditionBuilderBase"/>类型的实例
        /// </summary>
        /// <param name="isExcludeEmpty">是否排除空或null值</param>
        /// <param name="parameterKey">参数键</param>
        /// <param name="isBuildParameterSql">否生成参数化Sql语句</param>
        protected ConditionBuilderBase(bool isExcludeEmpty = true, string parameterKey = "P", bool isBuildParameterSql = true)
        {
            this.IsExcludeEmpty = isExcludeEmpty;
            this.ParameterKey = parameterKey;
            this.IsBuildParameterSql = isBuildParameterSql;
            this.Length = 0;
            this.ConditionAppendBuilder.Append(" WHERE ");
            this.ParamContext = new ParamContext();
        }

        #endregion

        #region Clear(清空条件)

        /// <summary>
        /// 清空条件
        /// </summary>
        /// <returns></returns>
        public virtual IConditionBuilder Clear()
        {
            this.Length = 0;
            ParamDictionary.Clear();
            ConditionAppendBuilder.Clear().Append(" Where ");
            return this;
        }

        #endregion

        #region Append(添加条件)

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="operator">操作符</param>
        /// <param name="conditionDict">条件字典，例如：A.Name 1</param>
        /// <returns></returns>
        public IConditionBuilder Append(SqlOperator @operator, Dictionary<string, object> conditionDict)
        {
            return Append(RelationType.And, @operator, conditionDict);
        }

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="operator">操作符</param>
        /// <param name="conditionDict">条件字典，例如：A.Name 1</param>
        /// <returns></returns>
        public IConditionBuilder Append(RelationType relationType, SqlOperator @operator, Dictionary<string, object> conditionDict)
        {
            foreach (var item in conditionDict)
            {
                Append(relationType, item.Key, @operator, item.Value);
            }
            return this;
        }

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="relationType">关联运算符</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="operator">操作符</param>
        /// <param name="fieldValue">字段值，注：1、不可谓数组;2、Between时，此字段必须填两个值</param>
        /// <returns></returns>
        public virtual IConditionBuilder Append<T>(RelationType relationType, string fieldName, SqlOperator @operator,
            params T[] fieldValue)
        {
            if (IsContinue(fieldValue))
            {
                return this;
            }
            if (!this.ConditionAppendBuilder.ToString().Trim()
                .EndsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
            {
                this.ConditionAppendBuilder.Append(GetRelation(relationType));
            }
            if (IsContinue(fieldValue))
            {
                return this;
            }
            switch (@operator)
            {
                case SqlOperator.Equal:
                case SqlOperator.NotEqual:
                case SqlOperator.GreaterThan:
                case SqlOperator.GreaterEqual:
                case SqlOperator.LessThan:
                case SqlOperator.LessEqual:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}{2}", GetFieldName(fieldName),
                        GetOperator(@operator), GetFieldValue(fieldValue[0]));
                    break;
                case SqlOperator.IsNull:
                case SqlOperator.IsNotNull:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}", GetFieldName(fieldName), GetOperator(@operator));
                    break;
                case SqlOperator.Contains:
                case SqlOperator.NotContains:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}{2}", GetFieldName(fieldName),
                        GetOperator(@operator), GetFieldValue(string.Format("%{0}%", fieldValue[0])));
                    break;
                case SqlOperator.In:
                case SqlOperator.NotIn:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}({2})", GetFieldName(fieldName),
                        GetOperator(@operator), GetArrayFieldValue(fieldValue));
                    break;
                case SqlOperator.Starts:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}{2}", GetFieldName(fieldName),
                        GetOperator(@operator), GetFieldValue(string.Format("{0}%", fieldValue[0])));
                    break;
                case SqlOperator.Ends:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}{2}", GetFieldName(fieldName),
                        GetOperator(@operator), GetFieldValue(string.Format("%{0}", fieldValue[0])));
                    break;
                case SqlOperator.Between:
                    this.ConditionAppendBuilder.AppendFormat("{0}{1}{2} AND {3}", GetFieldName(fieldName),
                        GetOperator(@operator), GetFieldValue(fieldValue[0]), GetFieldValue(fieldValue[1]));
                    break;
                default:
                    throw new SqlBuilderException("条件未定义!");
            }
            this.Length++;
            return this;
        }

        /// <summary>
        /// 添加条件
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="fieldName">字段名</param>
        /// <param name="operator">操作符</param>
        /// <param name="fieldValue">字段值，注：1、不可谓数组;2、Between时，此字段必须填两个值</param>
        /// <returns></returns>
        public virtual IConditionBuilder Append<T>(string fieldName, SqlOperator @operator, params T[] fieldValue)
        {
            return Append<T>(RelationType.And, fieldName, @operator, fieldValue);
        }

        /// <summary>
        /// 添加Sql语句条件，允许你写任何不支持上面的方法，所有它会给你最大的灵活性
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public IConditionBuilder AppendRaw(RelationType relationType, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return this;
            }
            if (sql.Trim().StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
            {
                return this;
            }
            if (!this.ConditionAppendBuilder.ToString().Trim()
                .EndsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
            {
                this.ConditionAppendBuilder.Append(GetRelation(relationType));
            }
            this.ConditionAppendBuilder.AppendFormat(" ({0}) ", sql);
            return this;
        }

        /// <summary>
        /// 添加Sql语句条件，允许你写任何不支持上面的方法，所有它会给你最大的灵活性
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <returns></returns>
        public virtual IConditionBuilder AppendRaw(string sql, params object[] param)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return this;
            }
            if (sql.Trim().StartsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
            {
                return this;
            }
            return AppendRaw(RelationType.And, string.Format(sql, param));
        }
        #endregion

        #region Block(添加含有括号的条件)

        /// <summary>
        /// 添加含有括号的条件
        /// </summary>
        /// <param name="relationType">关联运算符</param>
        /// <param name="condition">条件生成器</param>
        /// <returns></returns>
        public virtual IConditionBuilder Block(RelationType relationType, IConditionBuilder condition)
        {
            if (condition == null)
            {
                return this;
            }
            var conditionBuilder = condition as ConditionBuilder;
            if (conditionBuilder == null || conditionBuilder.ConditionAppendBuilder.ToString().Trim().Length < 6)
            {
                return this;
            }
            if (!ConditionAppendBuilder.ToString().Trim()
                .EndsWith("WHERE", StringComparison.InvariantCultureIgnoreCase))
            {
                this.ConditionAppendBuilder.Append(GetRelation(relationType));
            }
            this.ConditionAppendBuilder.AppendFormat(" ({0}) ",
                conditionBuilder.ConditionAppendBuilder.ToString().Remove(0, 6));
            if (conditionBuilder.ParamDictionary.Count > 0)
            {
                this.ParamDictionary.AddRange(conditionBuilder.ParamDictionary, true);
            }
            this.Length++;
            return this;
        }

        #endregion

        /// <summary>
        /// 克隆对象
        /// </summary>
        /// <returns></returns>
        public abstract IConditionBuilder Clone();

        /// <summary>
        /// 是否跳过此拼接条件
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="fieldValue">字段值</param>
        /// <returns>true:跳过,false:不跳过</returns>
        protected bool IsContinue<T>(params T[] fieldValue)
        {
            // 如果选择IsExcludeEmpty为true，并且该字段为空值的话则跳过
            if (IsExcludeEmpty)
            {
                return fieldValue == null || fieldValue.Length == 0 ||
                       (string.IsNullOrWhiteSpace(fieldValue[0] + "") || typeof(T) == typeof(Guid) &&
                        fieldValue[0].ToString().ToGuid().IsEmpty());
            }
            else
            {
                return fieldValue == null || fieldValue.Length == 0;
            }
        }

        /// <summary>
        /// 获取字段名
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <returns></returns>
        protected string GetFieldName(string fieldName)
        {
            return string.Format("{0}", fieldName);
        }

        /// <summary>
        /// 获取字段值
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="fieldValue">字段值</param>
        /// <returns></returns>
        protected string GetFieldValue<T>(params T[] fieldValue)
        {
            if (IsBuildParameterSql)
            {
                List<string> parameterNameList = new List<string>();
                foreach (var value in fieldValue)
                {
                    parameterNameList.Add(AddParameter(value));
                }
                return string.Join(",", parameterNameList);
            }
            else
            {
                return string.Format("'{0}'", string.Join("','", fieldValue));
            }
        }

        /// <summary>
        /// 获取数组字段值
        /// </summary>
        /// <typeparam name="T">字段值类型</typeparam>
        /// <param name="fieldValue">字段值</param>
        /// <returns></returns>
        protected string GetArrayFieldValue<T>(params T[] fieldValue)
        {
            return string.Format("'{0}'", string.Join("','", fieldValue));
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="fieldValue">字段值</param>
        /// <returns></returns>
        protected string AddParameter(object fieldValue)
        {
            ParamContext.Index++;
            string parameterName = string.Format("{0}{1}{2}", ParameterPrefix, ParameterKey, ParamContext.Index);
            this.ParamDictionary.Add(parameterName, fieldValue);
            return parameterName;
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="fieldName">字段名</param>
        /// <param name="fieldValue">字段值</param>
        /// <returns></returns>
        public string AddParameter(string fieldName, object fieldValue)
        {
            fieldName = fieldName.TrimStart(ParameterPrefix[0]);
            string parameterName = string.Format("{0}{1}", ParameterPrefix, fieldName);
            this.ParamDictionary.Add(parameterName, fieldValue);
            return parameterName;
        }

        /// <summary>
        /// 获取参数字典
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object> GetParamDict()
        {
            return this.ParamDictionary;
        }

        /// <summary>
        /// 获取关联类型字符串
        /// </summary>
        /// <param name="relationType">关联类型</param>
        /// <returns></returns>
        protected static string GetRelation(RelationType relationType)
        {
            string result;
            switch (relationType)
            {
                case RelationType.And:
                    result = " AND ";
                    break;
                case RelationType.Or:
                    result = " OR ";
                    break;
                default:
                    throw new SqlBuilderException("关联类型未定义!");
            }
            return result;
        }

        /// <summary>
        /// 获取操作符字符串
        /// </summary>
        /// <param name="operator">操作符类型</param>
        /// <returns></returns>
        protected static string GetOperator(SqlOperator @operator)
        {
            string result;
            switch (@operator)
            {
                case SqlOperator.Equal:
                    result = " = ";
                    break;
                case SqlOperator.NotEqual:
                    result = " <> ";
                    break;
                case SqlOperator.GreaterThan:
                    result = " > ";
                    break;
                case SqlOperator.GreaterEqual:
                    result = " >= ";
                    break;
                case SqlOperator.LessThan:
                    result = " < ";
                    break;
                case SqlOperator.LessEqual:
                    result = " <= ";
                    break;
                case SqlOperator.Contains:
                case SqlOperator.Starts:
                case SqlOperator.Ends:
                    result = " LIKE ";
                    break;
                case SqlOperator.NotContains:
                    result = " NOT LIKE ";
                    break;
                case SqlOperator.IsNull:
                    result = " IS NULL ";
                    break;
                case SqlOperator.IsNotNull:
                    result = " IS NOT NULL ";
                    break;
                case SqlOperator.In:
                    result = " IN ";
                    break;
                case SqlOperator.NotIn:
                    result = " NOT IN ";
                    break;
                case SqlOperator.Between:
                    result = " BETWEEN ";
                    break;
                default:
                    throw new SqlBuilderException("条件未定义!");
            }
            return result;
        }

        /// <summary>
        /// 获取 ConditionBuilder 中Sql条件语句（包含Where关键字）
        /// </summary>
        /// <param name="condition">条件生成器</param>
        /// <returns>若condition为null时，返回空字符串</returns>
        public static string ToString(IConditionBuilder condition)
        {
            return condition?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 获取 ConditionBuilder 中Sql参数字典
        /// </summary>
        /// <param name="conditionBuilder">条件生成器</param>
        /// <returns></returns>
        public static Dictionary<string, object> GetParamDict(IConditionBuilder conditionBuilder)
        {
            var condition = conditionBuilder as ConditionBuilder;
            return condition?.GetParamDict();
        }
    }
}
