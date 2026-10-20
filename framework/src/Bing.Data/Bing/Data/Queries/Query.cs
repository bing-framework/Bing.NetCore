using System.Linq.Expressions;
using Bing.Data.Queries.Conditions;
using Bing.Data.Queries.Internal;
using Bing.Expressions;

namespace Bing.Data.Queries;

/// <summary>
/// 查询对象
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public class Query<TEntity> : Query<TEntity, Guid>, IQuery<TEntity> where TEntity : class
{
    /// <summary>
    /// 初始化一个 <see cref="Query{TEntity}"/> 类型的实例。
    /// </summary>
    public Query() { }

    /// <summary>
    /// 初始化一个 <see cref="Query{TEntity}"/> 类型的实例。
    /// </summary>
    /// <param name="queryParameter">查询参数</param>
    public Query(IQueryParameter queryParameter) : base(queryParameter)
    {
    }
}

/// <summary>
/// 查询对象
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">实体标识类型</typeparam>
public class Query<TEntity, TKey> : IQuery<TEntity, TKey> where TEntity : class
{
    /// <summary>
    /// 查询参数
    /// </summary>
    private readonly IQueryParameter _parameter;

    /// <summary>
    /// 查询条件
    /// </summary>
    private Expression<Func<TEntity, bool>> _predicate;

    /// <summary>
    /// 排序生成器
    /// </summary>
    private readonly OrderByBuilder _orderByBuilder;

    /// <summary>
    /// 初始化一个 <see cref="Query{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    public Query() : this(new QueryParameter())
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="Query{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="parameter">查询参数</param>
    public Query(IQueryParameter parameter)
    {
        _parameter = parameter;
        _orderByBuilder = new OrderByBuilder();
        OrderBy(parameter.Order);
    }

    /// <inheritdoc />
    public Expression<Func<TEntity, bool>> GetCondition() => _predicate;

    /// <inheritdoc />
    public string GetOrder() => _orderByBuilder.Generate();

    /// <inheritdoc />
    public IPager GetPager() => new Pager(_parameter.Page, _parameter.PageSize, _parameter.TotalCount, GetOrder());

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Where(Expression<Func<TEntity, bool>> predicate) => And(predicate);

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Where(ICondition<TEntity> condition) => And(condition.GetCondition());

    /// <inheritdoc />
    public IQuery<TEntity, TKey> WhereIf(Expression<Func<TEntity, bool>> predicate, bool condition) => condition == false ? this : Where(predicate);

    /// <inheritdoc />
    public IQuery<TEntity, TKey> WhereIfNotEmpty(Expression<Func<TEntity, bool>> predicate)
    {
        predicate = Helper.GetWhereIfNotEmptyExpression(predicate);
        if (predicate == null)
            return this;
        return And(predicate);
    }

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, int? min, int? max, Boundary boundary = Boundary.Both) => Where(new IntSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, double? min, double? max, Boundary boundary = Boundary.Both) => Where(new DoubleSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, decimal? min, decimal? max, Boundary boundary = Boundary.Both) => Where(new DecimalSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary));

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Between<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, DateTime? min, DateTime? max, bool includeTime = true,
        Boundary? boundary = null) =>
        includeTime
            ? Where(new DateTimeSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary ?? Boundary.Both))
            : Where(new DateSegmentCondition<TEntity, TProperty>(propertyExpression, min, max, boundary ?? Boundary.Left));

    /// <inheritdoc />
    public IQuery<TEntity, TKey> OrderBy<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, bool desc = false) => OrderBy(Lambdas.GetName(propertyExpression), desc);

    /// <inheritdoc />
    public IQuery<TEntity, TKey> OrderBy(string propertyName, bool desc = false)
    {
        _orderByBuilder.Add(propertyName, desc);
        return this;
    }

    /// <inheritdoc />
    public IQuery<TEntity, TKey> And(Expression<Func<TEntity, bool>> predicate)
    {
        _predicate = _predicate.And(predicate);
        return this;
    }

    /// <inheritdoc />
    public IQuery<TEntity, TKey> And(IQuery<TEntity, TKey> query)
    {
        And(query.GetCondition());
        OrderBy(query.GetOrder());
        return this;
    }

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Or(params Expression<Func<TEntity, bool>>[] predicates)
    {
        if (predicates == null)
            return this;
        foreach (var item in predicates)
        {
            var predicate = Helper.GetWhereIfNotEmptyExpression(item);
            if (predicate == null)
                continue;
            _predicate = _predicate.Or(predicate);
        }
        return this;
    }

    /// <inheritdoc />
    public IQuery<TEntity, TKey> Or(IQuery<TEntity, TKey> query)
    {
        _predicate = _predicate.Or(query.GetCondition());
        OrderBy(query.GetOrder());
        return this;
    }
}