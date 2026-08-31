using Bing.Logging.Core.Callers;
using Bing.Logging.ExtraSupports;
using Bing.Utils.Json;

namespace Bing.Logging.Core;

/// <summary>
/// 保存单条日志事件的标签、参数、扩展属性和调用者信息。
/// </summary>
public class LogEventContext
{
    #region Tags(标签列表)

    /// <summary>
    /// 保存按插入顺序排列且已去重的标签。
    /// </summary>
    private readonly List<string> _tags = new();

    /// <summary>
    /// 获取日志事件的只读标签列表。
    /// </summary>
    internal IReadOnlyList<string> Tags => _tags;

    /// <summary>
    /// 添加日志标签并返回当前上下文。
    /// </summary>
    /// <param name="tags">要添加的标签列表。</param>
    /// <returns>当前日志事件上下文，以支持链式调用。</returns>
    /// <remarks>忽略 <c>null</c>、空白和已存在的标签，保留首次添加顺序。</remarks>
    public LogEventContext SetTags(params string[] tags)
    {
        if (tags == null)
            return this;
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            if (_tags.Contains(tag))
                continue;
            _tags.Add(tag);
        }
        return this;
    }

    #endregion

    #region Parameters(参数列表)

    /// <summary>
    /// 保存日志事件关联的非空参数。
    /// </summary>
    private readonly List<object> _parameters = new();

    /// <summary>
    /// 添加单个日志参数并返回当前上下文。
    /// </summary>
    /// <param name="parameter">要关联到日志事件的参数。</param>
    /// <returns>当前日志事件上下文，以支持链式调用。</returns>
    /// <remarks><c>null</c> 参数会被忽略。</remarks>
    public LogEventContext SetParameter(object parameter)
    {
        if (parameter == null)
            return this;
        _parameters.Add(parameter);
        return this;
    }

    /// <summary>
    /// 添加多个日志参数并返回当前上下文。
    /// </summary>
    /// <param name="parameters">要关联到日志事件的参数列表。</param>
    /// <returns>当前日志事件上下文，以支持链式调用。</returns>
    public LogEventContext SetParameters(params object[] parameters)
    {
        foreach (var parameter in parameters)
            SetParameter(parameter);
        return this;
    }

    /// <summary>
    /// 获取日志事件关联的只读参数列表。
    /// </summary>
    internal IReadOnlyList<object> Parameters => _parameters;

    #endregion

    #region ExtraProperties(扩展属性)

    /// <summary>
    /// 保存日志事件的命名空间化扩展属性。
    /// </summary>
    private readonly ContextData _extraProperties = new();

    /// <summary>
    /// 设置日志事件扩展属性并返回当前上下文。
    /// </summary>
    /// <param name="name">扩展属性名称。</param>
    /// <param name="value">扩展属性值。</param>
    /// <returns>当前日志事件上下文，以支持链式调用。</returns>
    /// <remarks>空名称和 <c>null</c> 值会被忽略；有效键以前缀命名空间化，且默认不作为普通输出字段直接公开。</remarks>
    public LogEventContext SetExtraProperty(string name, object value)
    {
        if (value is null)
            return this;
        if (string.IsNullOrWhiteSpace(name))
            return this;
        _extraProperties.AddOrUpdateItem($"{ContextDataTypes.ExtraProperty}{name}", value, false);
        return this;
    }

    /// <summary>
    /// 获取扩展属性容器。
    /// </summary>
    public ContextData ExtraProperties => _extraProperties;

    #endregion

    #region CallerInfo(调用者信息)

    /// <summary>
    /// 保存日志调用者信息，默认使用空调用者信息对象。
    /// </summary>
    private ILogCallerInfo _callerInfo = NullLogCallerInfo.Instance;

    /// <summary>
    /// 设置日志调用者信息并返回当前上下文。
    /// </summary>
    /// <param name="memberName">调用成员名称。</param>
    /// <param name="sourceFilePath">调用源文件路径。</param>
    /// <param name="sourceLineNumber">调用源代码行号。</param>
    /// <returns>当前日志事件上下文，以支持链式调用。</returns>
    /// <remarks>三个参数均未提供有效值时保留当前调用者信息。</remarks>
    public LogEventContext SetCallerInfo(string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
    {
        if (!string.IsNullOrWhiteSpace(memberName) || !string.IsNullOrWhiteSpace(sourceFilePath) || sourceLineNumber > 0)
            _callerInfo = new LogCallerInfo(memberName, sourceFilePath, sourceLineNumber);
        return this;
    }

    /// <summary>
    /// 获取当前日志调用者信息。
    /// </summary>
    public ILogCallerInfo LogCallerInfo => _callerInfo;

    #endregion

    #region ExposeScopeState(公开作用域状态)

    /// <summary>
    /// 创建可公开到日志作用域的上下文字典。
    /// </summary>
    /// <returns>包含标签、可公开扩展属性和调用者信息的作用域状态字典。</returns>
    public IDictionary<string, object> ExposeScopeState()
    {
        var dict = new Dictionary<string, object>();
        // 写入标签
        if (Tags.Any()) 
            dict[ContextDataTypes.Tags] = Tags;
        // 写入扩展属性
        if (ExtraProperties.Any())
        {
            foreach (var kvp in ExtraProperties) 
                dict.Add(kvp.Key, kvp.Value.Value);
        }
        // 写入日志调用者信息
        if (LogCallerInfo is not NullLogCallerInfo) 
            dict[ContextDataTypes.CallerInfo] = LogCallerInfo.ToJson();
        return dict;
    }

    #endregion
}
