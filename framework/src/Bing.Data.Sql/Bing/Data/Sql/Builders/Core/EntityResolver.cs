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
    /// 初始化一个<see cref="EntityResolver"/>类型的实例
    /// </summary>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="sqlOptions">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器</param>
    public EntityResolver(IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, SqlMetadataOptions options = null,
        SqlOptions sqlOptions = null, ISqlDatabaseContextResolver databaseContextResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null)
    {
        _entityModelMetadataProvider = entityModelMetadataProvider ?? new DefaultEntityMetadata();
        _entityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(
            databaseContextAccessor: databaseContextAccessor, options: options,
            entityModelMetadataProvider: _entityModelMetadataProvider);
        _databaseContextAccessor = databaseContextAccessor;
        _options = options ?? new SqlMetadataOptions();
        _sqlOptions = sqlOptions;
        _databaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(databaseContextAccessor,
            _options);
    }

    /// <summary>
    /// 使用旧实体元数据初始化实体解析器。
    /// </summary>
    /// <param name="metadata">旧实体元数据。</param>
    [Obsolete("请使用 IEntityModelMetadataProvider 或 IEntityMappingResolver 初始化实体解析器。")]
    public EntityResolver(IEntityMetadata metadata)
        : this(entityModelMetadataProvider: new EntityModelMetadataProviderAdapter(metadata))
    {
    }

    /// <summary>
    /// 获取表
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetTable(Type entity)
    {
        var mapping = GetMapping(entity);
        if (string.IsNullOrWhiteSpace(mapping?.TableName) == false)
            return mapping.TableName;
        var result = _entityModelMetadataProvider.GetTableName(entity);
        return string.IsNullOrWhiteSpace(result) ? entity.Name : result;
    }

    /// <summary>
    /// 获取结构化表引用
    /// </summary>
    /// <param name="entity">实体类型</param>
    public SqlTableReference GetTableReference(Type entity)
    {
        var mapping = GetMapping(entity);
        if (mapping?.TableReference != null)
            return mapping.TableReference;
        return new SqlTableReference
        {
            TableName = GetTable(entity),
            ResolvedTableName = GetTable(entity),
            PhysicalSchema = GetSchema(entity)
        };
    }

    /// <summary>
    /// 获取架构
    /// </summary>
    /// <param name="entity">实体类型</param>
    public string GetSchema(Type entity)
    {
        var mapping = GetMapping(entity);
        if (string.IsNullOrWhiteSpace(mapping?.Schema) == false)
            return mapping.Schema;
        return _entityModelMetadataProvider.GetPhysicalSchema(entity);
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="propertyAsAlias">是否将属性名映射为列别名</param>
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
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> column) => GetExpressionColumn<TEntity>(column);

    /// <summary>
    /// 获取表达式列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
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
    private string GetDictionaryColumns<TEntity>(ListInitExpression expression)
    {
        var dictionary = GetDictionaryByListInitExpression(expression);
        return GetColumnsByMatedata<TEntity>(dictionary);
    }

    /// <summary>
    /// 获取字典
    /// </summary>
    /// <param name="expression">列表表达式</param>
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
    /// <returns>数据库上下文</returns>
    private DatabaseContext GetDatabaseContext() =>
        _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
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

        if (entityType == null)
            return propertyOrColumnName;
        return _entityModelMetadataProvider.GetColumnName(entityType, propertyOrColumnName) ?? propertyOrColumnName;
    }

    /// <summary>
    /// 获取类型
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="right">是否取右侧操作数</param>
    public Type GetType(Expression expression, bool right = false)
    {
        var memberExpression = Lambdas.GetMemberExpression(expression, right);
        return memberExpression?.Expression?.Type;
    }
}