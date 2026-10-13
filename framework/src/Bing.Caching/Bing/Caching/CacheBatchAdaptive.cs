using System.Threading;

namespace Bing.Caching;

/// <summary>
/// 按响应字节预算自适应拆分缓存批次。
/// </summary>
/// <remarks>超出预算时会拆分并重读；子批次不是跨键快照，且每次重读都会增加 Redis 命令数。</remarks>
internal static class CacheBatchAdaptive
{
    /// <summary>
    /// 读取并校验一批缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键。</param>
    /// <param name="read">执行底层批量读取的委托。</param>
    /// <param name="validate">校验读取结果的委托。</param>
    /// <returns>按输入键顺序排列的读取结果。</returns>
    /// <remarks>遇到响应载荷超限时会递归拆分并重读子批次。</remarks>
    internal static T[] Read<T>(string[] keys, Func<string[], T[]> read, Action<string[], T[]> validate)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        if (read == null)
            throw new ArgumentNullException(nameof(read));
        if (validate == null)
            throw new ArgumentNullException(nameof(validate));
        if (keys.Length == 0)
            return Array.Empty<T>();

        try
        {
            var values = read(keys);
            validate(keys, values);
            return values;
        }
        catch (InvalidOperationException exception) when
            (keys.Length > 1 && CacheBatchBudget.IsPayloadLimit(exception))
        {
            var middle = keys.Length / 2;
            var leftKeys = new string[middle];
            var rightKeys = new string[keys.Length - middle];
            Array.Copy(keys, 0, leftKeys, 0, leftKeys.Length);
            Array.Copy(keys, middle, rightKeys, 0, rightKeys.Length);

            var left = Read(leftKeys, read, validate);
            var right = Read(rightKeys, read, validate);
            var result = new T[left.Length + right.Length];
            Array.Copy(left, 0, result, 0, left.Length);
            Array.Copy(right, 0, result, left.Length, right.Length);
            return result;
        }
    }

    /// <summary>
    /// 异步读取并校验一批缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的键。</param>
    /// <param name="read">执行底层批量读取的异步委托。</param>
    /// <param name="validate">校验读取结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入键顺序排列的读取结果。</returns>
    /// <remarks>遇到响应载荷超限时会递归拆分并重读子批次；取消后不再开始新的子批次。</remarks>
    internal static async Task<T[]> ReadAsync<T>(string[] keys, Func<string[], Task<T[]>> read,
        Action<string[], T[]> validate, CancellationToken cancellationToken)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        if (read == null)
            throw new ArgumentNullException(nameof(read));
        if (validate == null)
            throw new ArgumentNullException(nameof(validate));
        if (keys.Length == 0)
            return Array.Empty<T>();

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var values = await read(keys).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            validate(keys, values);
            return values;
        }
        catch (InvalidOperationException exception) when
            (keys.Length > 1 && CacheBatchBudget.IsPayloadLimit(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var middle = keys.Length / 2;
            var leftKeys = new string[middle];
            var rightKeys = new string[keys.Length - middle];
            Array.Copy(keys, 0, leftKeys, 0, leftKeys.Length);
            Array.Copy(keys, middle, rightKeys, 0, rightKeys.Length);

            var left = await ReadAsync(leftKeys, read, validate, cancellationToken).ConfigureAwait(false);
            var right = await ReadAsync(rightKeys, read, validate, cancellationToken).ConfigureAwait(false);
            var result = new T[left.Length + right.Length];
            Array.Copy(left, 0, result, 0, left.Length);
            Array.Copy(right, 0, result, left.Length, right.Length);
            return result;
        }
    }
}
