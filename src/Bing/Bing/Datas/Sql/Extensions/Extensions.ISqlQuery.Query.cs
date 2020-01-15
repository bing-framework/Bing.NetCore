using System;
using System.Data;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Datas.Sql
{
    /// <summary>
    /// Sql查询对象扩展 - 查询相关
    /// </summary>
    public static partial class Extensions
    {
        #region ToString(获取字符串值)

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static string ToString(this ISqlQuery sqlQuery, IDbConnection connection = null) => sqlQuery.ToScalar(connection).SafeString();

        /// <summary>
        /// 获取字符串值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<string> ToStringAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => (await sqlQuery.ToScalarAsync(connection)).SafeString();

        #endregion

        #region ToInt(获取整型值)

        /// <summary>
        /// 获取整型值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static int ToInt(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToInt(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取整型值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<int> ToIntAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToInt(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToIntOrNull(获取可空整型值)

        /// <summary>
        /// 获取可空整型值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static int? ToIntOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToIntOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空整型值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<int?> ToIntOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToIntOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToFloat(获取float值)

        /// <summary>
        /// 获取float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static float ToFloat(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToFloat(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<float> ToFloatAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToFloat(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToFloatOrNull(获取可空float值)

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static float? ToFloatOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToFloatOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<float?> ToFloatOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToFloatOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDouble(获取double值)

        /// <summary>
        /// 获取double值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static double ToDouble(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDouble(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取double值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<double> ToDoubleAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDouble(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDoubleOrNull(获取可空double值)

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static double? ToDoubleOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDoubleOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<double?> ToDoubleOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDoubleOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDecimal(获取decimal值)

        /// <summary>
        /// 获取decimal值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static decimal ToDecimal(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDecimal(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取decimal值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<decimal> ToDecimalAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDecimal(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDecimalOrNull(获取可空decimal值)

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static decimal? ToDecimalOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDecimalOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空float值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<decimal?> ToDecimalOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDecimalOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToBool(获取布尔值)

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static bool ToBool(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToBool(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取布尔值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<bool> ToBoolAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToBool(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToBoolOrNull(获取可空布尔值)

        /// <summary>
        /// 获取可空布尔值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static bool? ToBoolOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToBoolOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空布尔值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<bool?> ToBoolOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToBoolOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDate(获取日期值)

        /// <summary>
        /// 获取日期值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static DateTime ToDate(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDate(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取日期值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<DateTime> ToDateAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDate(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToDateOrNull(获取可空日期值)

        /// <summary>
        /// 获取可空日期值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static DateTime? ToDateOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDateOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空日期值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<DateTime?> ToDateOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToDateOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToGuid(获取Guid值)

        /// <summary>
        /// 获取Guid值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static Guid ToGuid(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToGuid(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取Guid值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<Guid> ToGuidAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToGuid(await sqlQuery.ToScalarAsync(connection));

        #endregion

        #region ToGuidOrNull(获取可空Guid值)

        /// <summary>
        /// 获取可空Guid值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static Guid? ToGuidOrNull(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToGuidOrNull(sqlQuery.ToScalar(connection));

        /// <summary>
        /// 获取可空Guid值
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="connection">数据库连接</param>
        public static async Task<Guid?> ToGuidOrNullAsync(this ISqlQuery sqlQuery, IDbConnection connection = null) => Conv.ToGuidOrNull(await sqlQuery.ToScalarAsync(connection));

        #endregion
    }
}
