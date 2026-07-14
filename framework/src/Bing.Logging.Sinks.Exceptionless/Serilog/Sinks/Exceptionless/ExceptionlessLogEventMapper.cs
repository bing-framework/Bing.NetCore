using Bing.Logging.ExtraSupports;
using Bing.Text;
using Exceptionless;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Exceptionless;

/// <summary>
/// Exceptionless日志事件映射器
/// </summary>
internal sealed class ExceptionlessLogEventMapper
{
    private static readonly string[] PriorityProperties = { "TraceId", "UserId", "TenantId", "SessionId", "BusinessTraceId" };
    private readonly ExceptionlessLogEventMapperOptions _options;
    private readonly SensitiveFieldRedactor _redactor;

    public ExceptionlessLogEventMapper(ExceptionlessLogEventMapperOptions options = null)
    {
        _options = options ?? new ExceptionlessLogEventMapperOptions();
        if (_options.MaxPropertyCount <= 0 || _options.MaxStringLength <= 0 || _options.MaxCollectionCount <= 0 || _options.MaxDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "映射限制必须大于0。");
        _redactor = new SensitiveFieldRedactor(_options);
    }

    public EventBuilder Map(
        ExceptionlessClient client,
        LogEvent logEvent,
        bool includeProperties,
        IEnumerable<string> defaultTags)
    {
        var builder = client.CreateFromLogEvent(logEvent);
        builder.SetMessage(_redactor.Truncate(logEvent.RenderMessage()));
        AddTags(builder, defaultTags);
        if (!includeProperties)
            return builder;

        var properties = logEvent.Properties
            .Where(x => x.Key != Constants.SourceContextPropertyName)
            .OrderBy(x => GetPriority(x.Key))
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Take(_options.MaxPropertyCount);
        foreach (var property in properties)
            MapProperty(builder, property.Key, property.Value);
        return builder;
    }

    private void MapProperty(EventBuilder builder, string name, LogEventPropertyValue value)
    {
        switch (name)
        {
            case Event.KnownDataKeys.UserInfo when value is StructureValue userInfoValue && string.Equals(nameof(UserInfo), userInfoValue.TypeTag):
                MapUserInfo(builder, userInfoValue);
                return;
            case Event.KnownDataKeys.UserDescription when value is StructureValue userDescriptionValue && string.Equals(nameof(UserDescription), userDescriptionValue.TypeTag):
                MapUserDescription(builder, userDescriptionValue);
                return;
            case "Tags":
            case ContextDataTypes.Tags:
                AddTags(builder, value.GetTags());
                return;
            case ContextDataTypes.CallerInfo:
                builder.SetProperty("CallerInfo", _redactor.Normalize("CallerInfo", value.FlattenProperties()));
                return;
        }

        var propertyName = name.StartsWith(ContextDataTypes.ExtraProperty)
            ? name.TrimPhraseStart(ContextDataTypes.ExtraProperty)
            : name;
        builder.SetProperty(propertyName, _redactor.Normalize(propertyName, value.FlattenProperties()));
    }

    private void MapUserInfo(EventBuilder builder, StructureValue value)
    {
        if (value.FlattenProperties() is not Dictionary<string, object> userInfo)
            return;
        userInfo.TryGetValue(nameof(UserInfo.Identity), out var identity);
        userInfo.TryGetValue(nameof(UserInfo.Name), out var name);
        if (identity != null || name != null)
            builder.SetUserIdentity(_redactor.Truncate(identity?.ToString()), _redactor.Truncate(name?.ToString()));
    }

    private void MapUserDescription(EventBuilder builder, StructureValue value)
    {
        if (value.FlattenProperties() is not Dictionary<string, object> description)
            return;
        description.TryGetValue(nameof(UserDescription.EmailAddress), out var email);
        description.TryGetValue(nameof(UserDescription.Description), out var text);
        if (email != null || text != null)
            builder.SetUserDescription(_redactor.Truncate(email?.ToString()), _redactor.Truncate(text?.ToString()));
    }

    private void AddTags(EventBuilder builder, IEnumerable<string> tags)
    {
        if (tags == null)
            return;
        builder.AddTags(tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(_options.MaxCollectionCount)
            .Select(_redactor.Truncate)
            .ToArray());
    }

    private static int GetPriority(string name)
    {
        var index = Array.IndexOf(PriorityProperties, name);
        return index < 0 ? PriorityProperties.Length : index;
    }
}