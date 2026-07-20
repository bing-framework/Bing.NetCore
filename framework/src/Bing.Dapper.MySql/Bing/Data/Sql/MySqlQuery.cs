namespace Bing.Data.Sql;

/// <summary>
/// MySql Sql查询对象
/// </summary>
public class MySqlQuery : MySqlQueryBase
{
    /// <inheritdoc />
    public MySqlQuery(IServiceProvider serviceProvider, SqlOptions<MySqlQuery> options)
        : base(serviceProvider, options)
    {
    }
}
