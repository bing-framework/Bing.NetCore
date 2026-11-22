using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL Builder 调试渲染职责。
/// </summary>
public abstract partial class SqlBuilderBase
{
    /// <inheritdoc />
    /// <remarks>
    /// 生成调试 SQL 语句，语句中的参数将被替换为可诊断字面量。
    /// </remarks>
    public virtual string ToDebugSql() => ToDebugSql(ToSql());

    /// <inheritdoc />
    /// <remarks>
    /// 根据已生成的 SQL 语句生成调试 SQL，参数标记被替换为可诊断字面量。
    /// </remarks>
    public virtual string ToDebugSql(string sql)
    {
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));
        return CreateDebugSql(sql, ParameterManager.GetParams()
            .Select(parameter => new SqlParam(parameter.Key, parameter.Value)), ParamLiteralsResolver);
    }

    /// <summary>
    /// 根据已经冻结的参数快照生成调试 SQL。
    /// </summary>
    /// <param name="sql">已经生成的 SQL 文本。</param>
    /// <param name="parameters">与 SQL 对应的参数快照。</param>
    /// <param name="paramLiteralsResolver">当前 Provider 的参数字面值解析器。</param>
    /// <returns>参数标记已替换为诊断字面值的 SQL。</returns>
    internal static string CreateDebugSql(string sql, IEnumerable<SqlParam> parameters,
        IParamLiteralsResolver paramLiteralsResolver)
        => CreateDebugSql(sql, parameters, paramLiteralsResolver, false);

    /// <summary>
    /// 根据已经冻结的参数快照生成调试 SQL，并控制参数名大小写匹配。
    /// </summary>
    /// <param name="sql">已经生成的 SQL 文本。</param>
    /// <param name="parameters">与 SQL 对应的参数快照。</param>
    /// <param name="paramLiteralsResolver">当前 Provider 的参数字面值解析器。</param>
    /// <param name="ignoreCase">是否忽略 SQL 占位符与参数名的大小写差异。</param>
    /// <returns>参数标记已替换为诊断字面值的 SQL。</returns>
    internal static string CreateDebugSql(string sql, IEnumerable<SqlParam> parameters,
        IParamLiteralsResolver paramLiteralsResolver, bool ignoreCase)
    {
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        if (paramLiteralsResolver == null)
            throw new ArgumentNullException(nameof(paramLiteralsResolver));
        var literals = parameters.Where(parameter => parameter != null)
            .ToDictionary(parameter => parameter.Name, parameter =>
                IsSensitiveParameterName(parameter.Name) ? "'<redacted>'" :
                paramLiteralsResolver.GetParamLiterals(parameter.Value));
        return ReplaceParameterTokens(sql, literals, ignoreCase);
    }

    /// <summary>
    /// 判断参数名称是否包含敏感信息标识。
    /// </summary>
    /// <param name="name">参数名称。</param>
    /// <returns>包含敏感信息标识时返回 <see langword="true"/>。</returns>
    internal static bool IsSensitiveParameterName(string name)
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
