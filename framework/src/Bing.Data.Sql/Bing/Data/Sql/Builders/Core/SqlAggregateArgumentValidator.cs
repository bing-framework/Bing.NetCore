namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 聚合参数验证器。
/// </summary>
internal static class SqlAggregateArgumentValidator
{
    /// <summary>
    /// 验证聚合函数是否受支持。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <exception cref="ArgumentOutOfRangeException">聚合函数未定义时抛出。</exception>
    public static void ValidateFunction(SqlAggregateFunction function)
    {
        if (Enum.IsDefined(typeof(SqlAggregateFunction), function) == false)
            throw new ArgumentOutOfRangeException(nameof(function), function, "不支持的 SQL 聚合函数。");
    }

    /// <summary>
    /// 验证结构化聚合列并返回去除首尾空白后的列名。
    /// </summary>
    /// <param name="column">结构化列名。</param>
    /// <returns>已规范化的结构化列名。</returns>
    /// <exception cref="ArgumentException">列名不是单个结构化标识符时抛出。</exception>
    public static string ValidateStructuredColumn(string column)
    {
        return ParseStructuredColumn(column).Name;
    }

    /// <summary>
    /// 验证并解析结构化聚合列路径。
    /// </summary>
    /// <param name="column">结构化列名。</param>
    /// <returns>解析后的结构化标识符路径。</returns>
    /// <exception cref="ArgumentException">列名不是单个结构化标识符时抛出。</exception>
    public static SqlIdentifierPath ParseStructuredColumn(string column)
    {
        if (SqlIdentifierPathParser.TryParse(column, out var path) == false)
            throw CreateStructuredColumnException(nameof(column));
        return path;
    }

    /// <summary>
    /// 验证 SQL 表达式参数非空。
    /// </summary>
    /// <param name="argumentSql">SQL 表达式参数。</param>
    /// <param name="argumentName">参数名称。</param>
    /// <returns>原始 SQL 表达式参数。</returns>
    /// <exception cref="ArgumentException">表达式为空白时抛出。</exception>
    public static string ValidateExpression(string argumentSql, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(argumentSql))
            throw new ArgumentException("聚合 SQL 表达式不能为空。", argumentName);
        return argumentSql;
    }

    /// <summary>
    /// 验证聚合通配符参数是否符合函数约束。
    /// </summary>
    /// <param name="function">聚合函数。</param>
    /// <param name="argument">聚合参数。</param>
    /// <param name="distinct">是否对聚合参数去重。</param>
    /// <param name="argumentName">聚合参数名称。</param>
    /// <returns>参数为通配符时返回 true。</returns>
    /// <exception cref="ArgumentException">通配符不符合聚合函数约束时抛出。</exception>
    public static bool ValidateWildcard(SqlAggregateFunction function, string argument, bool distinct,
        string argumentName)
    {
        if (string.Equals(argument?.Trim(), "*", StringComparison.Ordinal) == false)
            return false;
        if (function != SqlAggregateFunction.Count)
            throw new ArgumentException("仅 Count 聚合支持通配符参数。", argumentName);
        if (distinct)
            throw new ArgumentException("Count(*) 不支持 Distinct 聚合参数。", nameof(distinct));
        return true;
    }

    /// <summary>
    /// 创建结构化列参数异常。
    /// </summary>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>参数异常。</returns>
    private static ArgumentException CreateStructuredColumnException(string parameterName) => new(
        "结构化聚合仅支持单个列名。表达式请使用 AggregateExpression，完全原始 SQL 请使用 AggregateRaw。", parameterName);
}