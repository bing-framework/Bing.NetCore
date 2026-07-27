namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// SQL Builder 参数管理器。
/// </summary>
public interface IParameterManager
{
    /// <summary>
    /// 创建尚未使用的标准参数名称。
    /// </summary>
    /// <returns>可直接用于添加参数的唯一标准名称。</returns>
    string GenerateName();

    /// <summary>
    /// 将参数名称规范化为内部比较使用的标准形式。
    /// </summary>
    /// <param name="name">可能包含 Provider 前缀的参数名称。</param>
    /// <returns>去除 Provider 前缀并完成标准化的参数名称。</returns>
    string NormalizeName(string name);

    /// <summary>
    /// 添加参数；同名参数已存在时替换其值。
    /// </summary>
    /// <param name="name">参数名称。</param>
    /// <param name="value">参数值，可为 null。</param>
    /// <param name="operator">参数关联的条件运算符。</param>
    void Add(string name, object value, Operator? @operator = null);

    /// <summary>
    /// 获取当前参数的只读快照。
    /// </summary>
    /// <returns>以标准参数名称为键的参数值集合。</returns>
    IReadOnlyDictionary<string, object> GetParams();

    /// <summary>
    /// 判断是否存在指定参数。
    /// </summary>
    /// <param name="name">待查找的参数名称。</param>
    /// <returns>存在同名参数时返回 true；否则返回 false。</returns>
    bool Contains(string name);

    /// <summary>
    /// 获取指定参数的值。
    /// </summary>
    /// <param name="name">待查找的参数名称。</param>
    /// <returns>已保存的参数值；参数值本身可以为 null。</returns>
    object GetValue(string name);

    /// <summary>
    /// 克隆当前参数管理器。
    /// </summary>
    /// <returns>保留参数和值但不与当前实例共享可变状态的副本。</returns>
    IParameterManager Clone();

    /// <summary>
    /// 清空当前保存的全部参数。
    /// </summary>
    void Clear();
}
