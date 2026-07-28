namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// SQL Builder 参数管理器。
/// </summary>
/// <remarks>
/// 实例包含可变参数状态，不支持并发读写。跨线程共享实例时调用方必须自行同步；并发操作应使用独立的克隆实例。
/// </remarks>
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
    /// <param name="name">可能包含 <c>@</c>、<c>:</c> 或 <c>?</c> 前缀的参数名称。</param>
    /// <returns>移除已知前缀后按当前 Provider 前缀重建的标准参数名称；无效名称返回空字符串。</returns>
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
    /// <returns>调用时刻以标准参数名称为键的独立参数值集合；后续写入不会改变返回集合。</returns>
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
    /// <returns>保留参数的独立副本；管理器和参数容器不共享可变状态，任意参数值对象仍按引用保留。</returns>
    IParameterManager Clone();

    /// <summary>
    /// 清空当前保存的全部参数。
    /// </summary>
    void Clear();
}
