namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// And连接条件
/// </summary>
public class AndCondition : ICondition
{
    /// <summary>
    /// 左操作数
    /// </summary>
    private readonly string _left;

    /// <summary>
    /// 右操作数
    /// </summary>
    private readonly string _right;

    /// <summary>
    /// 初始化一个<see cref="AndCondition"/>类型的实例
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public AndCondition(string left, string right)
    {
        _left = left;
        _right = right;
    }

    /// <summary>
    /// 初始化一个<see cref="AndCondition"/>类型的实例
    /// </summary>
    /// <param name="left">左操作数</param>
    /// <param name="right">右操作数</param>
    public AndCondition(ICondition left, ICondition right)
    {
        _left = left?.GetCondition();
        _right = right?.GetCondition();
    }

    /// <summary>
    /// 获取查询条件
    /// </summary>
    /// <returns>返回由非空左右条件使用 <c>And</c> 连接形成的 SQL 条件；一侧为空时返回另一侧。</returns>
    public string GetCondition()
    {
        if (string.IsNullOrWhiteSpace(_left))
            return _right;
        if (string.IsNullOrWhiteSpace(_right))
            return _left;
        return $"{_left} And {_right}";
    }
}