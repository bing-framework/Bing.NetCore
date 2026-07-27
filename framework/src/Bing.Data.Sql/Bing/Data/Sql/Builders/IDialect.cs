namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 方言的标识符、参数和语法规则。
/// </summary>
public interface IDialect
{
    /// <summary>
    /// 标识符引用的起始字符。
    /// </summary>
    char OpeningIdentifier { get; }

    /// <summary>
    /// 标识符引用的结束字符。
    /// </summary>
    char ClosingIdentifier { get; }

    /// <summary>
    /// 批量 SQL 语句之间使用的分隔字符。
    /// </summary>
    char BatchSeperator { get; }

    /// <summary>
    /// 按当前方言转义标识符名称。
    /// </summary>
    /// <param name="name">待转义的逻辑标识符名称。</param>
    /// <returns>可安全写入 SQL 的方言标识符。</returns>
    string SafeName(string name);

    /// <summary>
    /// 获取当前方言使用的参数前缀。
    /// </summary>
    /// <returns>例如 <c>@</c> 或 <c>:</c> 的参数前缀。</returns>
    string GetPrefix();

    /// <summary>
    /// 判断 Select 别名是否支持显式 <c>As</c> 关键字。
    /// </summary>
    /// <returns>支持显式 <c>As</c> 时返回 true；否则返回 false。</returns>
    bool SupportSelectAs();

    /// <summary>
    /// 根据索引生成 Provider 标准参数名称。
    /// </summary>
    /// <param name="paramIndex">从零开始的参数索引。</param>
    /// <returns>包含或可转换为当前方言前缀的参数名称。</returns>
    string GenerateName(int paramIndex);

    /// <summary>
    /// 将参数名称格式化为当前方言的执行名称。
    /// </summary>
    /// <param name="paramName">内部保存的标准参数名称。</param>
    /// <returns>带当前方言参数前缀的参数名称。</returns>
    string GetParamName(string paramName);

    /// <summary>
    /// 将 CLR 参数值转换为 Provider 可接受的值。
    /// </summary>
    /// <param name="paramValue">待转换的原始参数值，可为 null。</param>
    /// <returns>可直接传给数据库驱动的参数值。</returns>
    object GetParamValue(object paramValue);
}