using System.Collections;

namespace Serilog.Sinks.Exceptionless;

/// <summary>
/// 敏感字段脱敏器
/// </summary>
internal sealed class SensitiveFieldRedactor
{
    public const string RedactedValue = "[REDACTED]";
    private readonly ExceptionlessLogEventMapperOptions _options;

    public SensitiveFieldRedactor(ExceptionlessLogEventMapperOptions options) =>
        _options = options ?? throw new ArgumentNullException(nameof(options));

    public object Normalize(string name, object value, int depth = 0)
    {
        if (IsSensitive(name))
            return RedactedValue;
        if (value == null)
            return null;
        if (depth >= _options.MaxDepth)
            return Truncate(value.ToString());
        if (value is string text)
            return Truncate(text);
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>();
            var count = 0;
            foreach (DictionaryEntry item in dictionary)
            {
                if (count++ >= _options.MaxCollectionCount)
                    break;
                var key = item.Key?.ToString() ?? string.Empty;
                result[key] = Normalize(key, item.Value, depth + 1);
            }
            return result;
        }
        if (value is IEnumerable enumerable)
        {
            var result = new List<object>();
            foreach (var item in enumerable)
            {
                if (result.Count >= _options.MaxCollectionCount)
                    break;
                result.Add(Normalize(name, item, depth + 1));
            }
            return result;
        }
        return value;
    }

    public string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxStringLength)
            return value;
        if (_options.MaxStringLength <= 3)
            return value.Substring(0, _options.MaxStringLength);
        return value.Substring(0, _options.MaxStringLength - 3) + "...";
    }

    private bool IsSensitive(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        var normalized = new string(name.Where(char.IsLetterOrDigit).ToArray());
        return _options.SensitiveNames.Any(x => normalized.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}