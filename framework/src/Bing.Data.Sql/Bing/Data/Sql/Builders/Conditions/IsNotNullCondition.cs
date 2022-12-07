namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// Is Not Null查询条件
/// </summary>
public class IsNotNullCondition : ICondition
{
    /// <summary>
    /// 列名
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// 初始化一个<see cref="IsNotNullCondition"/>类型的实例
    /// </summary>
    /// <param name="name">列名</param>
    public IsNotNullCondition(string name) => _name = name;

    /// <summary>
    /// 获取查询条件
    /// </summary>
    public string GetCondition() => string.IsNullOrWhiteSpace(_name) ? null : $"{_name} Is Not Null";
}