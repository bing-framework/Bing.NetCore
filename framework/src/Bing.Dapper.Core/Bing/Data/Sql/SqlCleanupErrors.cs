using System.Collections;

namespace Bing.Data.Sql;

/// <summary>只在真实清理失败时创建异常列表，避免成功执行分配列表内部状态。</summary>
internal sealed class SqlCleanupErrors : ICollection<Exception>, IReadOnlyCollection<Exception>
{
    private List<Exception> _errors;
    public int Count => _errors?.Count ?? 0;
    public bool IsReadOnly => false;
    public void Add(Exception item) => (_errors ??= new List<Exception>()).Add(item);
    public void Clear() => _errors?.Clear();
    public bool Contains(Exception item) => _errors?.Contains(item) ?? false;
    public bool Remove(Exception item) => _errors?.Remove(item) ?? false;
    public void CopyTo(Exception[] array, int arrayIndex) => (_errors as ICollection<Exception> ?? Array.Empty<Exception>()).CopyTo(array, arrayIndex);
    public IEnumerator<Exception> GetEnumerator() => (_errors as IEnumerable<Exception> ?? Array.Empty<Exception>()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static void Dispose(ICollection<Exception> errors, IDisposable resource)
    {
        try { resource.Dispose(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    internal static void Close(ICollection<Exception> errors, IDbConnection connection)
    {
        try { connection.Close(); }
        catch (Exception exception) { errors.Add(exception); }
    }
}
