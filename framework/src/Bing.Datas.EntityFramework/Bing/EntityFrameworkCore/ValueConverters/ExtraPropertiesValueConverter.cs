using Bing.Data.ObjectExtending;
using Bing.Utils.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bing.EntityFrameworkCore.ValueConverters;

/// <summary>
/// 扩展属性 值转换器
/// </summary>
public class ExtraPropertiesValueConverter : ValueConverter<ExtraPropertyDictionary, string>
{
    /// <inheritdoc />
    public ExtraPropertiesValueConverter() : base(d => PropertiesToJson(d), json => JsonToProperties(json))
    {
    }

    /// <summary>
    /// 扩展属性转换为json
    /// </summary>
    /// <param name="extraProperties">扩展属性</param>
    private static string PropertiesToJson(ExtraPropertyDictionary extraProperties)
    {
        return JsonHelper.ToJson(extraProperties);
    }

    /// <summary>
    /// json转换为扩展属性
    /// </summary>
    /// <param name="json">json</param>
    private static ExtraPropertyDictionary JsonToProperties(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new ExtraPropertyDictionary();
        return JsonHelper.ToObject<ExtraPropertyDictionary>(json) ?? new ExtraPropertyDictionary();
    }
}