using System.Collections.Concurrent;
using Bing.Data.Sql.Diagnostics;
using SkyApm.Tracing;
using SkyApm.Tracing.Segments;

namespace SkyApm.Diagnostics.Sql;

public class SqlQueryTracingDiagnosticProcessor : ITracingDiagnosticProcessor
{
    /// <summary>
    /// 上下文
    /// </summary>
    private readonly ConcurrentDictionary<string, SegmentContext> _contexts = new ConcurrentDictionary<string, SegmentContext>();

    private readonly ITracingContext _tracingContext;

    public SqlQueryTracingDiagnosticProcessor(ITracingContext tracingContext)
    {
        _tracingContext = tracingContext;
    }

    /// <summary>
    /// 监听名称
    /// </summary>
    public string ListenerName => SqlQueryDiagnosticListenerNames.DiagnosticListenerName;

    [DiagnosticName(SqlQueryDiagnosticListenerNames.BeforeExecute)]
    public void ExecuteBefore([Object] DiagnosticsMessage message)
    {
        var context = CreateSqlQueryLocalSegmentContext("Command");
        context.Span.AddTag(Common.Tags.DB_STATEMENT, message.Sql);
    }

    private SegmentContext CreateSqlQueryLocalSegmentContext(string operation)
    {
        var context = _tracingContext.CreateLocalSegmentContext(operation);
        context.Span.SpanLayer = SpanLayer.DB;
        context.Span.Component = "SqlQuery";
        context.Span.AddTag(Common.Tags.DB_TYPE, "Sql");
        return context;
    }
}
