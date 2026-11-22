using System.Collections.Concurrent;
using System.ComponentModel;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 读取静态注册实体声明的生成元数据提供程序。
/// </summary>
/// <remarks>
/// 读取生成器在模块初始化时注册的实体声明。显式元数据提供器仍优先。
/// </remarks>
public sealed class GeneratedEntityModelMetadataProvider : IEntityModelMetadataProvider
{
    /// <summary>
    /// 缓存已解析的实体模型元数据，键为实体类型。
    /// </summary>
    private static readonly ConcurrentDictionary<Type, EntityModelMetadata> Models = new();

    /// <summary>
    /// 注册实体模型元数据。
    /// </summary>
    /// <param name="model">要注册的实体模型元数据。</param>
    /// <remarks>
    /// 相同实体类型已有注册项时保留已有项，不替换。
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(EntityModelMetadata model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        Models.TryAdd(model.EntityType, model);
    }

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata(Type entityType) => entityType != null && Models.TryGetValue(entityType, out var model) ? model : null;

    /// <inheritdoc />
    public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
}
