namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL Builder 调试渲染职责。
/// </summary>
public abstract partial class SqlBuilderBase
{
    /// <summary>
    /// 生成调试 SQL 语句，语句中的参数将被替换为可诊断字面量。
    /// </summary>
    /// <returns>替换参数后的调试 SQL。</returns>
    public virtual string ToDebugSql() => ToDebugSql(ToSql());

    /// <summary>
    /// 根据已生成的 SQL 语句生成调试 SQL，参数标记被替换为可诊断字面量。
    /// </summary>
    /// <param name="sql">待渲染的 SQL 语句。</param>
    /// <returns>替换参数后的调试 SQL。</returns>
    public virtual string ToDebugSql(string sql)
    {
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));
        var parameters = ParameterManager.GetParams();
        var literals = parameters.ToDictionary(parameter => parameter.Key, parameter =>
            IsSensitiveParameterName(parameter.Key) ? "'<redacted>'" :
            ParamLiteralsResolver.GetParamLiterals(parameter.Value));
        return ReplaceParameterTokens(sql, literals);
    }

    /// <summary>
    /// 判断参数名称是否包含敏感信息标识。
    /// </summary>
    /// <param name="name">参数名称。</param>
    /// <returns>包含敏感信息标识时返回 <see langword="true"/>。</returns>
    private static bool IsSensitiveParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("passphrase", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("authorization", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("signature", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}