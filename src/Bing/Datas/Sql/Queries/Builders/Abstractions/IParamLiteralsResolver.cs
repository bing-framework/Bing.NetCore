namespace Bing.Datas.Sql.Queries.Builders.Abstractions
{
    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    public interface IParamLiteralsResolver
    {
        /// <summary>
        /// 获取参数字面值
        /// </summary>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        string GetParamLiterals(object value);
    }
}
