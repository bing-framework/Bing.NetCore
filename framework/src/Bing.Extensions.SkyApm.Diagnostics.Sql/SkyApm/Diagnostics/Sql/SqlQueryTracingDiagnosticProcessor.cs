using System.Globalization;
using System.Reflection;
using System.Text;
using Bing.Data.Sql.Diagnostics;
using Bing.Extensions;
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
    private readonly ILocalSegmentContextAccessor _contextAccessor;

    /// <summary>
    /// 跟踪配置
    /// </summary>
    private readonly TracingConfig _tracingConfig;

    /// <summary>
    /// 是否记录SQL参数值
    /// </summary>
    private readonly bool _logParameterValue;

    /// <summary>
    /// 组件
    /// </summary>
    private readonly StringOrIntValue _component;

    /// <summary>
    /// 初始化一个<see cref="SqlQueryTracingDiagnosticProcessor"/>类型的实例
    /// </summary>
    /// <param name="tracingContext">跟踪上下文</param>
    /// <param name="contextAccessor">上下文访问器</param>
    /// <param name="configAccessor">配置访问器</param>
    public SqlQueryTracingDiagnosticProcessor(
        ITracingContext tracingContext,
        ILocalSegmentContextAccessor contextAccessor,
        IConfigAccessor configAccessor)
    {
        _tracingContext = tracingContext;
        _contextAccessor = contextAccessor;
        _component = Components.SQLCLIENT;
        _tracingConfig = configAccessor.Get<TracingConfig>();
        _logParameterValue = configAccessor.Get<SamplingConfig>().LogSqlParameterValue;
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
        var context = CreateLocalSegmentContext(message.Sql);
        context.Span.AddTag(Common.Tags.DB_INSTANCE, message.Database);
        context.Span.AddTag(Common.Tags.DB_STATEMENT, message.Sql);
        context.Span.AddTag(Common.Tags.DB_BIND_VARIABLES, BuildParameterVariables(message.Parameters));
        context.Span.AddLog(LogEvent.Event($"{SqlQueryDiagnosticListenerNames.BeforeExecute.Split(' ').Last()}: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}"));
        context.Span.AddLog(LogEvent.Message($"sql: {message.Sql}{newLine}parameters: {parameterJson}{newLine}databaseType: {message.DatabaseType}{newLine}database: {message.Database}{newLine}timestamp: {message.Timestamp}"));
    }

    /// <summary>
    /// 创建SegmentContext
    /// </summary>
    /// <param name="sql">Sql语句</param>
    private SegmentContext CreateLocalSegmentContext(string sql)
    {
        var operationName = sql?.Split(' ').FirstOrDefault();
        var context = _tracingContext.CreateLocalSegmentContext(operationName);
        context.Span.SpanLayer = SpanLayer.DB;
        context.Span.Component = _component;
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

    /// <summary>
    /// 构建参数变量
    /// </summary>
    /// <param name="parameters">参数</param>
    private string BuildParameterVariables(object parameters)
    {
        if (parameters is IReadOnlyDictionary<string, object> dict)
            return FormatParameters(dict, _logParameterValue);
        return string.Empty;
    }

    /// <summary>
    /// 格式化参数
    /// </summary>
    /// <param name="parameters">参数字典</param>
    /// <param name="logParameterValues">是否记录参数值</param>
    private static string FormatParameters(IReadOnlyDictionary<string, object> parameters, bool logParameterValues)
    {
        return parameters.Select(x => FormatParameter(x.Key, logParameterValues ? x.Value : "?"))
            .Join();
    }

    /// <summary>
    /// 格式化参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    public static string FormatParameter(string name, object value)
    {
        var builder = new StringBuilder();
        builder
            .Append(name)
            .Append("=");
        FormatParameterValue(builder, value);
        return builder.ToString();
    }

    /// <summary>
    /// 格式化参数值
    /// </summary>
    /// <param name="builder">字符串拼接器</param>
    /// <param name="parameterValue">参数值</param>
    private static void FormatParameterValue(StringBuilder builder, object parameterValue)
    {
        if (parameterValue == null || parameterValue == DBNull.Value)
        {
            builder.Append("NULL");
        }
        else if (parameterValue.GetType() == typeof(DateTime))
        {
            builder
                .Append('\'')
                .Append(((DateTime)parameterValue).ToString("o"))
                .Append('\'');
        }
        else if (parameterValue.GetType() == typeof(DateTimeOffset))
        {
            builder
                .Append('\'')
                .Append(((DateTimeOffset)parameterValue).ToString("o"))
                .Append('\'');
        }
        else if (parameterValue.GetType() == typeof(byte[]))
        {
            AppendBytes(builder, (byte[])parameterValue);
        }
        else
        {
            var valueProperty = parameterValue.GetType().GetRuntimeProperty("Value");
            if (valueProperty != null
                && valueProperty.PropertyType != parameterValue.GetType())
            {
                var isNullProperty = parameterValue.GetType().GetRuntimeProperty("IsNull");
                if (isNullProperty != null
                    && (bool)isNullProperty.GetValue(parameterValue))
                {
                    builder.Append("''");
                }
                else
                {
                    FormatParameterValue(builder, valueProperty.GetValue(parameterValue));
                }
            }
            else
            {
                builder
                    .Append('\'')
                    .Append(Convert.ToString(parameterValue, CultureInfo.InvariantCulture))
                    .Append('\'');
            }
        }
    }

    /// <summary>
    /// 添加字节数组
    /// </summary>
    /// <param name="builder">字符串拼接器</param>
    /// <param name="bytes">字节数组</param>
    private static void AppendBytes(StringBuilder builder, byte[] bytes)
    {
        builder.Append("'0x");

        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 31)
            {
                builder.Append("...");
                break;
            }

            builder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        builder.Append('\'');
    }
}
