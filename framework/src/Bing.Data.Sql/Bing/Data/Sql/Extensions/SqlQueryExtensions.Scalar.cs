using Bing.Extensions;
using Bing.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

// SqlQuery - Scalar
public static partial class SqlQueryExtensions
{
    #region ToString(获取字符串值)

    /// <summary>
    /// 获取字符串值
    /// </summary>
    /// <param name="source">源</param>
    public static string ToString(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return source.ExecuteScalar().SafeString();
    }

    #endregion

    #region ToStringAsync(获取字符串值)

    /// <summary>
    /// 获取字符串值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<string> ToStringAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return result.SafeString();
    }

    #endregion

    #region ToInt(获取整型值)

    /// <summary>
    /// 获取32位整型值
    /// </summary>
    /// <param name="source">源</param>
    public static int ToInt(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToInt(source.ExecuteScalar());
    }

    #endregion

    #region ToIntAsync(获取整型值)

    /// <summary>
    /// 获取32位整型值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<int> ToIntAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToInt(result);
    }

    #endregion

    #region ToIntOrNull(获取可空整型值)

    /// <summary>
    /// 获取32位可空整型值
    /// </summary>
    /// <param name="source">源</param>
    public static int? ToIntOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToIntOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToIntOrNullAsync(获取可空整型值)

    /// <summary>
    /// 获取32位可空整型值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<int?> ToIntOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToIntOrNull(result);
    }

    #endregion

    #region ToLong(获取整型值)

    /// <summary>
    /// 获取64位整型值
    /// </summary>
    /// <param name="source">源</param>
    public static long ToLong(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToLong(source.ExecuteScalar());
    }

    #endregion

    #region ToLongAsync(获取整型值)

    /// <summary>
    /// 获取64位整型值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<long> ToLongAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToLong(result);
    }

    #endregion

    #region ToLongOrNull(获取可空整型值)

    /// <summary>
    /// 获取64位可空整型值
    /// </summary>
    /// <param name="source">源</param>
    public static long? ToLongOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToLongOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToLongOrNullAsync(获取可空整型值)

    /// <summary>
    /// 获取64位可空整型值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<long?> ToLongOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToLongOrNull(result);
    }

    #endregion

    #region ToGuid(获取Guid值)

    /// <summary>
    /// 获取Guid值
    /// </summary>
    /// <param name="source">源</param>
    public static Guid ToGuid(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToGuid(source.ExecuteScalar());
    }

    #endregion

    #region ToGuidAsync(获取Guid值)

    /// <summary>
    /// 获取Guid值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<Guid> ToGuidAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToGuid(result);
    }

    #endregion

    #region ToGuidOrNull(获取可空Guid值)

    /// <summary>
    /// 获取可空Guid值
    /// </summary>
    /// <param name="source">源</param>
    public static Guid? ToGuidOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToGuidOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToGuidOrNullAsync(获取可空Guid值)

    /// <summary>
    /// 获取可空Guid值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<Guid?> ToGuidOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToGuidOrNull(result);
    }

    #endregion

    #region ToBool(获取布尔值)

    /// <summary>
    /// 获取布尔值
    /// </summary>
    /// <param name="source">源</param>
    public static bool ToBool(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToBool(source.ExecuteScalar());
    }

    #endregion

    #region ToBoolAsync(获取布尔值)

    /// <summary>
    /// 获取布尔值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<bool> ToBoolAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToBool(result);
    }

    #endregion

    #region ToBoolOrNull(获取可空布尔值)

    /// <summary>
    /// 获取可空布尔值
    /// </summary>
    /// <param name="source">源</param>
    public static bool? ToBoolOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToBoolOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToBoolOrNullAsync(获取可空布尔值)

    /// <summary>
    /// 获取可空布尔值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<bool?> ToBoolOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToBoolOrNull(result);
    }

    #endregion

    #region ToFloat(获取浮点值)

    /// <summary>
    /// 获取32位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static float ToFloat(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToFloat(source.ExecuteScalar());
    }

    #endregion

    #region ToFloatAsync(获取浮点值)

    /// <summary>
    /// 获取32位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<float> ToFloatAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToFloat(result);
    }

    #endregion

    #region ToFloatOrNull(获取可空浮点值)

    /// <summary>
    /// 获取32位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static float? ToFloatOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToFloatOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToFloatOrNullAsync(获取可空浮点值)

    /// <summary>
    /// 获取32位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<float?> ToFloatOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToFloatOrNull(result);
    }

    #endregion

    #region ToDouble(获取浮点值)

    /// <summary>
    /// 获取64位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static double ToDouble(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDouble(source.ExecuteScalar());
    }

    #endregion

    #region ToDoubleAsync(获取浮点值)

    /// <summary>
    /// 获取64位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<double> ToDoubleAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDouble(result);
    }

    #endregion

    #region ToDoubleOrNull(获取可空浮点值)

    /// <summary>
    /// 获取64位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static double? ToDoubleOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDoubleOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToDoubleOrNullAsync(获取可空浮点值)

    /// <summary>
    /// 获取64位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<double?> ToDoubleOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDoubleOrNull(result);
    }

    #endregion

    #region ToDecimal(获取浮点值)

    /// <summary>
    /// 获取128位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static decimal ToDecimal(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDecimal(source.ExecuteScalar());
    }

    #endregion

    #region ToDecimalAsync(获取浮点值)

    /// <summary>
    /// 获取128位浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<decimal> ToDecimalAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDecimal(result);
    }

    #endregion

    #region ToDecimalOrNull(获取可空浮点值)

    /// <summary>
    /// 获取128位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static decimal? ToDecimalOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDecimalOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToDecimalOrNullAsync(获取可空浮点值)

    /// <summary>
    /// 获取128位可空浮点值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<decimal?> ToDecimalOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDecimalOrNull(result);
    }

    #endregion

    #region ToDateTime(获取日期值)

    /// <summary>
    /// 获取日期值
    /// </summary>
    /// <param name="source">源</param>
    public static DateTime ToDateTime(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDate(source.ExecuteScalar());
    }

    #endregion

    #region ToDateTimeAsync(获取日期值)

    /// <summary>
    /// 获取日期值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<DateTime> ToDateTimeAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDate(result);
    }

    #endregion

    #region ToDateTimeOrNull(获取可空日期值)

    /// <summary>
    /// 获取可空日期值
    /// </summary>
    /// <param name="source">源</param>
    public static DateTime? ToDateTimeOrNull(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        return Conv.ToDateOrNull(source.ExecuteScalar());
    }

    #endregion

    #region ToDateTimeOrNullAsync(获取可空日期值)

    /// <summary>
    /// 获取可空日期值
    /// </summary>
    /// <param name="source">源</param>
    public static async Task<DateTime?> ToDateTimeOrNullAsync(this ISqlQuery source)
    {
        source.CheckNull(nameof(source));
        var result = await source.ExecuteScalarAsync();
        return Conv.ToDateOrNull(result);
    }

    #endregion
}
