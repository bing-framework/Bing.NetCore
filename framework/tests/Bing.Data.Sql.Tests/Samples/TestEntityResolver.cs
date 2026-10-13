using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 实体解析器
/// </summary>
public class TestEntityResolver : IEntityResolver
{
    /// <inheritdoc />
    public string GetTable(Type entity) => $"t_{entity.Name}";

    /// <inheritdoc />
    public string GetSchema(Type entity) => "s";

    /// <inheritdoc />
    public string GetColumns<TEntity>(bool propertyAsAlias) => "*";

    /// <inheritdoc />
    public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias) => Lambdas.GetLastNames(columns).Select(column => $"t_{column}").Join();

    /// <inheritdoc />
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) => $"t_{Lambdas.GetLastName(column)}";

    /// <inheritdoc />
    public string GetColumn(Expression expression, Type entity, bool right) => $"t_{Lambdas.GetLastName(expression, right)}";

    /// <inheritdoc />
    public Type GetType(Expression expression, bool right = false) => typeof(Sample);
}
