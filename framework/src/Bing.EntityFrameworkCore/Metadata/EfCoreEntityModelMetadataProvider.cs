using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 基于 EF Core 关系模型提供实体映射元数据。
/// </summary>
/// <remarks>
/// 该实例绑定单个 <see cref="IModel"/>，不得注册为单例或在不同 <c>DbContext</c> 之间复用。
/// </remarks>
public sealed class EfCoreEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <summary>
    /// 当前 DbContext 的只读关系模型。
    /// </summary>
    private readonly IModel _model;

    /// <summary>
    /// 初始化一个<see cref="EfCoreEntityModelMetadataProvider"/>类型的实例。
    /// </summary>
    /// <param name="model">当前 DbContext 的关系模型。</param>
    public EfCoreEntityModelMetadataProvider(IModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null)
            return null;
        var entity = _model.FindEntityType(entityType);
        if (entity == null)
            return null;
        var tableName = GetAnnotationString(entity, "Relational:TableName");
        if (string.IsNullOrWhiteSpace(tableName))
            return null;
        var schema = GetAnnotationString(entity, "Relational:Schema") ?? string.Empty;
        var primaryKey = entity.FindPrimaryKey();
        return new EntityModelMetadata(entityType, tableName, schema,
            entity.GetProperties()
                .Where(property => property.PropertyInfo != null)
                .Select(property => CreatePropertyMetadata(property, primaryKey)));
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));

    /// <summary>
    /// 将 EF Core 属性映射为 SQL 实体属性元数据。
    /// </summary>
    /// <param name="property">EF Core 属性映射。</param>
    /// <param name="primaryKey">实体主键。</param>
    /// <returns>SQL 实体属性元数据。</returns>
    private static EntityPropertyMetadata CreatePropertyMetadata(IProperty property, IKey primaryKey)
    {
        var propertyInfo = property.PropertyInfo;
        return new EntityPropertyMetadata(propertyInfo,
            GetAnnotationString(property, "Relational:ColumnName") ?? property.Name,
            isKey: primaryKey?.Properties.Contains(property) == true,
            databaseGeneratedOption: GetDatabaseGeneratedOption(property),
            isConcurrencyToken: property.IsConcurrencyToken,
            isRequired: property.IsNullable == false,
            maxLength: property.GetMaxLength(),
            providerTypeName: GetAnnotationString(property, "Relational:ColumnType"));
    }

    /// <summary>
    /// 获取 EF Core 关系注解的字符串值。
    /// </summary>
    /// <param name="entity">EF Core 实体类型元数据。</param>
    /// <param name="name">注解名称。</param>
    /// <returns>注解字符串值；未设置时返回 <see langword="null"/>。</returns>
    private static string GetAnnotationString(IEntityType entity, string name) =>
        entity.FindAnnotation(name)?.Value as string;

    /// <summary>
    /// 获取 EF Core 属性关系注解的字符串值。
    /// </summary>
    /// <param name="property">EF Core 属性元数据。</param>
    /// <param name="name">注解名称。</param>
    /// <returns>注解字符串值；未设置时返回 <see langword="null"/>。</returns>
    private static string GetAnnotationString(IProperty property, string name) =>
        property.FindAnnotation(name)?.Value as string;

    /// <summary>
    /// 将 EF Core 生成策略转换为 DataAnnotations 生成策略。
    /// </summary>
    /// <param name="property">EF Core 属性映射。</param>
    /// <returns>数据库生成策略。</returns>
    private static DatabaseGeneratedOption GetDatabaseGeneratedOption(IProperty property)
    {
        return property.ValueGenerated switch
        {
            ValueGenerated.OnAdd => DatabaseGeneratedOption.Identity,
            ValueGenerated.OnAddOrUpdate => DatabaseGeneratedOption.Computed,
            _ => DatabaseGeneratedOption.None
        };
    }
}