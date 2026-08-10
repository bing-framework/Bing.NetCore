namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 参数名称规范化器
/// </summary>
internal sealed class DefaultSqlParameterNameNormalizer : ISqlParameterNameNormalizer
{
    /// <inheritdoc />
    public string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
    }
}