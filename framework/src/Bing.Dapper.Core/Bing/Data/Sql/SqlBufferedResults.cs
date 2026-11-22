namespace Bing.Data.Sql;

/// <summary>复用已经缓冲的列表，避免仅为集合转换保留 Dapper 的反射初始化链。</summary>
internal static class SqlBufferedResults
{
    /// <summary>保持原有空结果和列表所有权；未缓冲的序列只物化一次。</summary>
    internal static List<T> AsBufferedList<T>(this IEnumerable<T> rows) =>
        rows == null || rows is List<T> ? (List<T>)rows : rows.ToList();
}
