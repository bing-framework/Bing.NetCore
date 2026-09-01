namespace Bing.Localization;

/// <summary>
/// 提供不读取外部资源的空本地化查找器。
/// </summary>
public class NullStringLocalizer : IStringLocalizer
{
    /// <summary>
    /// 获取空本地化查找器单例。
    /// </summary>
    public static readonly IStringLocalizer Instance = new NullStringLocalizer();

    /// <inheritdoc />
    public LocalizedString this[string name] => new(name, name, true);

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = string.Format(name, arguments);
            return new LocalizedString(value, value, true);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => new List<LocalizedString>();
}
