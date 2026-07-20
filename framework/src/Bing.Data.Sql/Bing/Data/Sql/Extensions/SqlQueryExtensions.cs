using Bing.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象扩展
/// </summary>
public static partial class SqlQueryExtensions
{
    #region UseReadPreference(设置读取偏好)

    /// <summary>
    /// 设置读取偏好
    /// </summary>
    /// <param name="source">源</param>
    /// <param name="readPreference">读取偏好</param>
    public static ISqlQuery UseReadPreference(this ISqlQuery source, SqlReadPreference readPreference)
    {
        source.CheckNull(nameof(source));
        source.Config(options =>
        {
            var context = options.GetDatabaseContext() ?? new DatabaseContext();
            context.ReadPreference = readPreference;
            options.SetDatabaseContext(context);
        });
        return source;
    }

    /// <summary>
    /// 使用主库读取
    /// </summary>
    /// <param name="source">源</param>
    public static ISqlQuery UsePrimary(this ISqlQuery source) =>
        source.UseReadPreference(SqlReadPreference.Primary);

    #endregion

}
