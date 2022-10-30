using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Bing.Validation;

/// <summary>
/// 验证结果集合
/// </summary>
public class ValidationResultCollection : IValidationResult
{
    /// <summary>
    /// 无名称
    /// </summary>
    private const string NoName = "unamed";

    /// <summary>
    /// 验证结果集合
    /// </summary>
    private readonly List<ValidationResult> _results;

    /// <summary>
    /// 策略验证结果。策略 - 验证结果集合
    /// </summary>
    private readonly IDictionary<string, List<ValidationResult>> _resultsFlaggedByStrategy;

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    public ValidationResultCollection()
    {
        _results = new List<ValidationResult>();
        _resultsFlaggedByStrategy = new Dictionary<string, List<ValidationResult>>();
        UpdateResultFlaggedByStrategy(NoName, new List<ValidationResult>());
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="result">结果</param>
    public ValidationResultCollection(string result)
    {
        _results = new List<ValidationResult>();
        if (string.IsNullOrWhiteSpace(result))
            return;
        _results.Add(new ValidationResult(result));
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="result">验证结果</param>
    public ValidationResultCollection(ValidationResult result) : this()
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        _results.Add(result);
        UpdateResultFlaggedByStrategy(NoName, result);
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="result">验证结果</param>
    /// <param name="strategyName">策略名称</param>
    public ValidationResultCollection(ValidationResult result, string strategyName) : this()
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        _results.Add(result);
        UpdateResultFlaggedByStrategy(strategyName, result);
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="results">验证结果集合</param>
    public ValidationResultCollection(IEnumerable<ValidationResult> results) : this()
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        _results.AddRange(results);
        UpdateResultFlaggedByStrategy(NoName, results.ToList());
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="results">验证结果集合</param>
    /// <param name="strategyName">策略名称</param>
    public ValidationResultCollection(IEnumerable<ValidationResult> results, string strategyName) : this()
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));
        _results.AddRange(results);
        UpdateResultFlaggedByStrategy(strategyName, results.ToList());
    }

    /// <summary>
    /// 初始化一个<see cref="ValidationResultCollection"/>类型的实例
    /// </summary>
    /// <param name="collection">验证结果集合</param>
    public ValidationResultCollection(ValidationResultCollection collection) : this()
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));
        ErrorCode = collection.ErrorCode;
        Flag = collection.Flag;
        _results = collection._results;
        UpdateResultFlaggedByStrategy(collection);
    }

    /// <summary>
    /// 验证结果计数
    /// </summary>
    public int Count => _results.Count;

    /// <summary>
    /// 是否已验证
    /// </summary>
    public bool IsValid => _results.Count == 0;

    /// <summary>
    /// 错误码
    /// </summary>
    public long ErrorCode { get; set; } = 1001;

    /// <summary>
    /// 标识
    /// </summary>
    public string Flag { get; set; } = "__EMPTY_FLG";

    /// <summary>
    /// 成功验证结果集合
    /// </summary>
    public static ValidationResultCollection Success { get; } = new ValidationResultCollection();

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="result">验证结果</param>
    public void Add(ValidationResult result)
    {
        if (result == null)
            return;
        _results.Add(result);
    }

    /// <summary>
    /// 添加集合
    /// </summary>
    /// <param name="results">验证结果集合</param>
    public void AddRange(IEnumerable<ValidationResult> results)
    {
        if (results == null)
            return;
        _results.AddRange(results);
    }

    /// <summary>
    /// 转换为消息
    /// </summary>
    public string ToMessage()
    {
        var builder = new StringBuilder();
        if (IsValid)
            builder.Append("未发现验证错误。");
        else if (Count == 1)
            builder.Append("发现1个验证错误，请检查详细信息。");
        else
            builder.Append($"发现{Count}个验证错误，请检查详细信息。");
        builder.AppendLine();
        builder.Append($" (code: {ErrorCode}, Flag: {Flag})");
        return builder.ToString();
    }

    /// <summary>
    /// 转换为验证消息集合
    /// </summary>
    public IEnumerable<string> ToValidationMessages()
    {
        return IsValid ? Enumerable.Empty<string>() : __getErrorStringList();

        // ReSharper disable once InconsistentNaming
        IEnumerable<string> __getErrorStringList()
        {
            foreach (var error in _results)
                yield return $"{error.MemberNames.FirstOrDefault()}, {error.ErrorMessage}";
        }
    }

    /// <summary>
    /// 获取错误字符串
    /// </summary>
    /// <param name="spaceCount">空格数量</param>
    private StringBuilder GetErrorString(int spaceCount = 0)
    {
        var space = ' '.Repeat(spaceCount);
        var sb = new StringBuilder();
        foreach (var error in _results)
            sb.AppendLine($"{space}{error.MemberNames.FirstOrDefault()}, {error.ErrorMessage}");
        return sb;
    }

    /// <summary>
    /// 获取迭代器
    /// </summary>
    public IEnumerator<ValidationResult> GetEnumerator() => _results.GetEnumerator();

    /// <summary>
    /// 获取迭代器
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 输出字符串
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(ToMessage());
        sb.AppendLine("详情:");

        sb.Append(GetErrorString(6));
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// 更新指定策略的验证结果
    /// </summary>
    /// <param name="collection">验证结果集合</param>
    private void UpdateResultFlaggedByStrategy(ValidationResultCollection collection)
    {
        if (collection == null)
            return;
        foreach (var set in collection._resultsFlaggedByStrategy)
            UpdateResultFlaggedByStrategy(set.Key, set.Value);
    }

    /// <summary>
    /// 更新指定策略的验证结果
    /// </summary>
    /// <param name="name">策略名称</param>
    /// <param name="results">验证结果集合</param>
    private void UpdateResultFlaggedByStrategy(string name, List<ValidationResult> results)
    {
        if (results == null || string.IsNullOrWhiteSpace(name))
            return;
        if (_resultsFlaggedByStrategy.ContainsKey(name))
            _resultsFlaggedByStrategy[name].AddRange(results);
        else
            _resultsFlaggedByStrategy.Add(name, results);
    }

    /// <summary>
    /// 更新指定策略的验证结果
    /// </summary>
    /// <param name="name">策略名称</param>
    /// <param name="result">验证结果</param>
    private void UpdateResultFlaggedByStrategy(string name, ValidationResult result)
    {
        if (result == null || string.IsNullOrWhiteSpace(name))
            return;
        if (_resultsFlaggedByStrategy.ContainsKey(name))
            _resultsFlaggedByStrategy[name].Add(result);
        else
            _resultsFlaggedByStrategy.Add(name, new List<ValidationResult> { result });
    }

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="filter">过滤操作</param>
    internal ValidationResultCollection Filter(Action<IEnumerable<ValidationResult>> filter)
    {
        var ret = _results;
        filter?.Invoke(ret);
        return ret.Count == 0 ? null : new ValidationResultCollection(ret);
    }

    /// <summary>
    /// 过滤
    /// </summary>
    /// <param name="strategyName">策略名称</param>
    internal ValidationResultCollection Filter(string strategyName)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
            strategyName = NoName;
        return _resultsFlaggedByStrategy.TryGetValue(strategyName, out var ret)
            ? new ValidationResultCollection(ret)
            : null;
    }
}
