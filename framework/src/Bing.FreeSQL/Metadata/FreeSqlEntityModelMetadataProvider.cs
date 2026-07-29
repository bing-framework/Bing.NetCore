using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Bing.Data.Sql.Metadata;
using FreeSql;

namespace Bing.FreeSQL;

/// <summary>
/// 基于 FreeSQL CodeFirst 映射提供实体元数据。
/// </summary>
/// <remarks>
/// 该实例绑定单个 <see cref="IFreeSql"/>，不得注册为单例或跨工作单元复用。
/// </remarks>
public sealed class FreeSqlEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <summary>
    /// 当前工作单元使用的 FreeSQL ORM。
    /// </summary>
    private readonly IFreeSql _orm;

    /// <summary>
    /// 初始化一个<see cref="FreeSqlEntityModelMetadataProvider"/>类型的实例。
    /// </summary>
    /// <param name="orm">当前工作单元使用的 FreeSQL ORM。</param>
    public FreeSqlEntityModelMetadataProvider(IFreeSql orm)
    {
        _orm = orm ?? throw new ArgumentNullException(nameof(orm));
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType)
    {
        if (entityType == null)
            return null;
        try
        {
            var table = _orm.CodeFirst.GetTableByEntity(entityType);
            if (table == null)
                return null;
            return new EntityModelMetadata(entityType, table.DbName ?? entityType.Name, string.Empty,
                entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Select(property => CreatePropertyMetadata(property,
                        table.ColumnsByCs.TryGetValue(property.Name, out var column) ? column : null)));
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));

    /// <summary>
    /// 将 FreeSQL 列映射投影为 SQL 实体属性元数据。
    /// </summary>
    /// <param name="property">CLR 属性。</param>
    /// <param name="column">FreeSQL 列映射。</param>
    /// <returns>SQL 实体属性元数据。</returns>
    private static EntityPropertyMetadata CreatePropertyMetadata(PropertyInfo property,
        FreeSql.Internal.Model.ColumnInfo column)
    {
        var attribute = column?.Attribute;
        return new EntityPropertyMetadata(property, attribute?.Name ?? property.Name,
            isIgnored: attribute?.IsIgnore == true,
            isKey: attribute?.IsPrimary == true,
            databaseGeneratedOption: GetDatabaseGeneratedOption(attribute),
            isConcurrencyToken: attribute?.IsVersion == true,
            isRequired: attribute?.IsNullable == false,
            maxLength: attribute?.StringLength > 0 ? attribute.StringLength : null,
            providerTypeName: attribute?.DbType);
    }

    /// <summary>
    /// 将 FreeSQL 列生成策略转换为 DataAnnotations 生成策略。
    /// </summary>
    /// <param name="attribute">FreeSQL 列特性。</param>
    /// <returns>数据库生成策略。</returns>
    private static DatabaseGeneratedOption GetDatabaseGeneratedOption(FreeSql.DataAnnotations.ColumnAttribute attribute)
    {
        if (attribute == null)
            return DatabaseGeneratedOption.None;
        if (attribute.IsIdentity)
            return DatabaseGeneratedOption.Identity;
        return attribute.IsVersion ? DatabaseGeneratedOption.Computed : DatabaseGeneratedOption.None;
    }
}