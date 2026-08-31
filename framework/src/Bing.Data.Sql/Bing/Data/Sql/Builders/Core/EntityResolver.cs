using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using Bing.Data;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Expressions;
using Bing.Extensions;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 实体解析器
/// </summary>
public class EntityResolver : IEntityResolver
{
    /// <summary>
    /// 实体模型元数据提供器。
    /// </summary>
    private readonly IEntityModelMetadataProvider _entityModelMetadataProvider;

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    private readonly IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// Sql 配置
    /// </summary>
    private readonly SqlOptions _sqlOptions;

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    private readonly ISqlDatabaseContextResolver _databaseContextResolver;

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// 初始化一个 <see cref="EntityResolver"/> 类型的实例。
    /// </summary>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="sqlOptions">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文</param>
    public EntityResolver(IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, SqlMetadataOptions options = null,
        SqlOptions sqlOptions = null, ISqlDatabaseContextResolver databaseContextResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null, DatabaseContext databaseContext = null)
    {
        _entityModelMetadataProvider = entityModelMetadataProvider ?? new CompositeEntityModelMetadataProvider();
        _entityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(
            databaseContextAccessor: databaseContextAccessor, options: options,
            entityModelMetadataProvider: _entityModelMetadataProvider);
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
        _sqlOptions = sqlOptions;
        _databaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(databaseContextAccessor,
            _options);
        _databaseContext = DatabaseContextSnapshot.Create(databaseContext);
    }

    /// <summary>
    /// 使用实体模型元数据提供器初始化实体解析器。
    /// </summary>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器。</param>
    public EntityResolver(IEntityModelMetadataProvider entityModelMetadataProvider)
        : this(null, null, null, null, null, entityModelMetadataProvider)
    {
    }

    /// <summary>
    /// 获取表
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>实体映射对应的表名；无法解析映射或表名时返回 <see langword="null"/>。</returns>
    public string GetTable(Type entity)
    {
        var mapping = GetMapping(entity);
        return mapping?.Table?.TableName;
    }

    /// <summary>
    /// 获取结构化表引用
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>实体映射对应的结构化表引用；无法解析映射时返回 <see langword="null"/>。</returns>
    public SqlTableReference GetTableReference(Type entity)
    {
        var mapping = GetMapping(entity);
        return mapping?.Table;
    }

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="entity">实体类型</param>
    /// <returns>实体映射对应的架构名；无法解析映射或架构名时返回 <see langword="null"/>。</returns>
    public string GetSchema(Type entity)
    {
        var mapping = GetMapping(entity);
        return mapping?.Table?.Schema;
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns>按实体映射生成的列名列表。</returns>
    public string GetColumns<TEntity>(bool propertyAsAlias)
    {
        var type = typeof(TEntity);
        var names = GetProperties(type).Select(t => t.Name).ToList();
        return GetColumns<TEntity>(names, propertyAsAlias);
    }

    /// <summary>
    /// 获取属性列表
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>类型中未标记为 <see cref="NotMappedAttribute"/> 的属性列表。</returns>
    private List<PropertyInfo> GetProperties(Type type)
    {
        var result = new List<PropertyInfo>();
        var properties = type.GetProperties();
        foreach (var property in properties)
        {
            var notMapped = property.GetCustomAttribute<NotMappedAttribute>();
            if (notMapped != null)
                continue;
            result.Add(property);
        }
        return result;
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="columns">列名表达式</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns>按表达式和实体映射生成的列名列表。</returns>
    public string GetColumns<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
    {
        var names = Lambdas.GetLastNames(columns);
        return GetColumns<TEntity>(names, propertyAsAlias);
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="names">列名集合</param>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
    /// <returns>按列名集合和实体映射生成的列名列表。</returns>
    private string GetColumns<TEntity>(List<string> names, bool propertyAsAlias)
    {
        var entityType = typeof(TEntity);
        if (propertyAsAlias == false)
            return names.Select(name => ResolveColumn(entityType, name)).Join();
        return names.Select(name =>
        {
            var column = ResolveColumn(entityType, name);
            return column == name ? column : $"{column} As {name}";
        }).Join();
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="column">列名表达式</param>
    /// <returns>表达式对应的实体列名；无法解析表达式时返回 <see langword="null"/>。</returns>
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) => GetExpressionColumn<TEntity>(column);

    /// <summary>
    /// 获取表达式列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <returns>表达式对应的列 SQL；无法识别表达式时返回 <see langword="null"/>。</returns>
    private string GetExpressionColumn<TEntity>(Expression expression)
    {
        if (expression == null)
            return null;
        switch (expression.NodeType)
        {
            case ExpressionType.Lambda:
                return GetExpressionColumn<TEntity>(((LambdaExpression)expression).Body);

            case ExpressionType.Convert:
            case ExpressionType.MemberAccess:
                return GetSingleColumn<TEntity>(expression);

            case ExpressionType.ListInit:
                var isDictionary = typeof(Dictionary<object, string>).GetGenericTypeDefinition()
                    .IsAssignableFrom(expression.Type.GetGenericTypeDefinition());
                return isDictionary ? GetDictionaryColumns<TEntity>((ListInitExpression)expression) : null;
        }
        return null;
    }

    /// <summary>
    /// 获取单列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    /// <returns>表达式对应的单列名。</returns>
    private string GetSingleColumn<TEntity>(Expression expression)
    {
        var name = Lambdas.GetLastName(expression);
        return ResolveColumn(typeof(TEntity), name);
    }

    /// <summary>
    /// 获取字典多列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列表表达式</param>
    /// <returns>由字典列表表达式生成的列 SQL。</returns>
    private string GetDictionaryColumns<TEntity>(ListInitExpression expression)
    {
        var dictionary = GetDictionaryByListInitExpression(expression);
        return GetColumnsByMatedata<TEntity>(dictionary);
    }

    /// <summary>
    /// 获取字典
    /// </summary>
    /// <param name="expression">列表表达式</param>
    /// <returns>从列表初始化表达式解析出的键值对字典。</returns>
    private IDictionary<object, string> GetDictionaryByListInitExpression(ListInitExpression expression)
    {
        var result = new Dictionary<object, string>();
        foreach (var elementInit in expression.Initializers)
        {
            var keyValue = GetKeyValue(elementInit.Arguments);
            if (keyValue == null)
                continue;
            var item = keyValue.SafeValue();
            result.Add(item.Key, item.Value);
        }

        return result;
    }

    /// <summary>
    /// 获取键值对
    /// </summary>
    /// <param name="arguments">参数表达式</param>
    /// <returns>解析出的键值对；参数不足或为空时返回 <see langword="null"/>。</returns>
    private KeyValuePair<object, string>? GetKeyValue(IEnumerable<Expression> arguments)
    {
        if (arguments == null)
            return null;
        var list = arguments.ToList();
        if (list.Count < 2)
            return null;
        return new KeyValuePair<object, string>(Lambdas.GetName(list[0]), Lambdas.GetValue(list[1]).SafeString());
    }

    /// <summary>
    /// 通过元数据解析创建列
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="dictionary">字典</param>
    /// <returns>按实体元数据生成的列 SQL；字典为空时返回 <see langword="null"/>。</returns>
    private string GetColumnsByMatedata<TEntity>(IDictionary<object, string> dictionary)
    {
        string result = null;
        foreach (var item in dictionary)
            result += $"{ResolveColumn(typeof(TEntity), item.Key.SafeString())} As {item.Value},";
        return result?.TrimEnd(',');
    }

    /// <summary>
    /// 通过字典创建列
    /// </summary>
    /// <param name="dictionary">字典</param>
    /// <returns>按字典内容生成的列 SQL；字典为空时返回 <see langword="null"/>。</returns>
    private string GetColumns(IDictionary<object, string> dictionary)
    {
        string result = null;
        foreach (var item in dictionary)
            result += $"{item.Key} As {item.Value},";
        return result?.TrimEnd(',');
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="entity">实体类型</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <returns>表达式对应的实体列名。</returns>
    public string GetColumn(Expression expression, Type entity, bool right = false)
    {
        var column = Lambdas.GetLastName(expression, right);
        return ResolveColumn(entity, column);
    }

    /// <summary>
    /// 获取实体映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <returns>实体映射元数据</returns>
    private EntityMappingMetadata GetMapping(Type entityType)
    {
        if (entityType == null || _entityMappingResolver == null)
            return null;
        return _entityMappingResolver.Resolve(entityType, GetDatabaseContext());
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <returns>按固定快照、解析器、配置、访问器和默认配置顺序取得的数据库上下文。</returns>
    private DatabaseContext GetDatabaseContext() =>
        _databaseContext ?? _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
        _databaseContextAccessor?.Current ?? _options.DefaultDatabaseContext;

    /// <summary>
    /// 解析列名
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="propertyOrColumnName">属性名或列名</param>
    /// <returns>列名</returns>
    private string ResolveColumn(Type entityType, string propertyOrColumnName)
    {
        if (string.IsNullOrWhiteSpace(propertyOrColumnName))
            return propertyOrColumnName;
        var mapping = GetMapping(entityType);
        if (mapping?.Columns != null)
        {
            if (mapping.Columns.TryGetValue(propertyOrColumnName, out var column))
                return column.ColumnName;
            var mappedColumn = mapping.Columns.Values.FirstOrDefault(t =>
                string.Equals(t.PropertyName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.ColumnName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase));
            if (mappedColumn != null)
                return mappedColumn.ColumnName;
        }

        return propertyOrColumnName;
    }

    /// <summary>
    /// 获取类型
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    /// <returns>表达式操作数对应的实体类型；无法解析成员表达式时返回 <see langword="null"/>。</returns>
    public Type GetType(Expression expression, bool right = false)
    {
        var memberExpression = Lambdas.GetMemberExpression(expression, right);
        return memberExpression?.Expression?.Type;
    }
}