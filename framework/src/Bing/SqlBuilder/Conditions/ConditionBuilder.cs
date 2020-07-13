namespace Bing.SqlBuilder.Conditions
{
    /// <summary>
    /// 条件生成器
    /// </summary>
    public class ConditionBuilder : ConditionBuilderBase
    {
        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="ConditionBuilder"/>类型的实例
        /// </summary>
        /// <param name="isExcludeEmpty">是否排除空或null值</param>
        /// <param name="parameterKey">参数键</param>
        /// <param name="isBuildParameterSql">否生成参数化Sql语句</param>
        public ConditionBuilder(bool isExcludeEmpty = true, string parameterKey = "P", bool isBuildParameterSql = true) : base(isExcludeEmpty, parameterKey, isBuildParameterSql)
        {
        }

        #endregion

        #region Clone(克隆对象)

        /// <summary>
        /// 克隆对象
        /// </summary>
        /// <returns></returns>
        public override IConditionBuilder Clone()
        {
            return new ConditionBuilder(IsExcludeEmpty, ParameterKey, IsBuildParameterSql)
            {
                ParamContext = this.ParamContext
            };
        }

        #endregion

        /// <summary>
        /// 转化成Sql条件语句（包含Where关键字）
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (this.ConditionAppendBuilder.Length < 8)
            {
                return string.Empty;
            }
            return this.ConditionAppendBuilder.ToString();
        }
    }
}
