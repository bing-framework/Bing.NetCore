namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Sql查询条件
/// </summary>
public class SqlCondition : ICondition
{
    /// <summary>
    /// Sql查询条件
    /// </summary>
    private readonly string _condition;

    /// <summary>
    /// 初始化一个<see cref="SqlCondition"/>类型的实例
    /// </summary>
    /// <param name="condition">查询条件</param>
    public SqlCondition(string condition) => _condition = condition;

    /// <summary>
    ///  获取查询条件
    /// </summary>
    public string GetCondition() => _condition;
}