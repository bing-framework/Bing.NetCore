using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Bing.Data.KeyGeneration;

/// <summary>
/// 批量插入的输入实体与关联标识。
/// </summary>
/// <remarks>稳定输入关联标识；不能使用尚未回填的实体键代替。</remarks>
/// <typeparam name="TEntity">待写入的实体类型。</typeparam>
public sealed class InsertRow<TEntity> where TEntity : class
{
    /// <summary>
    /// 获取关联输入与返回行的稳定标识。
    /// </summary>
    public string CorrelationKey { get; }
    /// <summary>
    /// 获取待写入的实体。
    /// </summary>
    public TEntity Entity { get; }
    /// <summary>
    /// 初始化一个 <see cref="InsertRow{TEntity}" /> 类型的实例。
    /// </summary>
    /// <param name="correlationKey">用于关联批量输入项和返回结果的键。</param>
    /// <param name="entity">当前操作涉及的实体。</param>
    public InsertRow(string correlationKey, TEntity entity)
    {
        if (string.IsNullOrWhiteSpace(correlationKey)) throw new ArgumentException("关联标识不能为空。", nameof(correlationKey));
        CorrelationKey = correlationKey; Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }
}
/// <summary>
/// 标识批量写入 SQL 的可确认完成状态。
/// </summary>
/// <remarks>
/// Flushed 仅确认当前执行已发出 SQL；外层事务仍可能回滚。
/// </remarks>
public enum WriteCompletionState
{
    /// <summary>
    /// 批量 SQL 已成功执行，但尚未确认外层事务提交。
    /// </summary>
    Flushed,
    /// <summary>
    /// 包含批量写入的事务已确认提交。
    /// </summary>
    Committed,
    /// <summary>
    /// 批量写入的最终数据库结果无法确认。
    /// </summary>
    Unknown
}

/// <summary>
/// 经关联校验的批量插入结果。
/// </summary>
/// <remarks>经完整关联校验的结果，不依赖输入/返回行的顺序。</remarks>
/// <typeparam name="TKey">实体或关联标识的类型。</typeparam>
public sealed class InsertBatchResult<TKey>
{
    /// <summary>
    /// 获取按输入关联键索引的已确认实体键。
    /// </summary>
    public IReadOnlyDictionary<string, TKey> Keys { get; }
    /// <summary>
    /// 获取数据库报告的受影响行数。
    /// </summary>
    public int AffectedRows { get; }
    /// <summary>
    /// 获取批量写入的完成与提交状态。
    /// </summary>
    public WriteCompletionState Completion { get; }
    /// <summary>
    /// 获取实际采用的批量写入路径。
    /// </summary>
    public string ExecutionPath { get; }
    /// <summary>
    /// 初始化一个 <see cref="InsertBatchResult{TKey}" /> 类型的实例。
    /// </summary>
    /// <param name="expectedKeys">本批输入项的关联标识集合。</param>
    /// <param name="returnedKeys">按关联标识返回的实体主键集合。</param>
    /// <param name="affectedRows">本次写入影响的行数。</param>
    /// <param name="completion">本次写入的完成状态。</param>
    /// <param name="executionPath">执行本次写入所采用的路径说明。</param>
    public InsertBatchResult(IEnumerable<string> expectedKeys, IEnumerable<KeyValuePair<string, TKey>> returnedKeys,
        int affectedRows, WriteCompletionState completion, string executionPath)
    {
        if (expectedKeys == null) throw new ArgumentNullException(nameof(expectedKeys));
        if (returnedKeys == null) throw new ArgumentNullException(nameof(returnedKeys));
        if (affectedRows < 0 || !Enum.IsDefined(typeof(WriteCompletionState), completion)) throw new ArgumentOutOfRangeException(nameof(affectedRows));
        var expected = expectedKeys.ToArray();
        var set = new HashSet<string>(expected, StringComparer.Ordinal);
        if (expected.Any(string.IsNullOrWhiteSpace) || set.Count != expected.Length) throw new ArgumentException("输入关联标识为空或重复。", nameof(expectedKeys));
        var map = new Dictionary<string, TKey>(StringComparer.Ordinal);
        var uniqueKeys = new HashSet<TKey>();
        foreach (var pair in returnedKeys)
        {
            if (pair.Key == null || !set.Contains(pair.Key) || map.ContainsKey(pair.Key) ||
                EqualityComparer<TKey>.Default.Equals(pair.Value, default) || pair.Value is long longId && longId <= 0 || pair.Value is int intId && intId <= 0 || !uniqueKeys.Add(pair.Value))
                throw new InvalidOperationException("返回的关联标识或主键无效、重复或不属于输入批次。");
            map.Add(pair.Key, pair.Value);
        }
        if (map.Count != expected.Length || affectedRows != expected.Length) throw new InvalidOperationException("返回键/影响行数与输入批次不完整匹配。");
        Keys = new ReadOnlyDictionary<string, TKey>(map); AffectedRows = affectedRows; Completion = completion;
        ExecutionPath = executionPath ?? throw new ArgumentNullException(nameof(executionPath));
    }
}
