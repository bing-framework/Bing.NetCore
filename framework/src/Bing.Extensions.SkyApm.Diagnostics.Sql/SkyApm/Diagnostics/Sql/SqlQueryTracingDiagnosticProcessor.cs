using Bing.Data.Sql.Diagnostics;
using Bing.Utils.Json;
using SkyApm.Common;
using SkyApm.Config;
using SkyApm.Tracing;
using SkyApm.Tracing.Segments;

namespace SkyApm.Diagnostics.Sql;

/// <summary>
/// SqlQuery跟踪诊断处理器
/// </summary>
public class SqlQueryTracingDiagnosticProcessor : ITracingDiagnosticProcessor
{
    /// <summary>
    /// 跟踪上下文
    /// </summary>
    private readonly ITracingContext _tracingContext;

    /// <summary>
    /// 上下文访问器
    /// </summary>
    private readonly IExitSegmentContextAccessor _contextAccessor;

    /// <summary>
    /// 跟踪配置
    /// </summary>
    private readonly TracingConfig _tracingConfig;

    /// <summary>
    /// 组件
    /// </summary>
    private readonly StringOrIntValue? _component;

    public SqlQueryTracingDiagnosticProcessor(
        ITracingContext tracingContext,
        IExitSegmentContextAccessor contextAccessor,
        IConfigAccessor configAccessor,
        StringOrIntValue? component = null)
    {
        _tracingContext = tracingContext;
        _contextAccessor = contextAccessor;
        _component = component ?? Components.SQLCLIENT;
        _tracingConfig = configAccessor.Get<TracingConfig>();
    }

    /// <summary>
    /// 监听名称
    /// </summary>
    public string ListenerName => SqlQueryDiagnosticListenerNames.DiagnosticListenerName;

    /// <summary>
    /// 执行前
    /// </summary>
    /// <param name="message">诊断消息</param>
    [DiagnosticName(SqlQueryDiagnosticListenerNames.BeforeExecute)]
    public void BeforeExecute([Object] DiagnosticsMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Sql))
            return;
        var parameterJson = message.Parameters.ToJson();
        var newLine = Environment.NewLine;
        var context = CreateExitSegmentContext(message.Sql, message.DataSource);
        context.Span.AddTag(Common.Tags.DB_INSTANCE, message.Database);
        context.Span.AddTag(Common.Tags.DB_STATEMENT, message.Sql);
        context.Span.AddTag(Common.Tags.DB_BIND_VARIABLES, parameterJson);
        context.Span.AddLog(LogEvent.Event($"{SqlQueryDiagnosticListenerNames.BeforeExecute.Split(' ').Last()}: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}"));
        context.Span.AddLog(LogEvent.Message($"sql: {message.Sql}{newLine}parameters: {parameterJson}{newLine}databaseType: {message.DatabaseType}{newLine}dataSource: {message.DataSource}{newLine}timestamp: {message.Timestamp}"));
    }

    /// <summary>
    /// 创建SegmentContext
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="dataSource">数据源</param>
    private SegmentContext CreateExitSegmentContext(string sql, string dataSource)
    {
        var operationName = sql?.Split(' ').FirstOrDefault();
        var context = _tracingContext.CreateExitSegmentContext(operationName, dataSource);
        context.Span.SpanLayer = SpanLayer.DB;
        context.Span.Component = _component.Value;
        context.Span.AddTag(Common.Tags.DB_TYPE, "Sql");
        return context;
    }

    /// <summary>
    /// 执行后
    /// </summary>
    /// <param name="elapsedMilliseconds">耗时</param>
    [DiagnosticName(SqlQueryDiagnosticListenerNames.AfterExecute)]
    public void AfterExecute([Property(Name = "ElapsedMilliseconds")] long? elapsedMilliseconds)
    {
        var context = _contextAccessor.Context;
        if (context == null)
            return;
        context.Span.AddLog(LogEvent.Event($"{SqlQueryDiagnosticListenerNames.AfterExecute.Split(' ').Last()}: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}"));
        context.Span.AddLog(LogEvent.Message($"elapsedMilliseconds: {elapsedMilliseconds}ms"));
        _tracingContext.Release(context);
    }

    /// <summary>
    /// 执行异常
    /// </summary>
    /// <param name="ex">异常</param>
    [DiagnosticName(SqlQueryDiagnosticListenerNames.ErrorExecute)]
    public void ErrorExecute([Property(Name = "Exception")] Exception ex)
    {
        var context = _contextAccessor.Context;
        if (context == null)
            return;
        context.Span.ErrorOccurred(ex, _tracingConfig);
        _tracingContext.Release(context);
    }
}
